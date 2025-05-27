using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MdSearch_1._0
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = (Exception)args.ExceptionObject;
                MessageBox.Show($"Критическая ошибка: {exception.Message}\n\n{exception.StackTrace}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Ошибка в интерфейсе: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            var entities = new Entities();

            var setting = entities.Settings.FirstOrDefault();
            if (setting == null)
            {
                setting = new Settings { LoginRequired = true };
                entities.Settings.Add(setting);
                entities.SaveChanges();
            }

            if (setting.LoginRequired)
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
            }
            else
            {
                var mainWindow = new MainWindow(2);
                mainWindow.Show();
            }
        }
    }
}
