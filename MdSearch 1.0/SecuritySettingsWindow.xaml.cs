using System.Linq;
using System.Windows;

namespace MdSearch_1._0
{
    public partial class Users
    {
        public string DisplayText { get; set; }
    }

    public partial class SecuritySettingsWindow : Window
    {
        private Entities entities = new Entities();

        public SecuritySettingsWindow()
        {
            InitializeComponent();
            LoadUsers();
            LoadSettings();
        }

        private void LoadUsers()
        {
            // Загрузка только пользователей с ролью читатель
            var readers = entities.Users.Where(u => u.RoleId == 1).ToList();

            foreach (var user in readers)
            {
                user.DisplayText = $"{user.Login} (viewer)";
            }

            UserComboBox.ItemsSource = readers;
        }

        private void LoadSettings()
        {
            // Глобальная настройка входа
            var setting = entities.Settings.FirstOrDefault();
            if (setting == null)
            {
                var defaultSetting = new Settings { LoginRequired = true };
                entities.Settings.Add(defaultSetting);
                entities.SaveChanges();
                RequireAuthCheckBox.IsChecked = defaultSetting.LoginRequired;
            }
            else
            {
                RequireAuthCheckBox.IsChecked = setting.LoginRequired;
            }

            // Если пользователь выбран — загрузить его права
            if (UserComboBox.SelectedItem is Users selectedUser)
            {
                var permissions = entities.UserPermissions
                    .FirstOrDefault(p => p.UserID == selectedUser.Id);

                CanDeleteAllCB.IsChecked = permissions?.CanDeleteAll ?? false;
                CanClearHistoryCB.IsChecked = permissions?.CanClearHistory ?? false;
                CanEditMetadataCB.IsChecked = permissions?.CanEditMetadata ?? false;
            }
        }

        private void UserComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadSettings(); // Перезагрузка прав при смене пользователя
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var setting = entities.Settings.FirstOrDefault();
            if (setting == null)
            {
                setting = new Settings();
                entities.Settings.Add(setting);
            }

            setting.LoginRequired = RequireAuthCheckBox.IsChecked == true;

            if (UserComboBox.SelectedItem is Users selectedUser)
            {
                var permissions = entities.UserPermissions
                    .FirstOrDefault(p => p.UserID == selectedUser.Id);

                if (permissions == null)
                {
                    permissions = new UserPermissions { UserID = selectedUser.Id };
                    entities.UserPermissions.Add(permissions);
                }

                permissions.CanDeleteAll = CanDeleteAllCB.IsChecked == true;
                permissions.CanClearHistory = CanClearHistoryCB.IsChecked == true;
                permissions.CanEditMetadata = CanEditMetadataCB.IsChecked == true;
            }

            try
            {
                entities.SaveChanges();
                MessageBox.Show("Настройки и полномочия сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}