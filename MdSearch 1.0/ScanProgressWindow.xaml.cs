using System.Threading;
using System.Windows;

namespace MdSearch_1._0
{
    public partial class ScanProgressWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;

        public ScanProgressWindow()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void UpdateProgress(int progress, string speed, string loadedSize, string timeRemaining)
        {
            ProgressBar.Value = progress;
            ProgressTextBlock.Text = $"{progress}%";
            SpeedTextBlock.Text = speed;
            LoadedSizeTextBlock.Text = loadedSize;
            TimeRemainingTextBlock.Text = timeRemaining;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();
            this.Close();
        }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    }
}