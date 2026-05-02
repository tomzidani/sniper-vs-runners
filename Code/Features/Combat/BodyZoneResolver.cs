namespace SniperVsRunners.Features.Combat;

using System;

/// <summary>
/// Résout la zone touchée : d’abord hitbox / os du trace, sinon enveloppe du personnage + heuristique latérale.
/// </summary>
public static class BodyZoneResolver
{
	/// <summary>Résolution complète à partir du résultat de trace (préféré).</summary>
	public static BodyHitZone Resolve(GameObject victimRoot, SceneTraceResult trace)
	{
		if (!victimRoot.IsValid())
			return BodyHitZone.Torso;

		var hitWorld = trace.Hit ? trace.HitPosition : trace.EndPosition;

		if (trace.Hitbox is { } hb && HitboxBodyZoneMapper.TryMap(hb, out var fromHitbox))
			return fromHitbox;

		return ResolveFromBodyBounds(victimRoot, hitWorld);
	}

	/// <summary>Repli si vous n’avez qu’une position monde (tests, impacts sans hitbox).</summary>
	public static BodyHitZone ResolveFromWorldHit(GameObject victim, Vector3 hitWorld) =>
		ResolveFromBodyBounds(victim, hitWorld);

	static BodyHitZone ResolveFromBodyBounds(GameObject victim, Vector3 hitWorld)
	{
		var box = victim.GetBounds();
		var heightT = HeightFractionAlongCharacterUp(victim, box, hitWorld);

		var lateral = GetNormalizedLateralSeparation(victim, hitWorld);

		// Bras : mi-hauteur + assez sur le côté (citizen debout).
		if (heightT is > 0.38f and < 0.78f && lateral > 0.42f)
			return BodyHitZone.Arm;

		if (heightT > 0.82f)
			return BodyHitZone.Head;
		if (heightT < 0.34f)
			return BodyHitZone.Leg;
		return BodyHitZone.Torso;
	}

	/// <summary>0 = bas du volume, 1 = haut, selon l’axe « debout » du pawn.</summary>
	static float HeightFractionAlongCharacterUp(GameObject victim, BBox box, Vector3 hitWorld)
	{
		var axis = (victim.WorldRotation * Vector3.Up).Normal;

		var minP = float.MaxValue;
		var maxP = float.MinValue;
		foreach (var corner in box.Corners)
		{
			var p = Vector3.Dot(corner, axis);
			if (p < minP)
				minP = p;
			if (p > maxP)
				maxP = p;
		}

		var span = Math.Max(0.0001f, maxP - minP);
		var ph = Vector3.Dot(hitWorld, axis);
		return Math.Clamp((ph - minP) / span, 0f, 1f);
	}

	/// <summary>0 = axe du corps, 1 = sur le côté (coordonnées locales).</summary>
	static float GetNormalizedLateralSeparation(GameObject victim, Vector3 hitWorld)
	{
		var local = victim.WorldTransform.PointToLocal(hitWorld);
		var pc = victim.Components.Get<PlayerController>();
		var halfW = pc?.BodyRadius > 0.5f ? pc.BodyRadius : 16f;
		var ax = Math.Abs(local.x);
		return Math.Clamp(ax / Math.Max(1f, halfW), 0f, 2f);
	}
}
