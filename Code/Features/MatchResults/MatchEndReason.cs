namespace SniperVsRunners.Features.MatchResults;

/// <summary>
/// Cause autoritaire de fin de manche (hôte). Sert aux résultats subjectifs et aux stats.
/// </summary>
public enum MatchEndReason
{
	NotEnoughPlayers,
	SniperDisconnected,
	AllRunnersDisconnected,
	AllRunnersEliminated,
	SniperEliminated,
	TimeoutSniperAlive
}
