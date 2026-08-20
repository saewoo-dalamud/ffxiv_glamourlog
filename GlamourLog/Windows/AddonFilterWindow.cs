using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using GlamourLog.Tweaks.Cabinet;
using GlamourLog.Tweaks.PrismBox;

namespace GlamourLog;

internal sealed record FilterOption(string Label, string Tooltip, Func<Configuration, bool> Read, Action<Configuration> Flip, System.Action? OnChanged = null);

internal enum AddonFilterKind {
    None,
    Armoire,
    Dresser,
}

internal sealed class AddonFilterWindow : Window {
    public const float WindowWidth = 380f;

    private FilterOption[] _options = [];

    public AddonFilterKind ActiveKind { get; private set; }

    public static FilterOption[] ArmoireOptions { get; } = [
        new(
            "Hide already deposited items",
            "When the armoire window is open, all entries that already exist inside the armoire will be hidden.",
            c => c.HideCabinetOwnedItems,
            c => c.HideCabinetOwnedItems ^= true,
            () => CabinetListHandler.Get().OnConfigChanged()),
        new(
            "Hide items in gearsets",
            "When the armoire window is open, all entries that are part of gearsets will be hidden",
            c => c.HideCabinetGearsetItems,
            c => c.HideCabinetGearsetItems ^= true,
            () => CabinetListHandler.Get().OnConfigChanged()),
    ];

    public static FilterOption[] DresserOptions { get; } = [
        new(
            "Hide already deposited items",
            "When the glamour creation window is open, items already in the glamour dresser (loose or in an outfit) are hidden.",
            c => c.HideCrystallizeOwnedItems,
            c => c.HideCrystallizeOwnedItems ^= true,
            () => CrystallizeListHandler.Get().OnConfigChanged()),
        new(
            "Hide armoire-eligible items",
            "When the glamour creation window is open, items that can be stored in the armoire are hidden (whether or not you already own them there).",
            c => c.HideCrystallizeArmoireEligibleItems,
            c => c.HideCrystallizeArmoireEligibleItems ^= true,
            () => CrystallizeListHandler.Get().OnConfigChanged()),
        new(
            "Hide non-outfit items",
            "When the glamour creation window is open, items that are not part of any outfit set are hidden.",
            c => c.HideCrystallizeNonOutfitItems,
            c => c.HideCrystallizeNonOutfitItems ^= true,
            () => CrystallizeListHandler.Get().OnConfigChanged()),
    ];

    public AddonFilterWindow() : base("Filters##GlamourLogAddonFilter") {
        Size = new Vector2(WindowWidth, 0f);
        SizeCondition = ImGuiCond.Always;
    }

    public void OpenOrToggleNear(AddonFilterKind kind, string title, FilterOption[] options, Vector2 screenTopLeft) {
        if (IsOpen && ActiveKind == kind) {
            IsOpen = false;
            return;
        }

        ActiveKind = kind;
        WindowName = $"{title}##GlamourLogAddonFilter";
        _options = options;
        Position = ClampTopLeft(screenTopLeft, new Vector2(WindowWidth, 200f));
        PositionCondition = ImGuiCond.Always;
        IsOpen = true;
    }

    public void CloseIfOpen() => IsOpen = false;

    public static Vector2 ClampTopLeft(Vector2 origin, Vector2 size) {
        var screen = ImGuiHelpers.MainViewport.Size;
        var maxX = Math.Max(0f, screen.X - size.X);
        var maxY = Math.Max(0f, screen.Y - size.Y);
        return new Vector2(Math.Clamp(origin.X, 0f, maxX), Math.Clamp(origin.Y, 0f, maxY));
    }

    public override void Draw() {
        PositionCondition = ImGuiCond.FirstUseEver;

        foreach (var option in _options) {
            var value = option.Read(C);
            if (ImGui.Checkbox(option.Label, ref value)) {
                option.Flip(C);
                C.Save();
                option.OnChanged?.Invoke();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(option.Tooltip);
        }

        ImGui.Spacing();
        if (ImGui.Button("Close", new Vector2(-1f, 0f)))
            IsOpen = false;
    }

    public override void OnClose() => ActiveKind = AddonFilterKind.None;
}
