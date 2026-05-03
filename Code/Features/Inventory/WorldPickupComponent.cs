namespace SniperVsRunners.Features.Inventory;

using Sandbox;

/// <summary>
/// Objet au sol ramassable (répliqué depuis l’hôte).
/// </summary>
public sealed class WorldPickupComponent : Component
{
	[Sync(SyncFlags.FromHost)]
	public ItemKind Kind { get; set; }

	[Sync(SyncFlags.FromHost)]
	public int Count { get; set; } = 1;

	/// <summary>Distance max pour « utiliser » ramassage côté hôte.</summary>
	public float PickupRadius { get; set; } = 72f;
}
