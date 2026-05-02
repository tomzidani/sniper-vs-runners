namespace SniperVsRunners.Features.Combat;

using SniperVsRunners.Teams;

/// <summary>
/// Métadonnées combat répliquées (équipe sur le pawn pour le client et l’hôte).
/// </summary>
public sealed class PlayerCombatInfoComponent : Component
{
	[Sync(SyncFlags.FromHost)]
	public TeamTypes Team { get; set; } = TeamTypes.Spectators;
}
