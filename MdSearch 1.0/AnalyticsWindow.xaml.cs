using System;
using System.Linq;
using System.Windows;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace MdSearch_1._0
{
    public partial class AnalyticsWindow : Window
    {
        private Entities entities = new Entities();

        public AnalyticsWindow()
        {
            InitializeComponent();
            LoadReports();

            SortComboBox.SelectionChanged += FilterChanged;
            TypeFilterComboBox.SelectionChanged += FilterChanged;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadReports();
        }

        private void LoadReports()
        {
            try
            {
                var reportsQuery = entities.Reports
                    .Include(r => r.Files)
                    .AsQueryable();

                var selectedType = (TypeFilterComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (selectedType != "Все отчеты")
                {
                    reportsQuery = reportsQuery.Where(r => r.ReportType == selectedType);
                }

                var selectedSort = (SortComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (selectedSort == "Сначала новые")
                {
                    reportsQuery = reportsQuery.OrderByDescending(r => r.CreationDate);
                }
                else
                {
                    reportsQuery = reportsQuery.OrderBy(r => r.CreationDate);
                }

                var reports = reportsQuery.ToList();

                foreach (var report in reports.Where(r => !File.Exists(r.ReportPath)))
                {
                    TryFindMovedReport(report);
                }

                ReportsListView.ItemsSource = reports;

                NoReportsText.Visibility = reports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                ReportsListView.Visibility = reports.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке отчетов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NoReportsText.Visibility = Visibility.Visible;
                ReportsListView.Visibility = Visibility.Collapsed;
            }
        }

        private bool TryFindMovedReport(Reports report)
        {
            if (File.Exists(report.ReportPath))
                return true;

            var searchLocations = new List<string>
            {
                Path.GetDirectoryName(report.ReportPath),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };

            string fileName = Path.GetFileName(report.ReportPath);

            foreach (var location in searchLocations.Distinct().Where(Directory.Exists))
            {
                try
                {
                    var foundFiles = Directory.EnumerateFiles(location, fileName, SearchOption.AllDirectories);
                    foreach (var foundPath in foundFiles)
                    {
                        var fileInfo = new FileInfo(foundPath);
                        if (fileInfo.Length > 0)
                        {
                            report.ReportPath = foundPath;
                            entities.SaveChanges();
                            return true;
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
            }

            return false;
        }

        private void DeleteReport_Click(object sender, RoutedEventArgs e)
        {
            var selectedReports = ReportsListView.SelectedItems.Cast<Reports>().ToList();
            if (selectedReports.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один отчет для удаления", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить {selectedReports.Count} отчет(ов)?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var report in selectedReports)
                    {
                        entities.Reports.Remove(report);
                    }
                    entities.SaveChanges();
                    LoadReports();
                    MessageBox.Show("Отчеты успешно удалены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении отчетов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ReportsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem == null) return;

            var report = listBoxItem.DataContext as Reports;
            if (report == null) return;

            OpenReportFile(report);
        }

        private void OpenReportFile(Reports report)
        {
            try
            {
                if (!File.Exists(report.ReportPath) && !TryFindMovedReport(report))
                {
                    var result = MessageBox.Show(
                        "Файл отчета не найден. Хотите указать путь вручную?",
                        "Файл не найден",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        var openFileDialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Укажите путь к файлу отчета",
                            Filter = "Все файлы (*.*)|*.*",
                            FileName = Path.GetFileName(report.ReportPath)
                        };

                        if (openFileDialog.ShowDialog() == true)
                        {
                            report.ReportPath = openFileDialog.FileName;
                            entities.SaveChanges();
                        }
                        else
                        {
                            MessageBox.Show("Файл отчета не был выбран. Операция отменена.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Файл отчета не был выбран. Операция отменена.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = report.ReportPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии отчёта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenReportLocation_Click(object sender, RoutedEventArgs e)
        {
            var selectedReports = ReportsListView.SelectedItems.Cast<Reports>().ToList();
            if (selectedReports.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы один отчет", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var report in selectedReports)
            {
                try
                {
                    if (!File.Exists(report.ReportPath) && !TryFindMovedReport(report))
                    {
                        var result = MessageBox.Show(
                            "Файл отчета не найден. Хотите указать путь вручную?",
                            "Файл не найден",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );

                        if (result == MessageBoxResult.Yes)
                        {
                            var openFileDialog = new Microsoft.Win32.OpenFileDialog
                            {
                                Title = "Укажите путь к файлу отчета",
                                Filter = "Все файлы (*.*)|*.*",
                                FileName = Path.GetFileName(report.ReportPath)
                            };

                            if (openFileDialog.ShowDialog() == true)
                            {
                                report.ReportPath = openFileDialog.FileName;
                                entities.SaveChanges();
                            }
                            else
                            {
                                MessageBox.Show("Файл отчета не был выбран. Операция отменена.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                                continue;
                            }
                        }
                        else
                        {
                            MessageBox.Show("Файл отчета не был выбран. Операция отменена.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                            continue;
                        }
                    }
                    string argument = "/select, \"" + report.ReportPath + "\"";
                    Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии директории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ReportsListView_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem != null)
            {
                ReportsListView.SelectedItem = listBoxItem.DataContext;
                e.Handled = true;
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}