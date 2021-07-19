using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        MainWindowModel Model { get; set; } = new();
        ITorqueService TorqueService { get; }
        IMesService MesService { get; }

        public MainWindow(ITorqueService torqueService, IMesService mesService)
        {
            InitializeComponent();
            DataContext = Model;
            TorqueService = torqueService;
            MesService = mesService;
        }
    }
}
