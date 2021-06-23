using System;
using System.Threading.Tasks;
using System.Windows;
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

        const float a = (float)(10 - -10) / (float)(20000 - 4000);
        float b;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = Model;
        }

        public void Dispose() => ModbusClient?.Dispose();

        void Dispose(object sender, EventArgs e) => Dispose();

        void Connect(object sender, RoutedEventArgs e)
        {
            // TODO: 这一段代码在new ModbusClient出错时（比如无法建立连接）Task执行顺序会陷入混乱，只能重启应用
            ModbusClient?.Dispose();
            ModbusClient = new ModbusClient(Model.IPAddress, Model.Port);
        }

        async Task<float> Request()
        {
            var response = await ModbusClient.ReadHoldingRegisters(600, 2);
            Log($"收到响应{response}");
            var data = new ArraySegment<byte>(response.Data, 1, response.Data.Length - 1);
            // 还不懂为什么两个寄存器高低位进行调换后才正确
            return BigEndianConverter.ToSingle(new byte[] { data[2], data[3], data[0], data[1] });
        }

        async void Calibrate(object sender, RoutedEventArgs e)
        {
            var x = await Request();
            Log($"扭矩0时电流为{x / 1000}mA");
            b = -a * x;
            Log($"扭矩 = 电流 * 1000 * {a} + {b}");
        }

        async void Request(object sender, RoutedEventArgs e)
        {
            var x = await Request();
            var y = a * x + b;
            Log($"电流为{x / 1000}mA，扭矩为{y}N*M");
        }

        void Log(string log) => Model.Logs.Add(log);
    }
}
