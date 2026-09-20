using System.Text.Json;
using PDFcoDrive.Models;

namespace PDFcoDrive.Services
{
    public interface IJsonStorageService
    {
        Task<List<User>> GetUsersAsync();
        Task SaveUsersAsync(List<User> users);

        Task<List<Category>> GetCategoriesAsync();
        Task SaveCategoriesAsync(List<Category> categories);

        Task<List<FileItem>> GetFilesAsync();
        Task SaveFilesAsync(List<FileItem> files);

        Task<List<Folder>> GetFoldersAsync();
        Task SaveFoldersAsync(List<Folder> folders);

        Task<List<TrashItem>> GetTrashAsync();
        Task SaveTrashAsync(List<TrashItem> trash);

        Task AppendDownloadLogAsync(DownloadLog log);
        Task<List<DownloadLog>> GetDownloadLogsAsync();

        Task AppendViewLogAsync(ViewLog log);
        Task<List<ViewLog>> GetViewLogsAsync();

        // Shared Files
        Task<List<SharedFile>> GetSharedFilesAsync();
        Task SaveSharedFilesAsync(List<SharedFile> sharedFiles);
    }

    public class JsonStorageService : IJsonStorageService
    {
        private readonly string _dataFolder;
        private readonly JsonSerializerOptions _options;

        private const string UsersFile = "users.json";
        private const string CategoriesFile = "categories.json";
        private const string FilesFile = "files.json";
        private const string FoldersFile = "folders.json";
        private const string TrashFile = "trash.json";
        private const string DownloadLogsFile = "downloadlogs.json";
        private const string ViewLogsFile = "viewlogs.json";
        private const string SharedFilesFile = "sharedfiles.json";

        private readonly SemaphoreSlim _lock = new(1, 1);

        public JsonStorageService(IConfiguration config)
        {
            _dataFolder = config["Storage:DataPath"] ?? "D:\\PDFcoDrive\\Data";

            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);

            _options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        private async Task<List<T>> ReadAsync<T>(string fileName)
        {
            var path = Path.Combine(_dataFolder, fileName);
            if (!File.Exists(path)) return new List<T>();

            await _lock.WaitAsync();
            try
            {
                var json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json)) return new List<T>();
                return JsonSerializer.Deserialize<List<T>>(json, _options) ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task WriteAsync<T>(string fileName, List<T> data)
        {
            var path = Path.Combine(_dataFolder, fileName);
            var tempPath = path + ".tmp";

            await _lock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(data, _options);
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                _lock.Release();
            }
        }

        public Task<List<User>> GetUsersAsync() => ReadAsync<User>(UsersFile);
        public Task SaveUsersAsync(List<User> users) => WriteAsync(UsersFile, users);

        public Task<List<Category>> GetCategoriesAsync() => ReadAsync<Category>(CategoriesFile);
        public Task SaveCategoriesAsync(List<Category> categories) => WriteAsync(CategoriesFile, categories);

        public Task<List<FileItem>> GetFilesAsync() => ReadAsync<FileItem>(FilesFile);
        public Task SaveFilesAsync(List<FileItem> files) => WriteAsync(FilesFile, files);

        public Task<List<Folder>> GetFoldersAsync() => ReadAsync<Folder>(FoldersFile);
        public Task SaveFoldersAsync(List<Folder> folders) => WriteAsync(FoldersFile, folders);

        public Task<List<TrashItem>> GetTrashAsync() => ReadAsync<TrashItem>(TrashFile);
        public Task SaveTrashAsync(List<TrashItem> trash) => WriteAsync(TrashFile, trash);

        public async Task AppendDownloadLogAsync(DownloadLog log)
        {
            var logs = await GetDownloadLogsAsync();
            log.Id = logs.Count > 0 ? logs.Max(l => l.Id) + 1 : 1;
            logs.Add(log);
            if (logs.Count > 1000)
                logs = logs.OrderByDescending(l => l.DownloadedAt).Take(1000).ToList();
            await WriteAsync(DownloadLogsFile, logs);
        }

        public Task<List<DownloadLog>> GetDownloadLogsAsync() => ReadAsync<DownloadLog>(DownloadLogsFile);

        public async Task AppendViewLogAsync(ViewLog log)
        {
            var logs = await GetViewLogsAsync();
            log.Id = logs.Count > 0 ? logs.Max(l => l.Id) + 1 : 1;
            logs.Add(log);
            if (logs.Count > 1000)
                logs = logs.OrderByDescending(l => l.ViewedAt).Take(1000).ToList();
            await WriteAsync(ViewLogsFile, logs);
        }

        public Task<List<ViewLog>> GetViewLogsAsync() => ReadAsync<ViewLog>(ViewLogsFile);

        public Task<List<SharedFile>> GetSharedFilesAsync() => ReadAsync<SharedFile>(SharedFilesFile);
        public Task SaveSharedFilesAsync(List<SharedFile> sharedFiles) => WriteAsync(SharedFilesFile, sharedFiles);
    }
}