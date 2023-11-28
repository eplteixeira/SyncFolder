namespace SyncFolder
{
    using Serilog;
    using System.Numerics;

    internal class Program
    {
        private static Dictionary<string, string> replicaFileList = [];

        /**
         * Init logger only for console
         */
        static void InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        }

        /**
         * Init logger with console and file
         */
        static void InitLogger(string logFile)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
            .CreateLogger();
        }

        static Boolean IsValidFolders(string source, string target)
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
                string fullPathSource = Path.GetFullPath(source);
                string fullPathTarget = Path.GetFullPath(target);
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

        static void CreateDirectory(string sourceFilePath, string sourceDir, string targetDir)
        {
            string? sourceDirPath = Path.GetDirectoryName(sourceFilePath);
            if (sourceDirPath != null)
            {
                string targetDirPath = sourceDirPath.Replace(sourceDir, targetDir);
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

        /**
         * Sync Folders
         * - Sync source folder to the target/replica folder
         * - If the file change in source, we copy it again
         * - If a file/dir is deleted, replica is not deleted, only print to console the information
         * 
         * Note - we only copy new or updated files to replica, no file is deleted at replica
         */
        static void SyncFolders(string sourceDir, string targetDir)
        {
            // Let's copy the files
            var filesCopied = new Stack<string>();
            var sourceAllFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
            foreach (string sourceFilePath in sourceAllFiles)
            {
                Boolean needToCopy = false;

                Log.Debug("Evaluate file {0}", sourceFilePath);

                // PHASE 1 - Check if file exist in replica!
                if (replicaFileList.ContainsKey(sourceFilePath))
                {
                    Log.Debug("File {0} exist in replica folder", sourceFilePath);

                    // PHASE 1.1 - Exist in replica then need to check MD5 are different
                    var currentMD5 = SecurityUtil.GetMD5(sourceFilePath);
                    if (!currentMD5.Equals(replicaFileList[sourceFilePath]))
                    {
                        // we need to copy
                        // we consider this file a new file
                        needToCopy = true;
                        replicaFileList.Remove(sourceFilePath);
                        Log.Debug("Files are different. We need to OVERWRITE.");
                    }
                    else
                    {
                        filesCopied.Push(sourceFilePath);
                        Log.Debug("Files are identical and DON'T NEED TO COPY");
                    }
                }
                else
                {
                    needToCopy = true;
                    Log.Debug("This file {0} needs to be copied!", sourceFilePath);
                }

                if (needToCopy)
                {
                    // PHASE 2 - Create Folder to Target
                    CreateDirectory(sourceFilePath, sourceDir, targetDir);

                    // Copy file
                    File.Copy(sourceFilePath, sourceFilePath.Replace(sourceDir, targetDir), true);

                    // Add file into replica snapshot
                    replicaFileList.Add(sourceFilePath, SecurityUtil.GetMD5(sourceFilePath));
                    filesCopied.Push(sourceFilePath);

                    Log.Information("ADDED: File {0} added to target dir.", sourceFilePath);
                }
            }

            // PHASE 3 - remove files
            // Find files that previous exist in Source but not anymore
            // Hence, replica needs to be updated
            foreach (var replicaFile in replicaFileList)
            {
                Log.Debug("Replica: " + replicaFile.Key);
            }
            foreach (var tempReplicaFile in filesCopied)
            {
                Log.Debug("Current Copy Files: " + tempReplicaFile);
            }

            var deletedFiles = replicaFileList
                               .Select(x => x.Key)
                               .Where(x => !filesCopied.Contains(x))
                               .ToList();

            if (deletedFiles.Any())
            {
                Log.Debug("DEL: Files deleted from source that exist in replica!");
                foreach (var deletedFile in deletedFiles)
                {
                    Log.Information("DEL: This file was deleted in source {0}", deletedFile);
                    replicaFileList.Remove(deletedFile);
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
        static void Main(string[] args)
        {
            InitLogger();

            if (args.Length != 4 ) {
                Log.Information("Wrong arguments! ");
                Log.Information("arg1 & arg2: The two arguments are 'origin directory' and 'target directory'");
                Log.Information("arg3: Third argument is the sync time in milliseconds.");
                Log.Information("arg4: File location to save logs");
            }
            else
            {
                // Check if the folders are OK to copy
                string sourceDir = Path.GetFullPath(args[0]); ;
                string targetDir = Path.GetFullPath(args[1]); ;
                int syncInterval = int.Parse(args[2]);
                string logFile = Path.GetFullPath(args[3]);
                InitLogger(logFile);

                Log.Debug("Source Dir {0}", sourceDir);
                Log.Debug("Target Dir {0}", targetDir);
                Log.Debug("SyncInterval {0}", syncInterval);
                Log.Debug("Log File {0}", logFile);

                if (IsValidFolders(sourceDir, targetDir))
                {                    
                    while (true)
                    {
                        Log.Information("-----------BEGIN----------");
                        Log.Information("Starting Sync Folders");

                        SyncFolders(sourceDir, targetDir);

                        Log.Information("-----------END----------");
                        Log.Debug("Waiting {0} seconds", syncInterval/1000);
                        Thread.Sleep(syncInterval);
                    }
                }
            }
        }
    }
}
