using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace MdSearch_1._0
{
    public partial class MessageBoxExport : Window
    {
        private readonly string _filePath;

        public MessageBoxExport(string message, string title, string filePath)
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;
            _filePath = filePath;

            if (!File.Exists(filePath))
            {
                OpenButton.Visibility = Visibility.Collapsed;
                OpenFolderButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _filePath,
                    UseShellExecute = true
                });
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folderPath = Path.GetDirectoryName(_filePath);
                Process.Start("explorer.exe", folderPath);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть папку: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
