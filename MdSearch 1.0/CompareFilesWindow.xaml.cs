using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MdSearch_1._0
{
    public partial class CompareFilesWindow : Window
    {
        private bool isSortedByName = false;
        private MetadataModel originalMetadata1;
        private MetadataModel originalMetadata2;
        private List<ComparisonResult> allComparisonResults;

        public CompareFilesWindow()
        {
            InitializeComponent();
            MetadataListView1.SelectionMode = SelectionMode.Single;
            MetadataListView2.SelectionMode = SelectionMode.Single;
        }

        public void SetMetadata(MetadataModel metadata1, MetadataModel metadata2)
        {
            originalMetadata1 = metadata1;
            originalMetadata2 = metadata2;
            isSortedByName = false;

            var metadataList1 = new List<MetadataAttribute>();
            var metadataList2 = new List<MetadataAttribute>();

            AddCommonAttributes(metadata1, metadataList1);
            AddCommonAttributes(metadata2, metadataList2);
            AddFileSpecificAttributes(metadata1, metadataList1);
            AddFileSpecificAttributes(metadata2, metadataList2);

            MetadataListView1.ItemsSource = metadataList1;
            MetadataListView2.ItemsSource = metadataList2;

            CalculateComparisonStats(metadataList1, metadataList2);
        }

        private void CalculateComparisonStats(List<MetadataAttribute> list1, List<MetadataAttribute> list2)
        {
            int totalAttributes = 0;
            int matchingAttributes = 0;
            int differentAttributes = 0;
            int uniqueInFirst = 0;
            int uniqueInSecond = 0;

            var allAttributeNames = list1.Select(a => a.AttributeName)
                                          .Union(list2.Select(a => a.AttributeName))
                                          .Distinct()
                                          .ToList();

            var comparisonResults = new List<ComparisonResult>();

            foreach (var name in allAttributeNames)
            {
                var attr1 = list1.FirstOrDefault(a => a.AttributeName == name);
                var attr2 = list2.FirstOrDefault(a => a.AttributeName == name);

                if (attr1 != null && attr2 != null)
                {
                    totalAttributes += 2;

                    if (attr1.AttributeValue == attr2.AttributeValue)
                    {
                        matchingAttributes += 2;
                        comparisonResults.Add(new ComparisonResult
                        {
                            AttributeName = name,
                            Status = "Совпадает",
                            Color = Brushes.Green
                        });
                    }
                    else
                    {
                        differentAttributes += 2;
                        uniqueInFirst++;
                        uniqueInSecond++;
                        comparisonResults.Add(new ComparisonResult
                        {
                            AttributeName = name,
                            Status = "Различается",
                            Color = Brushes.Red
                        });
                    }
                }
                else if (attr1 != null)
                {
                    totalAttributes++;
                    uniqueInFirst++;
                    comparisonResults.Add(new ComparisonResult
                    {
                        AttributeName = name,
                        Status = "Только в первом файле",
                        Color = Brushes.Blue
                    });
                }
                else if (attr2 != null)
                {
                    totalAttributes++;
                    uniqueInSecond++;
                    comparisonResults.Add(new ComparisonResult
                    {
                        AttributeName = name,
                        Status = "Только во втором файле",
                        Color = Brushes.Purple
                    });
                }
            }

            allComparisonResults = comparisonResults;
            ComparisonStatsListView.ItemsSource = allComparisonResults;

            TotalAttributesText.Text = $"Всего атрибутов: {totalAttributes}";
            MatchingAttributesText.Text = $"Совпадающих: {matchingAttributes}";
            DifferentAttributesText.Text = $"Различающихся: {differentAttributes}";
            UniqueInFirstText.Text = $"Уникальных в первом файле: {uniqueInFirst}";
            UniqueInSecondText.Text = $"Уникальных во втором файле: {uniqueInSecond}";

            double similarityPercentage = totalAttributes > 0
                ? (double)matchingAttributes / totalAttributes * 100
                : 0;
            SimilarityPercentageText.Text = $"Процент совпадения: {similarityPercentage:F2}%";
        }

        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            if (!isSortedByName)
            {
                if (MetadataListView1.ItemsSource is List<MetadataAttribute> list1 &&
                    MetadataListView2.ItemsSource is List<MetadataAttribute> list2)
                {
                    list1 = list1.OrderBy(a => a.AttributeName).ToList();
                    list2 = list2.OrderBy(a => a.AttributeName).ToList();

                    MetadataListView1.ItemsSource = list1;
                    MetadataListView2.ItemsSource = list2;

                    CalculateComparisonStats(list1, list2);
                    isSortedByName = true;
                }
            }
            else
            {
                SetMetadata(originalMetadata1, originalMetadata2);
            }
        }

        private void SortByStatus_Click(object sender, RoutedEventArgs e)
        {
            if (allComparisonResults != null)
            {
                var orderedResults = allComparisonResults.OrderBy(r => r.Status).ToList();
                ComparisonStatsListView.ItemsSource = orderedResults;
            }
        }
        private void ShowOnlyDifferences_Click(object sender, RoutedEventArgs e)
        {
            if (allComparisonResults != null)
            {
                var filteredResults = allComparisonResults.Where(r => r.Status != "Совпадает").ToList();
                ComparisonStatsListView.ItemsSource = filteredResults;
            }
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            if (allComparisonResults != null)
            {
                ComparisonStatsListView.ItemsSource = allComparisonResults;
            }
        }

        private void AddCommonAttributes(MetadataModel metadata, List<MetadataAttribute> metadataList)
        {
            metadataList.AddRange(new[]
            {
                new MetadataAttribute {
                    AttributeName = "Имя файла",
                    AttributeValue = Path.GetFileNameWithoutExtension(metadata.FileName),
                    IsEditable = true
                },
                new MetadataAttribute { AttributeName = "Формат файла", AttributeValue = metadata.FileFormat },
                new MetadataAttribute { AttributeName = "Владелец", AttributeValue = metadata.UserName },
                new MetadataAttribute { AttributeName = "Путь к файлу", AttributeValue = metadata.FilePath },
                new MetadataAttribute { AttributeName = "Размер файла", AttributeValue = FileUtils.FormatFileSize(metadata.FileSize)},
                new MetadataAttribute { AttributeName = "Дата загрузки", AttributeValue = metadata.UploadDate.ToString("dd.MM.yyyy HH:mm") },
                new MetadataAttribute { AttributeName = "Дата создания", AttributeValue = metadata.CreationDate.ToString("dd.MM.yyyy HH:mm:ss") },
                new MetadataAttribute { AttributeName = "Дата изменения", AttributeValue = metadata.ModificationDate.ToString("dd.MM.yyyy HH:mm:ss") }
            });

            if (!string.IsNullOrEmpty(metadata.PreviewImage) && File.Exists(metadata.PreviewImage))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(metadata.PreviewImage);
                bitmap.EndInit();
                metadataList.Add(new MetadataAttribute { AttributeName = "Превью", AttributeValue = "", Image = bitmap });
            }
        }

        private void AddFileSpecificAttributes(MetadataModel metadata, List<MetadataAttribute> metadataList)
        {
            switch (metadata.FileFormat.ToLower())
            {
                case ".txt":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Язык документа", AttributeValue = metadata.Language ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Количество строк", AttributeValue = metadata.LineCount },
                        new MetadataAttribute { AttributeName = "Количество символов (с пробелами)", AttributeValue = metadata.SymbolCountWithSpaces },
                        new MetadataAttribute { AttributeName = "Количество символов (без пробелов)", AttributeValue = metadata.SymbolCountWithoutSpaces },
                        new MetadataAttribute { AttributeName = "Кодировка", AttributeValue = metadata.Encoding }
                    });
                    break;

                case ".doc":
                case ".docx":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator },
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
                        new MetadataAttribute { AttributeName = "Версия документа", AttributeValue = metadata.Version ?? "Не определена" }
                    });
                    break;

                case ".xlsx":
                case ".xls":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator },
                        new MetadataAttribute { AttributeName = "Количество листов", AttributeValue = metadata.PageCount },
                        new MetadataAttribute { AttributeName = "Количество строк", AttributeValue = metadata.LineCount },
                        new MetadataAttribute { AttributeName = "Количество таблиц", AttributeValue = metadata.TableCount.ToString() },
                        new MetadataAttribute { AttributeName = "Количество изображений", AttributeValue = metadata.ImageCount.ToString() },
                        new MetadataAttribute { AttributeName = "Количество формул", AttributeValue = metadata.FormulaCount.ToString() },
                        new MetadataAttribute { AttributeName = "Кодировка", AttributeValue = metadata.Encoding },
                        new MetadataAttribute { AttributeName = "Версия документа", AttributeValue = metadata.Version ?? "Не определена" }
                    });
                    break;

                case ".jpg":
                case ".jpeg":
                case ".png":
                    metadataList.AddRange(new[]
                    {
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator },
                        new MetadataAttribute { AttributeName = "Разрешение", AttributeValue = metadata.Resolution },
                        new MetadataAttribute { AttributeName = "Глубина цвета", AttributeValue = $"{metadata.ColorDepth} бит" },
                        new MetadataAttribute { AttributeName = "Ориентация", AttributeValue = metadata.Orientation ?? "Не определена" },
                        new MetadataAttribute { AttributeName = "Уровень сжатия", AttributeValue = metadata.CompressionLevel ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Цветовой профиль", AttributeValue = metadata.ColorProfile ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Модель камеры", AttributeValue = metadata.CameraModel ?? "Не определена" },
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
                        new MetadataAttribute { AttributeName = "Создатель", AttributeValue = metadata.Creator },
                        new MetadataAttribute { AttributeName = "Длительность", AttributeValue = $"{metadata.Duration} сек" },
                        new MetadataAttribute { AttributeName = "Частота дискретизации", AttributeValue = $"{metadata.SampleRate} Гц" },
                        new MetadataAttribute { AttributeName = "Количество каналов", AttributeValue = metadata.ChannelCount },
                        new MetadataAttribute { AttributeName = "Битрейт", AttributeValue = $"{metadata.AudioBitrate} кбит/с" },
                        new MetadataAttribute { AttributeName = "Название трека", AttributeValue = metadata.TrackTitle ?? "Не определено" },
                        new MetadataAttribute { AttributeName = "Исполнитель", AttributeValue = metadata.Artist ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Альбом", AttributeValue = metadata.Album ?? "Не определен" },
                        new MetadataAttribute { AttributeName = "Год выпуска", AttributeValue = metadata.ReleaseYear > 0 ? metadata.ReleaseYear.ToString() : "Не определен" },
                        new MetadataAttribute { AttributeName = "Жанр", AttributeValue = metadata.Genre ?? "Не определен" }
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
                    new MetadataAttribute { AttributeName = "Год выпуска", AttributeValue = metadata.ReleaseYear > 0 ? metadata.ReleaseYear.ToString() : "Не определен" },
                    new MetadataAttribute { AttributeName = "Количество аудиотреков", AttributeValue = metadata.AudioTrack.ToString() },
                    new MetadataAttribute { AttributeName = "Жанр", AttributeValue = metadata.Genre ?? "Неизвестно", IsEditable=true },
                    new MetadataAttribute { AttributeName = "Описание", AttributeValue = metadata.Description ?? "Нет описания" },
                    });
                    break;
            }
        }
        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MetadataListView1.SelectedItem is MetadataAttribute selectedAttribute)
            {
                var correspondingItem = MetadataListView2.Items.Cast<MetadataAttribute>()
                    .FirstOrDefault(item => item.AttributeName == selectedAttribute.AttributeName);

                if (correspondingItem != null)
                {
                    MetadataListView2.SelectionChanged -= ListView2_SelectionChanged;

                    MetadataListView2.SelectedItem = correspondingItem;
                    MetadataListView2.ScrollIntoView(correspondingItem);

                    MetadataListView2.SelectionChanged += ListView2_SelectionChanged;
                }
                else
                {
                    MetadataListView2.SelectedItem = null;
                }
            }
        }

        private void ListView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MetadataListView2.SelectedItem is MetadataAttribute selectedAttribute)
            {
                var correspondingItem = MetadataListView1.Items.Cast<MetadataAttribute>()
                    .FirstOrDefault(item => item.AttributeName == selectedAttribute.AttributeName);

                if (correspondingItem != null)
                {
                    MetadataListView1.SelectionChanged -= ListView1_SelectionChanged;

                    MetadataListView1.SelectedItem = correspondingItem;
                    MetadataListView1.ScrollIntoView(correspondingItem);

                    MetadataListView1.SelectionChanged += ListView1_SelectionChanged;
                }
                else
                {
                    MetadataListView1.SelectedItem = null;
                }
            }
        }
    }

    public class ComparisonResult
    {
        public string AttributeName { get; set; }
        public string Status { get; set; }
        public Brush Color { get; set; }
    }
}