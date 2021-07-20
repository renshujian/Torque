using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
                }
            });
        }

        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            scaned.Append(e.Text);
            getToolDelayed.Change(300, Timeout.Infinite);
        }
    }
}
