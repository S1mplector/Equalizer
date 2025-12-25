using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Flux.Application.Abstractions;
using Flux.Domain;

namespace Flux.Avalonia.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsPort? _settingsPort;
    private readonly IAudioDeviceProvider? _audioDeviceProvider;
    private FluxSettings? _settings;

    // Controls
    private ComboBox? _audioDeviceCombo;
    private Slider? _barCountSlider;
    private TextBlock? _barCountLabel;
    private Slider? _sensitivitySlider;
    private TextBlock? _sensitivityLabel;
    private Slider? _smoothingSlider;
    private TextBlock? _smoothingLabel;
    private CheckBox? _useGradientCheck;
    private CheckBox? _glowEnabledCheck;
    private RadioButton? _barsMode;
    private RadioButton? _circularMode;
    private CheckBox? _mirrorCheck;
    private CheckBox? _gpuRenderCheck;
    private Button? _saveButton;
    private Button? _resetButton;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(ISettingsPort settingsPort, IAudioDeviceProvider audioDeviceProvider)
    {
        _settingsPort = settingsPort;
        _audioDeviceProvider = audioDeviceProvider;
        
        InitializeComponent();
        LoadSettings();
        BindEvents();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Get control references
        _audioDeviceCombo = this.FindControl<ComboBox>("AudioDeviceCombo");
        _barCountSlider = this.FindControl<Slider>("BarCountSlider");
        _barCountLabel = this.FindControl<TextBlock>("BarCountLabel");
        _sensitivitySlider = this.FindControl<Slider>("SensitivitySlider");
        _sensitivityLabel = this.FindControl<TextBlock>("SensitivityLabel");
        _smoothingSlider = this.FindControl<Slider>("SmoothingSlider");
        _smoothingLabel = this.FindControl<TextBlock>("SmoothingLabel");
        _useGradientCheck = this.FindControl<CheckBox>("UseGradientCheck");
        _glowEnabledCheck = this.FindControl<CheckBox>("GlowEnabledCheck");
        _barsMode = this.FindControl<RadioButton>("BarsMode");
        _circularMode = this.FindControl<RadioButton>("CircularMode");
        _mirrorCheck = this.FindControl<CheckBox>("MirrorCheck");
        _gpuRenderCheck = this.FindControl<CheckBox>("GpuRenderCheck");
        _saveButton = this.FindControl<Button>("SaveButton");
        _resetButton = this.FindControl<Button>("ResetButton");
    }

    private void LoadSettings()
    {
        _settings = _settingsPort?.GetAsync().GetAwaiter().GetResult() ?? FluxSettings.Default;

        // Populate audio devices
        if (_audioDeviceCombo != null && _audioDeviceProvider != null)
        {
            var devices = _audioDeviceProvider.GetOutputDevices();
            _audioDeviceCombo.ItemsSource = devices;
            
            if (!string.IsNullOrEmpty(_settings.AudioDeviceId))
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i].Id == _settings.AudioDeviceId)
                    {
                        _audioDeviceCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        // Apply settings to controls
        if (_barCountSlider != null) _barCountSlider.Value = _settings.BarsCount;
        if (_barCountLabel != null) _barCountLabel.Text = _settings.BarsCount.ToString();
        if (_sensitivitySlider != null) _sensitivitySlider.Value = _settings.Responsiveness;
        if (_sensitivityLabel != null) _sensitivityLabel.Text = _settings.Responsiveness.ToString("F1");
        if (_smoothingSlider != null) _smoothingSlider.Value = _settings.Smoothing;
        if (_smoothingLabel != null) _smoothingLabel.Text = _settings.Smoothing.ToString("F2");
        if (_useGradientCheck != null) _useGradientCheck.IsChecked = _settings.GradientEnabled;
        if (_glowEnabledCheck != null) _glowEnabledCheck.IsChecked = _settings.GlowEnabled;
        if (_barsMode != null) _barsMode.IsChecked = _settings.VisualizerMode == VisualizerMode.Bars;
        if (_circularMode != null) _circularMode.IsChecked = _settings.VisualizerMode == VisualizerMode.Circular;
        if (_gpuRenderCheck != null) _gpuRenderCheck.IsChecked = _settings.RenderingMode == RenderingMode.Gpu;
    }

    private void BindEvents()
    {
        if (_barCountSlider != null)
        {
            _barCountSlider.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == "Value" && _barCountLabel != null)
                    _barCountLabel.Text = ((int)_barCountSlider.Value).ToString();
            };
        }

        if (_sensitivitySlider != null)
        {
            _sensitivitySlider.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == "Value" && _sensitivityLabel != null)
                    _sensitivityLabel.Text = _sensitivitySlider.Value.ToString("F1");
            };
        }

        if (_smoothingSlider != null)
        {
            _smoothingSlider.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == "Value" && _smoothingLabel != null)
                    _smoothingLabel.Text = _smoothingSlider.Value.ToString("F2");
            };
        }

        if (_saveButton != null)
        {
            _saveButton.Click += OnSaveClick;
        }

        if (_resetButton != null)
        {
            _resetButton.Click += OnResetClick;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        // Collect values from controls and create new immutable settings
        int barCount = _barCountSlider != null ? (int)_barCountSlider.Value : _settings.BarsCount;
        double sensitivity = _sensitivitySlider != null ? _sensitivitySlider.Value : _settings.Responsiveness;
        double smoothing = _smoothingSlider != null ? _smoothingSlider.Value : _settings.Smoothing;
        bool gradientEnabled = _useGradientCheck?.IsChecked ?? _settings.GradientEnabled;
        bool glowEnabled = _glowEnabledCheck?.IsChecked ?? _settings.GlowEnabled;
        var visualizerMode = (_circularMode?.IsChecked ?? false) ? VisualizerMode.Circular : VisualizerMode.Bars;
        var renderingMode = (_gpuRenderCheck?.IsChecked ?? false) ? RenderingMode.Gpu : RenderingMode.Cpu;
        
        string? audioDeviceId = _settings.AudioDeviceId;
        if (_audioDeviceCombo?.SelectedItem is AudioDeviceInfo device)
        {
            audioDeviceId = device.Id;
        }

        var newSettings = new FluxSettings(
            barsCount: barCount,
            responsiveness: sensitivity,
            smoothing: smoothing,
            color: _settings.Color,
            targetFps: _settings.TargetFps,
            colorCycleEnabled: _settings.ColorCycleEnabled,
            colorCycleSpeedHz: _settings.ColorCycleSpeedHz,
            barCornerRadius: _settings.BarCornerRadius,
            displayMode: _settings.DisplayMode,
            specificMonitorDeviceName: _settings.SpecificMonitorDeviceName,
            offsetX: _settings.OffsetX,
            offsetY: _settings.OffsetY,
            visualizerMode: visualizerMode,
            circleDiameter: _settings.CircleDiameter,
            overlayVisible: _settings.OverlayVisible,
            fadeOnSilenceEnabled: _settings.FadeOnSilenceEnabled,
            silenceFadeOutSeconds: _settings.SilenceFadeOutSeconds,
            silenceFadeInSeconds: _settings.SilenceFadeInSeconds,
            pitchReactiveColorEnabled: _settings.PitchReactiveColorEnabled,
            bassEmphasis: _settings.BassEmphasis,
            trebleEmphasis: _settings.TrebleEmphasis,
            beatShapeEnabled: _settings.BeatShapeEnabled,
            glowEnabled: glowEnabled,
            perfOverlayEnabled: _settings.PerfOverlayEnabled,
            gradientEnabled: gradientEnabled,
            gradientEndColor: _settings.GradientEndColor,
            audioDeviceId: audioDeviceId,
            renderingMode: renderingMode,
            monitorOffsets: _settings.MonitorOffsets
        );

        _ = _settingsPort?.SaveAsync(newSettings);
        Close();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _settings = FluxSettings.Default;
        LoadSettings();
    }
}
