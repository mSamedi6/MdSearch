using DocumentFormat.OpenXml.Packaging;
using MetadataExtractor;
using Microsoft.Win32;
using NTextCat;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;
using Point = System.Windows.Point;

namespace MdSearch_1._0
{
    public partial class MainWindow : Window
    {
        private int _userRoleId;
        private Entities entities = new Entities();
        private List<Files> filesList = new List<Files>();
        private CollectionViewSource filesViewSource;
        private Point _dragStartPoint;

        private FileSystemWatcher fileWatcher;
        private Dictionary<string, Files> filePathDictionary;
        private bool _isSortedNewestFirst = true;

        private void LogFieldChange(int fileID, string fieldName, object oldValue, object newValue)
        {
            if (Equals(oldValue, newValue))
                return;

            if ((oldValue is null && newValue is null) ||
                (oldValue is string oldStr && string.IsNullOrEmpty(oldStr) &&
                 newValue is string newStr && string.IsNullOrEmpty(newStr)))
                return;

            var change = new ChangesHistory
            {
                FileID = fileID,
                ChangeDate = DateTime.Now,
                ChangeDescription = $"Поле '{fieldName}' было изменено",
                OldValue = oldValue?.ToString(),
                NewValue = newValue?.ToString(),
                ChangeType = $"Изменение поля: {fieldName}"
            };

            entities.ChangesHistory.Add(change);
        }

        public MainWindow(int roleId)
        {
            InitializeComponent();

            _userRoleId = roleId;

            // Для установщщика
            //string installType = GetInstallType();

            //if (installType == "PRIVATE")
            //{
            //    var manageUsers = FindName("ManageUsers") as UIElement;
            //    if (manageUsers != null)
            //        manageUsers.Visibility = Visibility.Collapsed;

            //    if (this.FindResource("OtherContextMenu") is ContextMenu otherContextMenu)
            //    {
            //        var targetItem = otherContextMenu.Items
            //            .OfType<MenuItem>()
            //            .FirstOrDefault(mi => mi.Header?.ToString() == "Настройки безопасности");

            //        if (targetItem != null)
            //            otherContextMenu.Items.Remove(targetItem);
            //    }
            //}

            InitializeUserInterface();
            try
            {
                // Проверка целостности данных
                var brokenFiles = entities.Files.Where(f => f.FolderID != null &&
                                                  !entities.Folders.Any(folder => folder.FolderID == f.FolderID));

                if (brokenFiles.Any())
                {
                    // Авто исправление
                    foreach (var file in brokenFiles)
                    {
                        file.FolderID = null;
                    }
                    entities.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка проверки целостности: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            InitializeFileWatcher();
            filesViewSource = (CollectionViewSource)FindResource("FilesViewSource");
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            LoadFolders(false);
            LoadFiles();
            SizeFilterComboBox.SelectedIndex = 0;
            TypeFilterComboBox.SelectedIndex = 0;
            UpdateFileCounts();
        }

        // Для установщщика
        //private string GetInstallType()
        //{
        //    // Чтение из реестра
        //    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software/MdSearch"))
        //    {
        //        var value = key?.GetValue("InstallType") as string;
        //        if (!string.IsNullOrEmpty(value))
        //            return value;
        //    }

        //    // Либо чтение из файла
        //    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        //    string filePath = Path.Combine(appDataPath, "MdSearch", "installtype.cfg");

        //    if (File.Exists(filePath))
        //    {
        //        return File.ReadAllText(filePath).Trim();
        //    }

        //    return "COMMERCIAL";
        //}

        private void InitializeUserInterface()
        {
            if (_userRoleId != 2)
            {
                ManageUsers.Visibility = Visibility.Collapsed;

                var contextMenu = (ContextMenu)FindResource("OtherContextMenu");
                if (contextMenu != null)
                {
                    foreach (var item in contextMenu.Items)
                    {
                        if (item is MenuItem menuItem && menuItem.Header.ToString() == "Настройки безопасности")
                        {
                            menuItem.Visibility = Visibility.Collapsed;
                            break;
                        }
                    }
                }

                var userId = entities.Users
                    .Where(u => u.RoleId == _userRoleId)
                    .Select(u => u.Id)
                    .FirstOrDefault();

                if (userId != 0)
                {
                    var permission = entities.UserPermissions
                        .FirstOrDefault(p => p.UserID == userId);

                    if (permission?.CanDeleteAll != true)
                    {
                        ClearAll.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ClearAll.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    ClearAll.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ClearAll.Visibility = Visibility.Visible;
            }
        }

        private void InitializeFileWatcher()
        {
            fileWatcher = new FileSystemWatcher();
            fileWatcher.IncludeSubdirectories = true;
            fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                      NotifyFilters.LastWrite | NotifyFilters.Size;

            fileWatcher.Renamed += OnFileRenamed;
            fileWatcher.Changed += OnFileChanged;
            fileWatcher.Deleted += OnFileDeleted;
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath.Contains("~$") || e.FullPath.EndsWith(".tmp"))
                return;

            Dispatcher.Invoke(() =>
            {
                if (filePathDictionary.TryGetValue(e.FullPath, out var file))
                {
                    LoadFiles();
                }
            });
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (e.FullPath.Contains("~$") || e.FullPath.EndsWith(".tmp"))
                return;

            Dispatcher.Invoke(() =>
            {
                if (filePathDictionary.TryGetValue(e.OldFullPath, out var file))
                {
                    LoadFiles();
                }
            });
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath.Contains("~$") || e.FullPath.EndsWith(".tmp"))
                return;

            Dispatcher.Invoke(() =>
            {
                if (filePathDictionary.TryGetValue(e.FullPath, out var file))
                {
                    var fileInfo = new FileInfo(e.FullPath);

                    var fileInDb = entities.Files.FirstOrDefault(f => f.FileID == file.FileID);
                    if (fileInDb != null)
                    {
                        LogFieldChange(file.FileID, "FileSize", fileInDb.FileSize, fileInfo.Length);
                        LogFieldChange(file.FileID, "ModificationDate", fileInDb.ModificationDate, fileInfo.LastWriteTime);
                        LogFieldChange(file.FileID, "CreationDate", fileInDb.CreationDate, fileInfo.CreationTime);

                        fileInDb.FileSize = fileInfo.Length;
                        fileInDb.ModificationDate = fileInfo.LastWriteTime;
                        fileInDb.CreationDate = fileInfo.CreationTime;
                        entities.SaveChanges();
                    }
                    file.FileSize = fileInfo.Length;
                    file.ModificationDate = fileInfo.LastWriteTime;
                    file.CreationDate = fileInfo.CreationTime;

                    filesViewSource.View.Refresh();
                }
            });
        }

        private void UpdateWatchedPaths()
        {
            if (fileWatcher == null)
                return;

            if (filesList == null || filesList.Count == 0)
                return;

            filePathDictionary = filesList.ToDictionary(f => f.FilePath, f => f);

            var directories = filesList
                .Select(f => Path.GetDirectoryName(f.FilePath))
                .Where(dir => !string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                .Distinct()
                .ToList();

            if (directories.Count == 0)
                return;

            fileWatcher.EnableRaisingEvents = false;

            foreach (var dir in directories)
            {
                try
                {
                    fileWatcher.Path = dir;
                    fileWatcher.EnableRaisingEvents = true;
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Ошибка наблюдения за директорией {dir}: {ex.Message}");
                }
            }
        }

        private void UpdateFileCounts()
        {
            TotalFilesTextBlock.Text = $"Всего файлов: {filesList.Count}";
            FilteredFilesTextBlock.Text = $"Найдено файлов: {filesViewSource.View.Cast<Files>().Count()}";
        }

        private void LoadFiles()
        {
            try
            {
                filesList = entities.Files.OrderByDescending(f => f.UploadDate).ToList();
                filePathDictionary = filesList.ToDictionary(f => f.FilePath, f => f);
                UpdateWatchedPaths();

                filesViewSource.Source = filesList;
                ApplySorting();

                // Применеение текущего фильтра (если выбрана не папка Все)
                var selectedFolder = FoldersListView.SelectedItem as Folders;
                if (selectedFolder != null && selectedFolder.FolderID != -1)
                {
                    filesViewSource.View.Filter = item =>
                    {
                        var file = item as Files;
                        return file?.FolderID == selectedFolder.FolderID;
                    };
                }
                else
                {
                    filesViewSource.View.Filter = null;
                }

                filesViewSource.View.Refresh();
                UpdateFileCounts();
                CheckFile();

                var deletedFiles = new List<Files>();
                var movedFilesInfo = new StringBuilder();
                var renamedFilesInfo = new StringBuilder();
                var missingFilesInfo = new StringBuilder();
                int foundCount = 0;

                var missingFiles = filesList.Where(f => !File.Exists(f.FilePath)).ToList();

                foreach (var file in missingFiles)
                {
                    string originalPath = file.FilePath;
                    string originalName = Path.GetFileName(originalPath);

                    if (TryFindMovedFile(file, out string newPath, out List<string> actionTypes))
                    {
                        file.FilePath = newPath;
                        entities.Entry(file).State = EntityState.Modified;
                        foundCount++;

                        filePathDictionary.Remove(originalPath);
                        filePathDictionary[newPath] = file;

                        foreach (var actionType in actionTypes)
                        {
                            if (actionType == "renamed")
                                renamedFilesInfo.AppendLine($"- Было: {originalName} -> Стало: {Path.GetFileName(newPath)}");
                            else if (actionType == "moved")
                                movedFilesInfo.AppendLine($"- {file.FileName} (новое расположение: {Path.GetDirectoryName(newPath)})");
                        }
                    }
                    else
                    {
                        deletedFiles.Add(file);
                        missingFilesInfo.AppendLine($"- {file.FileName} (был по пути: {originalPath})");
                    }
                }

                if (foundCount > 0)
                {
                    entities.SaveChanges();
                    filesList = entities.Files.ToList();
                    filePathDictionary = filesList.ToDictionary(f => f.FilePath, f => f);
                    UpdateWatchedPaths();
                }

                if (deletedFiles.Any() || movedFilesInfo.Length > 0 || renamedFilesInfo.Length > 0)
                {
                    var dialog = new FileSearchResultWindow();
                    dialog.ShowDeleteButton(deletedFiles.Any());

                    dialog.SetResults(
                        foundCount,
                        renamedFilesInfo.ToString(),
                        movedFilesInfo.ToString(),
                        missingFilesInfo.ToString()
                    );

                    dialog.ManualSearchClicked += (filesFound) =>
                    {
                        ManualFileSearch(deletedFiles, out bool found);
                        if (found)
                        {
                            filesList = entities.Files.ToList();
                            filePathDictionary = filesList.ToDictionary(f => f.FilePath, f => f);
                            UpdateWatchedPaths();
                            dialog.Close();
                        }
                    };

                    dialog.OpenFolderClicked += () =>
                    {
                        if (deletedFiles.Any())
                            OpenOriginalFolder(deletedFiles.First());
                    };

                    dialog.DeleteClicked += () =>
                    {
                        DeleteMissingFiles(deletedFiles);
                        dialog.Close();
                    };

                    dialog.Closed += (sender, e) =>
                    {
                        try
                        {
                            if (!dialog.WasDeleted && deletedFiles.Any())
                            {
                                DeleteMissingFiles(deletedFiles);
                            }

                            // Обновление интерфейса в главном потоке
                            Dispatcher.Invoke(() =>
                            {
                                RefreshFileView();
                                this.Show();
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                                MessageBox.Show($"Ошибка при закрытии окна: {ex.Message}", "Ошибка",
                                              MessageBoxButton.OK, MessageBoxImage.Error));
                        }
                    };

                    Application.Current.MainWindow?.Hide();
                    dialog.ShowDialog();
                }
                else
                {
                    RefreshFileView();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private readonly string[] supportedExtensions = new[]
        {
            ".txt", ".doc", ".docx", ".xls", ".xlsx",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".mp3", ".wav", ".aac",
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm"
        };

        public void LoadingFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Supported Files|*.txt;*.doc;*.docx;*.xls;*.xlsx;*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp3;*.wav;*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.aac;*.webm|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProcessFiles(openFileDialog.FileNames);
            }
        }

        public void LoadingFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        string folderName = Path.GetFileName(folderDialog.SelectedPath);
                        if (string.IsNullOrEmpty(folderName)) folderName = "Новая папка";

                        if (entities.Folders.Any(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
                        {
                            MessageBox.Show("Папка с таким названием уже существует", "Информация",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        var newFolder = new Folders
                        {
                            Name = folderName,
                            CreationDate = DateTime.Now,
                            ModificationDate = DateTime.Now,
                            IsFolder = true
                        };

                        entities.Folders.Add(newFolder);
                        entities.SaveChanges();

                        var allFiles = Directory.GetFiles(folderDialog.SelectedPath, "*.*", SearchOption.AllDirectories)
                            .Where(file => !IsTemporaryOrSystemFile(file))
                            .ToArray();

                        var filteredFiles = allFiles.Where(file =>
                            supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())).ToList();

                        ProcessFiles(filteredFiles);

                        var addedFiles = entities.Files
                            .Where(f => f.FolderID == null &&
                                        filteredFiles.Contains(f.FilePath))
                            .ToList();

                        foreach (var file in addedFiles)
                        {
                            file.FolderID = newFolder.FolderID;
                        }

                        entities.SaveChanges();
                        LoadFolders();

                        var folderToSelect = entities.Folders.FirstOrDefault(f => f.FolderID == newFolder.FolderID);
                        if (folderToSelect != null)
                        {
                            FoldersListView.SelectedItem = folderToSelect;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при загрузке папки: {ex.Message}", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private bool IsTemporaryOrSystemFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            if (fileName.StartsWith("~$") || fileName.StartsWith(".~") || fileName.StartsWith("._"))
            {
                return true;
            }

            if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                var attributes = File.GetAttributes(filePath);
                if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        public void ProcessFiles(IEnumerable<string> filePaths)
        {
            int filesAdded = 0;
            int brokenFiles = 0;
            int tempFilesSkipped = 0;
            int duplicateFilesSkipped = 0;
            var brokenFilesList = new StringBuilder();
            var tempFilesList = new StringBuilder();
            var duplicateFilesList = new StringBuilder();

            foreach (string filePath in filePaths)
            {
                try
                {
                    var selectedFolder = FoldersListView.SelectedItem as Folders;

                    if (selectedFolder != null && selectedFolder.FolderID != -1)
                    {
                        var folderExists = entities.Folders.Any(f => f.FolderID == selectedFolder.FolderID);
                        if (!folderExists)
                        {
                            selectedFolder = null; // Сброс привязки к несуществующей папке
                        }
                    }

                    string fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith("~$") || fileName.StartsWith("._"))
                    {
                        tempFilesSkipped++;
                        tempFilesList.AppendLine($"- {fileName}");
                        continue;
                    }

                    if (fileName.IndexOf("копия", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        duplicateFilesSkipped++;
                        duplicateFilesList.AppendLine($"- {fileName} (копия)");
                        continue;
                    }

                    // Проверка дубликатов
                    string fileHash = ComputeFileHash(filePath);

                    if (entities.Files.Any(f => f.FileHash != null && f.FileHash == fileHash))
                    {
                        duplicateFilesSkipped++;
                        duplicateFilesList.AppendLine($"- {fileName}");
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        brokenFiles++;
                        brokenFilesList.AppendLine($"- {fileName}: Файл не существует");
                        continue;
                    }

                    // Проверка атрибутов файла
                    var attributes = File.GetAttributes(filePath);
                    if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        tempFilesSkipped++;
                        tempFilesList.AppendLine($"- {fileName} (скрытый/системный)");
                        continue;
                    }

                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length == 0)
                    {
                        brokenFiles++;
                        brokenFilesList.AppendLine($"- {fileName}: Пустой файл (0 байт)");
                        continue;
                    }

                    if (!IsFileAccessible(filePath))
                    {
                        brokenFiles++;
                        brokenFilesList.AppendLine($"- {fileName}: Файл заблокирован или поврежден");
                        continue;
                    }

                    string fileExtension = Path.GetExtension(filePath).ToLower();
                    if (IsImageFile(fileExtension) && !IsImageValid(filePath))
                    {
                        brokenFiles++;
                        brokenFilesList.AppendLine($"- {fileName}: Поврежденное изображение");
                        continue;
                    }

                    var file = new Files
                    {
                        FileName = fileName,
                        FileFormat = fileExtension,
                        FileSize = fileInfo.Length,
                        UserName = Environment.UserName,
                        UploadDate = DateTime.Now,
                        CreationDate = File.GetCreationTime(filePath),
                        ModificationDate = File.GetLastWriteTime(filePath),
                        FilePath = filePath,
                        FolderID = selectedFolder?.FolderID,
                        FileHash = fileHash
                    };

                    if (file.FolderID != null)
                    {
                        var folderExists = entities.Folders.Any(f => f.FolderID == file.FolderID);
                        if (!folderExists)
                        {
                            file.FolderID = null;
                        }
                    }

                    entities.Files.Add(file);
                    entities.SaveChanges();
                    ScanFile(file);
                    filesList.Insert(0, file);
                    filesAdded++;
                }
                catch (DbUpdateException dbEx)
                {
                    brokenFiles++;
                    var errorMsg = new StringBuilder();
                    errorMsg.AppendLine($"- {Path.GetFileName(filePath)}: Ошибка БД: {dbEx.Message}");

                    var innerEx = dbEx.InnerException;
                    while (innerEx != null)
                    {
                        errorMsg.AppendLine($"   Внутренняя ошибка: {innerEx.Message}");
                        Debug.WriteLine($"Ошибка БД: {innerEx.GetType().Name}: {innerEx.Message}");
                        Debug.WriteLine($"Stack Trace: {innerEx.StackTrace}");
                        innerEx = innerEx.InnerException;
                    }

                    if (dbEx.InnerException is SqlException sqlEx)
                    {
                        errorMsg.AppendLine($"   SQL ошибка #{sqlEx.Number}: {sqlEx.Message}");
                        Debug.WriteLine($"SQL ошибка: {sqlEx.Number} - {sqlEx.Message}");
                    }

                    brokenFilesList.Append(errorMsg.ToString());
                }
            }

            if (tempFilesSkipped > 0)
            {
                MessageBox.Show($"Пропущено временных файлов: {tempFilesSkipped}\n\n{tempFilesList}",
                              "Временные файлы", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (duplicateFilesSkipped > 0)
            {
                MessageBox.Show($"Пропущено дубликатов файлов: {duplicateFilesSkipped}\n\n{duplicateFilesList}",
                              "Дубликаты файлов", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (brokenFiles > 0)
            {
                MessageBox.Show($"Не загружено поврежденных файлов: {brokenFiles}\n\n{brokenFilesList}",
                              "Ошибки загрузки", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (filesAdded > 0)
            {
                filesViewSource.View.Refresh();
                MessageBox.Show($"Успешно загружено файлов: {filesAdded}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateFileCounts();
            }
        }

        private string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        // Проверка, доступен ли файл для чтения
        private bool IsFileAccessible(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool IsImageFile(string extension)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            return imageExtensions.Contains(extension.ToLowerInvariant());
        }

        // Проверка целостности изображения
        private bool IsImageValid(string filePath)
        {
            try
            {
                using (var img = System.Drawing.Image.FromFile(filePath))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void CheckFile()
        {
            var deletedFiles = new List<Files>();
            var movedFilesInfo = new StringBuilder();
            var renamedFilesInfo = new StringBuilder();
            var missingFilesInfo = new StringBuilder();
            int foundCount = 0;

            // Проверка изменений
            var missingFiles = filesList.Where(f => !File.Exists(f.FilePath)).ToList();
            if (!missingFiles.Any()) return;

            foreach (var file in missingFiles)
            {
                string originalPath = file.FilePath;
                string originalName = Path.GetFileName(originalPath);

                if (TryFindMovedFile(file, out string newPath, out List<string> actionTypes))
                {
                    file.FilePath = newPath;
                    entities.Entry(file).State = EntityState.Modified;
                    foundCount++;

                    filePathDictionary.Remove(originalPath);
                    filePathDictionary[newPath] = file;

                    foreach (var actionType in actionTypes)
                    {
                        if (actionType == "renamed")
                            renamedFilesInfo.AppendLine($"- Было: {originalName} -> Стало: {Path.GetFileName(newPath)}");
                        else if (actionType == "moved")
                            movedFilesInfo.AppendLine($"- {file.FileName} (новое расположение: {Path.GetDirectoryName(newPath)})");
                    }
                }
                else
                {
                    deletedFiles.Add(file);
                    missingFilesInfo.AppendLine($"- {file.FileName} (был по пути: {originalPath})");
                }
            }

            if (foundCount > 0)
            {
                entities.SaveChanges();
                filesList = entities.Files.ToList();
                filePathDictionary = filesList.ToDictionary(f => f.FilePath, f => f);
                UpdateWatchedPaths();
            }

            if (deletedFiles.Any() || movedFilesInfo.Length > 0 || renamedFilesInfo.Length > 0)
            {
                ShowFileSearchResultWindow(deletedFiles, foundCount,
                                        renamedFilesInfo.ToString(),
                                        movedFilesInfo.ToString(),
                                        missingFilesInfo.ToString());
            }
        }

        // Динамика. Отслеживание файлов на диске
        private void ShowFileSearchResultWindow(List<Files> deletedFiles, int foundCount, string renamedInfo, string movedInfo, string missingInfo)
        {
            var dialog = new FileSearchResultWindow();
            dialog.ShowDeleteButton(deletedFiles.Any());
            dialog.SetResults(foundCount, renamedInfo, movedInfo, missingInfo);

            dialog.ManualSearchClicked += (filesFound) =>
            {
                ManualFileSearch(deletedFiles, out bool found);
                if (found)
                {
                    filesList = entities.Files.ToList();
                    filePathDictionary = filesList.ToDictionary(f => f.FilePath, f => f);
                    UpdateWatchedPaths();
                    dialog.Close();
                }
            };

            dialog.OpenFolderClicked += () => OpenOriginalFolder(deletedFiles.First());
            dialog.DeleteClicked += () => DeleteMissingFiles(deletedFiles);
            dialog.Closed += (sender, e) => this.Show();

            dialog.ShowDialog();

            RefreshFileView();
        }

        private void RefreshFileView()
        {
            if (filesViewSource != null)
            {
                filesViewSource.Source = null;
                filesViewSource.Source = filesList;
            }
            UpdateFileCounts();
        }
        private void DeleteMissingFiles(List<Files> filesToDelete)
        {
            try
            {
                using (var freshEntities = new Entities())
                {
                    foreach (var file in filesToDelete.ToList())
                    {
                        var fileInDb = freshEntities.Files.FirstOrDefault(f => f.FileID == file.FileID);
                        if (fileInDb != null)
                        {
                            DeleteRelatedData(freshEntities, fileInDb.FileID);

                            freshEntities.Files.Remove(fileInDb);
                        }

                        filesList.RemoveAll(f => f.FileID == file.FileID);
                        filePathDictionary.Remove(file.FilePath);
                    }
                    freshEntities.SaveChanges();
                }
                LoadFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении файлов: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteRelatedData(Entities context, int fileId)
        {
            var images = context.Images.Where(i => i.FileID == fileId).ToList();
            foreach (var img in images)
                context.Images.Remove(img);

            var audioFiles = context.AudioFiles.Where(a => a.FileID == fileId).ToList();
            foreach (var audio in audioFiles)
                context.AudioFiles.Remove(audio);

            var videoFiles = context.VideoFiles.Where(v => v.FileID == fileId).ToList();
            foreach (var video in videoFiles)
                context.VideoFiles.Remove(video);

            var documents = context.Documents.Where(d => d.FileID == fileId).ToList();
            foreach (var doc in documents)
                context.Documents.Remove(doc);

            var reports = context.Reports.Where(r => r.FileID == fileId).ToList();
            foreach (var report in reports)
                context.Reports.Remove(report);

            var history = context.ChangesHistory.Where(c => c.FileID == fileId).ToList();
            foreach (var hist in history)
                context.ChangesHistory.Remove(hist);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            CheckFilesForChanges();
        }

        private void CheckFilesForChanges()
        {
            foreach (var file in filesList.ToList())
            {
                if (File.Exists(file.FilePath))
                {
                    var fileInfo = new FileInfo(file.FilePath);

                    if (fileInfo.LastWriteTime != file.ModificationDate ||
                        fileInfo.Length != file.FileSize)
                    {
                        var fileInDb = entities.Files.FirstOrDefault(f => f.FileID == file.FileID);
                        if (fileInDb != null)
                        {
                            fileInDb.FileSize = fileInfo.Length;
                            fileInDb.ModificationDate = fileInfo.LastWriteTime;
                            entities.SaveChanges();
                        }

                        file.FileSize = fileInfo.Length;
                        file.ModificationDate = fileInfo.LastWriteTime;
                    }
                }
                else
                {
                    if (!TryFindMovedFile(file, out string newPath, out List<string> actionTypes))
                    {
                        var fileInDb = entities.Files.FirstOrDefault(f => f.FileID == file.FileID);
                        if (fileInDb != null)
                        {
                            entities.Files.Remove(fileInDb);
                        }
                        filesList.Remove(file);
                        filePathDictionary.Remove(file.FilePath);
                        entities.SaveChanges();
                    }
                    else
                    {
                        var fileInDb = entities.Files.FirstOrDefault(f => f.FileID == file.FileID);
                        if (fileInDb != null)
                        {
                            fileInDb.FilePath = newPath;
                            fileInDb.FileName = Path.GetFileName(newPath);
                            entities.SaveChanges();
                        }
                        file.FilePath = newPath;

                        var newFileInfo = new FileInfo(newPath);
                        file.FileSize = newFileInfo.Length;
                        file.ModificationDate = newFileInfo.LastWriteTime;
                    }
                }
            }

            Dispatcher.Invoke(() =>
            {
                LoadFiles();
            });

            filesViewSource.View.Refresh();
            UpdateFileCounts();
        }

        private bool TryFindMovedFile(Files file, out string newPath, out List<string> actionTypes)
        {
            newPath = null;
            actionTypes = new List<string>();

            var searchLocations = new List<string>
            {
                Path.GetDirectoryName(file.FilePath),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Path.GetPathRoot(file.FilePath),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };

            foreach (var location in searchLocations.Distinct().Where(Directory.Exists))
            {
                var found = SearchInDirectory(location, file, maxDepth: 3);
                if (found != null)
                {
                    newPath = found;

                    bool isSameDirectory = Path.GetDirectoryName(found).Equals(
                        Path.GetDirectoryName(file.FilePath),
                        StringComparison.OrdinalIgnoreCase);

                    bool isRenamed = !Path.GetFileName(found).Equals(
                        file.FileName,
                        StringComparison.OrdinalIgnoreCase);

                    if (isSameDirectory && isRenamed)
                    {
                        actionTypes.Add("renamed");
                    }
                    else if (!isSameDirectory)
                    {
                        if (isRenamed)
                        {
                            actionTypes.Add("renamed");
                            actionTypes.Add("moved");
                        }
                        else
                        {
                            actionTypes.Add("moved");
                        }
                    }

                    var fileInDb = entities.Files.FirstOrDefault(f => f.FileID == file.FileID);
                    if (fileInDb != null)
                    {
                        if (fileInDb.FilePath != newPath)
                        {
                            LogFieldChange(file.FileID, "FilePath", fileInDb.FilePath, newPath);
                        }

                        string newFileName = Path.GetFileName(newPath);
                        if (fileInDb.FileName != newFileName)
                        {
                            LogFieldChange(file.FileID, "FileName", fileInDb.FileName, newFileName);
                        }

                        fileInDb.FilePath = newPath;
                        fileInDb.FileName = newFileName;
                        entities.SaveChanges();
                    }

                    file.FilePath = newPath;
                    file.FileName = Path.GetFileName(newPath);

                    return true;
                }
            }
            return false;
        }

        private string SearchInDirectory(string directory, Files originalFile, int maxDepth)
        {
            try
            {
                var sameNameFiles = Directory.EnumerateFiles(directory, originalFile.FileName);
                foreach (var filePath in sameNameFiles)
                {
                    if (FilesMatch(originalFile, filePath))
                        return filePath;
                }

                if (maxDepth > 0)
                {
                    foreach (var filePath in Directory.EnumerateFiles(directory))
                    {
                        if (FilesMatch(originalFile, filePath))
                            return filePath;
                    }

                    foreach (var subDir in Directory.EnumerateDirectories(directory))
                    {
                        var found = SearchInDirectory(subDir, originalFile, maxDepth - 1);
                        if (found != null) return found;
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        private void ManualFileSearch(List<Files> filesToSearch, out bool anyFileFound)
        {
            anyFileFound = false;
            var filesFound = new List<Files>();

            foreach (var file in filesToSearch.ToList())
            {
                try
                {
                    var openDialog = new OpenFileDialog
                    {
                        Title = $"Найдите файл: {file.FileName} (оригинальное имя: {Path.GetFileName(file.FilePath)}, размер: {file.FileSize} байт, дата: {file.ModificationDate})",
                        Filter = $"Файлы {Path.GetExtension(file.FileName)}|*{Path.GetExtension(file.FileName)}|Все файлы (*.*)|*.*",
                        FileName = file.FileName
                    };

                    if (openDialog.ShowDialog() == true)
                    {
                        if (File.Exists(openDialog.FileName))
                        {
                            file.FilePath = openDialog.FileName;

                            var fileInDb = entities.Files.FirstOrDefault(f => f.FileID == file.FileID);
                            if (fileInDb != null)
                            {
                                fileInDb.FilePath = file.FilePath;
                                entities.Entry(fileInDb).State = EntityState.Modified;
                            }
                            else
                            {
                                entities.Files.Attach(file);
                                entities.Entry(file).State = EntityState.Modified;
                            }

                            filesFound.Add(file);
                            anyFileFound = true;
                            filesToSearch.Remove(file);
                        }
                        else
                        {
                            MessageBox.Show("Выбранный файл не существует!", "Ошибка",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при выборе файла: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (filesFound.Any())
            {
                try
                {
                    entities.SaveChanges();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении изменений в базе данных: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool FilesMatch(Files dbFile, string candidatePath)
        {
            try
            {
                var fileInfo = new FileInfo(candidatePath);

                if (!File.Exists(candidatePath)) return false;
                if (fileInfo.Length != dbFile.FileSize) return false;

                DateTime modificationDate = dbFile.ModificationDate.GetValueOrDefault(DateTime.MinValue);
                if (Math.Abs((fileInfo.LastWriteTime - modificationDate).TotalSeconds) > 5)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OpenOriginalFolder(Files file)
        {
            try
            {
                string directory = Path.GetDirectoryName(file.FilePath);
                if (Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", directory);
                }
                else
                {
                    MessageBox.Show("Исходная директория больше не существует", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии папки: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (FilterPanel.Visibility == Visibility.Visible)
            {
                FilterPanel.Visibility = Visibility.Collapsed;
                this.Height -= 100;
            }
            else
            {
                FilterPanel.Visibility = Visibility.Visible;
                this.Height += 100;
            }
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            _isSortedNewestFirst = !_isSortedNewestFirst;
            ApplySorting();
        }

        private void ApplySorting()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(FilesListView.ItemsSource);

            if (_isSortedNewestFirst)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("UploadDate", ListSortDirection.Descending));
                view.SortDescriptions.Add(new SortDescription("ModificationDate", ListSortDirection.Descending));
                SortButton.ToolTip = "Сортировка: новые → старые";
                SortIconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/sort_icon_dp.png"));
            }
            else
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("UploadDate", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("ModificationDate", ListSortDirection.Ascending));
                SortButton.ToolTip = "Сортировка: старые → новые";
                SortIconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/sort_icon_up.png"));
            }

            view.Refresh();
        }

        private void SizeFilterComboBox_SC(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TypeFilterComboBox_SC(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Введите атрибуты файла...")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = Brushes.Black;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Введите атрибуты файла...";
                SearchTextBox.Foreground = Brushes.Gray;
            }
        }

        private void ApplyFilters()
        {
            string searchText = SearchTextBox.Text == "Введите атрибуты файла..." ? "" : SearchTextBox.Text;
            if (filesViewSource?.View == null) return;

            filesViewSource.View.Filter = item => FilterFileItem(item);
            UpdateFileCounts();
        }

        private bool FilterFileItem(object item)
        {
            var file = item as Files;
            if (file == null) return false;

            // Фильтр по папке (если выбрана не Все)
            var selectedFolder = FoldersListView.SelectedItem as Folders;
            if (selectedFolder != null && selectedFolder.FolderID != -1 && file.FolderID != selectedFolder.FolderID)
                return false;

            // Фильтр по поисковой строке
            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text) &&
                SearchTextBox.Text != "Введите атрибуты файла...")
            {
                string searchText = SearchTextBox.Text.ToLower();
                if (!(file.FileName.ToLower().Contains(searchText) ||
                    file.FileFormat.ToLower().Contains(searchText) ||
                    file.FileSize.ToString().Contains(searchText) ||
                    file.UploadDate.ToString().Contains(searchText) ||
                    file.CreationDate.ToString().Contains(searchText) ||
                    file.ModificationDate.ToString().Contains(searchText)))
                    return false;
            }

            var selectedSizeFilter = SizeFilterComboBox.SelectedItem as ComboBoxItem;
            if (selectedSizeFilter != null && selectedSizeFilter.Content.ToString() != "Все")
            {
                long fileSizeMb = file.FileSize / (1024 * 1024);
                switch (selectedSizeFilter.Content.ToString())
                {
                    case "Меньше 1 МБ": if (fileSizeMb >= 1) return false; break;
                    case "1-10 МБ": if (fileSizeMb < 1 || fileSizeMb > 10) return false; break;
                    case "10-100 МБ": if (fileSizeMb < 10 || fileSizeMb > 100) return false; break;
                    case "Больше 100 МБ": if (fileSizeMb <= 100) return false; break;
                }
            }

            var selectedTypeFilter = TypeFilterComboBox.SelectedItem as ComboBoxItem;
            if (selectedTypeFilter != null && selectedTypeFilter.Content.ToString() != "Все типы")
            {
                string selectedType = selectedTypeFilter.Content.ToString();
                bool isMatch = false;

                switch (selectedType)
                {
                    case "Текстовые (.txt)": isMatch = file.FileFormat == ".txt"; break;
                    case "Документы (.doc, .docx)": isMatch = file.FileFormat == ".doc" || file.FileFormat == ".docx"; break;
                    case "Таблицы (.xls, .xlsx)": isMatch = file.FileFormat == ".xls" || file.FileFormat == ".xlsx"; break;
                    case "Изображения (.jpg, .jpeg, .png)":
                        isMatch = file.FileFormat == ".jpg" || file.FileFormat == ".jpeg" || file.FileFormat == ".png";
                        break;
                    case "Графика (.gif, .bmp)": isMatch = file.FileFormat == ".gif" || file.FileFormat == ".bmp"; break;
                    case "Аудио (.mp3, .wav, .aac)":
                        isMatch = file.FileFormat == ".mp3" || file.FileFormat == ".wav" || file.FileFormat == ".aac";
                        break;
                    case "Видео (.mp4, .avi, .mkv, .mov, .wmv, .webm)":
                        isMatch = file.FileFormat == ".mp4" || file.FileFormat == ".avi" || file.FileFormat == ".mkv" ||
                                  file.FileFormat == ".mov" || file.FileFormat == ".wmv" || file.FileFormat == ".webm";
                        break;
                }

                if (!isMatch) return false;
            }

            return true;
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
        {
            e.Accepted = FilterFileItem(e.Item);
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = FilesListView.SelectedItem as Files;
            if (selectedFile != null)
            {
                var dialog = new RenameDialog(selectedFile.FileName);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    var fileInDb = entities.Files.FirstOrDefault(f => f.FileID == selectedFile.FileID);
                    if (fileInDb != null)
                    {
                        fileInDb.FileName = dialog.FileName;
                        entities.SaveChanges();
                    }

                    selectedFile.FileName = dialog.FileName;
                    selectedFile.IsDirty = true;
                    filesViewSource.View.Refresh();

                    MessageBox.Show("Файл успешно переименован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedFilesByCheckbox = filesList.Where(file => file.IsSelected).ToList();

            var selectedFilesByList = FilesListView.SelectedItems.Cast<Files>().ToList();

            // Объединение списков
            var filesToDelete = new List<Files>();
            filesToDelete.AddRange(selectedFilesByCheckbox);
            filesToDelete.AddRange(selectedFilesByList.Except(filesToDelete));

            if (!filesToDelete.Any())
            {
                MessageBox.Show("Нет выбранных файлов для удаления", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить этот(эти) {filesToDelete.Count} файл(ы)?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var file in filesToDelete)
                    {
                        var images = entities.Images.Where(i => i.FileID == file.FileID).ToList();
                        var audioFiles = entities.AudioFiles.Where(a => a.FileID == file.FileID).ToList();
                        var videoFiles = entities.VideoFiles.Where(v => v.FileID == file.FileID).ToList();
                        var documents = entities.Documents.Where(d => d.FileID == file.FileID).ToList();
                        var reports = entities.Reports.Where(r => r.FileID == file.FileID).ToList();

                        // Удаление записей по одной
                        foreach (var image in images)
                        {
                            entities.Images.Remove(image);
                        }
                        foreach (var audioFile in audioFiles)
                        {
                            entities.AudioFiles.Remove(audioFile);
                        }
                        foreach (var videoFile in videoFiles)
                        {
                            entities.VideoFiles.Remove(videoFile);
                        }
                        foreach (var document in documents)
                        {
                            entities.Documents.Remove(document);
                        }
                        foreach (var report in reports)
                        {
                            entities.Reports.Remove(report);
                        }
                        entities.Files.Remove(file);
                    }
                    entities.SaveChanges();

                    filesList.RemoveAll(file => filesToDelete.Contains(file));

                    filesViewSource.View.Refresh();
                    MessageBox.Show("Файлы успешно удалены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateFileCounts();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении файлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedFilesByCheckbox = filesList.Where(file => file.IsSelected).ToList();

            var selectedFilesByList = FilesListView.SelectedItems.Cast<Files>().ToList();

            var filesToRemoveFromFolder = new List<Files>();
            filesToRemoveFromFolder.AddRange(selectedFilesByCheckbox);
            filesToRemoveFromFolder.AddRange(selectedFilesByList.Except(filesToRemoveFromFolder));

            if (!filesToRemoveFromFolder.Any())
            {
                MessageBox.Show("Нет выбранных файлов для удаления из папки", "Информация",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить {filesToRemoveFromFolder.Count} файл(ов) из папки?",
                                        "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {              
                    foreach (var file in filesToRemoveFromFolder)
                    {
                        file.FolderID = null;
                    }

                    entities.SaveChanges();

                    LoadFiles();
                    MessageBox.Show($"Успешно удалено {filesToRemoveFromFolder.Count} файл(ов) из папки!",
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении файлов из папки: {ex.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ScanMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = FilesListView.SelectedItems.Cast<Files>().ToList();
            selectedFiles.AddRange(filesList.Where(file => file.IsSelected && !selectedFiles.Contains(file)).ToList());

            if (!selectedFiles.Any())
            {
                MessageBox.Show("Нет выбранных файлов для сканирования", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var selectedFile in selectedFiles)
            {
                var progressWindow = new ScanProgressWindow();
                progressWindow.Show();

                Task.Run(async () =>
                {
                    try
                    {
                        long totalSize = selectedFile.FileSize;
                        long loadedSize = 0;
                        int totalSteps = 99;
                        int currentProgress = 0;
                        DateTime startTime = DateTime.Now;

                        while (currentProgress <= totalSteps)
                        {
                            if (progressWindow.CancellationToken.IsCancellationRequested)
                                throw new OperationCanceledException();

                            await Task.Delay(30);
                            loadedSize += totalSize / totalSteps;
                            currentProgress++;

                            var elapsedTime = DateTime.Now - startTime;
                            var speed = (loadedSize / 1024) / elapsedTime.TotalSeconds;
                            var timeRemaining = (totalSize - loadedSize) / (speed * 1024);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressWindow.UpdateProgress(currentProgress,
                                    $"Скорость загрузки: {speed:F2} KB/s",
                                    $"Загружено: {loadedSize / 1024} KB из {totalSize / 1024} KB",
                                    $"Осталось времени: {timeRemaining:F0} сек");
                            });
                        }
                        var metadata = ScanFile(selectedFile);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            progressWindow.Close();
                            ShowScanResult(metadata);
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            progressWindow.Close();
                            MessageBox.Show("Сканирование отменено", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            progressWindow.Close();
                            MessageBox.Show($"Ошибка при сканировании файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        private void OpenFileLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = filesList.Where(file => file.IsSelected || FilesListView.SelectedItems.Contains(file)).ToList();

            foreach (var selectedFile in selectedFiles)
            {
                try
                {
                    string fullPath = Path.GetFullPath(selectedFile.FilePath);
                    if (File.Exists(fullPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                    }
                    else
                    {
                        MessageBox.Show($"Файл {selectedFile.FileName} не найден по указанному пути", "Ошибка",
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть расположение файла: {ex.Message}", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private MetadataModel ScanFile(Files file)
        {
            try
            {
                var metadata = new MetadataModel
                {
                    FileID = file.FileID,
                    UserName = file.UserName,
                    FileName = file.FileName,
                    FileFormat = file.FileFormat,
                    FilePath = file.FilePath,
                    FileSize = file.FileSize,
                    UploadDate = file.UploadDate,
                    CreationDate = file.CreationDate ?? DateTime.MinValue,
                    ModificationDate = file.ModificationDate ?? DateTime.MinValue
                };

                string filePath = file.FilePath;

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Файл не найден: {filePath}");
                }

                switch (file.FileFormat.ToLower())
                {
                    case ".txt":
                        metadata = ExtractTextFileMetadata(filePath, metadata);
                        SaveDocumentMetadata(file, metadata);
                        break;

                    case ".doc":
                    case ".docx":
                        metadata = ExtractDocxMetadata(filePath, metadata);
                        SaveDocumentMetadata(file, metadata);
                        break;

                    case ".xlsx":
                    case ".xls":
                        metadata = ExtractXlsxMetadata(filePath, metadata);
                        SaveDocumentMetadata(file, metadata);
                        break;

                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".gif":
                    case ".bmp":
                        metadata = ExtractImageMetadata(filePath, metadata);
                        SaveImageMetadata(file, metadata);
                        break;

                    case ".mp3":
                    case ".wav":
                    case ".aac":
                        metadata = ExtractAudioMetadata(filePath, metadata);
                        SaveAudioFileMetadata(file, metadata);
                        break;

                    case ".mp4":
                    case ".webm":
                    case ".avi":
                    case ".mkv":
                    case ".mov":
                    case ".wmv":
                        metadata = ExtractVideoMetadata(filePath, metadata);
                        SaveVideoFileMetadata(file, metadata);
                        break;
                }
                entities.SaveChanges();
                return metadata;
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"{x.PropertyName}: {x.ErrorMessage}");

                string fullErrorMessage = string.Join("\n", errorMessages);
                throw new Exception($"Ошибки валидации при сканировании файла:\n{fullErrorMessage}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при сканировании файла {file.FileName}: {ex.Message}");
            }
        }

        private void SaveDocumentMetadata(Files file, MetadataModel metadata)
        {
            var existingDocument = entities.Documents.FirstOrDefault(d => d.FileID == file.FileID);

            if (existingDocument != null)
            {
                LogFieldChange(file.FileID, "Encoding", existingDocument.Encoding, metadata.Encoding);
                existingDocument.Encoding = metadata.Encoding;

                LogFieldChange(file.FileID, "PageCount", existingDocument.PageCount, int.TryParse(metadata.PageCount, out var pageCount) ? pageCount : 0);
                existingDocument.PageCount = pageCount;

                LogFieldChange(file.FileID, "LineCount", existingDocument.LineCount, int.TryParse(metadata.LineCount, out var lineCount) ? lineCount : 0);
                existingDocument.LineCount = lineCount;

                LogFieldChange(file.FileID, "Language", existingDocument.Language, metadata.Language);
                existingDocument.Language = metadata.Language;

                LogFieldChange(file.FileID, "Version", existingDocument.Version, metadata.Version);
                existingDocument.Version = metadata.Version;

                LogFieldChange(file.FileID, "Creator", existingDocument.Creator, metadata.Creator);
                existingDocument.Creator = metadata.Creator;

                if (file.FileFormat.Equals(".doc", StringComparison.OrdinalIgnoreCase) ||
                    file.FileFormat.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    LogFieldChange(file.FileID, "ColumnCount", existingDocument.ColumnCount, int.TryParse(metadata.ColumnCount, out var columnCount) ? columnCount : 0);
                    existingDocument.ColumnCount = columnCount;

                    LogFieldChange(file.FileID, "ImageCount", existingDocument.ImageCount, metadata.ImageCount);
                    existingDocument.ImageCount = metadata.ImageCount;

                    LogFieldChange(file.FileID, "TableCount", existingDocument.TableCount, metadata.TableCount);
                    existingDocument.TableCount = metadata.TableCount;

                    LogFieldChange(file.FileID, "SymbolCountWithSpaces", existingDocument.SymbolCountWithSpaces, int.TryParse(metadata.SymbolCountWithSpaces, out var scws) ? scws : 0);
                    existingDocument.SymbolCountWithSpaces = scws;

                    LogFieldChange(file.FileID, "SymbolCountWithoutSpaces", existingDocument.SymbolCountWithoutSpaces, int.TryParse(metadata.SymbolCountWithoutSpaces, out var scwos) ? scwos : 0);
                    existingDocument.SymbolCountWithoutSpaces = scwos;

                    LogFieldChange(file.FileID, "WordCount", existingDocument.WordCount, int.TryParse(metadata.WordCount, out var wordCount) ? wordCount : 0);
                    existingDocument.WordCount = wordCount;
                }
                else if (file.FileFormat.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                         file.FileFormat.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    LogFieldChange(file.FileID, "FormulaCount", existingDocument.FormulaCount, metadata.FormulaCount);
                    existingDocument.FormulaCount = metadata.FormulaCount;

                    LogFieldChange(file.FileID, "ImageCount", existingDocument.ImageCount, metadata.ImageCount);
                    existingDocument.ImageCount = metadata.ImageCount;

                    LogFieldChange(file.FileID, "TableCount", existingDocument.TableCount, metadata.TableCount);
                    existingDocument.TableCount = metadata.TableCount;
                }
                else if (file.FileFormat.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    LogFieldChange(file.FileID, "SymbolCountWithSpaces", existingDocument.SymbolCountWithSpaces, int.TryParse(metadata.SymbolCountWithSpaces, out var scws) ? scws : 0);
                    existingDocument.SymbolCountWithSpaces = scws;

                    LogFieldChange(file.FileID, "SymbolCountWithoutSpaces", existingDocument.SymbolCountWithoutSpaces, int.TryParse(metadata.SymbolCountWithoutSpaces, out var scwos) ? scwos : 0);
                    existingDocument.SymbolCountWithoutSpaces = scwos;
                }
            }
            else
            {
                var document = new Documents
                {
                    FileID = file.FileID,
                    Encoding = metadata.Encoding,
                    PageCount = int.TryParse(metadata.PageCount, out var pageCount) ? pageCount : 0,
                    LineCount = int.TryParse(metadata.LineCount, out var lineCount) ? lineCount : 0,
                    Language = metadata.Language,
                    Version = metadata.Version,
                    Creator = metadata.Creator
                };

                if (file.FileFormat.Equals(".doc", StringComparison.OrdinalIgnoreCase) ||
                    file.FileFormat.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    document.ColumnCount = int.TryParse(metadata.ColumnCount, out var columnCount) ? columnCount : 0;
                    document.ImageCount = metadata.ImageCount;
                    document.TableCount = metadata.TableCount;
                    document.SymbolCountWithSpaces = int.TryParse(metadata.SymbolCountWithSpaces, out var scws) ? scws : 0;
                    document.SymbolCountWithoutSpaces = int.TryParse(metadata.SymbolCountWithoutSpaces, out var scwos) ? scwos : 0;
                    document.WordCount = int.TryParse(metadata.WordCount, out var wordCount) ? wordCount : 0;
                }
                else if (file.FileFormat.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                         file.FileFormat.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    document.FormulaCount = metadata.FormulaCount;
                    document.ImageCount = metadata.ImageCount;
                    document.TableCount = metadata.TableCount;
                }
                else if (file.FileFormat.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    document.SymbolCountWithSpaces = int.TryParse(metadata.SymbolCountWithSpaces, out var scws) ? scws : 0;
                    document.SymbolCountWithoutSpaces = int.TryParse(metadata.SymbolCountWithoutSpaces, out var scwos) ? scwos : 0;
                }

                entities.Documents.Add(document);
            }
        }

        private void SaveImageMetadata(Files file, MetadataModel metadata)
        {
            var existingImage = entities.Images.FirstOrDefault(i => i.FileID == file.FileID);

            if (existingImage != null)
            {
                LogFieldChange(file.FileID, "Resolution", existingImage.Resolution, metadata.Resolution);
                existingImage.Resolution = metadata.Resolution;

                LogFieldChange(file.FileID, "ColorDepth", existingImage.ColorDepth, int.TryParse(metadata.ColorDepth, out var parsedColorDepth) ? parsedColorDepth : 0);
                existingImage.ColorDepth = parsedColorDepth;

                LogFieldChange(file.FileID, "Orientation", existingImage.Orientation, metadata.Orientation);
                existingImage.Orientation = metadata.Orientation;

                LogFieldChange(file.FileID, "CompressionLevel", existingImage.CompressionLevel, metadata.CompressionLevel);
                existingImage.CompressionLevel = metadata.CompressionLevel;

                LogFieldChange(file.FileID, "ColorProfile", existingImage.ColorProfile, metadata.ColorProfile);
                existingImage.ColorProfile = metadata.ColorProfile;

                LogFieldChange(file.FileID, "CameraModel", existingImage.CameraModel, metadata.CameraModel);
                existingImage.CameraModel = metadata.CameraModel;

                LogFieldChange(file.FileID, "Geolocation", existingImage.Geolocation, metadata.Geolocation);
                existingImage.Geolocation = metadata.Geolocation;

                LogFieldChange(file.FileID, "PreviewImage", existingImage.PreviewImage, metadata.PreviewImage);
                existingImage.PreviewImage = metadata.PreviewImage;

                LogFieldChange(file.FileID, "ScalingLevel", existingImage.ScalingLevel, CalculateScalingLevel(metadata.Resolution));
                existingImage.ScalingLevel = CalculateScalingLevel(metadata.Resolution);

                LogFieldChange(file.FileID, "Creator", existingImage.Creator, metadata.Creator);
                existingImage.Creator = metadata.Creator;
            }
            else
            {
                var image = new Images
                {
                    FileID = file.FileID,
                    Resolution = metadata.Resolution,
                    ColorDepth = int.TryParse(metadata.ColorDepth, out var parsedColorDepth) ? parsedColorDepth : 0,
                    Orientation = metadata.Orientation,
                    CompressionLevel = metadata.CompressionLevel,
                    ColorProfile = metadata.ColorProfile,
                    CameraModel = metadata.CameraModel,
                    Geolocation = metadata.Geolocation,
                    PreviewImage = metadata.PreviewImage,
                    ScalingLevel = CalculateScalingLevel(metadata.Resolution),
                    Creator = metadata.Creator
                };
                entities.Images.Add(image);
            }
        }

        private string CalculateScalingLevel(string resolution)
        {
            if (string.IsNullOrEmpty(resolution)) return "Не определено";

            var parts = resolution.Split('x');
            if (parts.Length != 2) return "Не определено";

            if (!int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height))
                return "Не определено";

            double ratio = (double)width / height;

            if (Math.Abs(ratio - 4.0 / 3.0) < 0.01) return "4:3";
            if (Math.Abs(ratio - 16.0 / 9.0) < 0.01) return "16:9";
            if (Math.Abs(ratio - 1.0) < 0.01) return "1:1 (Квадрат)";
            if (ratio < 1.0) return $"Книжная ориентация ({ratio:F2})";
            return $"Альбомная ориентация ({ratio:F2})";
        }

        private void SaveAudioFileMetadata(Files file, MetadataModel metadata)
        {
            var existingAudioFile = entities.AudioFiles.FirstOrDefault(a => a.FileID == file.FileID);

            if (existingAudioFile != null)
            {
                LogFieldChange(file.FileID, "Duration", existingAudioFile.Duration, float.TryParse(metadata.Duration, out var duration) ? duration : 0f);
                existingAudioFile.Duration = duration;

                LogFieldChange(file.FileID, "SampleRate", existingAudioFile.SampleRate, int.TryParse(metadata.SampleRate, out var sampleRate) ? sampleRate : 0);
                existingAudioFile.SampleRate = sampleRate;

                LogFieldChange(file.FileID, "ChannelCount", existingAudioFile.ChannelCount, int.TryParse(metadata.ChannelCount, out var channelCount) ? channelCount : 0);
                existingAudioFile.ChannelCount = channelCount;

                LogFieldChange(file.FileID, "AudioBitrate", existingAudioFile.AudioBitrate, metadata.AudioBitrate);
                existingAudioFile.AudioBitrate = metadata.AudioBitrate;

                LogFieldChange(file.FileID, "TrackTitle", existingAudioFile.TrackTitle, metadata.TrackTitle);
                existingAudioFile.TrackTitle = metadata.TrackTitle;

                LogFieldChange(file.FileID, "Artist", existingAudioFile.Artist, metadata.Artist);
                existingAudioFile.Artist = metadata.Artist;

                LogFieldChange(file.FileID, "Album", existingAudioFile.Album, metadata.Album);
                existingAudioFile.Album = metadata.Album;

                LogFieldChange(file.FileID, "ReleaseYear", existingAudioFile.ReleaseYear, metadata.ReleaseYear);
                existingAudioFile.ReleaseYear = metadata.ReleaseYear;

                LogFieldChange(file.FileID, "Genre", existingAudioFile.Genre, metadata.Genre);
                existingAudioFile.Genre = metadata.Genre;

                LogFieldChange(file.FileID, "PreviewImage", existingAudioFile.PreviewImage, metadata.PreviewImage);
                existingAudioFile.PreviewImage = metadata.PreviewImage;

                LogFieldChange(file.FileID, "Creator", existingAudioFile.Creator, metadata.Creator);
                existingAudioFile.Creator = metadata.Creator;
            }
            else
            {
                var audioFile = new AudioFiles
                {
                    FileID = file.FileID,
                    Duration = float.TryParse(metadata.Duration, out var duration) ? duration : 0f,
                    SampleRate = int.TryParse(metadata.SampleRate, out var sampleRate) ? sampleRate : 0,
                    ChannelCount = int.TryParse(metadata.ChannelCount, out var channelCount) ? channelCount : 0,
                    AudioBitrate = metadata.AudioBitrate,
                    TrackTitle = metadata.TrackTitle,
                    Artist = metadata.Artist,
                    Album = metadata.Album,
                    ReleaseYear = metadata.ReleaseYear,
                    Genre = metadata.Genre,
                    PreviewImage = metadata.PreviewImage,
                    Creator = metadata.Creator
                };

                entities.AudioFiles.Add(audioFile);
            }
        }

        private void SaveVideoFileMetadata(Files file, MetadataModel metadata)
        {
            var existingVideoFile = entities.VideoFiles.FirstOrDefault(v => v.FileID == file.FileID);

            if (existingVideoFile != null)
            {
                LogFieldChange(file.FileID, "Duration", existingVideoFile.Duration, float.TryParse(metadata.Duration, out var duration) ? duration : 0f);
                existingVideoFile.Duration = duration;

                LogFieldChange(file.FileID, "Resolution", existingVideoFile.Resolution, metadata.Resolution);
                existingVideoFile.Resolution = metadata.Resolution;

                LogFieldChange(file.FileID, "FrameRate", existingVideoFile.FrameRate, float.TryParse(metadata.FrameRate, out var frameRate) ? frameRate : 0f);
                existingVideoFile.FrameRate = frameRate;

                LogFieldChange(file.FileID, "VideoCodec", existingVideoFile.VideoCodec, metadata.VideoCodec);
                existingVideoFile.VideoCodec = metadata.VideoCodec;

                LogFieldChange(file.FileID, "AudioCodec", existingVideoFile.AudioCodec, metadata.AudioCodec);
                existingVideoFile.AudioCodec = metadata.AudioCodec;

                LogFieldChange(file.FileID, "SampleRate", existingVideoFile.SampleRate, int.TryParse(metadata.SampleRate, out var sampleRate) ? sampleRate : 0);
                existingVideoFile.SampleRate = sampleRate;

                LogFieldChange(file.FileID, "ReleaseYear", existingVideoFile.ReleaseYear, metadata.ReleaseYear);
                existingVideoFile.ReleaseYear = metadata.ReleaseYear;

                LogFieldChange(file.FileID, "Description", existingVideoFile.Description, metadata.Description);
                existingVideoFile.Description = metadata.Description;

                LogFieldChange(file.FileID, "PreviewImage", existingVideoFile.PreviewImage, metadata.PreviewImage);
                existingVideoFile.PreviewImage = metadata.PreviewImage;

                LogFieldChange(file.FileID, "DataTransferRate", existingVideoFile.DataTransferRate, metadata.DataTransferRate);
                existingVideoFile.DataTransferRate = metadata.DataTransferRate;

                LogFieldChange(file.FileID, "TotalBitrate", existingVideoFile.TotalBitrate, metadata.TotalBitrate);
                existingVideoFile.TotalBitrate = metadata.TotalBitrate;

                LogFieldChange(file.FileID, "Genre", existingVideoFile.Genre, metadata.Genre);
                existingVideoFile.Genre = metadata.Genre;
            }
            else
            {
                var videoFile = new VideoFiles
                {
                    FileID = file.FileID,
                    Duration = float.TryParse(metadata.Duration, out var duration) ? duration : 0f,
                    Resolution = metadata.Resolution,
                    FrameRate = float.TryParse(metadata.FrameRate, out var frameRate) ? frameRate : 0f,
                    VideoCodec = metadata.VideoCodec,
                    AudioCodec = metadata.AudioCodec,
                    SampleRate = int.TryParse(metadata.SampleRate, out var sampleRate) ? sampleRate : 0,
                    ReleaseYear = metadata.ReleaseYear,
                    Description = metadata.Description,
                    PreviewImage = metadata.PreviewImage,
                    DataTransferRate = metadata.DataTransferRate,
                    TotalBitrate = metadata.TotalBitrate,
                    Genre = metadata.Genre
                };

                entities.VideoFiles.Add(videoFile);
            }
        }

        private MetadataModel ExtractTextFileMetadata(string filePath, MetadataModel metadata)
        {
            Encoding encoding = DetectFileEncoding(filePath);
            string content = File.ReadAllText(filePath, encoding);

            metadata.Encoding = encoding.BodyName;
            metadata.Language = DetectLanguageFromText(content);
            metadata.LineCount = content.Split('\n').Length.ToString();
            metadata.SymbolCountWithSpaces = content.Length.ToString();
            metadata.SymbolCountWithoutSpaces = content.Replace(" ", "").Length.ToString();
            metadata.WordCount = Regex.Matches(content, @"\b\w+\b").Count.ToString();

            return metadata;
        }

        private Encoding DetectFileEncoding(string filePath)
        {
            // Читает первые 4 байта для проверки BOM
            byte[] bom = new byte[4];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Определяет кодировку по BOM
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0 && bom[3] == 0)
                return Encoding.UTF32;
            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xFE && bom[3] == 0xFF)
                return new UTF32Encoding(true, true);

            // Если BOM нет, использет StreamReader для автоматического определения
            using (var reader = new StreamReader(filePath, Encoding.Default, true))
            {
                reader.Peek(); // Анализирует кодировку
                return reader.CurrentEncoding;
            }
        }

        private MetadataModel ExtractDocxMetadata(string filePath, MetadataModel metadata)
        {
            using (var doc = WordprocessingDocument.Open(filePath, false))
            {
                var props = doc.PackageProperties;
                metadata.Version = props.Version ?? "1.0";
                metadata.PageCount = doc.ExtendedFilePropertiesPart?.Properties.Pages?.Text ?? "1";
                metadata.LineCount = doc.MainDocumentPart?.Document?.Body?
                    .Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                    .Count(p => !string.IsNullOrEmpty(p.InnerText)).ToString() ?? "0";
                metadata.TableCount = doc.MainDocumentPart?.Document?.Body?
                    .Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>()
                    .Count() ?? 0;

                var allText = doc.MainDocumentPart?.Document?.Body?.InnerText ?? "Нет данных";
                metadata.Language = DetectLanguageFromText(allText);
                metadata.SymbolCountWithSpaces = allText.Length.ToString();
                metadata.SymbolCountWithoutSpaces = allText.Replace(" ", "").Length.ToString();
                metadata.WordCount = Regex.Matches(allText, @"\b\w+\b").Count.ToString();

                // Проверяем кодировку основного XML-документа
                var mainPart = doc.MainDocumentPart;
                if (mainPart != null)
                {
                    using (var reader = new StreamReader(mainPart.GetStream()))
                    {
                        metadata.Encoding = reader.CurrentEncoding.BodyName; // UTF-8 или UTF-16
                    }
                }
                else
                {
                    metadata.Encoding = "UTF-8";
                }

                if (!string.IsNullOrEmpty(props.Creator))
                    metadata.Creator = props.Creator;

                // Дополнительные метаданные (изображения, таблицы)
                metadata.ImageCount = doc.MainDocumentPart?.Document?.Body?
                    .Descendants<DocumentFormat.OpenXml.Drawing.Pictures.Picture>()
                    .Count() ?? 0;

                int maxColumns = 0;
                var tables = doc.MainDocumentPart?.Document?.Body?.Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>();
                if (tables != null)
                {
                    foreach (var table in tables)
                    {
                        var rows = table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>();
                        if (rows.Any())
                        {
                            int colsInTable = rows.First()
                                .Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>()
                                .Count();
                            if (colsInTable > maxColumns)
                                maxColumns = colsInTable;
                        }
                    }
                }
                metadata.ColumnCount = maxColumns > 0 ? maxColumns.ToString() : "0";
            }
            return metadata;
        }

        private string DetectLanguageFromText(string text)
        {
            var factory = new RankedLanguageIdentifierFactory();
            var identifier = factory.Load("Resources\\Core14.profile.xml");
            var languages = identifier.Identify(text);
            var mostCertainLanguage = languages.FirstOrDefault();
            return mostCertainLanguage?.Item1.Iso639_3 ?? "Не определено";
        }

        private MetadataModel ExtractXlsxMetadata(string filePath, MetadataModel metadata)
        {
            using (var spreadsheet = SpreadsheetDocument.Open(filePath, false))
            {
                var props = spreadsheet.PackageProperties;

                metadata.Version = props.Version ?? "1.0";
                metadata.PageCount = spreadsheet.WorkbookPart?.Workbook?.Sheets?.Count().ToString() ?? "1";

                metadata.TableCount = spreadsheet.WorkbookPart?.Workbook?
                    .Descendants<DocumentFormat.OpenXml.Spreadsheet.Table>()
                    .Count() ?? 0;

                metadata.FormulaCount = 0;
                var worksheets = spreadsheet.WorkbookPart?.WorksheetParts;
                if (worksheets != null)
                {
                    foreach (var worksheet in worksheets)
                    {
                        metadata.FormulaCount += worksheet.Worksheet?
                            .Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>()
                            .Count(c => c.CellFormula != null) ?? 0;
                    }
                }

                metadata.ImageCount = spreadsheet.WorkbookPart?.Workbook?
                    .Descendants<DocumentFormat.OpenXml.Drawing.Spreadsheet.Picture>()
                    .Count() ?? 0;

                int cellCount = 0;
                if (worksheets != null)
                {
                    foreach (var worksheet in worksheets)
                    {
                        cellCount += worksheet.Worksheet?
                            .Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>()
                            .Count(c => c.CellValue != null && !string.IsNullOrEmpty(c.CellValue.Text)) ?? 0;
                    }
                }
                metadata.LineCount = cellCount.ToString();

                var workbookPart = spreadsheet.WorkbookPart;
                if (workbookPart != null)
                {
                    using (var reader = new StreamReader(workbookPart.GetStream()))
                    {
                        metadata.Encoding = reader.CurrentEncoding.BodyName;
                    }
                }
                else
                {
                    metadata.Encoding = "UTF-8";
                }

                if (!string.IsNullOrEmpty(props.Creator))
                    metadata.Creator = props.Creator;
            }
            return metadata;
        }

        private MetadataModel ExtractImageMetadata(string filePath, MetadataModel metadata)
        {
            try
            {
                using (var image = System.Drawing.Image.FromFile(filePath))
                {
                    metadata.Resolution = $"{image.Width}x{image.Height}";
                    metadata.ColorDepth = GetColorDepth(image.PixelFormat);
                    metadata.Orientation = image.Width > image.Height ? "Альбомная" : "Книжная";
                }

                var directories = ImageMetadataReader.ReadMetadata(filePath);

                foreach (var directory in directories)
                {
                    foreach (var tag in directory.Tags)
                    {
                        switch (tag.Name)
                        {
                            case "Author":
                            case "Artist":
                            case "Creator":
                                if (!string.IsNullOrEmpty(tag.Description))
                                    metadata.Creator = tag.Description;
                                break;
                            case "Model":
                                metadata.CameraModel = tag.Description;
                                break;
                            case "GPS Latitude":
                                metadata.Geolocation = tag.Description;
                                break;
                            case "GPS Longitude":
                                metadata.Geolocation = string.IsNullOrEmpty(metadata.Geolocation)
                                    ? tag.Description
                                    : $"{metadata.Geolocation}, {tag.Description}";
                                break;
                            case "ICC Profile":
                            case "Interoperability Index":
                            case "Profile Description":
                            case "White Point":
                            case "Color Space":
                                if (!string.IsNullOrEmpty(tag.Description))
                                    metadata.ColorProfile = tag.Description;
                                break;
                            case "Compression":
                            case "Compression Level":
                                metadata.CompressionLevel = tag.Description;
                                break;
                        }
                    }
                }

                using (var sysImage = System.Drawing.Image.FromFile(filePath))
                {
                    foreach (var propItem in sysImage.PropertyItems)
                    {
                        string value = "";
                        switch (propItem.Type)
                        {
                            case 1:
                            case 2:
                                value = Encoding.ASCII.GetString(propItem.Value).TrimEnd('\0');
                                break;
                            case 7:
                            case 13:
                                value = Encoding.UTF8.GetString(propItem.Value).TrimEnd('\0');
                                break;
                            default:
                                value = Encoding.Unicode.GetString(propItem.Value).TrimEnd('\0');
                                break;
                        }

                        if (string.IsNullOrEmpty(value)) continue;

                        if (propItem.Id == 0x013B)
                        {
                            metadata.Creator = value;
                        }
                    }
                }

                metadata.PreviewImage = CreatePreviewImage(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при извлечении метаданных изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return metadata;
        }

        private string GetColorDepth(System.Drawing.Imaging.PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format1bppIndexed:
                    return "1 бит (Черно-белое)";
                case System.Drawing.Imaging.PixelFormat.Format4bppIndexed:
                    return "4 бита (16 цветов)";
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                    return "8 бит (256 цветов)";
                case System.Drawing.Imaging.PixelFormat.Format16bppGrayScale:
                    return "16 бит (Оттенки серого)";
                case System.Drawing.Imaging.PixelFormat.Format16bppRgb555:
                case System.Drawing.Imaging.PixelFormat.Format16bppRgb565:
                    return "16 бит (High Color)";
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return "24 бит (True Color)";
                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                    return "32 бит (True Color + Alpha)";
                case System.Drawing.Imaging.PixelFormat.Format48bppRgb:
                    return "48 бит (Deep Color)";
                case System.Drawing.Imaging.PixelFormat.Format64bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format64bppPArgb:
                    return "64 бит (Deep Color + Alpha)";
                default:
                    return "Нет данных";
            }
        }


        private string CreatePreviewImage(string filePath)
        {
            try
            {
                var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".jpg");

                using (var image = System.Drawing.Image.FromFile(filePath))
                {
                    int thumbWidth, thumbHeight;
                    const int maxSize = 800;

                    if (image.Width > image.Height)
                    {
                        thumbWidth = maxSize;
                        thumbHeight = (int)(image.Height * ((double)maxSize / image.Width));
                    }
                    else
                    {
                        thumbHeight = maxSize;
                        thumbWidth = (int)(image.Width * ((double)maxSize / image.Height));
                    }

                    using (var thumb = image.GetThumbnailImage(thumbWidth, thumbHeight, () => false, IntPtr.Zero))
                    {
                        thumb.Save(tempFilePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                }
                return tempFilePath;
            }
            catch
            {
                return null;
            }
        }

        private MetadataModel ExtractAudioMetadata(string filePath, MetadataModel metadata)
        {
            var file = TagLib.File.Create(filePath);

            metadata.Duration = file.Properties.Duration.TotalSeconds.ToString("F2");
            metadata.SampleRate = file.Properties.AudioSampleRate.ToString();
            metadata.ChannelCount = file.Properties.AudioChannels.ToString();
            metadata.AudioBitrate = file.Properties.AudioBitrate;
            metadata.Description = file.Tag.Comment ?? string.Empty;

            metadata.TrackTitle = file.Tag.Title ?? "Нет данных";
            metadata.Artist = file.Tag.FirstPerformer ?? "Неизвестный исполнитель";
            metadata.Album = file.Tag.Album ?? "Неизвестный альбом";
            metadata.ReleaseYear = (int)file.Tag.Year;
            metadata.Genre = file.Tag.FirstGenre ?? "Нет данных";

            if (!string.IsNullOrEmpty(file.Tag.FirstComposer))
                metadata.Creator = file.Tag.FirstComposer;
            else if (!string.IsNullOrEmpty(file.Tag.FirstPerformer))
                metadata.Creator = file.Tag.FirstPerformer;

            // Превью изображения
            if (file.Tag.Pictures.Length > 0)
            {
                var picture = file.Tag.Pictures[0];
                var tempFilePath = Path.GetTempFileName() + ".jpg";
                using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(picture.Data.Data, 0, picture.Data.Data.Length);
                }
                metadata.PreviewImage = tempFilePath;
            }
            else
            {
                metadata.PreviewImage = null;
            }

            return metadata;
        }

        private MetadataModel ExtractVideoMetadata(string filePath, MetadataModel metadata)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }

            var inputFile = new MediaToolkit.Model.MediaFile { Filename = filePath };
            using (var engine = new MediaToolkit.Engine())
            {
                try
                {
                    engine.GetMetadata(inputFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при извлечении метаданных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return metadata;
                }
            }

            if (inputFile.Metadata == null)
            {
                throw new Exception("Не удалось извлечь метаданные из видеофайла");
            }

            metadata.Duration = inputFile.Metadata.Duration.TotalSeconds.ToString("F2");

            if (inputFile.Metadata.VideoData != null)
            {
                if (!string.IsNullOrEmpty(inputFile.Metadata.VideoData.FrameSize))
                {
                    var resolutionParts = inputFile.Metadata.VideoData.FrameSize.Split('x');
                    if (resolutionParts.Length == 2)
                    {
                        metadata.Resolution = $"{resolutionParts[0]}x{resolutionParts[1]}";
                    }
                }

                metadata.FrameRate = inputFile.Metadata.VideoData.Fps.ToString();
                metadata.VideoCodec = inputFile.Metadata.VideoData.Format;
                metadata.TotalBitrate = (int)(inputFile.Metadata.VideoData.BitRateKbs +
                                            (inputFile.Metadata.AudioData?.BitRateKbs ?? 0));
            }

            if (inputFile.Metadata.AudioData != null)
            {
                metadata.AudioCodec = inputFile.Metadata.AudioData.Format;
                metadata.SampleRate = inputFile.Metadata.AudioData.SampleRate.ToString();

                if (int.TryParse(inputFile.Metadata.AudioData.ChannelOutput, out int audioTrack))
                {
                    metadata.AudioTrack = audioTrack;
                }
            }

            try
            {
                string videoPreviewPath = null;
                try
                {
                    videoPreviewPath = Path.GetTempFileName() + ".jpg";
                    using (var engine = new MediaToolkit.Engine())
                    {
                        var outputFile = new MediaToolkit.Model.MediaFile { Filename = videoPreviewPath };
                        var options = new MediaToolkit.Options.ConversionOptions
                        {
                            Seek = TimeSpan.FromSeconds(5)
                        };
                        engine.GetThumbnail(inputFile, outputFile, options);
                        metadata.PreviewImage = videoPreviewPath;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при создании превью из видео: {ex.Message}");
                    if (File.Exists(videoPreviewPath))
                        File.Delete(videoPreviewPath);
                }

                var tagFile = TagLib.File.Create(filePath);

                metadata.ReleaseYear = (int)tagFile.Tag.Year;
                metadata.Description = tagFile.Tag.Comment ?? string.Empty;
                metadata.Genre = tagFile.Tag.FirstGenre ?? "Нет данных";

                metadata.DataTransferRate = metadata.TotalBitrate;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении тегов файла: {ex.Message}");
            }

            return metadata;
        }

        private void ShowScanResult(MetadataModel metadata)
        {
            var scanWindow = new ScanResultWindow(_userRoleId);
            scanWindow.SetMetadata(metadata);
            scanWindow.Show();
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = FilesListView.SelectedItems.Cast<Files>().ToList();

            if (selectedFiles.Count == 0)
            {
                selectedFiles = filesList.Where(file => file.IsSelected).ToList();
            }

            if (selectedFiles.Count == 2)
            {
                var metadata1 = ScanFile(selectedFiles[0]);
                var metadata2 = ScanFile(selectedFiles[1]);

                var compareWindow = new CompareFilesWindow();
                compareWindow.SetMetadata(metadata1, metadata2);
                compareWindow.Show();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите два файла для сравнения",
                              "Информация",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить все данные? Это действие нельзя отменить",
                                        "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    entities.Configuration.AutoDetectChangesEnabled = false;

                    entities.Database.ExecuteSqlCommand("DELETE FROM Images");
                    entities.Database.ExecuteSqlCommand("DELETE FROM AudioFiles");
                    entities.Database.ExecuteSqlCommand("DELETE FROM VideoFiles");
                    entities.Database.ExecuteSqlCommand("DELETE FROM Documents");
                    entities.Database.ExecuteSqlCommand("DELETE FROM Reports");
                    entities.Database.ExecuteSqlCommand("DELETE FROM ChangesHistory");
                    entities.Database.ExecuteSqlCommand("DELETE FROM Files");
                    entities.Database.ExecuteSqlCommand("DELETE FROM Folders");

                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('Images', RESEED, 0)");
                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('AudioFiles', RESEED, 0)");
                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('VideoFiles', RESEED, 0)");
                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('Documents', RESEED, 0)");
                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('Reports', RESEED, 0)");
                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('ChangesHistory', RESEED, 0)");
                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('Files', RESEED, 0)");
                    entities.Database.ExecuteSqlCommand("DBCC CHECKIDENT ('Folders', RESEED, 0)");

                    filesList.Clear();
                    filePathDictionary?.Clear();

                    filesViewSource.View.Refresh();
                    LoadFolders(false);
                    UpdateFileCounts();

                    MessageBox.Show("Все данные успешно очищены", "Успех",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при очистке данных: {ex.Message}\n\n" +
                                  $"Подробности: {ex.InnerException?.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    entities.Configuration.AutoDetectChangesEnabled = true;

                    entities.Dispose();
                    entities = new Entities();
                }
            }
        }

        private void ManageUsers_Click(object sender, RoutedEventArgs e)
        {
            if (_userRoleId != 2)
            {
                MessageBox.Show("Нет прав для управления пользователями.", "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var manageUsersWindow = new ManageUsersWindow();
            manageUsersWindow.ShowDialog();

        }
        private void Other_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
            if (listViewItem == null) return;

            var selectedFile = listViewItem.DataContext as Files;
            if (selectedFile == null) return;

            try
            {
                string fullPath = Path.GetFullPath(selectedFile.FilePath);
                if (File.Exists(fullPath))
                {
                    Task.Run(() =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fullPath,
                            UseShellExecute = true
                        });
                    });
                }
                else
                {
                    MessageBox.Show($"Файл {selectedFile.FileName} не найден", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FilesListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var selectedFolder = FoldersListView.SelectedItem as Folders;

            bool hasSelection = FilesListView.SelectedItems.Count > 0 || filesList.Any(file => file.IsSelected);

            var removeFromFolderMenuItem = FileContextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(item => item.Header.ToString() == "Удалить из папки");

            if (removeFromFolderMenuItem != null)
            {
                removeFromFolderMenuItem.Visibility = (selectedFolder != null && selectedFolder.FolderID != -1 && hasSelection)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // Включение/отключение пунктов меню
            foreach (var item in FileContextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    menuItem.IsEnabled = hasSelection;
                }
            }
        }

        private void FilesListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);

            // Если клик по элементу списка
            if (listViewItem != null)
            {
                var file = listViewItem.DataContext as Files;
                if (file != null)
                {
                    // Если этот файл не выделен, снятие всех выделений и выделение его
                    if (!file.IsSelected && !FilesListView.SelectedItems.Contains(file))
                    {
                        foreach (Files f in FilesListView.SelectedItems)
                        {
                            f.IsSelected = false;
                        }
                        FilesListView.SelectedItems.Clear();

                        file.IsSelected = true;
                        FilesListView.SelectedItem = file;
                    }
                }
                e.Handled = true;
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialogWindow("Новая папка", "Введите название папки:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                if (dialog.ResponseText.Trim().Equals("Все", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Название 'Все' зарезервировано системой", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var newFolder = new Folders
                    {
                        Name = dialog.ResponseText,
                        CreationDate = DateTime.Now,
                        ModificationDate = DateTime.Now,
                        IsFolder = true
                    };

                    entities.Folders.Add(newFolder);
                    entities.SaveChanges();
                    LoadFolders();

                    var folderToSelect = entities.Folders.FirstOrDefault(f => f.Name.Equals(newFolder.Name));
                    if (folderToSelect != null)
                    {
                        FoldersListView.SelectedItem = folderToSelect;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании папки: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void LoadFolders(bool keepSelection = true)
        {
            try
            {
                var previouslySelectedFolder = FoldersListView.SelectedItem as Folders;
                int selectedIndex = FoldersListView.SelectedIndex;

                var folders = entities.Folders.ToList();
                folders.Insert(0, new Folders
                {
                    FolderID = -1,
                    Name = "Все",
                    CreationDate = DateTime.MinValue,
                    IsFolder = true
                });

                FoldersListView.ItemsSource = folders;

                // Восстанавление выбора только если явно не указано иное
                if (keepSelection && previouslySelectedFolder != null)
                {
                    var folderToSelect = folders.FirstOrDefault(f =>
                        f.FolderID == previouslySelectedFolder.FolderID);

                    if (folderToSelect != null)
                    {
                        FoldersListView.SelectedItem = folderToSelect;
                    }
                    else if (selectedIndex >= 0 && selectedIndex < folders.Count)
                    {
                        FoldersListView.SelectedIndex = selectedIndex;
                    }
                }
                else if (!keepSelection)
                {
                    FoldersListView.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке папок: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FoldersListView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void FoldersListView_Drop(object sender, DragEventArgs e)
        {
            var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
            if (listViewItem != null)
            {
                listViewItem.Background = Brushes.Transparent;
            }

            if (e.Data.GetDataPresent(typeof(List<Files>)))
            {
                var files = (List<Files>)e.Data.GetData(typeof(List<Files>));
                var targetFolder = listViewItem?.DataContext as Folders;

                if (targetFolder != null && targetFolder.FolderID != -1)
                {
                    var result = MessageBox.Show(
                        $"Переместить {files.Count} файл(ов) в папку '{targetFolder.Name}'?",
                        "Подтверждение",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            foreach (var file in files)
                            {
                                file.FolderID = targetFolder.FolderID;
                                file.IsSelected = false;
                            }

                            entities.SaveChanges();
                            LoadFiles();

                            filesViewSource.View.Refresh();

                            MessageBox.Show($"Файлы успешно перемещены в папку '{targetFolder.Name}'",
                                          "Успех",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при перемещении файлов: {ex.Message}",
                                          "Ошибка",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void FoldersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedFolder = FoldersListView.SelectedItem as Folders;

            if (selectedFolder != null && selectedFolder.FolderID != -1)
            {
                var folderExists = entities.Folders.Any(f => f.FolderID == selectedFolder.FolderID);
                if (!folderExists)
                {
                    MessageBox.Show("Выбранная папка не найдена в базе данных", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    FoldersListView.SelectedIndex = 0;
                    return;
                }
            }

            ApplyFilters();
        }

        private void RenameFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = FoldersListView.SelectedItem as Folders;
            if (selectedFolder == null || selectedFolder.FolderID == -1)
            {
                MessageBox.Show("Пожалуйста, выберите папку для переименования (кроме папки 'Все')",
                              "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var previouslySelectedIndex = FoldersListView.SelectedIndex;

            var dialog = new InputDialogWindow(
                "Переименовать папку",
                "Введите новое название папки:",
                selectedFolder.Name);

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                if (dialog.ResponseText.Trim().Equals("Все", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Название 'Все' зарезервировано системой", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    bool nameExists = entities.Folders
                        .Any(f => f.Name.Equals(dialog.ResponseText.Trim()) && f.FolderID != selectedFolder.FolderID);

                    if (nameExists)
                    {
                        MessageBox.Show("Папка с таким именем уже существует", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    selectedFolder.Name = dialog.ResponseText.Trim();
                    selectedFolder.ModificationDate = DateTime.Now;

                    entities.SaveChanges();

                    var folders = entities.Folders.ToList();
                    folders.Insert(0, new Folders
                    {
                        FolderID = -1,
                        Name = "Все",
                        CreationDate = DateTime.MinValue,
                        IsFolder = true
                    });

                    FoldersListView.ItemsSource = folders;

                    if (previouslySelectedIndex >= 0 && previouslySelectedIndex < folders.Count)
                    {
                        FoldersListView.SelectedIndex = previouslySelectedIndex;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при переименовании папки: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolders = FoldersListView.SelectedItems.Cast<Folders>().ToList();
            if (selectedFolders.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите папки для удаления", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedFolders.Any(f => f.FolderID == -1))
            {
                MessageBox.Show("Папка 'Все' не может быть удалена", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                selectedFolders.RemoveAll(f => f.FolderID == -1);
                if (selectedFolders.Count == 0) return;
            }

            if (selectedFolders.Count > 1)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить {selectedFolders.Count} папок?",
                                           "Подтверждение удаления",
                                           MessageBoxButton.YesNo,
                                           MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                try
                {
                    var folderIds = selectedFolders.Select(f => f.FolderID).ToList();
                    var allFilesInFolders = entities.Files
                        .Where(f => folderIds.Contains(f.FolderID.Value))
                        .ToList();

                    foreach (var file in allFilesInFolders)
                    {
                        file.FolderID = null;
                    }

                    foreach (var folder in selectedFolders)
                    {
                        entities.Folders.Remove(folder);
                    }

                    entities.SaveChanges();
                    MessageBox.Show($"Успешно удалено {selectedFolders.Count} папок", "Успех",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении папок: {ex.Message}", "Ошибка",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else // Удаление одной папки
            {
                var selectedFolder = selectedFolders.First();
                var filesInFolder = entities.Files.Where(f => f.FolderID == selectedFolder.FolderID).ToList();

                var dialog = new FolderDeletionDialog(selectedFolder.Name, filesInFolder.Select(f => f.FileName).ToList());
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        if (!dialog.KeepFiles)
                        {
                            foreach (var file in filesInFolder)
                            {
                                entities.Files.Remove(file);
                            }
                        }
                        else
                        {
                            foreach (var file in filesInFolder)
                            {
                                file.FolderID = null;
                            }
                        }

                        entities.Folders.Remove(selectedFolder);
                        entities.SaveChanges();
                        MessageBox.Show("Папка успешно удалена", "Успех",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении папки: {ex.Message}", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            LoadFolders();
        }

        private void FilesListView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(List<Files>)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        // Сброс
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                foreach (var file in filesList)
                {
                    file.IsSelected = false;
                }
                filesViewSource.View.Refresh();

                SizeFilterComboBox.SelectedIndex = 0;
                TypeFilterComboBox.SelectedIndex = 0;
                FilesListView.UnselectAll();
                SearchTextBox.Text = "Введите атрибуты файла...";
                SearchTextBox.Foreground = Brushes.Gray;

                Keyboard.ClearFocus();

                ApplyFilters();
            }
        }

        private void FilesListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(List<Files>)))
            {
                var files = (List<Files>)e.Data.GetData(typeof(List<Files>));
                var targetFolder = FoldersListView.SelectedItem as Folders;

                if (targetFolder != null && targetFolder.FolderID != -1)
                {
                    foreach (var file in files)
                    {
                        file.FolderID = targetFolder.FolderID;
                    }

                    try
                    {
                        entities.SaveChanges();
                        LoadFiles();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при перемещении файлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void FoldersListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(List<Files>)))
            {
                var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (listViewItem != null)
                {
                    var folder = listViewItem.DataContext as Folders;
                    if (folder != null && folder.FolderID != -1) // Нельзя в Все
                    {
                        listViewItem.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                        e.Effects = DragDropEffects.Move;
                        e.Handled = true;
                        return;
                    }
                }
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void FoldersListView_DragLeave(object sender, DragEventArgs e)
        {
            var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
            if (listViewItem != null)
            {
                listViewItem.Background = Brushes.Transparent;
            }
        }

        private void FilesListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(List<Files>)))
            {
                var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (listViewItem != null)
                {
                    listViewItem.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                    e.Effects = DragDropEffects.Move;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        private void FilesListView_DragLeave(object sender, DragEventArgs e)
        {
            var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
            if (listViewItem != null)
            {
                listViewItem.Background = Brushes.Transparent;
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }


        private void FilesListView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(null);
                var diff = _dragStartPoint - position;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDragOperation();
                }
            }
        }

        private void StartDragOperation()
        {
            var selectedFilesByCheckbox = filesList.Where(file => file.IsSelected).ToList();

            var selectedFilesByList = FilesListView.SelectedItems.Cast<Files>().ToList();

            var filesToDrag = new List<Files>();
            filesToDrag.AddRange(selectedFilesByCheckbox);
            filesToDrag.AddRange(selectedFilesByList.Except(filesToDrag));

            if (filesToDrag.Any())
            {
                var data = new DataObject(typeof(List<Files>), filesToDrag);
                DragDrop.DoDragDrop(FilesListView, data, DragDropEffects.Move);
            }
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var historyWindow = new HistoryWindow(_userRoleId);
                historyWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при открытии истории изменений:\n{ex.Message}\n\n" +
                    $"Трассировка: {ex.StackTrace}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Analytics_Click(object sender, RoutedEventArgs e)
        {
            var analyticsWindow = new AnalyticsWindow();
            analyticsWindow.Show();
        }

        private void SecuritySettings_Click(object sender, RoutedEventArgs e)
        {
            var securityWindow = new SecuritySettingsWindow();
            securityWindow.ShowDialog();
        }

        private void SaveReport(Files file, string reportData, string reportType, string filePath)
        {
            var report = new Reports
            {
                FileID = file?.FileID,
                CreationDate = DateTime.Now,
                ReportData = reportData,
                ReportType = reportType,
                ReportPath = filePath

            };

            entities.Reports.Add(report);
            entities.SaveChanges();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedFiles = new List<Files>();

                if (filesList != null)
                {
                    selectedFiles = filesList.Where(file => file != null && file.IsSelected).ToList();
                }

                if (selectedFiles.Count == 0 && FilesListView != null && FilesListView.SelectedItems != null)
                {
                    selectedFiles = FilesListView.SelectedItems.Cast<Files>().Where(f => f != null).ToList();
                }

                if (selectedFiles.Count > 0)
                {
                    foreach (var file in selectedFiles)
                    {
                        if (file != null && File.Exists(file.FilePath))
                        {
                            ExportSingleFileMetadata(file);
                        }
                        else if (file != null)
                        {
                            MessageBox.Show($"Файл отсутствует: {file.FilePath}", "Ошибка",
                                           MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    ExportGeneralStatistics();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}\n\n{ex.StackTrace}",
                               "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportGeneralStatistics()
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Метаданные файлов");
                worksheet.Cells.Style.Font.Name = "Times New Roman";

                worksheet.Cells[1, 1].Value = "Отчет по метаданным файлов (общая статистика)";
                worksheet.Cells[1, 1, 1, 5].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(181, 181, 181));
                worksheet.Cells[1, 1, 1, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                worksheet.Cells[2, 1].Value = "Номер проекта";
                worksheet.Cells[2, 2].Value = "Отчет_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                worksheet.Cells[3, 1].Value = "Название отчета";
                worksheet.Cells[3, 2].Value = "Аналитика метаданных загруженных файлов";
                worksheet.Cells[4, 1].Value = "Дата формирования";
                worksheet.Cells[4, 2].Value = DateTime.Now.ToString("dd.MM.yyyy");
                worksheet.Cells[5, 1].Value = "Сгенерировал";
                worksheet.Cells[5, 2].Value = Environment.UserName;

                for (int i = 2; i <= 5; i++)
                {
                    worksheet.Cells[i, 1].Style.Font.Size = 12;
                    worksheet.Cells[i, 2].Style.Font.Size = 12;
                    worksheet.Cells[i, 1, i, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                int row = 7;

                worksheet.Cells[row, 1].Value = "1. Общая статистика";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                worksheet.Cells[row, 1].Style.Font.Size = 14;
                worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(181, 181, 181));
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;

                var statsTableStart = row;
                worksheet.Cells[row, 1].Value = "Параметр";
                worksheet.Cells[row, 2].Value = "Значение";
                worksheet.Cells[row, 3].Value = "Доп. информация";
                worksheet.Cells[row, 1, row, 3].Style.Font.Size = 12;
                worksheet.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 1, row, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230));
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;

                worksheet.Cells[row, 1].Value = "Количество файлов";
                worksheet.Cells[row, 2].Value = filesList.Count;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                worksheet.Cells[row, 3].Value = filesList.Count > 0 ? "Анализ выполнен" : "Нет данных";
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;

                if (filesList.Count > 0)
                {
                    var totalSize = filesList.Sum(f => f.FileSize);
                    var avgSize = filesList.Average(f => f.FileSize);
                    var minSize = filesList.Min(f => f.FileSize);
                    var maxSize = filesList.Max(f => f.FileSize);
                    var uniqueFormats = filesList.Select(f => f.FileFormat).Distinct().Count();

                    worksheet.Cells[row, 1].Value = "Общий размер файлов";
                    worksheet.Cells[row, 2].Value = FileUtils.FormatFileSize(totalSize);
                    worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;

                    worksheet.Cells[row, 1].Value = "Средний размер файла";
                    worksheet.Cells[row, 2].Value = FileUtils.FormatFileSize((long)avgSize);
                    worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;

                    worksheet.Cells[row, 1].Value = "Минимальный размер";
                    worksheet.Cells[row, 2].Value = FileUtils.FormatFileSize(minSize);
                    worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;

                    worksheet.Cells[row, 1].Value = "Максимальный размер";
                    worksheet.Cells[row, 2].Value = FileUtils.FormatFileSize(maxSize);
                    worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;

                    worksheet.Cells[row, 1].Value = "Уникальные форматы";
                    worksheet.Cells[row, 2].Value = uniqueFormats;
                    worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                    worksheet.Cells[row, 3].Value = $"{(uniqueFormats / (double)filesList.Count):P2}";
                    worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;
                }

                row++;
                worksheet.Cells[row, 1].Value = "2. Распределение по форматам файлов";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                worksheet.Cells[row, 1].Style.Font.Size = 14;
                worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(181, 181, 181));
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;

                if (filesList.Count > 0)
                {
                    int formatTableStart = row;
                    worksheet.Cells[row, 1].Value = "Формат";
                    worksheet.Cells[row, 2].Value = "Количество";
                    worksheet.Cells[row, 3].Value = "Процент";
                    worksheet.Cells[row, 4].Value = "Общий размер";
                    worksheet.Cells[row, 5].Value = "Средний размер";
                    worksheet.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230));
                    worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;

                    var formatGroups = filesList.GroupBy(f => f.FileFormat).OrderByDescending(g => g.Count());
                    foreach (var group in formatGroups)
                    {
                        var count = group.Count();
                        var percent = (count / (double)filesList.Count) * 100;
                        var total = group.Sum(f => f.FileSize);
                        var avg = group.Average(f => f.FileSize);

                        worksheet.Cells[row, 1].Value = group.Key;
                        worksheet.Cells[row, 2].Value = count;
                        worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                        worksheet.Cells[row, 3].Value = $"{percent:F2}%";

                        worksheet.Cells[row, 4].Value = FileUtils.FormatFileSize(total);
                        worksheet.Cells[row, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 230, 140));

                        worksheet.Cells[row, 5].Value = FileUtils.FormatFileSize((long)avg);
                        worksheet.Cells[row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(245, 222, 179));

                        worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        row++;
                    }

                    row++;
                    int chartColOffset = 6;
                    int chartRowStart = formatTableStart - 16;

                    var chartPie = worksheet.Drawings.AddChart("chartFormatDistribution", eChartType.Pie3D);
                    chartPie.Title.Text = "Распределение по форматам";
                    chartPie.SetPosition(chartRowStart, 0, chartColOffset, 0);
                    chartPie.SetSize(680, 350);

                    var pieSeries = (ExcelPieChartSerie)chartPie.Series.Add(
                        worksheet.Cells[formatTableStart + 1, 2, row - 2, 2],
                        worksheet.Cells[formatTableStart + 1, 1, row - 2, 1]);

                    pieSeries.DataLabel.ShowCategory = false;
                    pieSeries.DataLabel.ShowValue = false;
                    pieSeries.DataLabel.ShowPercent = true;
                    pieSeries.DataLabel.Position = eLabelPosition.Center;
                    pieSeries.DataLabel.Separator = "";

                    chartPie.Legend.Position = eLegendPosition.Right;
                }

                row++;
                worksheet.Cells[row, 1].Value = "3. Дополнительная информация";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                worksheet.Cells[row, 1].Style.Font.Size = 14;
                worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(181, 181, 181));
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                row++;

                if (filesList.Count > 0)
                {
                    worksheet.Cells[row, 1].Value = "Самые большие файлы:";
                    worksheet.Cells[row, 1, row, 3].Merge = true;
                    worksheet.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230));
                    worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    row++;

                    var topFiles = filesList.OrderByDescending(f => f.FileSize).Take(5).ToList();
                    var topFilesTableStart = row;

                    foreach (var file in topFiles)
                    {
                        worksheet.Cells[row, 1].Value = file.FileName;
                        worksheet.Cells[row, 2].Value = FileUtils.FormatFileSize(file.FileSize);
                        worksheet.Cells[row, 6].Value = file.FileSize;
                        worksheet.Cells[row, 6].Style.Numberformat.Format = ";;;";

                        worksheet.Cells[row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(144, 238, 144));

                        worksheet.Cells[row, 3].Value = file.FileFormat;
                        worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        row++;
                    }

                    row++;
                    int chartColOffsetTopFiles = 6;
                    int chartRowStartTopFiles = topFilesTableStart - 8;

                    var chartBar = worksheet.Drawings.AddChart("chartTopFiles", eChartType.ColumnClustered);
                    chartBar.Title.Text = "Топ больших файлов";
                    chartBar.SetPosition(chartRowStartTopFiles, 0, chartColOffsetTopFiles, 0);
                    chartBar.SetSize(680, 350);

                    var barSeries = (ExcelBarChartSerie)chartBar.Series.Add(
                        worksheet.Cells[topFilesTableStart, 6, topFilesTableStart + topFiles.Count - 1, 6],
                        worksheet.Cells[topFilesTableStart, 1, topFilesTableStart + topFiles.Count - 1, 1]
                    );
                    barSeries.Header = "Размер файла";
                    chartBar.Legend.Position = eLegendPosition.Bottom;

                    if (filesList.All(f => f.CreationDate != default))
                    {
                        row++;
                        worksheet.Cells[row, 1].Value = "Временные характеристики:";
                        worksheet.Cells[row, 1, row, 3].Merge = true;
                        worksheet.Cells[row, 1, row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 1, row, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230));
                        worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        row++;

                        var oldest = filesList.OrderBy(f => f.CreationDate).First();
                        var newest = filesList.OrderByDescending(f => f.CreationDate).First();

                        worksheet.Cells[row, 1].Value = "Самый старый файл";
                        worksheet.Cells[row, 2].Value = oldest.FileName;
                        worksheet.Cells[row, 3].Value = oldest.CreationDate?.ToString("dd.MM.yyyy");
                        worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        row++;

                        worksheet.Cells[row, 1].Value = "Самый новый файл";
                        worksheet.Cells[row, 2].Value = newest.FileName;
                        worksheet.Cells[row, 3].Value = newest.CreationDate?.ToString("dd.MM.yyyy");
                        worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        row++;
                    }
                }

                // Автоширина столбцов
                for (int col = 1; col <= 5; col++)
                    worksheet.Column(col).AutoFit();

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel файлы (*.xlsx)|*.xlsx",
                    Title = "Сохранить отчет",
                    FileName = $"Общая_статистика_Отчет_по_метаданным_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        package.SaveAs(new FileInfo(saveDialog.FileName));
                        SaveReport(null, "Общий отчет по метаданным сформирован", "Общая статистика", saveDialog.FileName);
                        var dialog = new MessageBoxExport("Отчет по метаданным успешно сгенерирован!", "Успех", saveDialog.FileName);
                        dialog.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private Dictionary<string, (object Value, bool IsFilled)> GetAllMetadata(Files file, MetadataModel metadata)
        {
            var allMetadata = new Dictionary<string, (object Value, bool IsFilled)>
            {
                { "Имя файла", (metadata.FileName, !string.IsNullOrEmpty(metadata.FileName)) },
                { "Формат файла", (metadata.FileFormat, !string.IsNullOrEmpty(metadata.FileFormat)) },
                { "Владелец", (metadata.UserName, !string.IsNullOrEmpty(metadata.UserName)) },
                { "Путь к файлу", (metadata.FilePath, !string.IsNullOrEmpty(metadata.FilePath)) },
                { "Размер файла", (FileUtils.FormatFileSize(metadata.FileSize), metadata.FileSize > 0) },
                { "Дата загрузки", (metadata.UploadDate.ToString("dd.MM.yyyy HH:mm"), metadata.UploadDate != DateTime.MinValue) },
                { "Дата создания", (metadata.CreationDate.ToString("dd.MM.yyyy"), metadata.CreationDate != DateTime.MinValue) },
                { "Дата изменения", (metadata.ModificationDate.ToString("dd.MM.yyyy"), metadata.ModificationDate != DateTime.MinValue) }
            };

            switch (file.FileFormat.ToLower())
            {
                case ".txt":
                    allMetadata.Add("Количество строк", (metadata.LineCount, !string.IsNullOrEmpty(metadata.LineCount)));
                    allMetadata.Add("Количество символов (с пробелами)", (metadata.SymbolCountWithSpaces, !string.IsNullOrEmpty(metadata.SymbolCountWithSpaces)));
                    allMetadata.Add("Количество символов (без пробелов)", (metadata.SymbolCountWithoutSpaces, !string.IsNullOrEmpty(metadata.SymbolCountWithoutSpaces)));
                    allMetadata.Add("Кодировка", (metadata.Encoding, !string.IsNullOrEmpty(metadata.Encoding)));
                    allMetadata.Add("Язык документа", (metadata.Language, !string.IsNullOrEmpty(metadata.Language)));
                    break;

                case ".doc":
                case ".docx":
                    allMetadata.Add("Создатель", (metadata.Creator, !string.IsNullOrEmpty(metadata.Creator)));
                    allMetadata.Add("Количество страниц", (metadata.PageCount, !string.IsNullOrEmpty(metadata.PageCount)));
                    allMetadata.Add("Количество строк", (metadata.LineCount, !string.IsNullOrEmpty(metadata.LineCount)));
                    allMetadata.Add("Количество столбцов", (metadata.ColumnCount, !string.IsNullOrEmpty(metadata.ColumnCount)));
                    allMetadata.Add("Количество формул", (metadata.FormulaCount.ToString(), metadata.FormulaCount > 0));
                    allMetadata.Add("Количество изображений", (metadata.ImageCount.ToString(), metadata.ImageCount > 0));
                    allMetadata.Add("Количество таблиц", (metadata.TableCount.ToString(), metadata.TableCount > 0));
                    allMetadata.Add("Кодировка", (metadata.Encoding, !string.IsNullOrEmpty(metadata.Encoding)));
                    allMetadata.Add("Язык документа", (metadata.Language, !string.IsNullOrEmpty(metadata.Language)));
                    allMetadata.Add("Версия документа", (metadata.Version, !string.IsNullOrEmpty(metadata.Version)));
                    allMetadata.Add("Количество символов (с пробелами)", (metadata.SymbolCountWithSpaces, !string.IsNullOrEmpty(metadata.SymbolCountWithSpaces)));
                    allMetadata.Add("Количество символов (без пробелов)", (metadata.SymbolCountWithoutSpaces, !string.IsNullOrEmpty(metadata.SymbolCountWithoutSpaces)));
                    break;

                case ".xlsx":
                case ".xls":
                    allMetadata.Add("Создатель", (metadata.Creator, !string.IsNullOrEmpty(metadata.Creator)));
                    allMetadata.Add("Количество страниц", (metadata.PageCount, !string.IsNullOrEmpty(metadata.PageCount)));
                    allMetadata.Add("Количество строк", (metadata.LineCount, !string.IsNullOrEmpty(metadata.LineCount)));
                    allMetadata.Add("Количество столбцов", (metadata.ColumnCount, !string.IsNullOrEmpty(metadata.ColumnCount)));
                    allMetadata.Add("Количество формул", (metadata.FormulaCount.ToString(), metadata.FormulaCount > 0));
                    allMetadata.Add("Количество изображений", (metadata.ImageCount.ToString(), metadata.ImageCount > 0));
                    allMetadata.Add("Количество таблиц", (metadata.TableCount.ToString(), metadata.TableCount > 0));
                    allMetadata.Add("Кодировка", (metadata.Encoding, !string.IsNullOrEmpty(metadata.Encoding)));
                    break;

                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                    allMetadata.Add("Создатель", (metadata.Creator, !string.IsNullOrEmpty(metadata.Creator)));
                    allMetadata.Add("Разрешение", (metadata.Resolution, !string.IsNullOrEmpty(metadata.Resolution)));
                    allMetadata.Add("Глубина цвета", (metadata.ColorDepth, !string.IsNullOrEmpty(metadata.ColorDepth)));
                    allMetadata.Add("Уровень сжатия", (metadata.CompressionLevel, !string.IsNullOrEmpty(metadata.CompressionLevel)));
                    allMetadata.Add("Цветовой профиль", (metadata.ColorProfile, !string.IsNullOrEmpty(metadata.ColorProfile)));
                    allMetadata.Add("Уровень масштабирования", (metadata.ScalingLevel, !string.IsNullOrEmpty(metadata.ScalingLevel)));
                    allMetadata.Add("Модель камеры", (metadata.CameraModel, !string.IsNullOrEmpty(metadata.CameraModel)));
                    allMetadata.Add("Ориентация", (metadata.Orientation, !string.IsNullOrEmpty(metadata.Orientation)));
                    allMetadata.Add("Геолокация", (metadata.Geolocation, !string.IsNullOrEmpty(metadata.Geolocation)));
                    break;

                case ".mp3":
                case ".wav":
                case ".aac":
                    allMetadata.Add("Создатель", (metadata.Creator, !string.IsNullOrEmpty(metadata.Creator)));
                    allMetadata.Add("Длительность", (metadata.Duration, !string.IsNullOrEmpty(metadata.Duration)));
                    allMetadata.Add("Частота дискретизации", (metadata.SampleRate, !string.IsNullOrEmpty(metadata.SampleRate)));
                    allMetadata.Add("Количество каналов", (metadata.ChannelCount, !string.IsNullOrEmpty(metadata.ChannelCount)));
                    allMetadata.Add("Битрейт", (metadata.AudioBitrate == 0 ? "Нет данных" : metadata.AudioBitrate.ToString(), metadata.AudioBitrate > 0));
                    allMetadata.Add("Название трека", (metadata.TrackTitle, !string.IsNullOrEmpty(metadata.TrackTitle)));
                    allMetadata.Add("Исполнитель", (metadata.Artist, !string.IsNullOrEmpty(metadata.Artist)));
                    allMetadata.Add("Альбом", (metadata.Album, !string.IsNullOrEmpty(metadata.Album)));
                    allMetadata.Add("Год выпуска", (metadata.ReleaseYear == 0 ? "Нет данных" : metadata.ReleaseYear.ToString(), metadata.ReleaseYear > 0));
                    allMetadata.Add("Жанр", (metadata.Genre, !string.IsNullOrEmpty(metadata.Genre)));
                    break;

                case ".mp4":
                case ".webm":
                case ".avi":
                case ".mkv":
                case ".mov":
                case ".wmv":
                    allMetadata.Add("Длительность", (metadata.Duration, !string.IsNullOrEmpty(metadata.Duration)));
                    allMetadata.Add("Разрешение", (metadata.Resolution, !string.IsNullOrEmpty(metadata.Resolution)));
                    allMetadata.Add("Частота кадров", (metadata.FrameRate, !string.IsNullOrEmpty(metadata.FrameRate)));
                    allMetadata.Add("Видеокодек", (metadata.VideoCodec, !string.IsNullOrEmpty(metadata.VideoCodec)));
                    allMetadata.Add("Аудиокодек", (metadata.AudioCodec, !string.IsNullOrEmpty(metadata.AudioCodec)));
                    allMetadata.Add("Битрейт видео", (metadata.VideoBitrate == 0 ? "Нет данных" : metadata.VideoBitrate.ToString(), metadata.VideoBitrate > 0));
                    allMetadata.Add("Битрейт аудио", (metadata.AudioBitrate == 0 ? "Нет данных" : metadata.AudioBitrate.ToString(), metadata.AudioBitrate > 0));
                    allMetadata.Add("Год выпуска", (metadata.ReleaseYear == 0 ? "Нет данных" : metadata.ReleaseYear.ToString(), metadata.ReleaseYear > 0));
                    allMetadata.Add("Количество аудиотреков", (metadata.AudioTrack == 0 ? "Нет данных" : metadata.AudioTrack.ToString(), metadata.AudioTrack > 0));
                    allMetadata.Add("Описание", (metadata.Description, !string.IsNullOrEmpty(metadata.Description)));
                    break;
            }

            return allMetadata;
        }

        private void ExportSingleFileMetadata(Files file)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Метаданные файла");
                worksheet.Cells.Style.Font.Size = 12;
                worksheet.Cells.Style.Font.Name = "Times New Roman";

                worksheet.Cells[1, 1].Value = "Отчет по метаданным файла";
                worksheet.Cells[1, 1, 1, 5].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(181, 181, 181));
                worksheet.Cells[1, 1, 1, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                worksheet.Cells[2, 1].Value = "Номер проекта";
                worksheet.Cells[2, 2].Value = "Отчет_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                worksheet.Cells[2, 1, 2, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                worksheet.Cells[3, 1].Value = "Название отчета";
                worksheet.Cells[3, 2].Value = "Аналитика метаданных файла: " + file.FileName;
                worksheet.Cells[3, 1, 3, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                worksheet.Cells[4, 1].Value = "Дата формирования";
                worksheet.Cells[4, 2].Value = DateTime.Now.ToString("dd.MM.yyyy");
                worksheet.Cells[4, 1, 4, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                worksheet.Cells[5, 1].Value = "Сгенерировал";
                worksheet.Cells[5, 2].Value = Environment.UserName;
                worksheet.Cells[5, 1, 5, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                int row = 7;

                var metadata = ScanFile(file);
                var allMetadata = GetAllMetadata(file, metadata);

                worksheet.Cells[row, 1].Value = "Общие метаданные";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                worksheet.Cells[row, 1].Style.Font.Size = 14;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                row++;

                worksheet.Cells[row, 1].Value = "Имя файла";
                worksheet.Cells[row, 2].Value = metadata.FileName;
                row++;

                worksheet.Cells[row, 1].Value = "Формат файла";
                worksheet.Cells[row, 2].Value = metadata.FileFormat;
                row++;

                worksheet.Cells[row, 1].Value = "Владелец";
                worksheet.Cells[row, 2].Value = metadata.UserName;
                row++;

                worksheet.Cells[row, 1].Value = "Путь к файлу";
                worksheet.Cells[row, 2].Value = metadata.FilePath;
                row++;

                worksheet.Cells[row, 1].Value = "Размер файла";
                worksheet.Cells[row, 2].Value = FileUtils.FormatFileSize(metadata.FileSize);
                row++;

                worksheet.Cells[row, 1].Value = "Дата загрузки";
                worksheet.Cells[row, 2].Value = metadata.UploadDate.ToString("dd.MM.yyyy HH:mm");
                row++;

                worksheet.Cells[row, 1].Value = "Дата создания";
                worksheet.Cells[row, 2].Value = metadata.CreationDate.ToString("dd.MM.yyyy");
                row++;

                worksheet.Cells[row, 1].Value = "Дата изменения";
                worksheet.Cells[row, 2].Value = metadata.ModificationDate.ToString("dd.MM.yyyy");
                row++;

                if (!string.IsNullOrEmpty(metadata.PreviewImage) && File.Exists(metadata.PreviewImage))
                {
                    using (var img = System.Drawing.Image.FromFile(metadata.PreviewImage))
                    {
                        float aspectRatio = (float)img.Width / img.Height;

                        int cellHeightPixels = 100;
                        worksheet.Row(row).Height = cellHeightPixels * 0.75f;

                        int imageWidth = (int)(cellHeightPixels * aspectRatio);

                        worksheet.Cells[row, 1].Value = "Превью";
                        worksheet.Cells[row, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;

                        var imageFile = new FileInfo(metadata.PreviewImage);
                        var picture = worksheet.Drawings.AddPicture("PreviewImage", imageFile);

                        picture.SetPosition(row - 1, 0, 1, 0);
                        picture.SetSize(imageWidth, cellHeightPixels);

                        int columnWidth = (int)(imageWidth / 7.5);
                        if (worksheet.Column(2).Width < columnWidth)
                        {
                            worksheet.Column(2).Width = columnWidth;
                        }
                    }
                    row++;
                }

                worksheet.Cells[row, 1].Value = "Специфические данные";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                worksheet.Cells[row, 1].Style.Font.Size = 14;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                row++;

                switch (file.FileFormat.ToLower())
                {
                    case ".txt":
                        worksheet.Cells[row, 1].Value = "Количество строк";
                        worksheet.Cells[row, 2].Value = metadata.LineCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество символов (с пробелами)";
                        worksheet.Cells[row, 2].Value = metadata.SymbolCountWithSpaces;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество символов (без пробелов)";
                        worksheet.Cells[row, 2].Value = metadata.SymbolCountWithoutSpaces;
                        row++;
                        worksheet.Cells[row, 1].Value = "Кодировка";
                        worksheet.Cells[row, 2].Value = metadata.Encoding;
                        row++;
                        break;

                    case ".doc":
                    case ".docx":
                        worksheet.Cells[row, 1].Value = "Создатель";
                        worksheet.Cells[row, 2].Value = metadata.Creator;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество страниц";
                        worksheet.Cells[row, 2].Value = metadata.PageCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество строк";
                        worksheet.Cells[row, 2].Value = metadata.LineCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество столбцов";
                        worksheet.Cells[row, 2].Value = metadata.ColumnCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество формул";
                        worksheet.Cells[row, 2].Value = metadata.FormulaCount.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество изображений";
                        worksheet.Cells[row, 2].Value = metadata.ImageCount.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество таблиц";
                        worksheet.Cells[row, 2].Value = metadata.TableCount.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Кодировка";
                        worksheet.Cells[row, 2].Value = metadata.Encoding;
                        row++;
                        worksheet.Cells[row, 1].Value = "Язык документа";
                        worksheet.Cells[row, 2].Value = metadata.Language ?? "Нет данных";
                        row++;
                        worksheet.Cells[row, 1].Value = "Версия документа";
                        worksheet.Cells[row, 2].Value = metadata.Version ?? "Нет данных";
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество символов (с пробелами)";
                        worksheet.Cells[row, 2].Value = metadata.SymbolCountWithSpaces;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество символов (без пробелов)";
                        worksheet.Cells[row, 2].Value = metadata.SymbolCountWithoutSpaces;
                        row++;
                        break;

                    case ".xlsx":
                    case ".xls":
                        worksheet.Cells[row, 1].Value = "Создатель";
                        worksheet.Cells[row, 2].Value = metadata.Creator;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество страниц";
                        worksheet.Cells[row, 2].Value = metadata.PageCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество строк";
                        worksheet.Cells[row, 2].Value = metadata.LineCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество столбцов";
                        worksheet.Cells[row, 2].Value = metadata.ColumnCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество формул";
                        worksheet.Cells[row, 2].Value = metadata.FormulaCount.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество изображений";
                        worksheet.Cells[row, 2].Value = metadata.ImageCount.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество таблиц";
                        worksheet.Cells[row, 2].Value = metadata.TableCount.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Кодировка";
                        worksheet.Cells[row, 2].Value = metadata.Encoding;
                        row++;
                        break;

                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".gif":
                    case ".bmp":
                        worksheet.Cells[row, 1].Value = "Создатель";
                        worksheet.Cells[row, 2].Value = metadata.Creator;
                        row++;
                        worksheet.Cells[row, 1].Value = "Разрешение";
                        worksheet.Cells[row, 2].Value = metadata.Resolution;
                        row++;
                        worksheet.Cells[row, 1].Value = "Глубина цвета";
                        worksheet.Cells[row, 2].Value = metadata.ColorDepth;
                        row++;
                        worksheet.Cells[row, 1].Value = "Уровень сжатия";
                        worksheet.Cells[row, 2].Value = metadata.CompressionLevel;
                        row++;
                        worksheet.Cells[row, 1].Value = "Цветовой профиль";
                        worksheet.Cells[row, 2].Value = metadata.ColorProfile;
                        row++;
                        worksheet.Cells[row, 1].Value = "Уровень масштабирования";
                        worksheet.Cells[row, 2].Value = metadata.ScalingLevel;
                        row++;
                        worksheet.Cells[row, 1].Value = "Модель камеры";
                        worksheet.Cells[row, 2].Value = metadata.CameraModel;
                        row++;
                        worksheet.Cells[row, 1].Value = "Ориентация";
                        worksheet.Cells[row, 2].Value = metadata.Orientation ?? "Нет данных";
                        row++;
                        worksheet.Cells[row, 1].Value = "Геолокация";
                        worksheet.Cells[row, 2].Value = string.IsNullOrEmpty(metadata.Geolocation) ? "Нет данных" : metadata.Geolocation;
                        row++;
                        break;

                    case ".mp3":
                    case ".wav":
                    case ".aac":
                        worksheet.Cells[row, 1].Value = "Создатель";
                        worksheet.Cells[row, 2].Value = metadata.Creator;
                        row++;
                        worksheet.Cells[row, 1].Value = "Длительность";
                        worksheet.Cells[row, 2].Value = metadata.Duration;
                        row++;
                        worksheet.Cells[row, 1].Value = "Частота дискретизации";
                        worksheet.Cells[row, 2].Value = metadata.SampleRate;
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество каналов";
                        worksheet.Cells[row, 2].Value = metadata.ChannelCount;
                        row++;
                        worksheet.Cells[row, 1].Value = "Битрейт";
                        worksheet.Cells[row, 2].Value = metadata.AudioBitrate == 0 ? "Нет данных" : metadata.AudioBitrate.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Название трека";
                        worksheet.Cells[row, 2].Value = metadata.TrackTitle;
                        row++;
                        worksheet.Cells[row, 1].Value = "Исполнитель";
                        worksheet.Cells[row, 2].Value = metadata.Artist;
                        row++;
                        worksheet.Cells[row, 1].Value = "Альбом";
                        worksheet.Cells[row, 2].Value = metadata.Album;
                        row++;
                        worksheet.Cells[row, 1].Value = "Год выпуска";
                        worksheet.Cells[row, 2].Value = metadata.ReleaseYear == 0 ? "Нет данных" : metadata.ReleaseYear.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Жанр";
                        worksheet.Cells[row, 2].Value = metadata.Genre;
                        row++;
                        break;

                    case ".mp4":
                    case ".webm":
                    case ".avi":
                    case ".mkv":
                    case ".mov":
                    case ".wmv":
                        worksheet.Cells[row, 1].Value = "Длительность";
                        worksheet.Cells[row, 2].Value = metadata.Duration;
                        row++;
                        worksheet.Cells[row, 1].Value = "Разрешение";
                        worksheet.Cells[row, 2].Value = metadata.Resolution;
                        row++;
                        worksheet.Cells[row, 1].Value = "Частота кадров";
                        worksheet.Cells[row, 2].Value = metadata.FrameRate;
                        row++;
                        worksheet.Cells[row, 1].Value = "Видеокодек";
                        worksheet.Cells[row, 2].Value = metadata.VideoCodec;
                        row++;
                        worksheet.Cells[row, 1].Value = "Аудиокодек";
                        worksheet.Cells[row, 2].Value = metadata.AudioCodec;
                        row++;
                        worksheet.Cells[row, 1].Value = "Битрейт видео";
                        worksheet.Cells[row, 2].Value = metadata.VideoBitrate == 0 ? "Нет данных" : metadata.VideoBitrate.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Битрейт аудио";
                        worksheet.Cells[row, 2].Value = metadata.AudioBitrate == 0 ? "Нет данных" : metadata.AudioBitrate.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Год выпуска";
                        worksheet.Cells[row, 2].Value = metadata.ReleaseYear == 0 ? "Нет данных" : metadata.ReleaseYear.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Количество аудиотреков";
                        worksheet.Cells[row, 2].Value = metadata.AudioTrack == 0 ? "Нет данных" : metadata.AudioTrack.ToString();
                        row++;
                        worksheet.Cells[row, 1].Value = "Описание";
                        worksheet.Cells[row, 2].Value = metadata.Description;
                        row++;
                        break;
                }

                worksheet.Cells[row, 1].Value = "Аналитика по метаданным";
                worksheet.Cells[row, 1, row, 5].Merge = true;
                worksheet.Cells[row, 1].Style.Font.Size = 14;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGoldenrodYellow);
                worksheet.Cells[row, 1, row, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                row++;

                int totalFields = allMetadata.Count;
                int filledFields = allMetadata.Count(m => m.Value.IsFilled);
                double fillPercentage = totalFields > 0 ? (filledFields * 100.0) / totalFields : 0;

                double averageFileNameLength = metadata.FileName.Length;
                worksheet.Cells[row, 1].Value = "Средняя длина имени файла (символы)";
                worksheet.Cells[row, 2].Value = averageFileNameLength;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                TimeSpan fileAge = DateTime.Now - metadata.CreationDate;
                worksheet.Cells[row, 1].Value = "Возраст файла (дней)";
                worksheet.Cells[row, 2].Value = fileAge.Days;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                TimeSpan timeSinceLastEdit = DateTime.Now - metadata.ModificationDate;
                worksheet.Cells[row, 1].Value = "Время с последнего изменения (дней)";
                worksheet.Cells[row, 2].Value = timeSinceLastEdit.Days;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                worksheet.Cells[row, 1].Value = "Процент заполненности метаданных (%)";
                worksheet.Cells[row, 2].Value = $"{fillPercentage:F2}%";
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                worksheet.Cells[row, 1].Value = "Всего полей метаданных";
                worksheet.Cells[row, 2].Value = totalFields;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                worksheet.Cells[row, 1].Value = "Заполнено полей";
                worksheet.Cells[row, 2].Value = filledFields;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                worksheet.Cells[row, 1].Value = "Не заполнено полей";
                worksheet.Cells[row, 2].Value = totalFields - filledFields;
                worksheet.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                int chartStartColumn = 6;
                int chartWidth = 11;
                int chartHeight = 13;

                var pieChart = worksheet.Drawings.AddChart("FillPercentageChart", OfficeOpenXml.Drawing.Chart.eChartType.Pie3D);
                pieChart.Title.Text = "Заполненность метаданных";
                pieChart.SetPosition(1, 0, chartStartColumn, 0);
                pieChart.SetSize(chartWidth * 46, chartHeight * 20);

                int startRow = row - 4; // Начальная строка (Процент заполненности)
                int endRow = row - 1;   // Конечная строка (Не заполнено полей)

                // Только не нулевые значения
                var valuesRange = worksheet.Cells[startRow + 1, 2, endRow, 2];
                var labelsRange = worksheet.Cells[startRow + 1, 1, endRow, 1];

                var filteredValues = new List<ExcelRangeBase>();
                var filteredLabels = new List<ExcelRangeBase>();

                // Фильтрация данных, исключая нулевые значения
                for (int i = 0; i < 3; i++)
                {
                    var cellValue = worksheet.Cells[startRow + 1 + i, 2].GetValue<double>();
                    if (cellValue > 0)
                    {
                        filteredValues.Add(worksheet.Cells[startRow + 1 + i, 2]);
                        filteredLabels.Add(worksheet.Cells[startRow + 1 + i, 1]);
                    }
                }

                // Серия только с не нулевыми значениями
                if (filteredValues.Count > 0)
                {
                    var pieSeries = (OfficeOpenXml.Drawing.Chart.ExcelPieChartSerie)pieChart.Series.Add(
                        worksheet.Cells[filteredValues[0].Start.Row, 2, filteredValues[filteredValues.Count - 1].Start.Row, 2],
                        worksheet.Cells[filteredLabels[0].Start.Row, 1, filteredLabels[filteredLabels.Count - 1].Start.Row, 1]);

                    pieSeries.DataLabel.ShowPercent = true;
                    pieSeries.DataLabel.ShowCategory = false;
                    pieSeries.DataLabel.ShowValue = false;
                    pieChart.Legend.Position = OfficeOpenXml.Drawing.Chart.eLegendPosition.Right;
                }
                else
                {
                    worksheet.Cells[row + 1, 1].Value = "Нет данных для построения диаграммы";
                }

                worksheet.Column(1).AutoFit();
                worksheet.Column(2).AutoFit();
                worksheet.Column(3).AutoFit();
                worksheet.Column(4).AutoFit();
                worksheet.Column(5).AutoFit();

                for (int i = 1; i < row; i++)
                {
                    if (worksheet.Cells[i, 1].Value != null)
                    {
                        worksheet.Cells[i, 1, i, 5].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }
                }

                var saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Excel файлы (*.xlsx)|*.xlsx";
                saveFileDialog.Title = "Сохранить отчет по метаданные файла";
                string datePart = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                saveFileDialog.FileName = $"Отчёт_по_метаданным_файла_{Path.GetFileNameWithoutExtension(metadata.FileName)}_{datePart}.xlsx";

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        package.SaveAs(new FileInfo(saveFileDialog.FileName));
                        SaveReport(file, "Метаданные файла успешно выгружены в Excel", "Метаданные файла", saveFileDialog.FileName);

                        var dialog = new MessageBoxExport(
                            "Метаданные файла успешно выгружены в Excel!",
                            "Успех",
                            saveFileDialog.FileName);
                        dialog.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}