# Start with the project

1. dotnet build
2. Go to the exe generated (bin\Debug\net8.0) "SyncFolder.exe"
3. Run the program: ```SyncFolder.exe <source_dir> <replica_dir> <sync_interval_milliseconds> <log_file_location>```

    Example: ```SyncFolder.exe source .\replica 9000 c:\Temp\log.txt```