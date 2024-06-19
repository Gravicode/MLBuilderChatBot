using MLAgent.Data;
using MLAgent.Helpers;
using Microsoft.KernelMemory.DataFormats.Office;
using Microsoft.KernelMemory.DataFormats.Pdf;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;

internal sealed class FileManagerPlugin
{
    const int MaxCharRead = 2_000;
    private const int MaxTokens = 2000;
    HttpClient client { set; get; }
   
    private readonly KernelFunction _organizeFiles;
    private readonly KernelFunction _summarizeFile;
    private readonly PromptExecutionSettings _settings = new()
    {
        ExtensionData = new Dictionary<string, object>
        {
            { "max_tokens", MaxTokens },
            { "temperature", 0.7 }
        }
    };

    private static readonly string _myDocPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppConstants.FolderName);

    public FileManagerPlugin()
    {
        client = new HttpClient();
        
        if (!Directory.Exists(_myDocPath))
        {
            Directory.CreateDirectory(_myDocPath);
        }
        _organizeFiles = KernelFunctionFactory.CreateFromPrompt("""
            Based on files and their relative paths in 'FILE TO ORGANIZE', provide suggestions on how to organize these files to different folders. 
            Make sure to follow user's preference on how to organize the files. User's preference will be provided in 'User Preference'.
            Return the organized file structure in json. 

            EXAMPLE INPUT:
            file1.txt, image/image1.jpg, software.exe, document/document1.docx

            EXAMPLE OUTPUT:
            {
                'images': ['image1.jpg'],
                'documents': ['document1.docx'],
                'others': ['file1.txt', 'software.exe']
            }

            BEGIN FILE TO ORGANIZE
            {{$fileList}}
            END FILE TO ORGANIZE

            User Preference: {{$userPreference}}

            OUTPUT:
            """,
            functionName: "OrganizeFiles",
            description: "Given a list of files on MyFolder, suggests a new file structure to organize these files in JSON");

        _summarizeFile = KernelFunctionFactory.CreateFromPrompt("""
            Summarize this file:
            ```
            {{$input}}
            ```
            """,
            functionName: "SummarizeFile",
            description: "Summarize the content of a file");
    }
   

    [KernelFunction,
    Description("Write text content to a file")]
    public async Task<string> WriteContentToFile(Kernel kernel, [Description("Relative file path on MyFolder")] string filePath, [Description("text content")] string content)
    {
        try
        {
            ReportPluginInUse();

            if (filePath is null or "")
            {
                return $"Error writing file {filePath} Please provide a file path to write the content.";
            }
            var fullPath = Path.Combine(_myDocPath, filePath);
          
            FileInfo info = new FileInfo(fullPath);
            if (!info.Directory.Exists)
            {
                info.Directory.Create();
            }
            if (string.IsNullOrEmpty(content))
            {
                return "Cannot write file with empty content";
            }
            else
            {
                File.WriteAllText(fullPath, content);
            }

            return "File has been written.";
        }
        catch (Exception ex)
        {
            return "write file is failed because {ex}";
        }
    }
   
    /// <summary>
    /// Downloads a file to a local file path.
    /// </summary>
    /// <param name="url">URI of file to download</param>
    /// <param name="filePath">Path where to save file locally</param>
    /// <param name="cancellationToken">The token to use to request cancellation.</param>
    /// <returns>Task.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the location where to download the file is not provided</exception>
    [KernelFunction, Description("Downloads a file to MyFolder")]
    public async Task<string> DownloadToFileAsync(
        [Description("URL of file to download")] Uri url,
        [Description("Relative file path on MyFolder to save file locally")] string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ShowAlert("Download", $"{nameof(this.DownloadToFileAsync)} got called");

            ShowAlert("Download", $"Sending GET request for {url}");

            var bytes = await client.GetByteArrayAsync(url);
            var newpath = Path.Combine(_myDocPath, filePath);
            FileInfo info = new FileInfo(newpath);
            if (!info.Directory.Exists)
            {
                info.Directory.Create();
            }
            await File.WriteAllBytesAsync(newpath, bytes);
        }
        catch (Exception e)
        {
            return $"Error download file {filePath}. {e.Message}";
        }
        return $"download file from {url} is success";
    }

    [KernelFunction,
    Description("Send file to user")]
    public async Task<string> SendFileToUser(Kernel kernel, [Description("Relative file path on MyFolder")] string filePath)
    {
        try
        {
            ReportPluginInUse();

            if (filePath is null or "")
            {
                return $"Error reading file {filePath} Please provide a file path to read the content.";
            }
            var fullPath = Path.Combine(_myDocPath, filePath);
            if (File.Exists(fullPath))
            {
                //var ext = Path.GetExtension(fullPath);
                //var url = await cloudFile.UploadFileFromPath(fullPath);
                return fullPath;
            }
            else
            {
                return $"file {filePath} is not exist";
            }
        }
        catch (Exception ex)
        {
            return $"cannot send file {filePath} because {ex}";
        }
    }
    [KernelFunction, Description("Create a folder")]
    public async Task<string> CreateFolder(Kernel kernel, [Description("folder name or folder relative path")] string folderName)
    {
        try
        {
            ReportPluginInUse();

            if (folderName is null or "")
            {
                return $"Error create folder {folderName}, Please provide a folder path.";
            }
            var fullPath = Path.Combine(_myDocPath, folderName);
            DirectoryInfo newDir = new DirectoryInfo(fullPath);
            if (newDir.Exists)
            {
                return "folder is already exist";
            }
            else
            {
                newDir.Create();
                return "folder is created";
            }

        }
        catch (Exception ex)
        {
            return $"cannot create folder {folderName} because {ex}";
        }
        return "cannot create folder";
    }

    [KernelFunction, Description("Send folder to user")]
    public async Task<string> SendFolderToUser(Kernel kernel, [Description("Relative folder path on MyFolder")] string folderPath)
    {
        try
        {
            ReportPluginInUse();

            if (folderPath is null or "")
            {
                return $"Error reading folder {folderPath} Please provide a folder path to get the content.";
            }
            var fullPath = Path.Combine(_myDocPath, folderPath);
            DirectoryInfo sourceDir = new DirectoryInfo(fullPath);
            if (sourceDir.Exists)
            {
                using (var zipInMemory = new MemoryStream())
                {
                    using (var archive = new ZipArchive(zipInMemory, ZipArchiveMode.Create))
                    {
                        foreach (var file in sourceDir.AllFilesAndFolders().OfType<FileInfo>())
                        {
                            var relPath = file.FullName.Substring(sourceDir.FullName.Length + 1);
                            ZipArchiveEntry readmeEntry = archive.CreateEntryFromFile(file.FullName, relPath);
                        }
                    }
                    var bytes = zipInMemory.ToArray();
                    var fname = Path.GetTempFileName().Replace(".tmp", ".zip");
                    File.WriteAllBytes(fname, bytes);
                    return fname;
                }
            }
            else
            {
                return $"folder {folderPath} is not exist";
            }
                     
        }
        catch (Exception ex)
        {
            return $"cannot send folder {folderPath} because {ex}";
        }
        return "fail to send folder";
    }
   
    [KernelFunction,
     Description("Get the new file and folder structure based on user's preference. This doesn't act on the files.")]
    public async Task<string> GetNewFileStructure(Kernel kernel, [Description("List of files to organize")] string fileList, [Description("User preferred way to organize their MyFolder files")] string userPreference)
    {
        ReportPluginInUse();

        if (!string.IsNullOrEmpty(userPreference))
        {
            try
            {
                KernelArguments args = new(_settings)
                {
                    { "fileList", fileList },
                    { "userPreference", userPreference }
                };

                return (await _organizeFiles.InvokeAsync<string>(kernel, args))!;
            }
            catch (Exception e)
            {
                return $"Error organizing files. {e.Message}";
            }
        }

        return "Please provide a user preference to proceed with file organization.";
    }

    [KernelFunction,
     Description("Summarize the contents of a file")]
    public async Task<string> SummarizeFile(Kernel kernel, [Description("Relative file path on MyFolder")] string filePath)
    {
        ReportPluginInUse();

        if (filePath is null or "")
        {
            return $"Error reading file {filePath} Please provide a file path to read the content.";
        }
        var fullPath = Path.Combine(_myDocPath, filePath);
        var ext = Path.GetExtension(fullPath);
        var content = string.Empty;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
#pragma warning disable KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        switch (ext.ToLower())
        {
            case ".doc":
            case ".docx":
                {
                    var sections = new MsWordDecoder().DecodeAsync(fullPath);
                    content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                }
                break;

            case ".pdf":
                {
                    var sections = new PdfDecoder().DecodeAsync(fullPath);
                    content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                }

                break;
            case ".ppt":
            case ".pptx":
                {
                    var sections = new MsPowerPointDecoder().DecodeAsync(fullPath);
                    content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                }

                break;
            case ".xls":
            case ".xlsx":
                {
                    var sections = new MsExcelDecoder().DecodeAsync(fullPath);
                    content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                }

                break;
            default:
                {
                    content = File.ReadAllText(fullPath);
                }

                break;

        }
#pragma warning restore KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore IDE0059 // Unnecessary assignment of a value

        if (content.Length > MaxCharRead)
        {
            content = string.Concat(content.AsSpan(0, MaxCharRead), "...");
        }

        if (!string.IsNullOrEmpty(content))
        {
            try
            {
                KernelArguments args = new() { { "input", content } };

                //Add this line to switch to a local model for summarization
                /*
                args.ExecutionSettings = new Dictionary<string, PromptExecutionSettings>()
                {
                    { "localmodel", _settings }
                };
                */
                //return (await _summarizeFile.InvokeAsync<string>(kernel, args))!;

                var response = await _summarizeFile.InvokeAsync<string>(kernel, args);
                return response;
            }
            catch (Exception e)
            {
                return $"Error summarizing file {filePath}. {e.Message}";
            }
        }

        return "File is empty or failed to read contents from file.";
    }

    [KernelFunction,
     Description("Get a list of files with their relative path from MyFolder, separated by commas")]
    public string GetMyFolderFiles(
      [Description("Filter on file extension when getting files")] string fileExtensionFilter = "All")
    {
        ReportPluginInUse();

        var allFiles = GetFilesExcludingGitRepos(_myDocPath, "*", SearchOption.AllDirectories).ToArray();
        for (int i = 0; i < allFiles.Length; i++)
        {
            allFiles[i] = Path.GetRelativePath(_myDocPath, allFiles[i]);
        }

        if (fileExtensionFilter != "All")
        {
            allFiles = allFiles.Where(f => Path.GetExtension(f).Contains(fileExtensionFilter)).ToArray();
        }

        return string.Join(",", from file in allFiles
                                select Path.GetRelativePath(_myDocPath, file));
    }

    [KernelFunction,
     Description("Read the first 2000 characters from a file.")]
    public string ReadFileContent([Description("Relative file path on MyFolder")] string filePath)
    {
        ReportPluginInUse();
        try
        {


            if (filePath is null or "")
            {
                return $"Error reading file {filePath} Please provide a file path to read the content.";
            }
            var fullPath = Path.Combine(_myDocPath, filePath);
            var ext = Path.GetExtension(fullPath);
            var content = string.Empty;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
#pragma warning disable KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            switch (ext.ToLower())
            {
                case ".doc":
                case ".docx":
                    {
                        var sections = new MsWordDecoder().DecodeAsync(fullPath);
                        content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                    }
                    break;
                case ".pdf":
                    {
                        var sections = new PdfDecoder().DecodeAsync(fullPath);
                        content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                    }

                    break;
                case ".ppt":
                case ".pptx":
                    {
                        var sections = new MsPowerPointDecoder().DecodeAsync(fullPath);
                        content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                    }

                    break;
                case ".xls":
                case ".xlsx":
                    {
                        var sections = new MsExcelDecoder().DecodeAsync(fullPath);
                        content = string.Join(Environment.NewLine, sections.Result.Sections.Select(x => x.Content).ToArray());
                    }

                    break;
                default:
                    {
                        content = File.ReadAllText(fullPath);
                    }

                    break;

            }
#pragma warning restore KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            if (content.Length > MaxCharRead)
            {
                content = string.Concat(content.AsSpan(0, MaxCharRead), "...");
            }

            return content;
        }
        catch (Exception ex)
        {
            return $"fail to read file {filePath}.{ex}";
        }
    }

    [KernelFunction,
     Description("Move a single file from one location to another location using relative path on MyFolder. This function will create new folders as needed automatically. Both source file and destination file should be file not folder. This function can also be used to rename a file by moving it to a new location with a different file name.")]
    public async Task<string> MoveFile(
        [Description("Source file path, relative file path on MyFolder")] string filePath,
        [Description("Destination file path, relative destination path on MyFolder")] string destinationPath)
    {
        ReportPluginInUse();

        var userConsent = GetUserConsentAsync($"We are about to move {filePath} to {destinationPath}, please approve or deny.");
        if (!userConsent)
        {
            return "User declined the file movement, please retry.";
        }

        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(destinationPath))
        {
            return $"Error moving file {filePath} to {destinationPath}. Please provide a valid file path and destination path.";
        }

        filePath = Path.Combine(_myDocPath, filePath);
        destinationPath = Path.Combine(_myDocPath, destinationPath);

        try
        {
            string? destinationFolder = Path.GetDirectoryName(destinationPath);

            EnsureFolderExists(destinationFolder);

            File.Move(filePath, destinationPath);
            return $"File {filePath} moved to {destinationPath}";
        }
        catch (Exception e)
        {
            return $"Error moving file {filePath} to {destinationPath}. {e.Message}";
        }
    }

    [KernelFunction,
     Description("Move a single file or a folder from one location into another folder using relative path on MyFolder. This function will create new folders as needed automatically. If you want to move the entire folder, just pass the source folder path in, don't move all files under it one by one.")]
    public async Task<string> MoveIntoFolder(
        [Description("Source folder path, relative folder path on MyFolder")] string fileOrFolderPath,
        [Description("Destination folder path where the file or folder will be moved into, relative destination path on MyFolder")] string destinationFolder)
    {
        ReportPluginInUse();

        var userConsent = GetUserConsentAsync($"We are about to move {fileOrFolderPath} to folder {destinationFolder}, please approve or deny.");
        if (!userConsent)
        {
            return "User declined the file movement, please retry.";
        }

        if (string.IsNullOrEmpty(fileOrFolderPath) || string.IsNullOrEmpty(destinationFolder))
        {
            return $"Error moving folder {fileOrFolderPath} to {destinationFolder}. Please provide a valid folder path and destination path.";
        }

        destinationFolder = Path.Combine(_myDocPath, destinationFolder);

        EnsureFolderExists(destinationFolder);

        try
        {
            string sourcePath = Path.Combine(_myDocPath, fileOrFolderPath);
            string destinationPath = string.Empty;

            if (File.Exists(sourcePath))
            {
                destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
            }
            else if (Directory.Exists(sourcePath))
            {
                destinationPath = Path.Combine(destinationFolder, new DirectoryInfo(sourcePath).Name);
            }

            Directory.Move(sourcePath, destinationPath);
            return $"Successfully Moved {fileOrFolderPath} to {destinationFolder}";
        }
        catch (Exception e)
        {
            return $"Error moving folder {fileOrFolderPath} to {destinationFolder}. {e.Message}";
        }
    }

    [KernelFunction,
     Description("Move multiple files or folders from various locations into a folder using relative path on MyFolder. This function will create new folders as needed automatically. If you want to move the entire folder, just pass the source folder path in, don't move all files under it one by one.")]
    public async Task<string> BulkMoveFilesIntoFolder(
        [Description("A list of source files or folders with relative file path on MyFolder. If file is in a folder, make sure to include the folder info in the relative path.")] List<string> filePaths,
        [Description("Destination folder path where the file or folder will be moved into, relative destination path on MyFolder")] string destinationFolder)
    {
        ReportPluginInUse();

        var files = string.Join("\n", filePaths);
        var userConsent = GetUserConsentAsync($"We are about to move below files to folder {destinationFolder}, please approve or deny. \n {files}");
        if (!userConsent)
        {
            return "User declined the file movement, please retry.";
        }

        if (filePaths == null || filePaths.Count == 0 || destinationFolder == null || destinationFolder == "")
        {
            return $"Error moving files to {destinationFolder}. Please provide a valid file path and destination path.";
        }

        destinationFolder = Path.Combine(_myDocPath, destinationFolder);

        EnsureFolderExists(destinationFolder);

        List<string> movedFiles = [];
        try
        {
            foreach (var file in filePaths)
            {
                string sourcePath = Path.Combine(_myDocPath, file);
                string destinationPath = string.Empty;
                if (File.Exists(sourcePath))
                {
                    destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
                }
                else if (Directory.Exists(sourcePath))
                {
                    destinationPath = Path.Combine(destinationFolder, new DirectoryInfo(sourcePath).Name);
                }

                Directory.Move(sourcePath, destinationPath);
                movedFiles.Add(file);
            }
            return $"Files moved to {destinationFolder}";

        }
        catch (Exception e)
        {
            return $"Error moving all files to {destinationFolder}. {e.Message}. Moved files are: {string.Join(", ", [.. movedFiles])}";
        }
    }

    [KernelFunction,
     Description("Move all files and folders into a folder")]
    public async Task<string> MoveAllFilesIntoFolder(
        [Description("Destination folder name where the file or folder will be moved into")] string destinationFolder)
    {
        ReportPluginInUse();

        try
        {
            destinationFolder = Path.Combine(_myDocPath, destinationFolder);

            string[] files = Directory.GetFiles(_myDocPath);
            string[] directories = Directory.GetDirectories(_myDocPath);

            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            foreach (string file in files)
            {
                if (file is null or "")
                {
                    continue;
                }
                if (File.Exists(file))
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(destinationFolder, fileName);
                    File.Move(file, destFile);
                }
            }

            foreach (string directory in directories)
            {
                if (directory is null or "")
                {
                    continue;
                }
                if (Directory.Exists(directory))
                {
                    string dirName = new DirectoryInfo(directory).Name;
                    string destDir = Path.Combine(destinationFolder, dirName);
                    Directory.Move(directory, destDir);
                }
            }

            return $"All files and folders have been moved to {destinationFolder}.";
        }
        catch (Exception ex)
        {
            // Return the exception message if something goes wrong
            return $"An error occurred: {ex.Message}";
        }
    }

    [KernelFunction,
     Description("Delete all empty folders on MyFolder.")]
    public async Task<string> DeleteEmptyFolders()
    {
        ReportPluginInUse();

        var userConsent = GetUserConsentAsync("We are about to delete all empty folders on MyFolder. Please approve or deny.");
        if (!userConsent)
        {
            return "User denied the file operations. Please retry or ask something else";
        }

        try
        {
            foreach (var directory in Directory.GetDirectories(_myDocPath, "*", SearchOption.AllDirectories))
            {
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
            return "Empty folders deleted successfully.";
        }
        catch (Exception e)
        {
            return $"Error deleting empty folders. {e.Message}";
        }
    }

    [KernelFunction,
     Description("Count files on the MyFolder")]
    public int CountFiles()
    {
        ReportPluginInUse();

        return Directory.GetFiles(_myDocPath, "*", SearchOption.AllDirectories).Length;
    }

    [KernelFunction,
     Description("Restore MyFolder to its original state, and revert all changes.")]
    public async Task<string> RestoreMyFolder()
    {
        ReportPluginInUse();

        var userConsent = GetUserConsentAsync("We are about to restore MyFolder to its original state. Please approve or deny.");
        if (!userConsent)
        {
            return "User denied the file operations. Please retry or ask something else";
        }

        string? parentDirectory = Directory.GetParent(_myDocPath)?.FullName;
        if (parentDirectory is not null)
        {
            Directory.SetCurrentDirectory(parentDirectory);
            if (RunCommand("git reset --hard") &&
                RunCommand("git clean -f -d"))
            {
                return "MyFolder restored to its original state.";
            }
        }

        return "Error restoring MyFolder to its original state.";
    }

    private static void ReportPluginInUse([CallerMemberName] string? functionName = null)
    {
        Console.WriteLine($"Plugins: FileManagerPlugin; functionName => {functionName ?? "Unknown"}");

    }

    static void ShowAlert(string Caption, string Message)
    {
        Console.WriteLine($"{DateTime.Now}: [{Caption}]-> {Message}");
    }

    public static void Backup()
    {
        string? parentDirectory = Directory.GetParent(_myDocPath)?.FullName;

        if (parentDirectory is not null)
        {
            Directory.SetCurrentDirectory(parentDirectory);
            if (RunCommand("git add .") &&
                RunCommand("git commit -m 'backup'"))
            {
                ShowAlert("Backup MyFolder", "MyFolder was backed up successfully");
                return;
            }
        }

        ShowAlert("Backup MyFolder", "Failed to backup MyFolder, please retry");
    }

    public static bool RunCommand(string command)
    {
        using Process process = Process.Start(new ProcessStartInfo()
        {
            FileName = "cmd.exe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        })!;

        process.StandardInput.WriteLine(command);
        process.StandardInput.Close();

        _ = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        return process.ExitCode == 0;
    }

    private static void EnsureFolderExists(string? folderPath)
    {
        if (folderPath is not null && !Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
    }

    private static bool GetUserConsentAsync(string message)
    {
        //bypass
        var res = MessageBox.Show(message, "Warning", MessageBoxButtons.YesNo);
        return (res == DialogResult.Yes);


    }

    private IEnumerable<string> GetFilesExcludingGitRepos(string path, string searchPattern, SearchOption searchOption)
    {
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            var currentDirectory = stack.Pop();
            if (Directory.Exists(Path.Combine(currentDirectory, ".git")))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(currentDirectory, searchPattern);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (searchOption == SearchOption.AllDirectories)
            {
                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(currentDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var directory in directories)
                {
                    stack.Push(directory);
                }
            }
        }
    }
}