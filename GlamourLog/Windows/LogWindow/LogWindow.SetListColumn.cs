using System.ComponentModel;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GlamourLog.Services;
using GlamourLog.Windows.LogWindow;

namespace GlamourLog;

internal partial class LogWindow {
    private List<SetListRowData> _setListOptions = [];
    private GlamourSet? _setContextMenuTarget;

    private void RepopulateSetListFromFilteredRows() {
        var q = OwnershipService.Get().Query();
        var categoryRows = CategoryRows(_selectedCategoryId);
        SyncCurrencyFilterOptions();

        var searchTrimmed = string.IsNullOrWhiteSpace(_searchText) ? string.Empty : _searchText.Trim();
        var rows = SetListFilterSort.Apply(searchTrimmed, categoryRows, q, _currencyFilterItemId);

        _setListOptions = [.. rows.Select(set => BuildSetListRowData(set, q))];
    }

    private void SyncCurrencyFilterOptions() {
        var catalog = CatalogService.Get();
        _currencyFilterOptions = [.. catalog.GetCurrencyFilterItemIds(_selectedCategoryId == AllCategoryId ? null : _selectedCategoryId)];
        if (_currencyFilterItemId != 0 && !_currencyFilterOptions.Contains(_currencyFilterItemId))
            _currencyFilterItemId = 0;
    }

    private void DrawSetListColumn() {
        DrawSetListToolbar();
        ImGui.Separator();

        if (ImGui.BeginChild("##LogSetListRows")) {
            foreach (var row in _setListOptions)
                DrawSetListRow(row);
        }
        ImGui.EndChild();

        DrawSetContextMenuPopup();
    }

    private void DrawSetListToolbar() {
        ImGui.SetNextItemWidth(140f);
        var currentLabel = _currencyFilterItemId == 0 ? "All currencies" : Item.GetRow(_currencyFilterItemId).Name.ToString();
        if (ImGui.BeginCombo("##LogCurrencyFilter", currentLabel)) {
            if (ImGui.Selectable("All currencies", _currencyFilterItemId == 0))
                OnCurrencyFilterSelected(0);
            foreach (var currencyId in _currencyFilterOptions) {
                if (ImGui.Selectable(Item.GetRow(currencyId).Name.ToString(), _currencyFilterItemId == currencyId))
                    OnCurrencyFilterSelected(currencyId);
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.BeginCombo("##LogSortMode", SortModeLabel(C.SetListSortMode))) {
            foreach (var mode in Enum.GetValues<GlamourSetSortMode>()) {
                if (ImGui.Selectable(SortModeLabel(mode), mode == C.SetListSortMode))
                    OnSetListSortModeSelected(mode);
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        var directionIcon = C.SetListSortDirection == ListSortDirection.Ascending ? FontAwesomeIcon.SortAmountUp : FontAwesomeIcon.SortAmountDown;
        if (ImGuiComponents.IconButton(directionIcon))
            OnSetListSortDirectionToggle();

        ImGui.SameLine();
        var filterActive = _filterWindow.IsOpen;
        if (filterActive)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Filter))
            _filterWindow.OpenOrToggleNear(ComputeFilterWindowScreenOrigin());
        if (filterActive)
            ImGui.PopStyleColor();
    }

    private void DrawSetListRow(SetListRowData row) {
        var label = $"{row.Title}##set{row.Set.ItemId}_{row.IconItemId}";
        var selected = ReferenceEquals(_selectedSet, row.Set);
        if (ImGui.Selectable(label, selected)) {
            _selectedSet = row.Set;
            _selectedSourcePieceItemId = null;
            RefreshDetails();
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            _setContextMenuTarget = row.Set;
            ImGui.OpenPopup("##LogSetContextMenu");
        }

        ImGui.SameLine();
        ImGui.TextDisabled(RowStatusTag(row));

        ImGui.TextDisabled($"    {row.Subtitle}");
    }

    private static string SortModeLabel(GlamourSetSortMode mode) => mode switch {
        GlamourSetSortMode.Alphabetical => "Alphabetical",
        GlamourSetSortMode.ItemLevel => "Item level",
        GlamourSetSortMode.Patch => "Patch",
        _ => mode.ToString(),
    };

    private static string RowStatusTag(SetListRowData row) {
        var tags = new List<string>();
        if (row.IsOwned)
            tags.Add("Owned");
        if (row.ShowStorage)
            tags.Add(row.StorageKind switch {
                StorageKind.Armoire or StorageKind.ArmoireFaded => "Armoire",
                _ => "Dresser",
            });
        if (row.ShowArmoireWarning)
            tags.Add("Misplaced");
        if (row.IsUnobtainable)
            tags.Add("Unobtainable");
        if (row.IsMogstation)
            tags.Add("Mogstation");
        return tags.Count == 0 ? string.Empty : $"[{string.Join(", ", tags)}]";
    }

    private unsafe void DrawSetContextMenuPopup() {
        if (!ImGui.BeginPopup("##LogSetContextMenu"))
            return;

        if (_setContextMenuTarget is { } set && ImGui.Selectable("Try on (glamour plate)"))
            AgentTryon.Instance()->TryOnSilent(set.Items.ToArray());

        ImGui.EndPopup();
    }

    private void OnCurrencyFilterSelected(uint currencyItemId) {
        if (_currencyFilterItemId == currencyItemId)
            return;
        _currencyFilterItemId = currencyItemId;
        _dirty = true;
    }

    private SetListRowData BuildSetListRowData(GlamourSet set, OwnershipQuery q, bool appendNotInListSuffix = false) {
        var status = q.For(set);
        var subtitle = SetSublineText(status);
        if (appendNotInListSuffix) {
            var searchTrimmed = string.IsNullOrWhiteSpace(_searchText) ? string.Empty : _searchText.Trim();
            if (C.HideSharedModels && !SetListFilterSort.IsVisibleInSetList(set, searchTrimmed, CategoryRows(_selectedCategoryId), q, _currencyFilterItemId))
                subtitle += " · Not in list";
        }

        return new SetListRowData {
            Set = set,
            Title = set.Name,
            Subtitle = subtitle,
            IsOwned = status.IsComplete,
            IsUnobtainable = set.IsUnobtainable,
            IsMogstation = set.IsMogstation,
            ShowStorage = status.Storage is SetStorageState.Dresser or SetStorageState.Armoire,
            ShowArmoireWarning = status.ArmoireMisplaced,
            StorageKind = status.Storage == SetStorageState.Armoire ? StorageKind.Armoire : StorageKind.Dresser,
        };
    }

    // build a row for a lookalike item that may not have its own set
    private SetListRowData BuildSharedModelItemRow(uint itemId, OwnershipQuery q) {
        var catalog = CatalogService.Get();
        var set = catalog.FindCatalogSetForItem(itemId)
            ?? new GlamourSet { // fake a one-piece set so the normal row renderer gets reused
                ItemId = itemId,
                Name = Item.GetRow(itemId).Name.ToString(),
                CategoryName = null,
                IsUnobtainable = false,
                BaseIsUnobtainable = false,
                Items = [itemId],
                ItemLevel = Item.GetRow(itemId).LevelItem.RowId,
                PatchNo = 0m,
                NonSetCabinetPiece = true,
                IsIncompatible = false,
                IsMogstation = SetListFilterSort.IsMogstationItem(itemId),
                ModelSignature = SetModelSignature.ForMiscSingle(itemId),
                SharedModelGroupSize = 1,
                HasPartialSharedModels = false,
            };

        var piece = q.For(set).Piece(itemId);
        var location = piece?.Location ?? q.Locate(itemId);
        var ownedInStorage = location is PieceLocation.Armoire or PieceLocation.LooseDresser or PieceLocation.OutfitSlot;
        var ownedAnywhere = location is not PieceLocation.None;
        var subtitle = ownedInStorage ? "Obt. 1/1" : ownedAnywhere ? "In inventory" : "Obt. 0/1";

        var storageState = piece?.BadgeLocation ?? location switch {
            PieceLocation.Armoire => ItemStorageState.Armoire,
            PieceLocation.LooseDresser => ItemStorageState.DresserLoose,
            PieceLocation.OutfitSlot => ItemStorageState.DresserSet,
            _ => ItemStorageState.None,
        };

        var storageKind = storageState switch {
            ItemStorageState.Armoire => StorageKind.Armoire,
            ItemStorageState.DresserLoose => StorageKind.DresserFaded,
            ItemStorageState.DresserSet => StorageKind.Dresser,
            _ => StorageKind.Dresser,
        };

        return new SetListRowData {
            Set = set,
            Title = Item.GetRow(itemId).Name.ToString(),
            Subtitle = subtitle,
            IsOwned = ownedInStorage,
            IsUnobtainable = set.IsUnobtainable,
            IsMogstation = set.IsMogstation,
            ShowStorage = storageState is ItemStorageState.DresserSet or ItemStorageState.DresserLoose or ItemStorageState.Armoire,
            ShowArmoireWarning = piece?.ShowArmoireWarning ?? false,
            StorageKind = storageKind,
            IconItemId = itemId,
        };
    }

    private void OnSetListSortModeSelected(GlamourSetSortMode mode) {
        if (C.SetListSortMode == mode)
            return;
        C.SetListSortMode = mode;
        C.SetListSortDirection = mode.DefaultDirection();
        C.Save();
        _dirty = true;
    }

    private void OnSetListSortDirectionToggle() {
        C.SetListSortDirection = C.SetListSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        C.Save();
        _dirty = true;
    }

    private static string SetSublineText(SetStatus status) {
        var set = status.Set;
        var n = set.Items.Count;
        var c = status.OwnedCount;
        string core;
        if (set.NonSetCabinetPiece) {
            core = status.IsComplete ? "Obt. 1/1" : $"Obt. {c}/1";
        }
        else if (status.IsComplete)
            core = $"Obt. {n}/{n}";
        else if (n == 0)
            core = "Obt. 0/0";
        else if (c == n)
            core = "Completable"; // every piece owned, but at least one still needs storing
        else
            core = $"Obt. {c}/{n}";

        var sortHint = C.SetListSortMode switch {
            GlamourSetSortMode.Patch => set.PatchNo == 0m ? "Patch —" : $"Patch {set.PatchNo}",
            GlamourSetSortMode.ItemLevel => set.ItemLevel == 0 ? "iLvl —" : $"iLvl {set.ItemLevel}",
            _ => null,
        };
        return sortHint is null ? core : $"{core} · {sortHint}";
    }
}
