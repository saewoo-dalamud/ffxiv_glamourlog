using Dalamud.Interface.Windowing;
using GlamourLog.Windows.GuideWindow;
using System.Threading.Tasks;

namespace GlamourLog.Services;

internal sealed class WindowsService : IPluginService, IDisposable {
    public int InitOrder => 5;

    private readonly WindowSystem _windowSystem = new("GlamourLog");
    private readonly GuideWindow _mainMenuWindow = new();
    private readonly FilterWindow _filterWindow = new();
    private readonly AddonFilterWindow _addonFilterWindow = new();
    private readonly LogWindow _logWindow;

    public WindowsService() {
        _logWindow = new LogWindow(_filterWindow);

        _windowSystem.AddWindow(_mainMenuWindow);
        _windowSystem.AddWindow(_filterWindow);
        _windowSystem.AddWindow(_addonFilterWindow);
        _windowSystem.AddWindow(_logWindow);
        Svc.Interface.UiBuilder.Draw += _windowSystem.Draw;
        Svc.Interface.UiBuilder.OpenMainUi += ToggleMainWindow;
        Svc.Interface.UiBuilder.OpenConfigUi += ToggleMainMenu;
    }

    internal FilterWindow FilterWindow => _filterWindow;

    internal AddonFilterWindow AddonFilterWindow => _addonFilterWindow;

    internal GuideWindow MainMenuWindow => _mainMenuWindow;

    internal LogWindow LogWindow => _logWindow;

    internal void ToggleMainWindow() => _logWindow.Toggle();
    internal void ToggleMainMenu() => MainMenuWindow.OpenOrToggleCentered();

    internal void ToggleMainMenuNearLogWindow() {
        if (LogWindow.IsOpen)
            MainMenuWindow.OpenOrToggleNear(LogWindow.ComputeMainMenuScreenOrigin());
        else
            MainMenuWindow.OpenOrToggleCentered();
    }

    internal void RefreshLogWindow() => LogWindow.RefreshListsAndDetails();

    public void Dispose() {
        Svc.Interface.UiBuilder.OpenMainUi -= ToggleMainWindow;
        Svc.Interface.UiBuilder.OpenConfigUi -= ToggleMainMenu;
        Svc.Interface.UiBuilder.Draw -= _windowSystem.Draw;
        _windowSystem.RemoveAllWindows();
    }
}
