using FFXIVClientStructs.FFXIV.Component.GUI;

namespace GlamourLog.Services;

// clib's AtkUnitBase.IsAddonReady(string) calls FFXIVClientStructs' signature-scanned
// RaptureAtkUnitManager.GetAddonByName natively, which crashes (0xC0000005) on the Korean
// client (see crash-20260821001443, hit via the AutoDuty /glamourlog store IPC path). Dalamud's
// own IGameGui.GetAddonByName is a separately-implemented, already-safe lookup used elsewhere
// in this plugin (CabinetListHandler, CrystallizeNativeTree) without issue.
//
// Mirrors clib's actual check (decompiled): addon != null && IsVisible && IsReady && IsFullyLoaded().
// IsVisible/IsReady are plain bitfield reads; IsFullyLoaded is a [VirtualFunction] (vtable dispatch
// off the live object), not a [MemberFunction] signature scan, so it isn't the same crash class as
// GetAddonByName — only that one call needed replacing to match clib's readiness check exactly.
internal static class NativeAddon {
    internal static unsafe bool IsReady(string addonName)
        => Svc.GameGui.GetAddonByName<AtkUnitBase>(addonName) is not null and var addon
        && addon->IsVisible && addon->IsReady && addon->IsFullyLoaded();
}
