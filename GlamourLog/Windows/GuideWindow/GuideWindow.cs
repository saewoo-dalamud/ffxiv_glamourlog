using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;

namespace GlamourLog.Windows.GuideWindow;

// Rendered with plain ImGui instead of KamiToolKit native nodes: this window doesn't need to
// look like a native game addon, and native node attachment is what crashes on some clients
// (see ExtraAddonButtons for the native-addon-attach case).
public sealed partial class GuideWindow : Window {
    public const float WindowWidth = 944f;
    public const float WindowHeight = 600f;

    private const float LeftColumnWidth = 200f;

    private int _expandedCategoryIndex;
    private Page _selectedPage = null!;

    public GuideWindow() : base("Help & Settings##GlamourLogGuide") {
        Size = new Vector2(WindowWidth, WindowHeight);
        SizeCondition = ImGuiCond.FirstUseEver;
        _selectedPage = NavCategories[0].Pages[0];
    }

    public void OpenOrToggleNear(Vector2 screenTopLeft) {
        if (IsOpen) {
            IsOpen = false;
            return;
        }

        Position = ClampTopLeft(screenTopLeft);
        PositionCondition = ImGuiCond.Always;
        IsOpen = true;
    }

    public void OpenOrToggleCentered() {
        var screen = ImGuiHelpers.MainViewport.Size;
        var topLeft = new Vector2(
            (screen.X - WindowWidth) * 0.5f,
            (screen.Y - WindowHeight) * 0.5f);
        OpenOrToggleNear(topLeft);
    }

    public void CloseIfOpen() => IsOpen = false;

    public static Vector2 ClampTopLeft(Vector2 origin) {
        var screen = ImGuiHelpers.MainViewport.Size;
        var maxX = Math.Max(0f, screen.X - WindowWidth);
        var maxY = Math.Max(0f, screen.Y - WindowHeight);
        return new Vector2(
            Math.Clamp(origin.X, 0f, maxX),
            Math.Clamp(origin.Y, 0f, maxY));
    }

    public override void Draw() {
        // one-shot position lands only on the frame OpenOrToggleNear set it
        PositionCondition = ImGuiCond.FirstUseEver;

        DrawSidebar();
        ImGui.SameLine();
        DrawRightPane();
    }

    private void DrawSidebar() {
        if (!ImGui.BeginChild("##GuideSidebar", new Vector2(LeftColumnWidth, 0), true)) {
            ImGui.EndChild();
            return;
        }

        for (var c = 0; c < NavCategories.Length; c++) {
            var category = NavCategories[c];
            var expanded = c == _expandedCategoryIndex;

            if (ImGui.Selectable(category.Title, expanded))
                _expandedCategoryIndex = c;

            if (!expanded)
                continue;

            ImGui.Indent();
            foreach (var page in category.Pages) {
                if (ImGui.Selectable(page.SubCategoryTitle, ReferenceEquals(page, _selectedPage)))
                    _selectedPage = page;
            }
            ImGui.Unindent();
        }

        ImGui.EndChild();
    }
}
