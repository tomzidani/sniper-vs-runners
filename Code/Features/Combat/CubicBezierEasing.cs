namespace SniperVsRunners.Features.Combat;

using System;

/// <summary>
/// Courbe d’accélération type CSS <c>cubic-bezier(x1,y1,x2,y2)</c> avec P0=(0,0) et P3=(1,1).
/// </summary>
public static class CubicBezierEasing
{
	/// <param name="linear">Avancement temporel normalisé [0..1].</param>
	/// <returns>Facteur d’interpolation [0..1] à utiliser dans un <c>Lerp</c>.</returns>
	public static float Sample(float linear, float x1, float y1, float x2, float y2)
	{
		linear = Math.Clamp(linear, 0f, 1f);
		if (linear <= 0f)
			return 0f;
		if (linear >= 1f)
			return 1f;

		x1 = Math.Clamp(x1, 0f, 1f);
		x2 = Math.Clamp(x2, 0f, 1f);

		var t = SolveTForX(linear, x1, x2);
		return BezierAxis(t, y1, y2);
	}

	static float BezierAxis(float t, float c1, float c2)
	{
		var u = 1f - t;
		return 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t;
	}

	static float SolveTForX(float x, float x1, float x2)
	{
		var lo = 0f;
		var hi = 1f;
		for (var i = 0; i < 28; i++)
		{
			var mid = (lo + hi) * 0.5f;
			if (BezierAxis(mid, x1, x2) < x)
				lo = mid;
			else
				hi = mid;
		}

		return (lo + hi) * 0.5f;
	}
}
