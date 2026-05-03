namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

public static class WeaponSpread
{
	/// <summary>
	/// Applique une dispersion aléatoire dans un cône autour de <paramref name="forward"/>.
	/// </summary>
	public static Vector3 ApplyCone(Vector3 forward, float halfAngleDegrees)
	{
		if (halfAngleDegrees < 0.0001f)
			return forward;

		forward = forward.Normal;

		var right = Vector3.Cross(forward, Vector3.Up);
		if (right.Length < 0.001f)
			right = Vector3.Cross(forward, Vector3.Right);
		right = right.Normal;

		var up = Vector3.Cross(right, forward).Normal;

		var u = (float)(Random.Shared.NextDouble() * 2.0 - 1.0);
		var v = (float)(Random.Shared.NextDouble() * 2.0 - 1.0);
		var maxRad = halfAngleDegrees * (MathF.PI / 180f);
		var angleRad = (float)(Random.Shared.NextDouble() * maxRad);
		var off = (right * u + up * v).Normal * MathF.Tan(angleRad);
		return (forward + off).Normal;
	}
}
