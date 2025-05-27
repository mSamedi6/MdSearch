using System.Windows;
using System.Collections.Generic;

namespace MdSearch_1._0
{
    public partial class FolderDeletionDialog : Window
    {
        public bool KeepFiles { get; private set; } = true;
        public List<string> FilesInFolder { get; private set; }

        public FolderDeletionDialog(string folderName, List<string> filesInFolder)
        {
            InitializeComponent();

            FilesInFolder = filesInFolder;

            DataContext = new
            {
                FolderName = folderName,
                FilesInFolder = filesInFolder
            };

            KeepFilesCheckBox.IsChecked = true;

            NoFilesText.Visibility = filesInFolder.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            KeepFiles = KeepFilesCheckBox.IsChecked ?? true;
            DialogResult = true;
            Close();
        }
    }
}