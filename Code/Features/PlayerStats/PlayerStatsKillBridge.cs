namespace SniperVsRunners.Features.PlayerStats;

using Sandbox;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Teams;

/// <summary>
/// Relie les morts par dégâts/saignement aux stats d’élimination (hôte).
/// </summary>
public static class PlayerStatsKillBridge
{
	public static void NotifyKillFromDamage(GameObject attackerRoot, GameObject victimRoot)
	{
		if (!Networking.IsActive || !Networking.IsHost)
			return;

		if (!attackerRoot.IsValid() || !victimRoot.IsValid())
			return;

		if (attackerRoot == victimRoot)
			return;

		var killerInfo = attackerRoot.Components.Get<PlayerCombatInfoComponent>();
		var victimInfo = victimRoot.Components.Get<PlayerCombatInfoComponent>();
		if (killerInfo == null || victimInfo == null)
			return;

		if (killerInfo.Team == TeamTypes.Spectators || victimInfo.Team == TeamTypes.Spectators)
			return;

		var killerConn = attackerRoot.Network?.Owner;
		var victimConn = victimRoot.Network?.Owner;
		if (killerConn == null || victimConn == null)
			return;

		if (killerConn.Id == victimConn.Id)
			return;

		PlayerStatsHost.Service?.RecordElimination(killerConn);
	}
}
