namespace SniperVsRunners.Features.PlayerStats;

/// <summary>
/// Compteurs persistés (propriétés requises pour WriteJson / ReadJson s&box).
/// </summary>
public sealed class PlayerStatsRecord
{
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int Eliminations { get; set; }
}
