using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Torque
{
    class MainWindowModel : INotifyPropertyChanged
    {
        User user = new("匿名用户");
        public User User
        {
            get => user;
            set
            {
                user = value;
                OnPropertyChanged();
            }
        }

        Tool? tool;
        public Tool? Tool
        {
            get => tool;
            set
            {
                tool = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Test> Tests { get; } = new();
        Test? lastTest;
        public Test? LastTest
        {
            get => lastTest;
            set
            {
                lastTest = value;
                OnPropertyChanged();
            }
        }

        public void ClearTests()
        {
            LastTest = null;
            Tests.Clear();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
