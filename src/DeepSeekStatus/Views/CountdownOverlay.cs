using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DeepSeekStatus.Models;
using DeepSeekStatus.Support;
using WinForms = System.Windows.Forms;

namespace DeepSeekStatus.Views;

public sealed class CountdownOverlay : Window
{
    private readonly PricingStore _store;
    private readonly Action _togglePanel;
    private readonly TextBlock _text;
    private readonly Ellipse _dot;
    private bool _userMoved;
    private bool _dragging;
    private Point _dragStart;

    public CountdownOverlay(PricingStore store, Action togglePanel)
    {
        _store = store;
        _togglePanel = togglePanel;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _text = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Segoe UI"),
            FontSize = 15.3,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Typography = { NumeralAlignment = FontNumeralAlignment.Tabular },
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(11, 4, 11, 5),
        };
        panel.Children.Add(_dot);
        panel.Children.Add(_text);

        Content = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x12, 0x14, 0x1C)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Child = panel,
        };

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        SourceInitialized += (_, _) => ApplyNoActivate();
    }

    public void Update()
    {
        if (!_store.ShowsCountdown)
        {
            if (IsVisible)
            {
                Hide();
            }

            return;
        }

        _text.Text = PricingFormatter.CompactCountdown(_store.Snapshot.SecondsUntilTransition);
        _dot.Fill = new SolidColorBrush(WhaleTheme.Accent(_store.Period));
        if (!IsVisible)
        {
            Show();
            UpdateLayout();
        }

        Reposition();
    }

    public void Reposition(bool force = false)
    {
        if (!IsVisible || (_userMoved && !force))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = ActualWidth * dpi.DpiScaleX;
        var height = ActualHeight * dpi.DpiScaleY;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        int x;
        int y;
        if (NativeMethods.GetTrayNotifyRect() is { } tray)
        {
            x = tray.Left - (int)Math.Ceiling(width) - 6;
            y = tray.Top + (tray.Height - (int)Math.Ceiling(height)) / 2;
        }
        else
        {
            var area = WinForms.Screen.PrimaryScreen!.WorkingArea;
            x = area.Right - (int)Math.Ceiling(width) - 8;
            y = area.Bottom - (int)Math.Ceiling(height) - 8;
        }

        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, NativeMethods.HWND_TOPMOST,
                                   x, y, 0, 0,
                                   NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void ApplyNoActivate()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragging = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_dragging || Math.Abs(position.X - _dragStart.X) <= 4 && Math.Abs(position.Y - _dragStart.Y) <= 4)
        {
            return;
        }

        _dragging = true;
        _userMoved = true;
        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            _togglePanel();
        }

        _dragging = false;
    }
}
