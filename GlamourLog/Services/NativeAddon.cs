using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GlamourLog.Services;

// clib's AtkUnitBase.IsAddonReady(string) calls FFXIVClientStructs' signature-scanned
// RaptureAtkUnitManager.GetAddonByName natively, which crashes (0xC0000005) on the Korean
// client (see crash-20260821001443, hit via the AutoDuty /glamourlog store IPC path). Dalamud's
// own IGameGui.GetAddonByName is a separately-implemented, already-safe lookup used elsewhere
// in this plugin (CabinetListHandler, CrystallizeNativeTree) without issue; IsReady itself is
// just a bitfield read on the returned pointer, no native call involved.
internal static class NativeAddon {
    internal static unsafe bool IsReady(string addonName)
        => Svc.GameGui.GetAddonByName<AtkUnitBase>(addonName) is not null and var addon && addon->IsReady;
}
