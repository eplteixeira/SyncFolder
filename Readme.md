# Sync Folder

This program sync two folders (source and replica), and only only copy files (i.e., content), folders are not important.
1. If replica contains some files, program will push them info into memory for further evaluate when a file come into source, requires or not to copy.
1. If a file is new we copy to replica.
1. If the file already exist in replica, we check the content (from MD5 hash code) to make sure if they have identical content or not, if yes don't copy, otherwise overwrite file.
1. Empty folders in source are not copy.

*Note* When launch the program the application will scan/map the replica folder

## Start with the project

1. dotnet build
2. Go to the exe generated (bin\Debug\net8.0) "SyncFolder.exe"
3. Run the program: ```SyncFolder.exe <source_dir> <replica_dir> <sync_interval_milliseconds> <log_file_location>```

    Example: ```SyncFolder.exe source .\replica 9000 c:\Temp\log.txt```


# Future Work
1. Use external lib to handle parameters
1. Flag to also copy files if exist in replica
1. Flag to delete files/folders from replica
1. Introduce CI/CD to generate versioning