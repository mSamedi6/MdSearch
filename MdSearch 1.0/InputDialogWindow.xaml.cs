using System;
using System.Collections.Generic;
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

namespace MdSearch_1._0
{
    public partial class InputDialogWindow : Window
    {
        public string ResponseText { get; private set; }

        public InputDialogWindow(string title, string prompt, string initialText = "")
        {
            InitializeComponent();
            this.Title = title;
            PromptTextBlock.Text = prompt;
            ResponseTextBox.Text = initialText;
            ResponseTextBox.Focus();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = ResponseTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
