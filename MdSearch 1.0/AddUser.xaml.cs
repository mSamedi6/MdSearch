    using System;
    using System.Linq;
    using System.Windows;

    namespace MdSearch_1._0
    {
        public partial class AddUserAdmin : Window
        {
            public string Login { get; private set; }
            public string Email { get; private set; }
            public string Password { get; private set; }
            public int RoleId { get; private set; }

            private Entities entities = new Entities();

            public AddUserAdmin()
            {
                InitializeComponent();

                RoleComboBox.ItemsSource = entities.Roles.ToList();
                RoleComboBox.SelectedIndex = 0;
            }

            private void GeneratePasswordButton_Click(object sender, RoutedEventArgs e)
            {
                string generatedPassword = PasswordGenerator.GenerateSecurePassword();
                PasswordBox.Text = generatedPassword;
            }

            private void OkButton_Click(object sender, RoutedEventArgs e)
            {
                Login = LoginTextBox.Text.Trim();
                Email = EmailTextBox.Text.Trim();
                Password = PasswordBox.Text;
                RoleId = (int)RoleComboBox.SelectedValue;

                if (string.IsNullOrEmpty(Login))
                {
                    MessageBox.Show("Логин не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!string.IsNullOrEmpty(Email))
                {
                    try
                    {
                        var addr = new System.Net.Mail.MailAddress(Email);
                        if (addr.Address != Email)
                        {
                            throw new FormatException();
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Введите корректный email адрес", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (entities.Users.Any(u => u.Login == Login))
                {
                    MessageBox.Show("Пользователь с таким логином уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!string.IsNullOrEmpty(Email) && entities.Users.Any(u => u.Email == Email))
                {
                    MessageBox.Show("Пользователь с таким email уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrEmpty(Password) || Password.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать не менее 6 символов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

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