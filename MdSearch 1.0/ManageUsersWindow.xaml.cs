using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MdSearch_1._0
{
    public partial class ManageUsersWindow : Window
    {
        private Entities entities = new Entities();
        public List<Roles> Roles { get; set; }

        public ManageUsersWindow()
        {
            InitializeComponent();
            LoadRoles();
            LoadUsers();
            DataContext = this;
            this.Closing += ManageUsersWindow_Closing;
        }

        private void LoadRoles()
        {
            Roles = entities.Roles.ToList();
        }

        private void LoadUsers()
        {
            UsersDataGrid.ItemsSource = entities.Users.ToList();
        }
        private bool ValidateAdminCount()
        {
            var currentAdmins = entities.Users
                .Local
                .Where(u => u.RoleId == 2 && !entities.Entry(u).State.HasFlag(EntityState.Deleted))
                .Count();

            if (currentAdmins < 1)
            {
                MessageBox.Show("В системе должен оставаться хотя бы один администратор!",
                               "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void UsersDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() == "Роль")
            {
                var selectedUser = (Users)e.Row.Item;
                var comboBox = e.EditingElement as ComboBox;
                var selectedRole = comboBox.SelectedItem as Roles;

                if (selectedUser != null && selectedRole != null && selectedUser.RoleId != selectedRole.Id)
                {
                    if (selectedUser.RoleId == 2)
                    {
                        int remainingAdmins = entities.Users.Count(u => u.RoleId == 2 && u.Id != selectedUser.Id);
                        if (remainingAdmins == 0)
                        {
                            MessageBox.Show("Нельзя изменить роль последнего администратора! Создайте другого администратора перед изменением роли этого",
                                          "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            e.Cancel = true;
                            return;
                        }
                    }

                    selectedUser.RoleId = selectedRole.Id;
                    MessageBox.Show("Роль пользователя изменена. Не забудьте сохранить изменения перед закрытием окна!",
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (e.Column.Header.ToString() == "Логин")
            {
                var selectedUser = (Users)e.Row.Item;
                var textBox = e.EditingElement as TextBox;
                string newLogin = textBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(newLogin))
                {
                    MessageBox.Show("Логин не может быть пустым", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    textBox.Text = selectedUser.Login;
                    e.Cancel = true;
                    return;
                }

                var existingUser = entities.Users.FirstOrDefault(u => u.Login == newLogin && u.Id != selectedUser.Id);
                if (existingUser != null)
                {
                    MessageBox.Show("Пользователь с таким логином уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    textBox.Text = selectedUser.Login;
                    e.Cancel = true;
                    return;
                }

                if (selectedUser != null && newLogin != selectedUser.Login)
                {
                    selectedUser.Login = newLogin;
                    MessageBox.Show("Логин пользователя изменён. Не забудьте сохранить изменения перед закрытием окна!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (e.Column.Header.ToString() == "Email")
            {
                var selectedUser = (Users)e.Row.Item;
                var textBox = e.EditingElement as TextBox;
                string newEmail = textBox.Text.Trim();

                if (!string.IsNullOrEmpty(newEmail))
                {
                    try
                    {
                        var addr = new System.Net.Mail.MailAddress(newEmail);
                        if (addr.Address != newEmail)
                        {
                            throw new FormatException();
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Введите корректный email адрес", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        textBox.Text = selectedUser.Email;
                        e.Cancel = true;
                        return;
                    }

                    var existingUser = entities.Users.FirstOrDefault(u => u.Email == newEmail && u.Id != selectedUser.Id);
                    if (existingUser != null)
                    {
                        MessageBox.Show("Пользователь с таким email уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        textBox.Text = selectedUser.Email;
                        e.Cancel = true;
                        return;
                    }
                }

                if (selectedUser != null && newEmail != selectedUser.Email)
                {
                    selectedUser.Email = newEmail;
                    MessageBox.Show("Email пользователя изменён. Не забудьте сохранить изменения перед закрытием окна!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void UsersDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var user = (Users)e.Row.DataContext;
            if (user != null)
            {
                var comboBoxColumn = UsersDataGrid.Columns[1] as DataGridComboBoxColumn;
                if (comboBoxColumn != null)
                {
                    var comboBox = comboBoxColumn.GetCellContent(e.Row) as ComboBox;
                    if (comboBox != null)
                    {
                        comboBox.SelectedValue = user.RoleId;
                    }
                }
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var selectedUser = (Users)UsersDataGrid.SelectedItem;
            if (selectedUser == null)
            {
                MessageBox.Show("Выберите пользователя для изменения пароля", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var changePasswordWindow = new ChangePasswordWindow { Owner = this };
            if (changePasswordWindow.ShowDialog() == true)
            {
                string newPassword = changePasswordWindow.NewPassword;
                selectedUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                MessageBox.Show("Пароль изменён. Не забудьте сохранить изменения перед закрытием окна! Сохраните себе пароль!!!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ManageUsersWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (entities.ChangeTracker.HasChanges())
            {
                var result = MessageBox.Show("Сохранить изменения перед закрытием?", "Подтверждение", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (!ValidateAdminCount())
                        {
                            e.Cancel = true;
                            return;
                        }   
                        entities.SaveChanges();
                        MessageBox.Show("Изменения успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var addUserWindow = new AddUserAdmin { Owner = this };

            if (addUserWindow.ShowDialog() == true)
            {
                var newUser = new Users
                {
                    Login = addUserWindow.Login,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(addUserWindow.Password),
                    RoleId = addUserWindow.RoleId,
                    RegistrationDate = DateTime.Now,
                    Email = addUserWindow.Email
                };

                entities.Users.Add(newUser);
                UsersDataGrid.ItemsSource = entities.Users.Local.ToList();
                MessageBox.Show("Новый пользователь добавлен. Не забудьте сохранить изменения перед закрытием окна! Сохраните себе пароль!!!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var selectedUser = (Users)UsersDataGrid.SelectedItem;

            if (selectedUser == null)
            {
                MessageBox.Show("Выберите пользователя для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool isAdmin = selectedUser.RoleId == 2;

            if (isAdmin)
            {
                int remainingAdmins = entities.Users
                    .Local
                    .Where(u => u.RoleId == 2 &&
                               u.Id != selectedUser.Id &&
                               !entities.Entry(u).State.HasFlag(EntityState.Deleted))
                    .Count();

                if (remainingAdmins == 0)
                {
                    MessageBox.Show("Нельзя удалить последнего администратора! Создайте другого администратора перед удалением этого.",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var confirmResult = MessageBox.Show($"Вы уверены, что хотите удалить пользователя {selectedUser.Login}?",
                                              "Подтверждение удаления",
                                              MessageBoxButton.YesNo,
                                              MessageBoxImage.Question);

            if (confirmResult == MessageBoxResult.Yes)
            {
                try
                {
                    entities.Users.Remove(selectedUser);
                    UsersDataGrid.ItemsSource = entities.Users.Local.ToList();
                    MessageBox.Show("Пользователь удален. Не забудьте сохранить изменения перед закрытием окна!",
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении пользователя: {ex.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (entities.ChangeTracker.HasChanges())
            {
                try
                {
                    if (!ValidateAdminCount())
                        return;
                    entities.SaveChanges();
                    MessageBox.Show("Изменения успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Нет изменений для сохранения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}