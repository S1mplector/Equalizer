using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Equalizer.Domain;
using Forms = System.Windows.Forms;

namespace Equalizer.Presentation.Controls;

public partial class ColorPickerWindow : Window
{
    public ColorRgb SelectedColor { get; private set; }

    private bool _isPickingColor;
    private System.Drawing.Bitmap? _screenCapture;
    private int _screenLeft;
    private int _screenTop;
    private double _savedLeft;
    private double _savedTop;
    private double _savedWidth;
    private double _savedHeight;
    private bool _savedTopmost;
    private double _savedOpacity;

    public ColorPickerWindow(ColorRgb initial)
    {
        InitializeComponent();

        SelectedColor = initial;
        ColorR.Value = initial.R;
        ColorG.Value = initial.G;
        ColorB.Value = initial.B;
        UpdateFromSliders();

        ColorR.ValueChanged += (_, __) => UpdateFromSliders();
        ColorG.ValueChanged += (_, __) => UpdateFromSliders();
        ColorB.ValueChanged += (_, __) => UpdateFromSliders();

        ColorRValue.LostFocus += (_, __) => ApplyTextToSlider(ColorRValue, ColorR);
        ColorGValue.LostFocus += (_, __) => ApplyTextToSlider(ColorGValue, ColorG);
        ColorBValue.LostFocus += (_, __) => ApplyTextToSlider(ColorBValue, ColorB);

        EyedropperButton.Click += EyedropperButton_Click;
        OkButton.Click += OkButton_Click;
        CancelButton.Click += (_, __) => Close();

        MouseLeftButtonDown += ColorPickerWindow_MouseLeftButtonDown;
        MouseRightButtonDown += ColorPickerWindow_MouseRightButtonDown;
        Closed += (_, __) =>
        {
            _screenCapture?.Dispose();
            _screenCapture = null;
        };
    }

    private void UpdateFromSliders()
    {
        var rgb = new ColorRgb((byte)ColorR.Value, (byte)ColorG.Value, (byte)ColorB.Value);
        SelectedColor = rgb;
        ColorRValue.Text = ((int)ColorR.Value).ToString();
        ColorGValue.Text = ((int)ColorG.Value).ToString();
        ColorBValue.Text = ((int)ColorB.Value).ToString();
        var media = System.Windows.Media.Color.FromRgb(rgb.R, rgb.G, rgb.B);
        ColorPreview.Background = new SolidColorBrush(media);
    }

    private void ApplyTextToSlider(System.Windows.Controls.TextBox box, System.Windows.Controls.Slider slider)
    {
        if (!int.TryParse(box.Text, out var value))
        {
            box.Text = ((int)slider.Value).ToString();
            return;
        }
        value = Math.Clamp(value, 0, 255);
        slider.Value = value;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isPickingColor)
        {
            EndEyedropper();
        }
        DialogResult = true;
        Close();
    }

    private void EyedropperButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isPickingColor) return;

        try
        {
            _savedLeft = Left;
            _savedTop = Top;
            _savedWidth = Width;
            _savedHeight = Height;
            _savedTopmost = Topmost;
            _savedOpacity = Opacity;

            var virtualScreen = Forms.SystemInformation.VirtualScreen;
            _screenLeft = virtualScreen.Left;
            _screenTop = virtualScreen.Top;
            var width = virtualScreen.Width;
            var height = virtualScreen.Height;

            Topmost = true;
            Left = _screenLeft;
            Top = _screenTop;
            Width = width;
            Height = height;

            Opacity = 0;

            _screenCapture?.Dispose();
            _screenCapture = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(_screenCapture))
            {
                g.CopyFromScreen(_screenLeft, _screenTop, 0, 0, new System.Drawing.Size(width, height));
            }

            Opacity = _savedOpacity;

            _isPickingColor = true;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
        }
        catch
        {
            _isPickingColor = false;
        }
    }

    private void ColorPickerWindow_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isPickingColor || _screenCapture == null) return;

        var p = e.GetPosition(this);
        var screenPoint = PointToScreen(p);
        int x = (int)(screenPoint.X - _screenLeft);
        int y = (int)(screenPoint.Y - _screenTop);
        if (x >= 0 && y >= 0 && x < _screenCapture.Width && y < _screenCapture.Height)
        {
            var c = _screenCapture.GetPixel(x, y);
            var rgb = new ColorRgb(c.R, c.G, c.B);
            SelectedColor = rgb;
            ColorR.Value = rgb.R;
            ColorG.Value = rgb.G;
            ColorB.Value = rgb.B;
            var media = System.Windows.Media.Color.FromRgb(rgb.R, rgb.G, rgb.B);
            ColorPreview.Background = new SolidColorBrush(media);
        }

        EndEyedropper();
        e.Handled = true;
    }

    private void ColorPickerWindow_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isPickingColor) return;
        EndEyedropper();
        e.Handled = true;
    }

    private void EndEyedropper()
    {
        _isPickingColor = false;
        System.Windows.Input.Mouse.OverrideCursor = null;
        Cursor = System.Windows.Input.Cursors.Arrow;
        _screenCapture?.Dispose();
        _screenCapture = null;

        Left = _savedLeft;
        Top = _savedTop;
        Width = _savedWidth;
        Height = _savedHeight;
        Topmost = _savedTopmost;
        Opacity = _savedOpacity;
    }
}
