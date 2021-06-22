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
using Modbus;

namespace Torque
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        MainWindowModel Model { get; set; } = new();
        ModbusClient? ModbusClient { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = Model;
            ModbusClient = new(Model.IPAddress, Model.Port);
        }

        public void Dispose() => ModbusClient?.Dispose();

        void Dispose(object sender, EventArgs e) => Dispose();

        async void Connect(object sender, RoutedEventArgs e)
        {
            var response = await ModbusClient.RequestAsync(3, new byte[4] { 2, 88, 0, 16 });
            Log($"收到响应{response}");
            var data = new ArraySegment<byte>(response.Data, 1, response.Data.Length - 1);
            var floats = new float[8];
            for (int i = 0; i < data.Count; i += 4)
            {
                // 还不懂为什么两个寄存器高低位进行调换后才正确
                floats[i/4] = BigEndianConverter.ToSingle(new byte[] { data[i+2], data[i+3], data[i], data[i+1] });
            }
            Log($"数据字节转换为单精度浮点{string.Join(',', floats)}");
        }

        void Log(string log) => Model.Logs.Add(log);
    }
}
