# Flux

A lightweight Windows desktop customization tool featuring a reactive audio visualizer and widget system. A modern Rainmeter-like alternative built with WPF and clean hexagonal architecture.

## Features
- **Audio Visualizer**: Real-time spectrum visualization with bars or circular mode
- **Widget System**: Clock, Date, System Info widgets with drag-and-drop positioning
- **GPU Rendering**: Optional SkiaSharp-powered GPU acceleration
- **Multi-monitor**: Overlay spans all monitors or specific ones
- **Customizable**: Colors, gradients, glow effects, beat reactivity
- **Settings persistence**: JSON in `%AppData%/Flux/`

## Architecture
- **Domain** (`Flux.Domain/`): Core models - `FluxSettings`, `ColorRgb`, widget configs
- **Application** (`Flux.Application/`): Services like `IFluxService`, `SpectrumProcessor`
- **Infrastructure** (`Flux.Infrastructure/`): Audio capture (NAudio), settings persistence
- **Presentation** (`Flux.Presentation/`): WPF app, tray icon, overlays, settings UI

## Run
```powershell
dotnet build
dotnet run --project .\Flux.Presentation\Flux.Presentation.csproj
```
Tray icon appears; right-click for options.

## Controls
- **Tray Menu**: Toggle overlay, settings, widgets, edit mode
- **Global Hotkeys**: Ctrl+Alt+Shift+E (toggle), Ctrl+Alt+Shift+S (settings)

## Tests
```powershell
dotnet test
```

## Packages
- NAudio (WASAPI loopback audio)
- MathNet.Numerics (FFT)
- SkiaSharp (GPU rendering)
- Microsoft.Extensions.* (Hosting, DI)
