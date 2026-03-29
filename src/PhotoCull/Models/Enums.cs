namespace PhotoCull.Models;

public enum CullStatus
{
    Unreviewed,
    Rejected,
    Selected
}

public enum GroupType
{
    Burst,
    Scene,
    Single
}

public enum Round
{
    QuickCull,
    GroupPick
}

public enum AppPhase
{
    License,
    Import,
    QuickCull,
    GroupPick,
    Export
}

public enum QuickCullTab
{
    Rejected,
    Kept
}

public enum LicenseStatus
{
    Valid,
    Expired,
    ActivationWindowExpired,
    Invalid
}

public enum LicenseType
{
    Trial,
    Permanent
}

public enum ExportLabel
{
    Green,
    Red,
    Yellow,
    Blue,
    Purple
}
