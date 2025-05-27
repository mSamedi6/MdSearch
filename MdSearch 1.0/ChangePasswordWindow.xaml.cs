using System.Windows;

namespace MdSearch_1._0
{
    public partial class ChangePasswordWindow : Window
    {
        public string NewPassword { get; private set; }

        public ChangePasswordWindow()
        {
            InitializeComponent();
            NewPasswordBox.Focus();
        }

        private void GeneratePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            NewPasswordBox.Text = PasswordGenerator.GenerateSecurePassword();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string newPassword = NewPasswordBox.Text.Trim();

            if (string.IsNullOrEmpty(newPassword))
            {
                MessageBox.Show("Пароль не может быть пустым", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (newPassword.Length < 6)
            {
                MessageBox.Show("Пароль должен содержать не менее 6 символов", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            NewPassword = newPassword;
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