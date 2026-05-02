namespace SniperVsRunners.Features.PlayerStats;

using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Persistance hôte via <c>FileSystem.Data</c> (JSON unique).
/// </summary>
public sealed class FileSystemPlayerStatsStore
{
	public const string FileName = "player_stats.json";

	public Dictionary<string, PlayerStatsRecord> Load()
	{
		var map = new Dictionary<string, PlayerStatsRecord>();
		var dto = FileSystem.Data.ReadJsonOrDefault<PlayerStatsFileDto>(FileName);
		if (dto?.Entries == null)
			return map;

		foreach (var line in dto.Entries)
		{
			if (string.IsNullOrEmpty(line.Key))
				continue;

			map[line.Key] = new PlayerStatsRecord
			{
				Wins = line.Wins,
				Losses = line.Losses,
				Eliminations = line.Eliminations
			};
		}

		return map;
	}

	public void Save(IReadOnlyDictionary<string, PlayerStatsRecord> records)
	{
		var dto = new PlayerStatsFileDto { Version = 1 };
		foreach (var kv in records)
		{
			if (string.IsNullOrEmpty(kv.Key))
				continue;

			dto.Entries.Add(new PlayerStatsLineDto
			{
				Key = kv.Key,
				Wins = kv.Value.Wins,
				Losses = kv.Value.Losses,
				Eliminations = kv.Value.Eliminations
			});
		}

		FileSystem.Data.WriteJson(FileName, dto);
	}
}
