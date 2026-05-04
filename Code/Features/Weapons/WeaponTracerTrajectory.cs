namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

/// <summary>
/// Trajectoire tracer alignée sur le hitscan : segment droit, ou arc cubique (même départ / même impact)
/// avec chute visuelle pilotée par <see cref="WeaponDefinition.BulletDropIntensity"/> — pas de changement du point d’impact.
/// </summary>
public static class WeaponTracerTrajectory
{
	public static Vector3 SampleHitscanSegment(Vector3 start, Vector3 end, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return Vector3.Lerp(start, end, t);
	}

	public static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		var u = 1f - t;
		var uu = u * u;
		var tt = t * t;
		return p0 * (uu * u)
		       + p1 * (3f * uu * t)
		       + p2 * (3f * u * tt)
		       + p3 * (tt * t);
	}

	/// <summary>
	/// Cubique : tangente au départ = direction de tir (spread + drop) ; tangente d’approche tirée vers le bas dans le plan (corde, gravité).
	/// </summary>
	public static bool TryBuildStrictDropCubic(
		Vector3 startWorld,
		Vector3 endWorld,
		Vector3 shotForwardWorld,
		WeaponDefinition def,
		out Vector3 p0,
		out Vector3 p1,
		out Vector3 p2,
		out Vector3 p3)
	{
		p0 = startWorld;
		p3 = endWorld;
		p1 = startWorld;
		p2 = endWorld;

		var chord = endWorld - startWorld;
		var chordLen = chord.Length;
		if (chordLen < 0.001f)
			return false;

		var chordDir = chord / chordLen;
		var aim = shotForwardWorld.Length > 0.0001f ? shotForwardWorld.Normal : chordDir;

		var dropT = def != null ? Math.Clamp(def.BulletDropIntensity, 0f, 1f) : 0f;
		var maxPitchRad = def != null
			? def.BulletDropMaxPitchDegrees * (MathF.PI / 180f)
			: 0f;

		var horizDown = Vector3.Down - Vector3.Dot(Vector3.Down, chordDir) * chordDir;
		if (horizDown.Length < 0.001f)
			horizDown = Vector3.Cross(chordDir, aim);
		if (horizDown.Length < 0.001f)
			horizDown = Vector3.Right;

		var sagDir = horizDown.Normal;
		var sagMag = chordLen * MathF.Tan(dropT * maxPitchRad) * 0.38f;

		var arm1 = MathF.Min(chordLen * 0.32f, chordLen * 0.48f);
		p1 = startWorld + aim * arm1;
		p2 = endWorld - chordDir * (chordLen * 0.11f) + sagDir * sagMag;
		return true;
	}

	public static float ApproximateCubicBezierArcLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int segments = 96)
	{
		segments = Math.Max(8, segments);
		var prev = p0;
		var len = 0f;
		for (var i = 1; i <= segments; i++)
		{
			var t = i / (float)segments;
			var p = EvaluateCubic(p0, p1, p2, p3, t);
			len += (p - prev).Length;
			prev = p;
		}

		return len;
	}

	public static void BuildCumulativeArcLengthTable(
		Vector3 p0,
		Vector3 p1,
		Vector3 p2,
		Vector3 p3,
		int segments,
		float[] cumulativeOut)
	{
		segments = Math.Max(8, segments);
		cumulativeOut[0] = 0f;
		var prev = p0;
		for (var i = 1; i <= segments; i++)
		{
			var t = i / (float)segments;
			var p = EvaluateCubic(p0, p1, p2, p3, t);
			cumulativeOut[i] = cumulativeOut[i - 1] + (p - prev).Length;
			prev = p;
		}
	}

	/// <summary>
	/// <paramref name="arcFraction01"/> : fraction de la longueur d’arc totale (0 = départ, 1 = impact). Retourne le paramètre cubique t ∈ [0,1].
	/// </summary>
	public static float ArcLengthFractionToCurveParameter(float[] cumulative, int segments, float arcFraction01)
	{
		var total = cumulative[segments];
		if (total < 1e-6f)
			return Math.Clamp(arcFraction01, 0f, 1f);

		var target = Math.Clamp(arcFraction01, 0f, 1f) * total;
		if (target <= 0f)
			return 0f;
		if (target >= total - 1e-6f)
			return 1f;

		for (var i = 0; i < segments; i++)
		{
			if (cumulative[i + 1] < target)
				continue;

			var c0 = cumulative[i];
			var c1 = cumulative[i + 1];
			var t0 = i / (float)segments;
			var t1 = (i + 1) / (float)segments;
			if (c1 <= c0 + 1e-8f)
				return t0;

			var w = (target - c0) / (c1 - c0);
			return t0 + w * (t1 - t0);
		}

		return 1f;
	}
}
