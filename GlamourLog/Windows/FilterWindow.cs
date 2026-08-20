using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using GlamourLog.Services;

namespace GlamourLog;

internal sealed class FilterWindow : Window {
    public const float WindowWidth = 300f;
    public const float WindowHeight = 0f; // auto

    public FilterWindow() : base("Set list filters##GlamourLogFilter") {
        Size = new Vector2(WindowWidth, WindowHeight);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void OpenOrToggleNear(Vector2 screenTopLeft) {
        if (IsOpen) {
            IsOpen = false;
            return;
        }

        Position = ClampFilterWindowTopLeft(screenTopLeft);
        PositionCondition = ImGuiCond.Always;
        IsOpen = true;
    }

    public void CloseIfOpen() => IsOpen = false;

    public static Vector2 ClampFilterWindowTopLeft(Vector2 origin) {
        var screen = ImGuiHelpers.MainViewport.Size;
        var maxX = Math.Max(0f, screen.X - WindowWidth);
        var maxY = Math.Max(0f, screen.Y - 400f);
        return new Vector2(
            Math.Clamp(origin.X, 0f, maxX),
            Math.Clamp(origin.Y, 0f, maxY));
    }

    public override void Draw() {
        PositionCondition = ImGuiCond.FirstUseEver;

        var changed = false;
        changed |= Checkbox("Hide completed", "Hide sets where every piece is owned", c => c.HideCompleted, c => c.HideCompleted ^= true);
        changed |= Checkbox("Hide incompatible items", "Hides all sets whose items are unwearable due to race or sex restrictions", c => c.HideIncompatible, c => c.HideIncompatible ^= true);
        changed |= Checkbox("Hide unobtainable", "Hide sets that cannot currently be obtained (seasonal/old series). Completed sets still show.", c => c.HideUnobtainable, c => c.HideUnobtainable ^= true);
        changed |= Checkbox("Hide mogstation", "Hide sets and pieces that come from the mogstation", c => c.HideMogstation, c => c.HideMogstation ^= true);
        changed |= Checkbox("Hide uncontributable", "Hide sets where no piece is in your inventory to contribute to the set", c => c.HideUnready, c => c.HideUnready ^= true);
        changed |= Checkbox("Hide shared models", "Hide outfits that share the same models. Will still show any sets that are started or completed.", c => c.HideSharedModels, c => c.HideSharedModels ^= true);
        changed |= Checkbox("Show only completed", "Show only sets where every piece is owned", c => c.ShowOnlyCompleted, c => c.ShowOnlyCompleted ^= true);
        changed |= Checkbox("Show only affordable sets", "Show only sets where you can afford the currency cost of all pieces", c => c.HideUnaffordable, c => c.HideUnaffordable ^= true);
        changed |= Checkbox("Show only tradeable", "Show only sets whose pieces can be bought on the marketboard or traded", c => c.HideNoMarketboard, c => c.HideNoMarketboard ^= true);
        changed |= Checkbox("Show only started", "Show only sets that are partially completed", c => c.HideNonPartials, c => c.HideNonPartials ^= true);
        changed |= Checkbox("Show only misplaced", "Show only sets that have pieces in the dresser that could be stored in the armoire", c => c.ShowOnlyMisplaced, c => c.ShowOnlyMisplaced ^= true);

        if (changed) {
            C.Save();
            CatalogService.Get().NotifyOwnershipChanged();
        }

        ImGui.Spacing();
        if (ImGui.Button("Close", new Vector2(-1f, 0f)))
            IsOpen = false;
    }

    private static bool Checkbox(string label, string tooltip, Func<Configuration, bool> read, Action<Configuration> flip) {
        var value = read(C);
        var clicked = ImGui.Checkbox(label, ref value);
        if (clicked)
            flip(C);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        return clicked;
    }
}
