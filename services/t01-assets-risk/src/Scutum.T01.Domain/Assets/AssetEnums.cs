namespace Scutum.T01.Domain.Assets;

public enum AssetType
{
    Application = 1,
    Service = 2,
    Device = 3,
    Data = 4,
    Other = 5
}

public enum LifecycleStatus
{
    Active = 1,
    Inactive = 2,
    Decommissioned = 3
}