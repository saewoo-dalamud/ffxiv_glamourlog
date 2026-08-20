using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourLog.Tweaks.Cabinet;
using GlamourLog.Tweaks.PrismBox;
using GlamourLog.Services;
using KamiToolKit.Controllers;
using System.Threading.Tasks;

namespace GlamourLog.Tweaks;

// Shows a small ImGui panel with Filters/Store All while the Cabinet or Crystallize addon is
// open, instead of attaching KamiToolKit nodes directly onto those native addons: attaching
// native nodes goes through AtkUnitManager.GetAddonByNode, which crashes on some clients (see
// git history for details). A plain ImGui panel never touches that code path.
internal sealed class ExtraAddonButtons : IPluginService, IAsyncDisposable {
    public int InitOrder => 15;

    private readonly AddonController _cabinetController;
    private readonly AddonController _crystallizeController;

    private bool _cabinetOpen;
    private bool _crystallizeOpen;

    public unsafe ExtraAddonButtons() {
        _cabinetController = new AddonController {
            AddonName = "Cabinet",
            OnSetup = _ => _cabinetOpen = true,
            OnFinalize = OnCabinetFinalize,
        };
        _crystallizeController = new AddonController {
            AddonName = CrystallizeNativeTree.AddonName,
            OnSetup = _ => _crystallizeOpen = true,
            OnFinalize = OnCrystallizeFinalize,
        };

        Svc.Interface.UiBuilder.Draw += Draw;

        IFramework.Get().Run(() => {
            _cabinetController.Enable();
            _crystallizeController.Enable();
        });
    }

    private unsafe void OnCabinetFinalize(AtkUnitBase* _) {
        _cabinetOpen = false;
        var filter = WindowsService.Get().AddonFilterWindow;
        if (filter.ActiveKind == AddonFilterKind.Armoire)
            filter.CloseIfOpen();
    }

    private unsafe void OnCrystallizeFinalize(AtkUnitBase* _) {
        _crystallizeOpen = false;
        var filter = WindowsService.Get().AddonFilterWindow;
        if (filter.ActiveKind == AddonFilterKind.Dresser)
            filter.CloseIfOpen();
    }

    private void Draw() {
        if (_cabinetOpen)
            DrawPanel("GlamourLog: Armoire##ExtraAddonCabinet", AddonFilterKind.Armoire, AddonFilterWindow.ArmoireOptions, StoreAllArmoire);

        if (_crystallizeOpen)
            DrawPanel("GlamourLog: Dresser##ExtraAddonCrystallize", AddonFilterKind.Dresser, AddonFilterWindow.DresserOptions, StoreAllDresser);
    }

    private static void StoreAllArmoire() {
        if (AtkUnitBase.IsAddonReady("Cabinet"))
            Svc.Automation.Start(new StoreAllArmoireTask());
    }

    private static void StoreAllDresser() {
        if (AtkUnitBase.IsAddonReady(CrystallizeNativeTree.AddonName))
            Svc.Automation.Start(new StoreAllDresserTask());
    }

    private static void DrawPanel(string title, AddonFilterKind kind, FilterOption[] options, System.Action storeAll) {
        ImGui.SetNextWindowSize(new Vector2(160f, 0f), ImGuiCond.FirstUseEver);
        var flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        if (!ImGui.Begin(title, flags)) {
            ImGui.End();
            return;
        }

        var windows = WindowsService.Get();

        if (ImGui.Button("Filters", new Vector2(-1f, 0f))) {
            var pos = ImGui.GetWindowPos() + new Vector2(0f, ImGui.GetWindowHeight());
            windows.AddonFilterWindow.OpenOrToggleNear(kind, Addon.GetRow(7542).Text.ToString(), options, pos);
        }

        if (ImGui.Button("Store All", new Vector2(-1f, 0f)))
            storeAll();

        ImGui.End();
    }

    public async ValueTask DisposeAsync() {
        Svc.Interface.UiBuilder.Draw -= Draw;

        await Svc.Framework.RunOnFrameworkThread(() => {
            _cabinetController.Dispose();
            _crystallizeController.Dispose();
        });
    }
}
