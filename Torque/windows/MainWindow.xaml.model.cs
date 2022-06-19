using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        public double AllowedDiviation { get; set; } = 0.2;

        public ISeries[] Series { get; } =
        {
            new LineSeries<TimeSpanPoint>
            {
                Values = new ObservableCollection<TimeSpanPoint>(),
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.DeepSkyBlue, 1),
                GeometrySize = 5,
                GeometryFill = new SolidColorPaint(SKColors.DeepSkyBlue),
                GeometryStroke = null,
            }
        };

        public Axis[] XAxes { get; } =
        {
            new Axis
            {
                Labeler = value => new TimeSpan((long)value).ToString(),
                UnitWidth = TimeSpan.FromMilliseconds(1).Ticks,
                MinStep = TimeSpan.FromSeconds(1).Ticks,
            }
        };

        public ObservableCollection<Test> Tests { get; } = new();
        public bool TestsAreOK => Tests.All(t => t.IsOK);

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
