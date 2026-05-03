namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

/// <summary>
/// Sons 3D optionnels (chemins ressource .sound assignés sur <see cref="WeaponDefinition"/>).
/// </summary>
public static class WeaponFx
{
	public static void PlayPrimaryFire(WeaponDefinition def, Vector3 worldPosition)
	{
		if (def == null)
			return;

		TryPlaySound(def.PrimaryFireSoundPath, worldPosition, def.PrimaryFireSoundVolume, def.PrimaryFireHearingRange);
	}

	public static void PlayReload(WeaponDefinition def, Vector3 worldPosition)
	{
		if (def == null)
			return;

		TryPlaySound(def.ReloadSoundPath, worldPosition, def.ReloadSoundVolume, def.ReloadHearingRange);
	}

	static void TryPlaySound(string path, Vector3 worldPosition, float volume, float hearingRange)
	{
		if (string.IsNullOrWhiteSpace(path))
			return;

		try
		{
			var handle = Sound.Play(path.Trim(), worldPosition);
			handle.Volume = Math.Clamp(volume, 0f, 2f);
			if (hearingRange > 1f)
				handle.Distance = hearingRange;
		}
		catch (Exception e)
		{
			Log.Warning($"WeaponFx: son « {path} » : {e.Message}");
		}
	}
}
