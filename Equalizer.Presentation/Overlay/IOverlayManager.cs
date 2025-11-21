using System.Threading.Tasks;

namespace Equalizer.Presentation.Overlay;

public interface IOverlayManager
{
    Task ShowAsync();
    Task HideAsync();
    Task ToggleAsync();
    bool IsVisible { get; }
    bool ClickThrough { get; }
    bool AlwaysOnTop { get; }
    Task SetClickThroughAsync(bool value);
    Task ToggleClickThroughAsync();
    Task SetAlwaysOnTopAsync(bool value);
    Task ToggleAlwaysOnTopAsync();
    Task ResetPositionAsync();
}
