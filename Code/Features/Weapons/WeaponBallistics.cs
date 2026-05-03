namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

/// <summary>
/// Direction de tir : dispersion + chute balistique (hitscan). La prévisualisation dev utilise la même chute sans RNG.
/// </summary>
public static class WeaponBallistics
{
	/// <summary>Tir réel : cône aléatoire puis chute selon <see cref="WeaponDefinition.BulletDropIntensity"/>.</summary>
	public static Vector3 ComputeFireDirection(Vector3 forwardWorld, WeaponDefinition def)
	{
		if (def == null)
			return forwardWorld.Normal;

		var f = WeaponSpread.ApplyCone(forwardWorld, def.SpreadHalfAngleDegrees);
		return ApplyBulletDrop(f, def);
	}

	/// <summary>Visée dev / HUD : pas de spread, même chute que le tir (centre du cône).</summary>
	public static Vector3 ComputePreviewDirection(Vector3 forwardWorld, WeaponDefinition def)
	{
		if (def == null)
			return forwardWorld.Normal;

		return ApplyBulletDrop(forwardWorld.Normal, def);
	}

	static Vector3 ApplyBulletDrop(Vector3 forward, WeaponDefinition def)
	{
		var t = Math.Clamp(def.BulletDropIntensity, 0f, 1f);
		if (t < 0.0001f)
			return forward.Normal;

		var dropDeg = t * def.BulletDropMaxPitchDegrees;
		var look = Rotation.LookAt(forward.Normal, Vector3.Up);
		var ang = look.Angles();
		ang.pitch += dropDeg;
		return ang.ToRotation().Forward.Normal;
	}
}
