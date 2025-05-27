using System;
using System.Linq;
using System.Windows;

namespace MdSearch_1._0
{
    public partial class FileSearchResultWindow : Window
    {
        public event Action<bool> ManualSearchClicked;
        public event Action OpenFolderClicked;
        public event Action DeleteClicked;

        public bool FilesWereFound { get; set; } = false;
        public bool WasDeleted { get; private set; } = false;

        public FileSearchResultWindow()
        {
            InitializeComponent();
            this.Closing += FileSearchResultWindow_Closing;
            DeleteBtn.Visibility = Visibility.Collapsed;
        }

        private void FileSearchResultWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (FilesWereFound || WasDeleted)
            {
                var result = MessageBox.Show("Закрыть окно и обновить список файлов?",
                                          "Подтверждение",
                                          MessageBoxButton.OKCancel,
                                          MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
            else
            {
                var result = MessageBox.Show("Если вы закроете окно, отсутствующие файлы будут удалены. Продолжить?",
                                          "Подтверждение",
                                          MessageBoxButton.OKCancel,
                                          MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        public void ShowDeleteButton(bool show)
        {
            DeleteBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ManualSearchBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            OpenFolderBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ManualSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            ManualSearchClicked?.Invoke(true);
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderClicked?.Invoke();
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы действительно хотите удалить все ненайденные файлы из базы данных?",
                                      "Подтверждение удаления",
                                      MessageBoxButton.YesNo,
                                      MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    WasDeleted = true;
                    DeleteClicked?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении файлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void SetResults(int foundCount, string renamedFiles, string movedFiles, string missingFiles)
        {
            if (foundCount > 0)
            {
                AutoFoundSection.Text = $"Автоматически найдено и обновлено файлов - {foundCount}: ";
                AutoFoundSection.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(renamedFiles))
            {
                RenamedSection.Visibility = Visibility.Visible;
                RenamedContent.Text = renamedFiles;
                RenamedContent.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(movedFiles))
            {
                MovedSection.Visibility = Visibility.Visible;
                MovedContent.Text = movedFiles;
                MovedContent.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrEmpty(missingFiles))
            {
                MissingSection.Text = $"Не найдены ({missingFiles.Split('\n').Count(s => !string.IsNullOrWhiteSpace(s))}):";
                MissingSection.Visibility = Visibility.Visible;
                MissingContent.Text = missingFiles;
                MissingContent.Visibility = Visibility.Visible;
            }
        }
    }
}