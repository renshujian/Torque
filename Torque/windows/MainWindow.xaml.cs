using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Torque
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal MainWindowModel Model { get; } = new();
        ITorqueService TorqueService { get; }
        IMesService MesService { get; }
        StringBuilder scaned = new();
        Timer getToolDelayed;

        public MainWindow(ITorqueService torqueService, IMesService mesService)
        {
            InitializeComponent();
            DataContext = Model;
            TorqueService = torqueService;
            MesService = mesService;
            getToolDelayed = new(_ =>
            {
                var id = scaned.ToString();
                scaned.Clear();
                var tool = MesService.GetTool(id);
                if (tool is null)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"没找到电批{id}"));
                }
                else
                {
                    Model.Tool = tool;
                    Dispatcher.Invoke(ClearTests);
                }
            });
        }

        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            scaned.Append(e.Text);
            getToolDelayed.Change(300, Timeout.Infinite);
        }

        private async void ReadTorque(object sender, RoutedEventArgs e)
        {
            var torque = await TorqueService.ReadAsync();
            var test = new Test
            {
                ToolId = Model.Tool!.Id,
                SetTorque = Model.Tool.SetTorque,
                RealTorque = torque,
                Diviation = (torque - Model.Tool.SetTorque) / Model.Tool.SetTorque,
                TestTime = DateTime.Now
            };
            Model.LastTest = test;
            Model.Tests.Add(test);
            if (!test.IsOK)
            {
                if (MessageBox.Show("数据NG，是否重新测量", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    ClearTests();
                    return;
                }
            }
            if (Model.Tests.Count >= 12 && Model.Tests.All(t => t.IsOK))
            {
                if (MessageBox.Show("校准完成，是否上传数据", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    MesService.Upload(Model.Tests);
                    ClearTests();
                }
            }
        }

        private void ResetTorque(object sender, RoutedEventArgs e)
        {
            // torque.reset
            ClearTests();
        }

        void ClearTests()
        {
            Model.LastTest = null;
            Model.Tests.Clear();
        }
    }
}
