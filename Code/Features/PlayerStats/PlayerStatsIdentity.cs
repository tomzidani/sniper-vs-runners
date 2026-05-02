namespace SniperVsRunners.Features.PlayerStats;

using Sandbox;

/// <summary>
/// Clé stable pour persistance : SteamId si disponible, sinon Guid de connexion.
/// </summary>
public static class PlayerStatsIdentity
{
	public static string GetStorageKey(Connection connection)
	{
		if (connection == null)
			return "unknown";

		var steam = connection.SteamId.ValueUnsigned;
		if (steam != 0)
			return $"steam_{steam}";

		return $"conn_{connection.Id:N}";
	}
}
