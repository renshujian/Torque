using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Torque
{
    /// <summary>
    /// TestsViewer.xaml 的交互逻辑
    /// </summary>
    public partial class TestsViewer : Window
    {
        AppDbContext AppDbContext { get; }
        List<Test> Data { get; set; } = new();

        public TestsViewer(AppDbContext appDbContext)
        {
            InitializeComponent();
            AppDbContext = appDbContext;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ToolIdBox.Text = string.Empty;
            FromDatePicker.SelectedDate = null;
            ToDatePicker.SelectedDate = null;
        }

        private void QueryButton_Click(object sender, RoutedEventArgs e)
        {
            string toolId = ToolIdBox.Text;
            DateTime? from = FromDatePicker.SelectedDate;
            DateTime? to = ToDatePicker.SelectedDate;
            IEnumerable<Test> tests = AppDbContext.Tests;
            if (toolId != string.Empty)
            {
                tests = tests.Where(it => it.ToolId.Contains(toolId));
            }
            if (from != null)
            {
                tests = tests.Where(it => it.TestTime >= from);
            }
            if (to != null)
            {
                tests = tests.Where(it => it.TestTime < to.Value.Date.AddDays(1));
            }
            Data = tests.OrderByDescending(it => it.TestTime).ToList();
            listView.ItemsSource = Data;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                DefaultExt = "csv",
                Filter = "CSV|*.csv",
            };
            if (dialog.ShowDialog() == true)
            {
                // UTF-8的CSV用Excel打开乱码，UTF-16则不认逗号分隔符，要用TAB分隔
                using StreamWriter writer = new(File.Create(dialog.FileName), Encoding.Unicode);
                writer.WriteLine("电批编码\t目标扭矩\t实测扭矩\t允许偏倚\t偏倚\t结果\t检测时间");
                foreach (var test in Data)
                {
                    writer.WriteLine($"{test.ToolId}\t{test.SetTorque}\t{test.RealTorque}\t{test.AllowedDiviation}\t{test.Diviation}\t{test.IsPass}\t{test.TestTime}");
                }
            }
        }
    }
}
