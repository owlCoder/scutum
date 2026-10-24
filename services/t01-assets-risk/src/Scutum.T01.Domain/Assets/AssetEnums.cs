// PRIVREMENA KOPIJA iz plana KT1 (enum-i, Gajic).
// Posle merge-a feature/R1-02-model: rebase i obrisati ovaj fajl.
namespace Scutum.T01.Domain.Assets;

public enum AssetType { Application = 1, Service = 2, Device = 3, Data = 4, Other = 5 }
public enum LifecycleStatus { Active = 1, Inactive = 2, Decommissioned = 3 }
