namespace SniperVsRunners.Features.Weapons;

using SniperVsRunners.Components.Game;
using SniperVsRunners.Teams;

/// <summary>Choisit l’ident d’arme au spawn à partir du <see cref="GameComponent"/> (loadout).</summary>
public static class WeaponSpawnIds
{
	public static string ResolvePrimaryIdent(bool forceEveryoneSame, TeamTypes team, GameComponent session)
	{
		if (session == null)
			return team == TeamTypes.Sniper ? "m700" : "usp";

		if (forceEveryoneSame)
		{
			if (session.DevEveryonePrimary != null)
				return session.DevEveryonePrimary.Ident.Trim();
			if (session.RunnerPrimary != null)
				return session.RunnerPrimary.Ident.Trim();
			return "usp";
		}

		if (team == TeamTypes.Sniper)
		{
			if (session.SniperPrimary != null)
				return session.SniperPrimary.Ident.Trim();
			return "m700";
		}

		if (session.RunnerPrimary != null)
			return session.RunnerPrimary.Ident.Trim();

		return "usp";
	}
}
