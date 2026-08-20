using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using GlamourLog.Services;
using GlamourLog.Windows.GuideWindow;
using GlamourLog.Windows.LogWindow;

namespace GlamourLog;

internal sealed partial class LogWindow : Window {
    private readonly FilterWindow _filterWindow;
    private const string AllCategoryId = "All";
    private const float CategoryColumnWidth = 200f;
    private const float SetListColumnWidth = 320f;
    private const float FooterHeight = 30f;

    private List<string> _categoryPaneOrder = [];
    private readonly Dictionary<string, (int Owned, int Total)> _categoryCounts = [];
    private string _selectedCategoryId = AllCategoryId;
    private string _searchText = string.Empty;
    private uint _currencyFilterItemId;
    private List<uint> _currencyFilterOptions = [];
    private GlamourSet? _selectedSet;
    private uint? _selectedSourcePieceItemId; // when set, costs/sources/lookalikes are narrowed to this piece
    private int _lastDataVersion = -1;
    private bool _dirty = true;

    public LogWindow(FilterWindow filterWindow) : base("Glamour Log##GlamourLog") {
        _filterWindow = filterWindow;
        Size = new Vector2(920f, 660f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnOpen() => _dirty = true;

    internal void RefreshListsAndDetails() => _dirty = true;

    public override void Draw() {
        SyncCategoryPaneToDataVersion();

        if (_dirty) {
            _dirty = false;
            RecomputeCategoryCounts();
            RepopulateSetListFromFilteredRows();
            RefreshDetails();
        }

        var columnsHeight = Math.Max(0f, ImGui.GetContentRegionAvail().Y - FooterHeight);

        if (ImGui.BeginChild("##LogCategoryColumn", new Vector2(CategoryColumnWidth, columnsHeight), true))
            DrawCategoryColumn();
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild("##LogSetListColumn", new Vector2(SetListColumnWidth, columnsHeight), true))
            DrawSetListColumn();
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild("##LogDetailColumn", new Vector2(0f, columnsHeight), true))
            DrawDetailColumn();
        ImGui.EndChild();

        DrawFooter();
    }

    private void DrawFooter() {
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Question, new Vector2(FooterHeight - 4f)))
            WindowsService.Get().ToggleMainMenuNearLogWindow();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Help and tweak settings");

        var q = OwnershipService.Get().Query();
        var mirageCatalogSets = CatalogService.Get().GlamourSets.Where(s => !s.NonSetCabinetPiece).ToList();
        var counts = q.CountCompletions(mirageCatalogSets);
        var spaceSaved = mirageCatalogSets.Where(s => q.For(s).IsComplete).Sum(x => x.Items.Count - 1);

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"Sets: {counts.OwnedObtainable}/{counts.TotalObtainable}    Dresser space saved: {spaceSaved}");
    }

    private void OnCategorySelected(string categoryId) {
        if (_selectedCategoryId == categoryId)
            return;
        _selectedCategoryId = categoryId;
        _currencyFilterItemId = 0;
        _selectedSet = null;
        _selectedSourcePieceItemId = null;
        _dirty = true;
    }

    private void DrawCategoryColumn() {
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputTextWithHint("##LogSearch", "Search...", ref _searchText, 128))
            _dirty = true;

        ImGui.Separator();

        foreach (var categoryId in _categoryPaneOrder) {
            var (owned, total) = _categoryCounts.TryGetValue(categoryId, out var c) ? c : (0, 0);
            if (ImGui.Selectable($"{categoryId}##cat", categoryId == _selectedCategoryId))
                OnCategorySelected(categoryId);
            ImGui.SameLine(CategoryColumnWidth - 60f);
            ImGui.TextDisabled($"{owned}/{total}");
        }
    }

    private List<string> BuildOrderedCategoryPaneList() {
        var r = new List<string> { AllCategoryId, CatalogService.Get().UncategorizedTab.Name };
        foreach (var (category, _) in CatalogService.Get().OutfitCategories.Select((c, ix) => (c, ix)).OrderBy(x => x.c.UiPriority).ThenBy(x => x.ix))
            r.Add(category.Name);
        r.Add(CatalogService.Get().MiscArmoireTab.Name);
        return r;
    }

    private IReadOnlyList<GlamourSet> CategoryRows(string categoryId)
        => categoryId == AllCategoryId ? CatalogService.Get().GlamourSets : CatalogService.Get().GlamourSetsByCategory.TryGetValue(categoryId, out var list) ? list : [];

    private void SyncCategoryPaneToDataVersion() {
        var catalog = CatalogService.Get();
        var dataVersion = catalog.DataVersion;
        if (_lastDataVersion == dataVersion && _categoryPaneOrder.Count > 0)
            return;

        _lastDataVersion = dataVersion;
        _categoryPaneOrder = BuildOrderedCategoryPaneList();
        if (!_categoryPaneOrder.Contains(_selectedCategoryId))
            _selectedCategoryId = catalog.UncategorizedTab.Name;
        _dirty = true;
    }

    private void RecomputeCategoryCounts() {
        var q = OwnershipService.Get().Query();
        _categoryCounts.Clear();
        foreach (var categoryId in _categoryPaneOrder) {
            var counts = q.CountCompletions(CategoryRows(categoryId));
            _categoryCounts[categoryId] = (counts.OwnedObtainable, counts.TotalObtainable);
        }
    }

    internal Vector2 ComputeMainMenuScreenOrigin() {
        var topLeft = Position ?? new Vector2(80f, 80f);
        var size = Size ?? new Vector2(920f, 660f);
        var center = topLeft + size * 0.5f;
        var origin = center - new Vector2(GuideWindow.WindowWidth, GuideWindow.WindowHeight) * 0.5f;
        return GuideWindow.ClampTopLeft(origin);
    }

    private Vector2 ComputeFilterWindowScreenOrigin() {
        var topLeft = Position ?? new Vector2(80f, 80f);
        var size = Size ?? new Vector2(920f, 660f);
        var center = topLeft + size * 0.5f;
        var origin = center - new Vector2(FilterWindow.WindowWidth, 400f) * 0.5f;
        return FilterWindow.ClampFilterWindowTopLeft(origin);
    }
}
