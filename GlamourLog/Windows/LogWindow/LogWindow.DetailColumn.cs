using clib.TaskSystem;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using GlamourLog.Services;
using GlamourLog.Windows.LogWindow;
using System.Threading.Tasks;

namespace GlamourLog;

internal partial class LogWindow {
    private List<DetailSection> _detailSections = [];
    private DetailListRowData? _pieceContextMenuTarget;
    private (uint CfcId, SourceNavigateTarget? Nav)? _sourceContextMenuTarget;

    private void RefreshDetails() {
        var q = OwnershipService.Get().Query();

        if (_selectedSet == null)
            _selectedSourcePieceItemId = null;

        _detailSections = [];

        if (_selectedSet is not { } selectedSet) {
            _detailSections.Add(new DetailSection {
                Header = "Set Details",
                Entries = [new DetailListRowData { Kind = DetailRowKind.EmptyHint, PrimaryText = "No set selected" }],
            });
            return;
        }

        var isCabinetOnly = selectedSet.NonSetCabinetPiece;
        var status = q.For(selectedSet);
        if (isCabinetOnly)
            _selectedSourcePieceItemId = null;

        var detailsEntries = new List<DetailListRowData>();
        foreach (var piece in status.Pieces) {
            var storageKind = StorageKindFor(piece.BadgeLocation);
            detailsEntries.Add(new DetailListRowData {
                Kind = DetailRowKind.Piece,
                ItemId = piece.ItemId,
                PrimaryText = Item.GetRow(piece.ItemId).Name.ToString(),
                IsSelected = _selectedSourcePieceItemId == piece.ItemId,
                StorageKind = storageKind,
                ShowInventoryBadge = storageKind is null && piece.Location is PieceLocation.Inventory,
                ShowArmoireWarning = piece.ShowArmoireWarning,
            });
        }

        _detailSections.Add(new DetailSection {
            Header = isCabinetOnly ? "Item Details" : "Set Details",
            Entries = detailsEntries,
        });

        var items = selectedSet.Items;
        if (items.Count > 0 && TryGetCostTotals(selectedSet, _selectedSourcePieceItemId, out var costTotals)) {
            var costEntries = new List<DetailListRowData>();
            var ordered = costTotals.OrderBy(x => Item.GetRow(x.Key).Name.ToString(), StringComparer.Ordinal).ToList();
            foreach (var kv in ordered) {
                var owned = OwnershipService.GetOwnedCurrencyCount(kv.Key);
                var (costNav, costTip, npcName, shopName) = SourcesPanelBuilder.FindVendorForCurrency(CatalogService.Get(), selectedSet, _selectedSourcePieceItemId, kv.Key);
                var currencyName = Item.GetRow(kv.Key).Name.ToString().Trim();
                costEntries.Add(new DetailListRowData {
                    Kind = DetailRowKind.Cost,
                    ItemId = kv.Key,
                    PrimaryText = Item.GetRow(kv.Key).Name.ToString(),
                    SecondaryText = $"Obt. {owned}/{kv.Value}",
                    NavigateTarget = costNav,
                    CostVendorTextTooltip = costTip,
                    CostMapFlagLabel = costNav is not null && npcName.Length > 0 && shopName.Length > 0 ? $"{currencyName} - {npcName} - {shopName}" : string.Empty,
                });
            }

            _detailSections.Add(new DetailSection {
                Header = _selectedSourcePieceItemId is not null ? "Currencies Required (Single Item)" : "Currencies Required (Full Set)",
                Entries = costEntries,
            });
        }

        var sourceSections = SourcesPanelBuilder.BuildSourceSections(CatalogService.Get(), selectedSet, _selectedSourcePieceItemId);
        _detailSections.AddRange(sourceSections);

        if (TryBuildSharedModelsSection(q) is { } sharedSection)
            _detailSections.Add(sharedSection);
    }

    private bool TryGetCostTotals(GlamourSet set, uint? pieceFilterPieceItemId, out Dictionary<uint, uint> totals) {
        totals = [];
        IEnumerable<uint> pieceIds = pieceFilterPieceItemId is { } only ? [only] : set.Items;
        foreach (var itemId in pieceIds) {
            foreach (var (cid, amt) in CatalogService.Get().GetPrimaryItemCosts(itemId, CatalogService.Get().GetCategoryForPreferredCost(set))) {
                totals.TryGetValue(cid, out var t);
                totals[cid] = t + amt;
            }
        }
        return totals.Count > 0;
    }

    private static StorageKind? StorageKindFor(ItemStorageState storageState)
        => storageState switch {
            ItemStorageState.Armoire => StorageKind.Armoire,
            ItemStorageState.DresserLoose => StorageKind.DresserFaded,
            ItemStorageState.DresserSet => StorageKind.Dresser,
            _ => null,
        };

    private void OnDetailPieceItemLeftClick(uint itemId) {
        if (_selectedSet?.NonSetCabinetPiece == true)
            return;
        _selectedSourcePieceItemId = _selectedSourcePieceItemId == itemId ? null : itemId;
        RefreshDetails();
    }

    private DetailSection? TryBuildSharedModelsSection(OwnershipQuery q) {
        if (_selectedSet is not { } selectedSet)
            return null;

        var catalog = CatalogService.Get();

        if (_selectedSourcePieceItemId is { } pieceId) {
            var itemSiblings = catalog.GetSharedModelItemSiblings(pieceId);
            if (itemSiblings.Count == 0)
                return null;

            var entries = new List<DetailListRowData>();
            foreach (var itemId in itemSiblings) {
                var set = catalog.FindCatalogSetForItem(itemId);
                if (set is null)
                    continue;
                entries.Add(new DetailListRowData {
                    Kind = DetailRowKind.SharedModelSet,
                    SharedModelItemId = itemId,
                    SharedModelRow = BuildSharedModelItemRow(itemId, q),
                });
            }

            return new DetailSection { Header = "Shared Models (Items with this appearance)", Entries = entries };
        }

        var siblings = catalog.GetSharedModelSiblings(selectedSet);
        if (siblings.Count == 0)
            siblings = catalog.GetPartialSharedModelSetSiblings(selectedSet); // exact outfit twins first, then piece-level lookalikes
        if (siblings.Count == 0)
            return null;

        var setEntries = new List<DetailListRowData>();
        foreach (var sibling in siblings) {
            setEntries.Add(new DetailListRowData {
                Kind = DetailRowKind.SharedModelSet,
                SharedModelRow = BuildSetListRowData(sibling, q, appendNotInListSuffix: true),
            });
        }

        return new DetailSection { Header = "Shared Models (Sets that contain same-model items)", Entries = setEntries };
    }

    private void OnSharedModelItemLeftClick(uint itemId, GlamourSet catalogSet) {
        if (_selectedSourcePieceItemId is not null && _selectedSet?.Items.Contains(itemId) == true) {
            if (_selectedSourcePieceItemId == itemId)
                return;
            _selectedSourcePieceItemId = itemId;
            RefreshDetails();
            return;
        }

        OnSharedModelSetLeftClick(catalogSet);
    }

    private void OnSharedModelSetLeftClick(GlamourSet set) {
        if (ReferenceEquals(_selectedSet, set))
            return;

        if (_selectedCategoryId != AllCategoryId) {
            _selectedCategoryId = AllCategoryId;
            _searchText = string.Empty;
        }

        _selectedSet = set;
        _selectedSourcePieceItemId = null;
        _dirty = true;
    }

    private void DrawDetailColumn() {
        foreach (var section in _detailSections)
            DrawDetailSection(section);

        DrawPieceContextMenuPopup();
        DrawSourceContextMenuPopup();
    }

    private void DrawDetailSection(DetailSection section) {
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), section.Header);
        ImGui.Separator();

        foreach (var row in section.Entries)
            DrawDetailRow(row);

        if (section.Children is { } children) {
            ImGui.Indent();
            foreach (var child in children)
                DrawDetailSection(child);
            ImGui.Unindent();
        }
    }

    private unsafe void DrawDetailRow(DetailListRowData row) {
        switch (row.Kind) {
            case DetailRowKind.JournalHeader:
                ImGui.TextUnformatted(row.PrimaryText);
                if (row.CraftRecipeRowId != 0 && ImGui.IsItemClicked())
                    AgentRecipeNote.Instance()->OpenRecipeByRecipeId(row.CraftRecipeRowId);
                else if (row.NavigateTarget is { TerritoryTypeId: not 0 } nav && ImGui.IsItemClicked())
                    nav.OpenMap(row.PrimaryText);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && (row.CraftRecipeRowId != 0 || row.NavigateTarget is not null)) {
                    _sourceContextMenuTarget = (0, row.NavigateTarget);
                    ImGui.OpenPopup("##LogSourceContextMenu");
                }
                break;

            case DetailRowKind.EmptyHint:
                ImGui.TextDisabled(row.PrimaryText);
                break;

            case DetailRowKind.Piece: {
                var label = $"{row.PrimaryText}##piece{row.ItemId}";
                if (ImGui.Selectable(label, row.IsSelected))
                    OnDetailPieceItemLeftClick(row.ItemId);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                    _pieceContextMenuTarget = row;
                    ImGui.OpenPopup("##LogPieceContextMenu");
                }
                var tag = PieceTag(row);
                if (tag.Length > 0) {
                    ImGui.SameLine();
                    ImGui.TextDisabled(tag);
                }
                break;
            }

            case DetailRowKind.Cost: {
                ImGui.TextUnformatted($"{row.PrimaryText}  {row.SecondaryText}");
                if (row.CostVendorTextTooltip.Length > 0 && ImGui.IsItemHovered())
                    ImGui.SetTooltip(row.CostVendorTextTooltip);
                if (row.NavigateTarget is { TerritoryTypeId: not 0 } costNav && ImGui.IsItemClicked()) {
                    var mapLabel = row.CostMapFlagLabel.Length > 0 ? row.CostMapFlagLabel : row.PrimaryText;
                    costNav.OpenMap(mapLabel);
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                    _pieceContextMenuTarget = row;
                    ImGui.OpenPopup("##LogPieceContextMenu");
                }
                break;
            }

            case DetailRowKind.SourceDuty:
                ImGui.TextUnformatted(row.PrimaryText);
                if (row.NavigateTarget is { TerritoryTypeId: not 0 } dutyNav && ImGui.IsItemClicked())
                    dutyNav.OpenMap(row.PrimaryText);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && (row.ContentFinderConditionId != 0 || row.NavigateTarget is not null)) {
                    _sourceContextMenuTarget = (row.ContentFinderConditionId, row.NavigateTarget);
                    ImGui.OpenPopup("##LogSourceContextMenu");
                }
                break;

            case DetailRowKind.SourceChest: {
                var label = row.PrimaryText.Length > 0
                    ? row.SecondaryText.Length > 0 ? $"{row.PrimaryText} ({row.SecondaryText})" : row.PrimaryText
                    : "Sources";
                var itemNames = row.SourceItemIds is { Count: > 0 } ids ? string.Join(", ", ids.Select(id => Item.GetRow(id).Name.ToString())) : string.Empty;
                if (row.DungeonChestRowId != 0) {
                    if (ImGui.Selectable($"{label}:##chest{row.DungeonChestRowId}") && DungeonChestLayout.Instance.TryGet(row.DungeonChestRowId, out var chest))
                        chest.OpenMap(row.PrimaryText);
                    ImGui.SameLine();
                    ImGui.TextWrapped(itemNames);
                }
                else {
                    ImGui.TextWrapped($"{label}: {itemNames}");
                }
                break;
            }

            case DetailRowKind.SourceArrowFlow: {
                var left = row.SourceFlowLeftIds is { Count: > 0 } l ? string.Join(", ", l.Select(id => Item.GetRow(id).Name.ToString())) : string.Empty;
                var right = row.SourceItemIds is { Count: > 0 } r ? string.Join(", ", r.Select(id => Item.GetRow(id).Name.ToString())) : string.Empty;
                ImGui.TextWrapped(left.Length > 0 ? $"{left} → {right}" : right);
                break;
            }

            case DetailRowKind.SharedModelSet:
                if (row.SharedModelRow is not { } sharedRow)
                    break;
                var sharedLabel = $"{sharedRow.Title}##shared{sharedRow.Set.ItemId}_{row.SharedModelItemId}";
                if (ImGui.Selectable(sharedLabel)) {
                    if (row.SharedModelItemId != 0)
                        OnSharedModelItemLeftClick(row.SharedModelItemId, sharedRow.Set);
                    else
                        OnSharedModelSetLeftClick(sharedRow.Set);
                }
                ImGui.SameLine();
                ImGui.TextDisabled($"{RowStatusTag(sharedRow)}    {sharedRow.Subtitle}");
                break;
        }
    }

    private static string PieceTag(DetailListRowData row) {
        var tags = new List<string>();
        if (row.StorageKind is { } sk)
            tags.Add(sk switch { StorageKind.Armoire or StorageKind.ArmoireFaded => "Armoire", _ => "Dresser" });
        else if (row.ShowInventoryBadge)
            tags.Add("Inventory");
        if (row.ShowArmoireWarning)
            tags.Add("Misplaced");
        return tags.Count == 0 ? string.Empty : $"[{string.Join(", ", tags)}]";
    }

    private unsafe void DrawPieceContextMenuPopup() {
        if (!ImGui.BeginPopup("##LogPieceContextMenu"))
            return;

        if (_pieceContextMenuTarget is { ItemId: not 0 } row) {
            var itemId = row.ItemId;
            var itemName = Item.GetRow(itemId).Name.ToString();

            if (ImGui.Selectable(Addon.GetRow(4379).Text.ToString()))
                ItemFinderModule.Instance()->SearchForItem(itemId);
            if (ImGui.Selectable(Addon.GetRow(4697).Text.ToString())) {
                Svc.Chat.Print(SeString.CreateItemLink(itemId));
                AgentChatLog.Instance()->LinkItem(itemId);
            }
            if (ImGui.Selectable(Addon.GetRow(159).Text.ToString()))
                ImGui.SetClipboardText(itemName);
            if (ImGui.Selectable(Addon.GetRow(2426).Text.ToString()))
                AgentTryon.Instance()->TryOnSilent(itemId);

            if (Recipe.FirstOrNull(r => r.RowId > 0 && r.ItemResult.RowId == itemId) is { RowId: var recipeId }) {
                if (ImGui.Selectable(Addon.GetRow(1412).Text.ToString()))
                    AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipeId);
            }
        }

        ImGui.EndPopup();
    }

    private unsafe void DrawSourceContextMenuPopup() {
        if (!ImGui.BeginPopup("##LogSourceContextMenu"))
            return;

        if (_sourceContextMenuTarget is { } target) {
            if (target.Nav is { TerritoryTypeId: not 0 and var territoryId, WorldPosition: var pos } && Svc.Interface.IsPluginLoaded("vnavmesh")) {
                if (ImGui.Selectable("Navigate to location"))
                    Svc.Automation.Start(new NavToSourceTask(territoryId, pos));
            }

            if (target.CfcId != 0) {
                if (ImGui.Selectable(Addon.GetRow(15890).Text.ToString()))
                    AgentContentsFinder.Instance()->OpenRegularDuty(target.CfcId);
                if (ImGui.Selectable($"{Addon.GetRow(9663).Text} ({Addon.GetRow(1145).Text})")) {
                    if (ContentFinderCondition.GetRowRef(target.CfcId) is { IsValid: true, Value: var cfc })
                        cfc.QueueDuty(levelSync: true);
                }
                if (ImGui.Selectable($"{Addon.GetRow(9663).Text} ({Addon.GetRow(10008).Text})")) {
                    if (ContentFinderCondition.GetRowRef(target.CfcId) is { IsValid: true, Value: var cfc })
                        cfc.QueueDuty(levelSync: false);
                }
                if (Svc.Interface.IsPluginLoaded("AutoDuty") && ImGui.Selectable("AutoDuty"))
                    AutoDutyIpc.Get().FarmOutfit(target.CfcId);
            }
        }

        ImGui.EndPopup();
    }

    private sealed class NavToSourceTask(uint territoryTypeId, Vector3 worldPosition) : TaskBase {
        protected override async Task Execute()
            => await MoveTo(territoryTypeId, worldPosition, MovementConfig.Everything);
    }
}
