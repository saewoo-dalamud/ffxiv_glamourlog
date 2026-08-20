using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GlamourLog.Services;
using GlamourLog.Windows.LogWindow;
using KamiToolKit.Controllers;
using System.Threading.Tasks;

namespace GlamourLog.Tweaks;

// Flags loot-window items that are glamour-relevant and not yet owned. Previously drew a badge
// icon glued onto each item's native list row (KamiToolKit AttachNode onto the row's icon node);
// that attach path crashes on some clients (see git history), and there's no ImGui equivalent
// for "overlay onto a specific row of a native scrolling list" anyway. Shown as a plain list in
// a small ImGui panel instead.
internal sealed class LootWindowBadges : IPluginService, IAsyncDisposable {
    public int InitOrder => 15;

    private readonly AddonController<AddonNeedGreed> _addonController;
    private readonly List<(string Name, StorageKind Part)> _eligibleItems = [];
    private bool _open;

    public unsafe LootWindowBadges() {
        _addonController = new AddonController<AddonNeedGreed> {
            AddonName = "NeedGreed",
            OnSetup = _ => _open = true,
            OnFinalize = OnFinalize,
            OnRefresh = OnRefresh,
        };

        Svc.Interface.UiBuilder.Draw += Draw;

        IFramework.Get().Run(() => _addonController.Enable());
    }

    private unsafe void OnFinalize(AddonNeedGreed* _) {
        _open = false;
        _eligibleItems.Clear();
    }

    private unsafe void OnRefresh(AddonNeedGreed* addon) {
        var count = GetNumItems(addon);

        var catalog = CatalogService.Get();
        var ownership = OwnershipService.Get();
        var query = ownership.Query();

        _eligibleItems.Clear();

        for (var index = 0; index < count; index++) {
            ref var itemInfo = ref addon->Items[index];
            if (itemInfo.ItemId == 0)
                continue;

            var itemId = ItemUtil.GetBaseId(itemInfo.ItemId).ItemId;
            if (itemId == 0)
                continue;

            // don't flag already owned
            if (query.Locate(itemId) is not PieceLocation.None)
                continue;

            if (ownership.IsCabinetItem(itemId) || catalog.ArmoireItemIds.Contains(itemId)) {
                _eligibleItems.Add((Item.GetRow(itemId).Name.ToString(), StorageKind.Armoire));
                continue;
            }

            if (catalog.IsMirageOutfitPiece(itemId))
                _eligibleItems.Add((Item.GetRow(itemId).Name.ToString(), StorageKind.Dresser));
        }
    }

    private void Draw() {
        if (!_open || _eligibleItems.Count == 0)
            return;

        ImGui.SetNextWindowSize(new Vector2(260f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("GlamourLog: Loot##LootWindowBadges", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled("Not yet owned, glamour-relevant:");
        foreach (var (name, part) in _eligibleItems) {
            var tag = part is StorageKind.Armoire ? "[Armoire]" : "[Dresser]";
            ImGui.TextUnformatted($"{tag} {name}");
        }

        ImGui.End();
    }

    private static unsafe int GetNumItems(AddonNeedGreed* addon) {
        var agent = AgentLoot.Instance();
        var numItems = agent is not null ? agent->NumItems : 0;
        return Math.Clamp(numItems, 0, addon->Items.Length);
    }

    public async ValueTask DisposeAsync() {
        Svc.Interface.UiBuilder.Draw -= Draw;

        await Svc.Framework.RunOnFrameworkThread(() => {
            _addonController.Dispose();
        });
    }
}
