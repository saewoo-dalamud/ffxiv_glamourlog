namespace GlamourLog.Windows.LogWindow;

internal enum StorageKind {
    None,
    Armoire,
    ArmoireFaded,
    Dresser,
    DresserFaded,
}

internal sealed class SetListRowData {
    public required GlamourSet Set { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required bool IsOwned { get; init; }
    public required bool IsUnobtainable { get; init; }
    public required bool IsMogstation { get; init; }
    public required bool ShowStorage { get; init; }
    public bool ShowArmoireWarning { get; init; }
    public StorageKind StorageKind { get; init; } = StorageKind.Dresser;
    public uint IconItemId { get; init; } // row uses this item id instead of set token when non-zero
}

internal enum DetailRowKind {
    JournalHeader,
    EmptyHint,
    Piece,
    Cost,
    SourceDuty, // heading for a duty/fate source
    SourceChest,
    SourceArrowFlow,
    SharedModelSet,
}

internal sealed class DetailListRowData {
    public required DetailRowKind Kind { get; init; }
    public string PrimaryText { get; init; } = string.Empty;
    public string SecondaryText { get; init; } = string.Empty;
    public uint ItemId { get; init; }
    public bool IsSelected { get; init; }
    public StorageKind? StorageKind { get; init; }
    public bool ShowInventoryBadge { get; init; }
    public bool ShowArmoireWarning { get; init; }
    public uint ContentFinderConditionId { get; init; }
    public IReadOnlyList<uint>? SourceItemIds { get; init; }
    public uint CraftRecipeRowId { get; init; } // creates an open-recipe click when set
    public SourceNavigateTarget? NavigateTarget { get; init; }
    public string CostVendorTextTooltip { get; init; } = string.Empty;
    public string CostMapFlagLabel { get; init; } = string.Empty;
    public IReadOnlyList<uint>? SourceFlowLeftIds { get; init; } // left strip ids for SourceArrowFlow, right is SourceItemIds
    public SetListRowData? SharedModelRow { get; init; }
    public uint SharedModelItemId { get; init; } // shared model row represents this id for piece filter scope
    public uint DungeonChestRowId { get; init; } // duty chest row; left-click opens map marker
}

// A collapsible section of detail rows, optionally with nested child sections (mirrors the shape
// KamiToolKit.Classes.TreeListSection<T> had, without the KamiToolKit dependency).
internal sealed class DetailSection {
    public required string Header { get; init; }
    public List<DetailListRowData> Entries { get; init; } = [];
    public List<DetailSection>? Children { get; init; }
}
