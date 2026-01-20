using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IPC
{
    public enum TrafficLightState
    {
        Off,
        Red,
        Yellow,
        Green
    }

    public partial class TrafficLightControl : UserControl
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(
                nameof(State),
                typeof(TrafficLightState),
                typeof(TrafficLightControl),
                new PropertyMetadata(TrafficLightState.Off, OnStateChanged));

        public TrafficLightState State
        {
            get => (TrafficLightState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public TrafficLightControl()
        {
            InitializeComponent();
            UpdateVisuals(State);
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrafficLightControl ctl && e.NewValue is TrafficLightState st)
            {
                if (!ctl.Dispatcher.CheckAccess())
                {
                    ctl.Dispatcher.Invoke(() => ctl.UpdateVisuals(st));
                }
                else
                {
                    ctl.UpdateVisuals(st);
                }
            }
        }

        private void UpdateVisuals(TrafficLightState state)
        {
            Brush off = TryFindResource("LightOff") as Brush ?? Brushes.Gray;
            Brush red = TryFindResource("RedOn") as Brush ?? Brushes.Red;
            Brush yellow = TryFindResource("YellowOn") as Brush ?? Brushes.Yellow;
            Brush green = TryFindResource("GreenOn") as Brush ?? Brushes.Green;

            switch (state)
            {
                case TrafficLightState.Red:
                    LightCircle.Fill = red;
                    break;
                case TrafficLightState.Yellow:
                    LightCircle.Fill = yellow;
                    break;
                case TrafficLightState.Green:
                    LightCircle.Fill = green;
                    break;
                default:
                    LightCircle.Fill = off;
                    break;
            }
        }

        public void SetRed() => State = TrafficLightState.Red;
        public void SetYellow() => State = TrafficLightState.Yellow;
        public void SetGreen() => State = TrafficLightState.Green;
        public void SetOff() => State = TrafficLightState.Off;
    }
}