using DocumentFormat.OpenXml.Packaging;
using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Encoder = System.Drawing.Imaging.Encoder;
using File = System.IO.File;
using Path = System.IO.Path;

namespace MdSearch_1._0
{
    public partial class ScanResultWindow : Window
    {
        private Entities entities = new Entities();
        private MetadataModel _metadata;
        private readonly int _userRoleId;
        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch
            {
                return true;
            }

            return false;
        }

        public ScanResultWindow(int userRoleId)
        {
            InitializeComponent();
            _userRoleId = userRoleId;
            this.DataContext = new { CanEditMetadata = CanEditMetadata() };
            InitializeUserInterface();
        }
        private void InitializeUserInterface()
        {
            SaveButton.Visibility = Visibility.Collapsed;

            if (_userRoleId == 2)
            {
                SaveButton.Visibility = Visibility.Visible;
                return;
            }

            var userId = entities.Users
                .Where(u => u.RoleId == _userRoleId)
                .Select(u => u.Id)
                .FirstOrDefault();

            if (userId != 0)
            {
                var permission = entities.UserPermissions
                    .FirstOrDefault(p => p.UserID == userId);

                if (permission?.CanEditMetadata == true)
                {
                    SaveButton.Visibility = Visibility.Visible;
                }
            }

            var contextMenu = (ContextMenu)FindResource("ImageContextMenu");
        }

        public void SetMetadata(MetadataModel metadata)
        {
            _metadata = metadata;
            var metadataList = new List<MetadataAttribute>
            {
                new MetadataAttribute { AttributeName = "Имя файла", AttributeValue = Path.GetFileNameWithoutExtension(metadata.FileName), IsEditable = true },
                new MetadataAttribute { AttributeName = "Формат файла", AttributeValue = metadata.FileFormat },
                new MetadataAttribute { AttributeName = "Владелец", AttributeValue = metadata.UserName },
                new MetadataAttribute { AttributeName = "Путь к файлу", AttributeValue = metadata.FilePath },
                new MetadataAttribute { AttributeName = "Размер файла", AttributeValue = FileUtils.FormatFileSize(metadata.FileSize) },
                new MetadataAttribute { AttributeName = "Дата загрузки", AttributeValue = metadata.UploadDate.ToString("dd.MM.yyyy HH:mm") },
                new MetadataAttribute { AttributeName = "Дата создания", AttributeValue = metadata.CreationDate.ToString("dd.MM.yyyy HH:mm:ss"), IsEditable = true },
                new MetadataAttribute { AttributeName = "Дата изменения", AttributeValue = metadata.ModificationDate.ToString("dd.MM.yyyy HH:mm:ss"), IsEditable = true }
            };

            if (!string.IsNullOrEmpty(metadata.PreviewImage) && File.Exists(metadata.PreviewImage))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(metadata.PreviewImage);
                bitmap.EndInit();
                metadataList.Add(new MetadataAttribute { AttributeName = "Превью", AttributeValue = "", Image = bitmap });
            }

            switch (metadata.FileFormat.ToLower())
            {
                case ".txt":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Язык документа", AttributeValue = metadata.Language ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Количество строк", AttributeValue = metadata.LineCount },
                        new MetadataAttribute { AttributeName = "Количество слов", AttributeValue = metadata.WordCount.ToString() },
                        new MetadataAttribute { AttributeName = "Количество символов (с пробелами)", AttributeValue = metadata.SymbolCountWithSpaces },
                        new MetadataAttribute { AttributeName = "Количество символов (без пробелов)", AttributeValue = metadata.SymbolCountWithoutSpaces },
                        new MetadataAttribute { AttributeName = "Кодировка", AttributeValue = metadata.Encoding }
                    });
                    break;

                case ".doc":
                case ".docx":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator, IsEditable=true },
                        new MetadataAttribute { AttributeName = "Количество страниц", AttributeValue = metadata.PageCount },
                        new MetadataAttribute { AttributeName = "Количество строк", AttributeValue = metadata.LineCount },
                        new MetadataAttribute { AttributeName = "Количество символов (с пробелами)", AttributeValue = metadata.SymbolCountWithSpaces },
                        new MetadataAttribute { AttributeName = "Количество символов (без пробелов)", AttributeValue = metadata.SymbolCountWithoutSpaces },
                        new MetadataAttribute { AttributeName = "Количество таблиц", AttributeValue = metadata.TableCount.ToString() },
                        new MetadataAttribute { AttributeName = "Количество изображений", AttributeValue = metadata.ImageCount.ToString() },
                        new MetadataAttribute { AttributeName = "Количество столбцов", AttributeValue = metadata.ColumnCount },
                        new MetadataAttribute { AttributeName = "Количество слов", AttributeValue = metadata.WordCount.ToString() },
                        new MetadataAttribute { AttributeName = "Кодировка", AttributeValue = metadata.Encoding },
                        new MetadataAttribute { AttributeName = "Язык документа", AttributeValue = metadata.Language ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Версия документа", AttributeValue = metadata.Version ?? "Не определена", IsEditable=true }
                    });
                    break;

                case ".xlsx":
                case ".xls":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator, IsEditable=true },
                        new MetadataAttribute { AttributeName = "Количество листов", AttributeValue = metadata.PageCount },
                        new MetadataAttribute { AttributeName = "Количество строк", AttributeValue = metadata.LineCount },
                        new MetadataAttribute { AttributeName = "Количество таблиц", AttributeValue = metadata.TableCount.ToString() },
                        new MetadataAttribute { AttributeName = "Количество изображений", AttributeValue = metadata.ImageCount.ToString() },
                        new MetadataAttribute { AttributeName = "Количество формул", AttributeValue = metadata.FormulaCount.ToString() },
                        new MetadataAttribute { AttributeName = "Кодировка", AttributeValue = metadata.Encoding },
                        new MetadataAttribute { AttributeName = "Версия документа", AttributeValue = metadata.Version ?? "Не определена", IsEditable=true }
                    });
                    break;

                case ".jpg":
                case ".jpeg":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator, IsEditable=true },
                        new MetadataAttribute { AttributeName = "Разрешение", AttributeValue = metadata.Resolution },
                        new MetadataAttribute { AttributeName = "Глубина цвета", AttributeValue = $"{metadata.ColorDepth} бит" },
                        new MetadataAttribute { AttributeName = "Ориентация", AttributeValue = metadata.Orientation ?? "Не определена" },
                        new MetadataAttribute { AttributeName = "Уровень сжатия", AttributeValue = metadata.CompressionLevel ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Цветовой профиль", AttributeValue = metadata.ColorProfile ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Модель камеры", AttributeValue = metadata.CameraModel ?? "Не определена", IsEditable=true },
                        new MetadataAttribute { AttributeName = "Геолокация", AttributeValue = metadata.Geolocation ?? "Не определена" },
                    });
                    break;

                case ".png":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator, IsEditable=true },
                        new MetadataAttribute { AttributeName = "Разрешение", AttributeValue = metadata.Resolution },
                        new MetadataAttribute { AttributeName = "Глубина цвета", AttributeValue = $"{metadata.ColorDepth} бит" },
                        new MetadataAttribute { AttributeName = "Ориентация", AttributeValue = metadata.Orientation ?? "Не определена" },
                        new MetadataAttribute { AttributeName = "Уровень сжатия", AttributeValue = metadata.CompressionLevel ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Цветовой профиль", AttributeValue = metadata.ColorProfile ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Геолокация", AttributeValue = metadata.Geolocation ?? "Не определена" },
                    });
                    break;

                case ".gif":
                case ".bmp":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Разрешение", AttributeValue = metadata.Resolution },
                        new MetadataAttribute { AttributeName = "Глубина цвета", AttributeValue = $"{metadata.ColorDepth} бит" },
                        new MetadataAttribute { AttributeName = "Ориентация", AttributeValue = metadata.Orientation ?? "Не определена" },
                        new MetadataAttribute { AttributeName = "Цветовой профиль", AttributeValue = metadata.ColorProfile ?? "Не определен" },
                    });
                    break;

                case ".mp3":
                case ".wav":
                case ".aac":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator, IsEditable=true },
                        new MetadataAttribute { AttributeName = "Длительность", AttributeValue = $"{metadata.Duration} сек" },
                        new MetadataAttribute { AttributeName = "Частота дискретизации", AttributeValue = $"{metadata.SampleRate} Гц" },
                        new MetadataAttribute { AttributeName = "Количество каналов", AttributeValue = metadata.ChannelCount },
                        new MetadataAttribute { AttributeName = "Битрейт", AttributeValue = $"{metadata.AudioBitrate} кбит/с" },
                        new MetadataAttribute { AttributeName = "Название трека", AttributeValue = metadata.TrackTitle ?? "Не определено", IsEditable=true },
                        new MetadataAttribute { AttributeName = "Исполнитель", AttributeValue = metadata.Artist ?? "Не определен", IsEditable=true },
                        new MetadataAttribute { AttributeName = "Альбом", AttributeValue = metadata.Album ?? "Не определен", IsEditable=true },
                        new MetadataAttribute { AttributeName = "Год выпуска", AttributeValue = metadata.ReleaseYear > 0 ? metadata.ReleaseYear.ToString() : "Не определен", IsEditable=true },
                        new MetadataAttribute { AttributeName = "Жанр", AttributeValue = metadata.Genre ?? "Не определен", IsEditable=true }
                    });
                    break;

                case ".mp4":
                case ".webm":
                case ".avi":
                case ".mkv":
                case ".mov":
                case ".wmv":
                    metadataList.AddRange(new[]
                    {
                    new MetadataAttribute { AttributeName = "Длительность", AttributeValue = $"{metadata.Duration} сек" },
                    new MetadataAttribute { AttributeName = "Разрешение", AttributeValue = metadata.Resolution },
                    new MetadataAttribute { AttributeName = "Частота кадров", AttributeValue = $"{metadata.FrameRate} кадр/сек" },
                    new MetadataAttribute { AttributeName = "Скорость передачи данных", AttributeValue = $"{metadata.DataTransferRate} кбит/с" },
                    new MetadataAttribute { AttributeName = "Видеокодек", AttributeValue = metadata.VideoCodec ?? "Не определен" },
                    new MetadataAttribute { AttributeName = "Аудиокодек", AttributeValue = metadata.AudioCodec ?? "Не определен" },
                    new MetadataAttribute { AttributeName = "Общая скорость потока данных", AttributeValue = $"{metadata.TotalBitrate} кбит/с" },
                    new MetadataAttribute { AttributeName = "Частота дискретизации", AttributeValue = $"{metadata.SampleRate} Гц" },
                    new MetadataAttribute { AttributeName = "Год выпуска", AttributeValue = metadata.ReleaseYear > 0 ? metadata.ReleaseYear.ToString() : "Не определен", IsEditable=true },
                    new MetadataAttribute { AttributeName = "Количество аудиотреков", AttributeValue = metadata.AudioTrack.ToString() },
                    new MetadataAttribute { AttributeName = "Жанр", AttributeValue = metadata.Genre ?? "Неизвестно", IsEditable=true },
                    new MetadataAttribute { AttributeName = "Описание", AttributeValue = metadata.Description ?? "Нет описания", IsEditable=true },
                });
                    break;
            }
            if (!CanEditMetadata())
            {
                foreach (var attr in metadataList)
                {
                    attr.IsEditable = false;
                }
            }
            MetadataDataGrid.ItemsSource = metadataList;
        }

        private bool CanEditMetadata()
        {
            if (_userRoleId == 2) return true;

            var userId = entities.Users
                .Where(u => u.RoleId == _userRoleId)
                .Select(u => u.Id)
                .FirstOrDefault();

            if (userId == 0) return false;

            var permission = entities.UserPermissions
                .FirstOrDefault(p => p.UserID == userId);

            return permission?.CanEditMetadata == true;
        }

        private void RenameFile(string newFileName)
        {
            try
            {
                string directory = Path.GetDirectoryName(_metadata.FilePath);
                string extension = Path.GetExtension(_metadata.FilePath);
                string newFilePath = Path.Combine(directory, newFileName + extension);

                if (File.Exists(newFilePath))
                {
                    MessageBox.Show("Файл с таким именем уже существует в этой папке.",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (IsFileLocked(new FileInfo(_metadata.FilePath)))
                {
                    MessageBox.Show("Невозможно переименовать файл — он используется другой программой.",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                File.Move(_metadata.FilePath, newFilePath);

                _metadata.FileName = newFileName + extension;
                _metadata.FilePath = newFilePath;

                UpdateFileNameInDatabase(_metadata.FileID, _metadata.FileName, newFilePath);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Нет прав для доступа к файлу. Закройте его в других программах.",
                                "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex) when ((ex.HResult & 0x0000FFFF) == 32)
            {
                MessageBox.Show("Файл заблокирован другой программой. Закройте его и попробуйте снова.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении имени файла: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateFileNameInDatabase(int fileId, string newFileName, string newFilePath)
        {
            try
            {
                var fileToUpdate = entities.Files.FirstOrDefault(f => f.FileID == fileId);
                if (fileToUpdate != null)
                {
                    fileToUpdate.FileName = newFileName;
                    fileToUpdate.FilePath = newFilePath;
                    entities.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления имени файла в БД: {ex.Message}",
                              "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var metadataList = (List<MetadataAttribute>)MetadataDataGrid.ItemsSource;

            // Сначала изменение имени файла, если оно изменилось
            var fileNameAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Имя файла");
            string newFileName = fileNameAttribute?.AttributeValue;
            string currentFileNameWithoutExt = Path.GetFileNameWithoutExtension(_metadata.FileName);

            if (!string.IsNullOrEmpty(newFileName) && newFileName != currentFileNameWithoutExt)
            {
                RenameFile(newFileName);
                // Обновление _metadata после переименования
                _metadata.FileName = newFileName + Path.GetExtension(_metadata.FileName);
                _metadata.FilePath = Path.Combine(
                    Path.GetDirectoryName(_metadata.FilePath),
                    _metadata.FileName
                );
            }

            // Толкьо потом остальные
            var creatorAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Создатель");
            var cameraModelAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Модель камеры");
            var versionAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Версия документа");

            var trackTitleAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Название трека");
            var artistAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Исполнитель");
            var albumAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Альбом");
            var releaseYearAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Год выпуска");
            var genreAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Жанр");
            var descriptionAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Описание");

            var creationDateAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Дата создания");
            var modificationDateAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Дата изменения");

            try
            {
                if (creationDateAttribute != null && !DateTime.TryParse(creationDateAttribute.AttributeValue, out _))
                {
                    MessageBox.Show("Неверный формат даты создания.\nФормат: dd.MM.yyyy HH:mm:ss",
                                  "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (modificationDateAttribute != null && !DateTime.TryParse(modificationDateAttribute.AttributeValue, out _))
                {
                    MessageBox.Show("Неверный формат даты изменения.\nФормат: dd.MM.yyyy HH:mm:ss",
                                  "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DateTime newCreationDate = _metadata.CreationDate;
                DateTime newModificationDate = _metadata.ModificationDate;

                if (creationDateAttribute != null)
                    DateTime.TryParse(creationDateAttribute.AttributeValue, out newCreationDate);

                if (modificationDateAttribute != null)
                    DateTime.TryParse(modificationDateAttribute.AttributeValue, out newModificationDate);

                if (newModificationDate < newCreationDate)
                {
                    MessageBox.Show("Дата изменения не может быть раньше даты создания.",
                                  "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                File.SetCreationTime(_metadata.FilePath, newCreationDate);
                _metadata.CreationDate = newCreationDate;
                UpdateCreationDateInDatabase(_metadata.FileID, newCreationDate);

                File.SetLastWriteTime(_metadata.FilePath, newModificationDate);
                _metadata.ModificationDate = newModificationDate;
                UpdateModificationDateInDatabase(_metadata.FileID, newModificationDate);

                string extension = Path.GetExtension(_metadata.FilePath)?.ToLower();
                switch (extension)
                {
                    case ".mp3":
                    case ".wav":
                    case ".aac":
                        SaveAudioMetadata(_metadata.FilePath, _metadata, metadataList);
                        break;
                    case ".docx":
                    case ".xlsx":
                        if (creatorAttribute != null && !string.IsNullOrEmpty(creatorAttribute.AttributeValue))
                        {
                            UpdateDocumentCreator(_metadata.FilePath, creatorAttribute.AttributeValue);
                            _metadata.Creator = creatorAttribute.AttributeValue;
                        }
                        if (versionAttribute != null && !string.IsNullOrEmpty(versionAttribute.AttributeValue))
                        {
                            UpdateDocumentVersion(_metadata.FilePath, versionAttribute.AttributeValue);
                            _metadata.Version = versionAttribute.AttributeValue;
                        }
                        break;
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                        UpdateImageMetadataExtended(
                            _metadata.FilePath,
                            cameraModelAttribute?.AttributeValue,
                            creatorAttribute?.AttributeValue);
                        break;
                    case ".mp4":
                    case ".webm":
                    case ".avi":
                    case ".mkv":
                    case ".mov":
                    case ".wmv":
                        SaveVideoMetadata(_metadata.FileID,
                                        releaseYearAttribute?.AttributeValue,
                                        descriptionAttribute?.AttributeValue,
                                        genreAttribute?.AttributeValue);
                        break;
                }

                try
                {
                    File.SetCreationTime(_metadata.FilePath, newCreationDate);
                    File.SetLastWriteTime(_metadata.FilePath, newModificationDate);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось вернуть даты: {ex.Message}");
                }

                MessageBox.Show("Изменения успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCreationDateInDatabase(int fileId, DateTime newCreationDate)
        {
            try
            {
                var fileToUpdate = entities.Files.FirstOrDefault(f => f.FileID == fileId);
                if (fileToUpdate != null)
                {
                    fileToUpdate.CreationDate = newCreationDate;
                    entities.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления даты создания в БД: {ex.Message}",
                              "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateModificationDateInDatabase(int fileId, DateTime newModificationDate)
        {
            try
            {
                var fileToUpdate = entities.Files.FirstOrDefault(f => f.FileID == fileId);
                if (fileToUpdate != null)
                {
                    fileToUpdate.ModificationDate = newModificationDate;
                    entities.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления даты изменения в БД: {ex.Message}",
                              "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveVideoMetadata(int fileId, string releaseYear, string description, string genre)
        {
            try
            {
                var videoFile = entities.VideoFiles.FirstOrDefault(v => v.FileID == fileId);
                if (videoFile != null)
                {
                    if (int.TryParse(releaseYear, out int year))
                    {
                        videoFile.ReleaseYear = year;
                        _metadata.ReleaseYear = year;
                    }

                    if (!string.IsNullOrEmpty(description))
                    {
                        videoFile.Description = description;
                        _metadata.Description = description;
                    }

                    if (!string.IsNullOrEmpty(genre))
                    {
                        videoFile.Genre = genre;
                        _metadata.Genre = genre;
                    }

                    entities.SaveChanges();

                    using (var file = TagLib.File.Create(_metadata.FilePath))
                    {
                        if (int.TryParse(releaseYear, out int y))
                            file.Tag.Year = (uint)y;

                        if (!string.IsNullOrEmpty(description))
                            file.Tag.Comment = description;

                        if (!string.IsNullOrEmpty(genre))
                            file.Tag.Genres = new[] { genre };

                        file.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить метаданные видео: {ex.Message}\n\nПроверьте доступ к файлу и попробуйте снова.",
                                "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAudioMetadata(string filePath, MetadataModel metadata, List<MetadataAttribute> metadataList)
        {
            try
            {
                using (var file = TagLib.File.Create(filePath))
                {
                    var trackTitleAttr = metadataList.FirstOrDefault(a => a.AttributeName == "Название трека");
                    if (trackTitleAttr != null) file.Tag.Title = trackTitleAttr.AttributeValue;

                    var artistAttr = metadataList.FirstOrDefault(a => a.AttributeName == "Исполнитель");
                    if (artistAttr != null) file.Tag.Performers = new[] { artistAttr.AttributeValue };

                    var albumAttr = metadataList.FirstOrDefault(a => a.AttributeName == "Альбом");
                    if (albumAttr != null) file.Tag.Album = albumAttr.AttributeValue;

                    var yearAttr = metadataList.FirstOrDefault(a => a.AttributeName == "Год выпуска");
                    if (yearAttr != null && uint.TryParse(yearAttr.AttributeValue, out uint year))
                        file.Tag.Year = year;

                    var genreAttr = metadataList.FirstOrDefault(a => a.AttributeName == "Жанр");
                    if (genreAttr != null) file.Tag.Genres = new[] { genreAttr.AttributeValue };

                    if (!string.IsNullOrEmpty(metadata.PreviewImage) && File.Exists(metadata.PreviewImage))
                    {
                        var picture = new TagLib.Id3v2.AttachmentFrame
                        {
                            Type = TagLib.PictureType.FrontCover,
                            Description = "Cover",
                            MimeType = GetMimeType(metadata.PreviewImage)
                        };

                        picture.Data = TagLib.ByteVector.FromPath(metadata.PreviewImage);
                        file.Tag.Pictures = new[] { picture };
                    }
                    else
                    {
                        file.Tag.Pictures = new TagLib.IPicture[0];
                    }

                    file.Save();
                    File.SetLastWriteTime(filePath, DateTime.Now);

                    UpdateAudioMetadataInDatabase(
                        metadata.FileID,
                        trackTitleAttr?.AttributeValue,
                        artistAttr?.AttributeValue,
                        albumAttr?.AttributeValue,
                        yearAttr?.AttributeValue,
                        genreAttr?.AttributeValue,
                        metadata.PreviewImage
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить метаданные аудиофайла: {ex.Message}\n\nУбедитесь, что файл не используется другими программами.",
                                "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                default:
                    return "application/octet-stream";
            }
        }

        private void UpdateAudioMetadataInDatabase(
            int fileId,
            string trackTitle,
            string artist,
            string album,
            string year,
            string genre,
            string previewImage)
        {
            try
            {
                var audioFile = entities.AudioFiles.FirstOrDefault(a => a.FileID == fileId);
                if (audioFile != null)
                {
                    audioFile.TrackTitle = trackTitle;
                    audioFile.Artist = artist;
                    audioFile.Album = album;

                    if (int.TryParse(year, out int y)) audioFile.ReleaseYear = y;

                    audioFile.Genre = genre;
                    audioFile.PreviewImage = previewImage;

                    entities.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления аудио метаданных в базе данных: {ex.Message}",
                              "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDocumentVersion(string filePath, string newVersion)
        {
            try
            {
                string extension = Path.GetExtension(filePath)?.ToLower();

                if (extension == ".docx")
                {
                    using (var doc = WordprocessingDocument.Open(filePath, true))
                    {
                        if (doc.PackageProperties != null)
                        {
                            doc.PackageProperties.Version = newVersion;
                        }
                    }
                }
                else if (extension == ".xlsx")
                {
                    using (var spreadsheet = SpreadsheetDocument.Open(filePath, true))
                    {
                        if (spreadsheet.PackageProperties != null)
                        {
                            spreadsheet.PackageProperties.Version = newVersion;
                        }
                    }
                }

                UpdateVersionInDatabase(_metadata.FileID, newVersion);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось изменить версию документа: {ex.Message}\n\nПроверьте, что файл не защищен от записи.",
                                "Ошибка изменения версии", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateVersionInDatabase(int fileId, string newVersion)
        {
            try
            {
                var fileToUpdate = entities.Files.FirstOrDefault(f => f.FileID == fileId);
                if (fileToUpdate != null)
                {
                    var document = entities.Documents.FirstOrDefault(d => d.FileID == fileId);
                    if (document != null)
                    {
                        document.Version = newVersion;
                        entities.SaveChanges();
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось обновить версию документа в базе данных.\nДанные в файле были изменены, но информация в системе может быть неактуальной.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateImageMetadataExtended(string filePath, string cameraModel, string creator)
        {
            try
            {
                if (!string.IsNullOrEmpty(cameraModel))
                    _metadata.CameraModel = cameraModel;

                if (!string.IsNullOrEmpty(creator))
                    _metadata.Creator = creator;

                UpdateImageMetadata(filePath, creator, cameraModel);

                UpdateImageMetadataInDatabase(_metadata.FileID, creator, cameraModel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось обновить метаданные изображения: {ex.Message}",
                               "Ошибка обновления", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateImageMetadataInDatabase(int fileId, string creator, string cameraModel)
        {
            try
            {
                var imageFile = entities.Images.FirstOrDefault(i => i.FileID == fileId);
                if (imageFile != null)
                {
                    if (!string.IsNullOrEmpty(creator))
                        imageFile.Creator = creator;

                    if (!string.IsNullOrEmpty(cameraModel))
                        imageFile.CameraModel = cameraModel;

                    entities.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось обновить метаданные в базе данных: {ex.Message}",
                               "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateDocumentCreator(string filePath, string newCreator)
        {
            try
            {
                string extension = Path.GetExtension(filePath)?.ToLower();

                switch (extension)
                {
                    case ".docx":
                        using (var doc = WordprocessingDocument.Open(filePath, true))
                        {
                            if (doc.PackageProperties != null)
                            {
                                doc.PackageProperties.Creator = newCreator;
                                doc.PackageProperties.LastModifiedBy = newCreator;
                            }
                        }
                        break;

                    case ".xlsx":
                        using (var spreadsheet = SpreadsheetDocument.Open(filePath, true))
                        {
                            if (spreadsheet.PackageProperties != null)
                            {
                                spreadsheet.PackageProperties.Creator = newCreator;
                                spreadsheet.PackageProperties.LastModifiedBy = newCreator;
                            }
                        }
                        break;
                }

                UpdateCreatorInDatabase(_metadata.FileID, newCreator);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось обновить информацию о создателе: {ex.Message}\n\nПроверьте права доступа к файлу.",
                                "Ошибка обновления", MessageBoxButton.OK, MessageBoxImage.Error); ;
            }
        }

        private void UpdateImageMetadata(string filePath, string creator, string cameraModel)
        {
            try
            {
                string tempFile = Path.GetTempFileName();

                try
                {
                    File.Copy(filePath, tempFile, true);

                    using (var image = System.Drawing.Image.FromFile(tempFile))
                    {
                        var propItems = image.PropertyItems.ToList();

                        if (!string.IsNullOrEmpty(creator))
                        {
                            SetPropertyItem(propItems, 0x013B, "Artist", creator);
                            SetPropertyItem(propItems, 0x9C9D, "Creator", creator);
                            SetPropertyItem(propItems, 0x9C9C, "Authors", creator);
                        }

                        if (!string.IsNullOrEmpty(cameraModel))
                        {
                            SetPropertyItem(propItems, 0x0110, "Model", cameraModel);
                            SetPropertyItem(propItems, 0x010F, "Make", cameraModel);
                        }

                        using (var newImage = new Bitmap(image))
                        {
                            foreach (var propItem in propItems)
                            {
                                newImage.SetPropertyItem(propItem);
                            }

                            ImageFormat format = image.RawFormat;
                            ImageCodecInfo encoder = GetEncoder(format) ?? GetEncoder(ImageFormat.Jpeg);

                            var encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 100L);

                            newImage.Save(filePath, encoder, encoderParams);
                        }
                    }
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обновлении метаданных изображения: {ex.Message}", ex);
            }
        }

        private void SetPropertyItem(List<PropertyItem> propItems, int id, string name, string value)
        {
            var existingItem = propItems.FirstOrDefault(p => p.Id == id);

            if (existingItem != null)
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(value + "\0");
                existingItem.Value = bytes;
                existingItem.Len = bytes.Length;
            }
            else
            {
                // Создание нового свойства
                var newItem = (PropertyItem)System.Runtime.Serialization.FormatterServices
                    .GetUninitializedObject(typeof(PropertyItem));

                newItem.Id = id;
                newItem.Type = 2; // ASCII
                newItem.Value = System.Text.Encoding.ASCII.GetBytes(value + "\0");
                newItem.Len = newItem.Value.Length;

                propItems.Add(newItem);
            }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void UpdateCreatorInDatabase(int fileId, string newCreator)
        {
            try
            {
                var fileToUpdate = entities.Files.FirstOrDefault(f => f.FileID == fileId);
                if (fileToUpdate != null)
                {
                    var document = entities.Documents.FirstOrDefault(d => d.FileID == fileId);
                    if (document != null)
                    {
                        document.Creator = newCreator;
                        entities.SaveChanges();
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось обновить информацию о создателе в базе данных.\nДанные в файле были изменены, но информация в системе может быть неактуальной.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ChangePreview_Click(object sender, RoutedEventArgs e)
        {
            var audioExtensions = new[] { ".mp3", ".wav", ".aac" };
            string extension = Path.GetExtension(_metadata.FilePath)?.ToLower();

            if (!audioExtensions.Contains(extension))
            {
                MessageBox.Show("Изменение превью доступно только для аудиофайлов",
                              "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.jpg; *.jpeg; *.png)|*.jpg; *.jpeg; *.png",
                Title = "Выберите новое превью"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Создание временной копии изображения
                    string tempFile = Path.GetTempFileName() + Path.GetExtension(openFileDialog.FileName);
                    File.Copy(openFileDialog.FileName, tempFile, true);

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(tempFile);
                    bitmap.EndInit();

                    // Элемент с превью в списке метаданных
                    var metadataList = (List<MetadataAttribute>)MetadataDataGrid.ItemsSource;
                    var previewAttribute = metadataList.FirstOrDefault(attr => attr.AttributeName == "Превью");

                    if (previewAttribute != null)
                    {
                        previewAttribute.Image = bitmap;
                    }
                    else
                    {
                        metadataList.Add(new MetadataAttribute
                        {
                            AttributeName = "Превью",
                            AttributeValue = "",
                            Image = bitmap
                        });
                    }

                    MetadataDataGrid.ItemsSource = null;
                    MetadataDataGrid.ItemsSource = metadataList;

                    _metadata.PreviewImage = tempFile;

                    MessageBox.Show("Превью успешно изменено. Не забудьте сохранить изменения",
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при изменении превью: {ex.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class MetadataAttribute
    {
        public string AttributeName { get; set; }
        public string AttributeValue { get; set; }
        public ImageSource Image { get; set; }
        public bool IsEditable { get; set; }
    }
}