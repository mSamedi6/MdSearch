using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace MdSearch_1._0
{
    public partial class HistoryWindow : Window
    {
        private List<dynamic> _allHistoryItems;
        private bool _isSortedNewestFirst = true;
        private int _userRoleId;
        private Entities entities = new Entities();
        public HistoryWindow(int userRoleId)
        {
            InitializeComponent();
            LoadFullHistory();
            _userRoleId = userRoleId;
            InitializeUserInterface();
        }

        private void InitializeUserInterface()
        {
            if (_userRoleId != 2)
            {
                DeleteAllButton.Visibility = Visibility.Collapsed;
                DeleteSelect.Visibility = Visibility.Collapsed;

                var userId = entities.Users
                    .Where(u => u.RoleId == _userRoleId)
                    .Select(u => u.Id)
                    .FirstOrDefault();

                if (userId != 0)
                {
                    var permission = entities.UserPermissions
                        .FirstOrDefault(p => p.UserID == userId);

                    bool canClearHistory = permission?.CanClearHistory == true;

                    if (canClearHistory)
                    {
                        DeleteAllButton.Visibility = Visibility.Visible;
                        DeleteSelect.Visibility = Visibility.Visible;
                    }
                }
            }
            else
            {
                DeleteAllButton.Visibility = Visibility.Visible;
                DeleteSelect.Visibility = Visibility.Visible;
            }
        }

        private void LoadFullHistory()
        {
            using (var db = new Entities())
            {
                var historyFromDb = db.ChangesHistory
                    .Include(h => h.Files)
                    .OrderByDescending(h => h.ChangeDate)
                    .Select(h => new
                    {
                        h.ChangeID,
                        h.ChangeDate,
                        h.Files.FileName,
                        h.Files.FilePath,
                        FileType = h.Files.FileFormat.ToUpper(),
                        h.ChangeType,
                        h.OldValue,
                        h.NewValue,
                        h.ChangeDescription
                    })
                    .ToList();

                _allHistoryItems = historyFromDb.Select(h => new
                {
                    Id = h.ChangeID,
                    h.ChangeDate,
                    h.FileName,
                    h.FilePath,
                    h.FileType,
                    TranslatedChangeType = FieldTranslationHelper.Translate(h.ChangeType),
                    h.OldValue,
                    h.NewValue,
                    h.ChangeDescription
                }).ToList<dynamic>();

                TotalHistoryCountTextBlock.Text = $"Всего записей: {_allHistoryItems.Count}";

                ApplyFilters();
                ApplySorting();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HistoryDataGrid.UnselectAll();

                TypeFilterComboBox.SelectedIndex = 0;
                SearchTextBox.Text = "Введите текст...";
                SearchTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9E9C9C"));

                Keyboard.ClearFocus();
            }
        }

        private void ApplySorting()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(HistoryDataGrid.ItemsSource);

            if (_isSortedNewestFirst)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("ChangeDate", ListSortDirection.Descending));
                SortButton.ToolTip = "Сортировка: новые → старые";
                SortIconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/sort_icon_dp.png"));
            }
            else
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription("ChangeDate", ListSortDirection.Ascending));
                SortButton.ToolTip = "Сортировка: старые → новые";
                SortIconImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/sort_icon_up.png"));
            }

            view.Refresh();
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            _isSortedNewestFirst = !_isSortedNewestFirst;
            ApplySorting();
        }

        private void ApplyFilters()
        {
            if (_allHistoryItems == null) return;

            var filteredItems = _allHistoryItems.AsEnumerable();

            var selectedType = (TypeFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (selectedType != null && selectedType != "Все типы")
            {
                var fileExtensions = GetFileExtensionsForType(selectedType);
                filteredItems = filteredItems.Where(item => fileExtensions.Any(ext => item.FileType.Contains(ext)));
            }

            var searchText = SearchTextBox.Text?.ToLower();
            if (!string.IsNullOrWhiteSpace(searchText) && searchText != "введите текст...")
            {
                filteredItems = filteredItems.Where(item =>
                    (item.ChangeDate.ToString()?.ToLower().Contains(searchText) ?? false) ||
                    (item.FileName?.ToLower().Contains(searchText) ?? false) ||
                    (item.FileType?.ToLower().Contains(searchText) ?? false) ||
                    (item.TranslatedChangeType?.ToLower().Contains(searchText) ?? false) ||
                    (item.OldValue?.ToLower().Contains(searchText) ?? false) ||
                    (item.NewValue?.ToLower().Contains(searchText) ?? false) ||
                    (item.ChangeDescription?.ToLower().Contains(searchText) ?? false));
            }

            var result = filteredItems.ToList();    
            HistoryDataGrid.ItemsSource = result;
            FilteredHistoryCountTextBlock.Text = $"Найдено записей: {result.Count}";
            ApplySorting();
        }

        private List<string> GetFileExtensionsForType(string typeName)
        {
            if (typeName == "Текстовые (.txt)")
                return new List<string> { ".TXT" };
            else if (typeName == "Документы (.doc, .docx)")
                return new List<string> { ".DOC", ".DOCX" };
            else if (typeName == "Таблицы (.xls, .xlsx)")
                return new List<string> { ".XLS", ".XLSX" };
            else if (typeName == "Изображения (.jpg, .jpeg, .png)")
                return new List<string> { ".JPG", ".JPEG", ".PNG" };
            else if (typeName == "Графика (.gif, .bmp)")
                return new List<string> { ".GIF", ".BMP" };
            else if (typeName == "Аудио (.mp3, .wav, .aac)")
                return new List<string> { ".MP3", ".WAV", ".AAC" };
            else if (typeName == "Видео (.mp4, .avi, .mkv, .mov, .wmv)")
                return new List<string> { ".MP4", ".AVI", ".MKV", ".MOV", ".WMV" };
            else
                return new List<string>();
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Введите текст...")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Введите текст...";
                SearchTextBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void HistoryDataGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement((DataGrid)sender, e.OriginalSource as DependencyObject) as DataGridRow;
            if (row != null)
            {
                if (!row.IsSelected)
                {
                    row.IsSelected = true;
                }
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = HistoryDataGrid.SelectedItems.Cast<dynamic>().ToList();
            if (selectedItems.Count == 0) return;

            foreach (var item in selectedItems)
            {
                try
                {
                    string filePath = item.FilePath;
                    if (System.IO.File.Exists(filePath))
                    {
                        string argument = "/select, \"" + filePath + "\"";
                        Process.Start("explorer.exe", argument);
                    }
                    else
                    {
                        MessageBox.Show($"Файл не найден: {filePath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии расположения файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = HistoryDataGrid.SelectedItems.Cast<dynamic>().ToList();
            if (selectedItems.Count == 0) return;

            var result = MessageBox.Show($"Вы уверены, что хотите удалить {selectedItems.Count} записей из истории?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var db = new Entities())
                    {
                        var idsToDelete = selectedItems.Select(i => (int)i.Id).ToList();
                        var itemsToDelete = db.ChangesHistory.Where(h => idsToDelete.Contains(h.ChangeID)).ToList();

                        foreach (var item in itemsToDelete)
                        {
                            db.ChangesHistory.Remove(item);
                        }

                        db.SaveChanges();
                        LoadFullHistory();
                        MessageBox.Show("Записи успешно удалены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении записей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить ВСЕ записи истории? Это действие нельзя отменить",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var db = new Entities())
                    {
                        db.Database.ExecuteSqlCommand("DELETE FROM ChangesHistory");
                        db.SaveChanges();

                        LoadFullHistory();
                        MessageBox.Show("Вся история успешно удалена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении истории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}