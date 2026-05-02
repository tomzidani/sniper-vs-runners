namespace SniperVsRunners.Features.PlayerStats;

using System.Collections.Generic;

/// <summary>
/// Enveloppe fichier unique sous FileSystem.Data (s&box).
/// </summary>
public sealed class PlayerStatsFileDto
{
	public int Version { get; set; } = 1;
	public List<PlayerStatsLineDto> Entries { get; set; } = new();
}

public sealed class PlayerStatsLineDto
{
	public string Key { get; set; } = "";
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int Eliminations { get; set; }
}
