namespace SniperVsRunners.Features.PlayerStats;

using System.Collections.Generic;
using Sandbox;
using SniperVsRunners.Entities;
using SniperVsRunners.Features.MatchResults;
using SniperVsRunners.Features.Vitality;

/// <summary>
/// Hôte uniquement : stats persistantes + application fin de manche + éliminations.
/// </summary>
public sealed class PlayerStatsService
{
	readonly FileSystemPlayerStatsStore _store;
	readonly Dictionary<string, PlayerStatsRecord> _records = new();

	static bool IsStatsAuthority => !Networking.IsActive || Networking.IsHost;

	public PlayerStatsService(FileSystemPlayerStatsStore store)
	{
		_store = store;
	}

	public void LoadFromDisk()
	{
		if (!IsStatsAuthority)
			return;

		_records.Clear();
		foreach (var kv in _store.Load())
			_records[kv.Key] = kv.Value;
	}

	public void SaveToDisk()
	{
		if (!IsStatsAuthority)
			return;

		_store.Save(_records);
	}

	public PlayerStatsRecord GetRecordCopy(string statsKey)
	{
		if (string.IsNullOrEmpty(statsKey) || !_records.TryGetValue(statsKey, out var r))
			return new PlayerStatsRecord();

		return new PlayerStatsRecord
		{
			Wins = r.Wins,
			Losses = r.Losses,
			Eliminations = r.Eliminations
		};
	}

	PlayerStatsRecord GetOrCreate(string statsKey)
	{
		if (!_records.TryGetValue(statsKey, out var r))
		{
			r = new PlayerStatsRecord();
			_records[statsKey] = r;
		}

		return r;
	}

	/// <summary>
	/// Crédite une élimination au tireur (hôte, une fois par kill).
	/// </summary>
	public void RecordElimination(Connection killerConnection)
	{
		if (!IsStatsAuthority || killerConnection == null)
			return;

		var key = PlayerStatsIdentity.GetStorageKey(killerConnection);
		if (string.IsNullOrEmpty(key))
			return;

		GetOrCreate(key).Eliminations++;
		SaveToDisk();
	}

	/// <summary>
	/// Applique victoires / défaites subjectives pour la manche en cours.
	/// </summary>
	public void ApplyMatchOutcomes(MatchEndReason reason, IReadOnlyList<MatchParticipantSnapshot> participants)
	{
		if (!IsStatsAuthority || participants == null || participants.Count == 0)
			return;

		var outcomes = MatchOutcomeEvaluator.Evaluate(reason, participants);

		foreach (var p in participants)
		{
			if (string.IsNullOrEmpty(p.StatsKey))
				continue;

			if (!outcomes.TryGetValue(p.StatsKey, out var o))
				continue;

			var rec = GetOrCreate(p.StatsKey);
			if (o == PlayerSubjectiveOutcome.Win)
				rec.Wins++;
			else if (o == PlayerSubjectiveOutcome.Loss)
				rec.Losses++;
		}

		SaveToDisk();
	}

	/// <summary>
	/// Construit le snapshot à partir des joueurs connectés côté hôte au moment T.
	/// </summary>
	public static List<MatchParticipantSnapshot> BuildParticipantSnapshots(IReadOnlyList<PlayerEntity> players)
	{
		var list = new List<MatchParticipantSnapshot>();

		foreach (var player in players)
		{
			var key = PlayerStatsIdentity.GetStorageKey(player.Connection);
			var alive = IsPlayerAlive(player);
			list.Add(new MatchParticipantSnapshot(key, player.CurrentTeam, alive));
		}

		return list;
	}

	static bool IsPlayerAlive(PlayerEntity player)
	{
		var pawn = player.Pawn;
		if (!pawn.IsValid())
			return false;

		var v = pawn.Components.Get<PlayerVitalityComponent>();
		return v == null || !v.IsDead;
	}
}
