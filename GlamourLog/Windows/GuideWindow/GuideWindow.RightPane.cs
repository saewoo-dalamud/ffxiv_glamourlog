using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using KamiToolKit.Enums;

namespace GlamourLog.Windows.GuideWindow;

public partial class GuideWindow {
    private void DrawRightPane() {
        if (!ImGui.BeginChild("##GuideRightPane", Vector2.Zero, true)) {
            ImGui.EndChild();
            return;
        }

        ImGui.TextUnformatted(_selectedPage.SubCategoryTitle);
        ImGui.Separator();
        ImGui.Spacing();

        var blockIndex = 0;
        foreach (var block in _selectedPage.EnumerateBlocks()) {
            ImGui.PushID(blockIndex++);
            DrawBlock(block);
            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private void DrawBlock(ContentBlock block) {
        switch (block) {
            case GuideTextBlock text:
                if (text.TextLeftInset > 0f)
                    ImGui.Indent(text.TextLeftInset);
                ImGui.TextWrapped(text.Text.ExtractText());
                if (text.TextLeftInset > 0f)
                    ImGui.Unindent(text.TextLeftInset);
                break;

            case GuideHeadingBlock heading:
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), heading.Title);
                ImGui.Separator();
                break;

            case IconExampleBlock icon:
                ImGui.TextDisabled($"[{icon.Kind}]");
                ImGui.SameLine();
                ImGui.TextWrapped(icon.Description.ExtractText());
                break;

            case CircleButtonExampleBlock circle:
                ImGuiComponents.IconButton(CircleIconToFontAwesome(circle.Icon));
                ImGui.SameLine();
                ImGui.TextWrapped(circle.Description.ExtractText());
                break;

            case CircleButtonGalleryBlock:
                DrawCircleButtonGallery();
                break;

            case CheckboxSettingBlock setting:
                DrawCheckboxSetting(setting);
                break;

            case DataExportActionBlock export:
                if (ImGui.Button($"Copy {export.Format} to clipboard"))
                    CopyDataExportToClipboard(export.Format);
                break;
        }

        ImGui.Spacing();
    }

    private static void DrawCheckboxSetting(CheckboxSettingBlock setting) {
        var value = setting.Read();
        if (ImGui.Checkbox(setting.Label, ref value))
            setting.Write(value);

        if (!string.IsNullOrEmpty(setting.InfoTooltip) && ImGui.IsItemHovered())
            ImGui.SetTooltip(setting.InfoTooltip);
    }

    private static void DrawCircleButtonGallery() {
        foreach (var icon in Enum.GetValues<CircleButtonIcon>()) {
            ImGuiComponents.IconButton(icon.ToString(), CircleIconToFontAwesome(icon));
            ImGui.SameLine();
            ImGui.TextUnformatted(icon.ToString());
        }
    }

    private static FontAwesomeIcon CircleIconToFontAwesome(CircleButtonIcon icon) => icon switch {
        CircleButtonIcon.GearCog or CircleButtonIcon.ActiveGearCog or CircleButtonIcon.GearCogWithChatBubble => FontAwesomeIcon.Cog,
        CircleButtonIcon.Filter or CircleButtonIcon.ActiveFilter => FontAwesomeIcon.Filter,
        CircleButtonIcon.Chest or CircleButtonIcon.FlatbedCartBoxes => FontAwesomeIcon.Box,
        CircleButtonIcon.QuestionMark => FontAwesomeIcon.Question,
        CircleButtonIcon.Refresh or CircleButtonIcon.Update => FontAwesomeIcon.Sync,
        CircleButtonIcon.ChatBubble => FontAwesomeIcon.Comment,
        CircleButtonIcon.LeftArrow => FontAwesomeIcon.ArrowLeft,
        CircleButtonIcon.RightArrow => FontAwesomeIcon.ArrowRight,
        CircleButtonIcon.UpArrow => FontAwesomeIcon.ArrowUp,
        CircleButtonIcon.ArrowDown => FontAwesomeIcon.ArrowDown,
        CircleButtonIcon.Document or CircleButtonIcon.InsetDocument => FontAwesomeIcon.FileAlt,
        CircleButtonIcon.Edit or CircleButtonIcon.EditSmall => FontAwesomeIcon.Edit,
        CircleButtonIcon.Add => FontAwesomeIcon.Plus,
        CircleButtonIcon.Cross or CircleButtonIcon.CrossSmall => FontAwesomeIcon.Times,
        CircleButtonIcon.CheckedBox => FontAwesomeIcon.Check,
        CircleButtonIcon.Eye or CircleButtonIcon.EyeSmall => FontAwesomeIcon.Eye,
        CircleButtonIcon.Envelope => FontAwesomeIcon.Envelope,
        CircleButtonIcon.Volume => FontAwesomeIcon.VolumeUp,
        CircleButtonIcon.Mute => FontAwesomeIcon.VolumeMute,
        CircleButtonIcon.Globe => FontAwesomeIcon.Globe,
        CircleButtonIcon.MagnifyingGlass => FontAwesomeIcon.Search,
        CircleButtonIcon.Sword or CircleButtonIcon.WeaponDraw => FontAwesomeIcon.Crosshairs,
        CircleButtonIcon.Headgear => FontAwesomeIcon.HatWizard,
        CircleButtonIcon.Sprout => FontAwesomeIcon.Seedling,
        CircleButtonIcon.Dice => FontAwesomeIcon.Dice,
        CircleButtonIcon.MusicNote => FontAwesomeIcon.Music,
        CircleButtonIcon.PersonStanding => FontAwesomeIcon.User,
        CircleButtonIcon.PaintBucket => FontAwesomeIcon.FillDrip,
        CircleButtonIcon.Undo => FontAwesomeIcon.Undo,
        CircleButtonIcon.PinPaper => FontAwesomeIcon.Thumbtack,
        CircleButtonIcon.Emotes => FontAwesomeIcon.Smile,
        CircleButtonIcon.WavePulse => FontAwesomeIcon.Heartbeat,
        _ => FontAwesomeIcon.Circle,
    };
}
