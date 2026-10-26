namespace Scutum.T01.Domain.Assets;

public static class AssetLifecycle
{
    public static bool CanTransition(
        LifecycleStatus current,
        LifecycleStatus next)
    {
        switch (current)
        {
            case LifecycleStatus.Active:
                return next == LifecycleStatus.Inactive
                    || next == LifecycleStatus.Decommissioned;

            case LifecycleStatus.Inactive:
                return next == LifecycleStatus.Active
                    || next == LifecycleStatus.Decommissioned;

            default:
                return false;
        }
    }
}