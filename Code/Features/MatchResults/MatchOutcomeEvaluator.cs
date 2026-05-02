namespace SniperVsRunners.Features.MatchResults;

using System.Collections.Generic;
using SniperVsRunners.Teams;

/// <summary>
/// Déduit un résultat par joueur (gagné / perdu / neutre) à partir de la cause de fin et d’un snapshot figé.
/// </summary>
public static class MatchOutcomeEvaluator
{
	public static Dictionary<string, PlayerSubjectiveOutcome> Evaluate(
		MatchEndReason reason,
		IReadOnlyList<MatchParticipantSnapshot> participants
	)
	{
		var result = new Dictionary<string, PlayerSubjectiveOutcome>();

		foreach (var p in participants)
		{
			if (string.IsNullOrEmpty(p.StatsKey))
				continue;

			result[p.StatsKey] = p.Team == TeamTypes.Spectators
				? PlayerSubjectiveOutcome.Neutral
				: EvaluateParticipant(reason, p);
		}

		return result;
	}

	static PlayerSubjectiveOutcome EvaluateParticipant(MatchEndReason reason, MatchParticipantSnapshot p)
	{
		switch (reason)
		{
			case MatchEndReason.NotEnoughPlayers:
			case MatchEndReason.SniperDisconnected:
				return PlayerSubjectiveOutcome.Neutral;

			case MatchEndReason.AllRunnersDisconnected:
				if (p.Team == TeamTypes.Sniper)
					return PlayerSubjectiveOutcome.Win;
				if (p.Team == TeamTypes.Runners)
					return PlayerSubjectiveOutcome.Loss;
				return PlayerSubjectiveOutcome.Neutral;

			case MatchEndReason.AllRunnersEliminated:
				if (p.Team == TeamTypes.Sniper)
					return p.IsAlive ? PlayerSubjectiveOutcome.Win : PlayerSubjectiveOutcome.Loss;
				if (p.Team == TeamTypes.Runners)
					return PlayerSubjectiveOutcome.Loss;
				return PlayerSubjectiveOutcome.Neutral;

			case MatchEndReason.SniperEliminated:
				if (p.Team == TeamTypes.Sniper)
					return PlayerSubjectiveOutcome.Loss;
				if (p.Team == TeamTypes.Runners)
					return p.IsAlive ? PlayerSubjectiveOutcome.Win : PlayerSubjectiveOutcome.Loss;
				return PlayerSubjectiveOutcome.Neutral;

			case MatchEndReason.TimeoutSniperAlive:
				if (p.Team == TeamTypes.Sniper)
					return p.IsAlive ? PlayerSubjectiveOutcome.Win : PlayerSubjectiveOutcome.Loss;
				if (p.Team == TeamTypes.Runners)
					return PlayerSubjectiveOutcome.Loss;
				return PlayerSubjectiveOutcome.Neutral;

			default:
				return PlayerSubjectiveOutcome.Neutral;
		}
	}
}
