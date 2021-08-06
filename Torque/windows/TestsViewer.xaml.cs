using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace Torque
{
    /// <summary>
    /// TestsViewer.xaml 的交互逻辑
    /// </summary>
    public partial class TestsViewer : Window
    {
        AppDbContext AppDbContext { get; }

        public TestsViewer(AppDbContext appDbContext)
        {
            InitializeComponent();
            AppDbContext = appDbContext;
            appDbContext.Tests.Load();
            listView.ItemsSource = appDbContext.Tests.Local.ToObservableCollection().Reverse();
        }
    }
}
