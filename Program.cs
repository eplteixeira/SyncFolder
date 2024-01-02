using Serilog;
using SyncFolder.Utils;

namespace SyncFolder;

internal class Program
{
    private static readonly Dictionary<string, string> ReplicaFileList = [];

    /**
     * Init logger only for console
     */
    private static void InitLogger() => Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateLogger();

    /**
     * Init logger with console and file
     */
    private static void InitLogger(string logFile) => Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
        .CreateLogger();

    private static bool IsValidFolders(string source, string target)
    {
        try
        {
            // Valid Folders?
            var dirArgsSource = new DirectoryInfo(source);
            var dirArgsTarget = new DirectoryInfo(target);
            if (!dirArgsSource.Exists)
            {
                Log.Error("The specified directory doesn't exist ['{0}']", source);
                return false;
            }

            if (!dirArgsTarget.Exists)
            {
                Log.Error("The specified directory doesn't exist ['{0}']", target);
                return false;
            }

            // Check if they are not the same origin and replica
            var fullPathSource = Path.GetFullPath(source);
            var fullPathTarget = Path.GetFullPath(target);
            if (!fullPathSource.Equals(fullPathTarget))
            {
                Log.Debug("Valid Folder! We have different folders.");
                return true;
            }

            Log.Error("Folders are identical! Check the specified folders.");
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
        }

        return false;
    }

    /// <summary>
    /// This method check if in current and sub-directories exist any file.
    /// In case exists empty directories, no file exist!
    /// </summary>
    /// <param name="dir">Directory to check if it's empty of files</param>
    /// <returns>true if current and all sub-dirs doesn't contain any file </returns>
    private static bool IsEmptyDir(string dir)
    {
        // Check if target dir contains any file to map (in current directory or sub-directories)
        var targetDirEmpty = true;
        var folders = Directory.GetDirectories(dir);
        var files = Directory.GetFiles(dir);
        if (files.Length > 0)
        {
            foreach (var folder in folders)
            {
                if (Directory.GetFiles(folder).Length > 0)
                {
                    targetDirEmpty = false;
                    break;
                }
            }
        }
        else
        {
            targetDirEmpty = false;
        }

        if (targetDirEmpty)
        {
            Log.Information("Nothing to map in target dir. Directory empty of files!");
        }

        return targetDirEmpty;
    }

    /// <summary>
    /// This method will virtual create a representative of current file structure
    /// in the target dir.
    /// </summary>
    /// <param name="sourceDir">Folder to lookup for new content.</param>
    /// <param name="targetDir">Folder to map.</param>
    private static void MappingTargetDir(string targetDir)
    {
        // First Map Files int Target Dir
        var files = Directory.GetFiles(targetDir);
        foreach (var file in files)
        {
            Log.Debug("ADDED: virtual to replica {0}", file);
            ReplicaFileList.Add(file, SecurityUtil.GetMD5HashFileContent(file));
        }

        // Recursive call -  If there is more folders under this one, go deeper
        var folders = Directory.GetDirectories(targetDir);
        foreach (var folder in folders)
        {
            MappingTargetDir(folder);
        }
    }

    /// <summary>
    /// This method will make sure the target dir is created.
    /// </summary>
    /// <param name="sourceFilePath">The file to copy to target dir.</param>
    /// <param name="sourceDir">The source dir.</param>
    /// <param name="targetDir">The target dir.</param>
    private static void CreateTargetDir(string sourceFilePath, string sourceDir, string targetDir)
    {
        var sourceDirPath = Path.GetDirectoryName(sourceFilePath);
        if (sourceDirPath != null)
        {
            var targetDirPath = sourceDirPath.Replace(sourceDir, targetDir);
            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
                Log.Information("CREATED: Directory {0} created", targetDirPath);
            }
            else
            {
                Log.Debug("Directy {0} exist, don't need to create!", targetDirPath);
            }
        }
        else
        {
            Log.Error("Something went wrong returning Directory {0} from {1}", sourceDirPath, sourceFilePath);
        }
    }

    /// <summary>
    /// Sync Folders
    /// - Sync source folder to the target/replica folder
    /// - If the file change in source, we copy it again
    /// - If a file/dir is deleted, replica is not deleted, only print to console the information
    /// 
    ///  Note - we only copy new or updated files to replica, no file is deleted at replica
    /// </summary>
    /// <param name="sourceDir">The source dir.</param>
    /// <param name="targetDir">The target dir.</param>
    private static void SyncFolders(string sourceDir, string targetDir)
    {
        // Let's copy the files
        var filesCopied = new Stack<string>();
        var sourceAllFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
        foreach (var sourceFilePath in sourceAllFiles)
        {
            var toCopy = false;

            Log.Debug("Evaluate file {0}", sourceFilePath);

            // PHASE 1 - Check if file exist in replica!
            var replicaMD5 = string.Empty;
            var sourceFileMD5 = string.Empty;
            var targetFilePath = sourceFilePath.Replace(sourceDir, targetDir);
            if (ReplicaFileList.TryGetValue(targetFilePath, out replicaMD5))
            {
                Log.Debug("File {0} exist in replica folder", sourceFilePath);

                // PHASE 1.1 - Exist in replica then need to check MD5 are different
                sourceFileMD5 = SecurityUtil.GetMD5HashFileContent(sourceFilePath);
                if (!sourceFileMD5.Equals(replicaMD5))
                {
                    // we need to copy
                    // we consider this file a new file
                    toCopy = true;
                    ReplicaFileList.Remove(targetFilePath);
                    Log.Debug("Target file is different from source. We need to OVERWRITE.");
                }
                else
                {
                    filesCopied.Push(targetFilePath);
                    Log.Debug("Files (source and replica) are identical and DON'T NEED TO COPY!");
                }
            }
            else
            {
                toCopy = true;
                Log.Debug("This file {0} needs to be copied!", sourceFilePath);
            }

            if (toCopy)
            {
                // PHASE 2 - Create Folder to Target
                CreateTargetDir(sourceFilePath, sourceDir, targetDir);

                // Copy file
                File.Copy(sourceFilePath, targetFilePath, File.Exists(targetFilePath));
                Log.Information("ADDED: File {0} added to target dir.", sourceFilePath);

                // Add file into replica snapshot
                ReplicaFileList.Add(targetFilePath, sourceFileMD5);
                filesCopied.Push(targetFilePath);
            }
        }

        // PHASE 3 - remove files
        // Find files that previous exist in Source but not anymore
        // Hence, replica needs to be updated
        foreach (var replicaFile in ReplicaFileList)
        {
            Log.Debug("Replica: " + replicaFile.Key);
        }

        foreach (var tempReplicaFile in filesCopied)
        {
            Log.Debug("Files copied: " + tempReplicaFile);
        }

        var deletedFiles = ReplicaFileList
                           .Select(x => x.Key)
                           .Where(x => !filesCopied.Contains(x))
                           .ToList();

        if (deletedFiles.Count != 0)
        {
            Log.Debug("DEL: Files deleted from source that exist in replica!");
            foreach (var deletedFile in deletedFiles)
            {
                Log.Information("DEL: This file was deleted in source {0}", deletedFile);
                ReplicaFileList.Remove(deletedFile);
            }
        }
        else
        {
            Log.Debug("Replica is identical to source. No file was deleted in source!");
        }
    }

    /**
     * This program accept two arguments:
     * - arg1: source: origin folder to copy
     * - arg2: replica: destionation folder
     * - arg3: syncTime: interval milliseconds to sync
     * - arg4: logfile: location for the log file
     * 
     * example:
     * SyncFolder c:\source c:\replica 1000 c:\log.txt
     * 
     */
    private static void Main(string[] args)
    {
        InitLogger();

        if (args.Length != 4)
        {
            Log.Information("Wrong arguments! ");
            Log.Information("arg1 & arg2: The two arguments are 'origin directory' and 'target directory'");
            Log.Information("arg3: Third argument is the sync time in milliseconds.");
            Log.Information("arg4: File location to save logs");
        }
        else
        {
            // Check if the folders are OK to copy
            var sourceDir = Path.GetFullPath(args[0]);
            var targetDir = Path.GetFullPath(args[1]);
            var syncInterval = int.Parse(args[2]);
            var logFile = Path.GetFullPath(args[3]);
            InitLogger(logFile);

            Log.Debug("Source Dir {0}", sourceDir);
            Log.Debug("Target Dir {0}", targetDir);
            Log.Debug("SyncInterval {0}", syncInterval);
            Log.Debug("Log File {0}", logFile);

            if (IsValidFolders(sourceDir, targetDir))
            {
                // Mapping target dir virtually
                Log.Debug("Starting Mapping Target Dir");
                if (!IsEmptyDir(targetDir))
                {
                    MappingTargetDir(targetDir);
                }

                Log.Debug("Ending Mapping Target Dir");
                while (true)
                {
                    Log.Information("-----------BEGIN----------");
                    Log.Information("Starting Sync Folders");

                    SyncFolders(sourceDir, targetDir);

                    Log.Information("-----------END----------");
                    Log.Debug("Waiting {0} seconds", syncInterval / 1000);
                    Thread.Sleep(syncInterval);
                }
            }
        }
    }
}
