namespace SniperVsRunners.Features.MatchResults;

using SniperVsRunners.Teams;

/// <summary>
/// État d’un joueur au moment du gel de fin de manche (hôte).
/// </summary>
public readonly struct MatchParticipantSnapshot
{
	public MatchParticipantSnapshot(string statsKey, TeamTypes team, bool isAlive)
	{
		StatsKey = statsKey;
		Team = team;
		IsAlive = isAlive;
	}

	public string StatsKey { get; }
	public TeamTypes Team { get; }
	public bool IsAlive { get; }
}
