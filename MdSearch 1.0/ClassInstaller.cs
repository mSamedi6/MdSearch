using System;
using System.Collections;
using System.Configuration.Install;
using Microsoft.Win32;
using System.IO;
using System.ComponentModel;

[RunInstaller(true)]
public class ClassInstaller : Installer
{
    public override void Install(IDictionary stateSaver)
    {
        base.Install(stateSaver);

        // Параметр на установку
        string installType = Context.Parameters["installtype"];

        if (!string.IsNullOrEmpty(installType))
        {
            // Создание типа установки в реестре
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software/MdSearch"))
            {
                key.SetValue("InstallType", installType, RegistryValueKind.String);
            }

            // Либо сохраненние в файл конфигурации
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string filePath = Path.Combine(appDataPath, "MdSearch", "installtype.cfg");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, installType);
        }
    }
}