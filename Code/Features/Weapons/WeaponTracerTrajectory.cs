namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

/// <summary>
/// Échantillonnage du segment monde du hitscan (start → end), identique au rayon de dégâts sync.
/// </summary>
public static class WeaponTracerTrajectory
{
	public static Vector3 SampleHitscanSegment(Vector3 start, Vector3 end, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return Vector3.Lerp(start, end, t);
	}
}
