namespace GlamourLog.Windows.GuideWindow;

public partial class GuideWindow {
    private static readonly Page SettingsLogWindow = new() {
        CategoryTitle = "Settings",
        SubCategoryTitle = "Glamour Log Window",
        Blocks = [
            new CheckboxSettingBlock(
                "Persist search",
                "Keeps the search text when the Glamour Log window is closed and reopened.",
                () => C.PersistSearch,
                v => C.PersistSearch = v),
        ],
    };
}
