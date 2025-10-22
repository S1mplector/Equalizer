using System;
using System.Windows;
using System.Windows.Controls;
using Equalizer.Application.Abstractions;
using Equalizer.Domain;

namespace Equalizer.Presentation.Settings;

public partial class SettingsWindow : Window
{
    private readonly ISettingsPort _settings;

    public SettingsWindow(ISettingsPort settings)
    {
        _settings = settings;
        InitializeComponent();
        Loaded += OnLoaded;
        SaveButton.Click += OnSave;
        CancelButton.Click += (_, __) => Close();

        BarsSlider.ValueChanged += (_, __) => BarsValue.Text = ((int)BarsSlider.Value).ToString();
        RespSlider.ValueChanged += (_, __) => RespValue.Text = RespSlider.Value.ToString("0.00");
        SmoothSlider.ValueChanged += (_, __) => SmoothValue.Text = SmoothSlider.Value.ToString("0.00");
        ColorR.ValueChanged += (_, __) => ColorRValue.Text = ((int)ColorR.Value).ToString();
        ColorG.ValueChanged += (_, __) => ColorGValue.Text = ((int)ColorG.Value).ToString();
        ColorB.ValueChanged += (_, __) => ColorBValue.Text = ((int)ColorB.Value).ToString();
        FpsSlider.ValueChanged += (_, __) => FpsValue.Text = ((int)FpsSlider.Value).ToString();
        ColorCycleSpeed.ValueChanged += (_, __) => ColorCycleSpeedValue.Text = ColorCycleSpeed.Value.ToString("0.00");
        CornerRadiusSlider.ValueChanged += (_, __) => CornerRadiusValue.Text = CornerRadiusSlider.Value.ToString("0.0");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var s = await _settings.GetAsync();
        BarsSlider.Value = s.BarsCount;
        BarsValue.Text = s.BarsCount.ToString();
        RespSlider.Value = s.Responsiveness;
        RespValue.Text = s.Responsiveness.ToString("0.00");
        SmoothSlider.Value = s.Smoothing;
        SmoothValue.Text = s.Smoothing.ToString("0.00");
        ColorR.Value = s.Color.R;
        ColorG.Value = s.Color.G;
        ColorB.Value = s.Color.B;
        ColorRValue.Text = s.Color.R.ToString();
        ColorGValue.Text = s.Color.G.ToString();
        ColorBValue.Text = s.Color.B.ToString();

        FpsSlider.Value = s.TargetFps;
        FpsValue.Text = s.TargetFps.ToString();
        ColorCycleEnabled.IsChecked = s.ColorCycleEnabled;
        ColorCycleSpeed.Value = s.ColorCycleSpeedHz;
        ColorCycleSpeedValue.Text = s.ColorCycleSpeedHz.ToString("0.00");
        CornerRadiusSlider.Value = s.BarCornerRadius;
        CornerRadiusValue.Text = s.BarCornerRadius.ToString("0.0");
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            int bars = (int)BarsSlider.Value;
            double resp = RespSlider.Value;
            double smooth = SmoothSlider.Value;
            byte r = (byte)ColorR.Value;
            byte g = (byte)ColorG.Value;
            byte b = (byte)ColorB.Value;

            int fps = (int)FpsSlider.Value;
            bool cycle = ColorCycleEnabled.IsChecked == true;
            double cycleHz = ColorCycleSpeed.Value;
            double radius = CornerRadiusSlider.Value;

            var s = new EqualizerSettings(bars, resp, smooth, new ColorRgb(r, g, b), fps, cycle, cycleHz, radius);
            await _settings.SaveAsync(s);
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
