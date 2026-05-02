namespace SniperVsRunners.Features.Combat;

using System;

/// <summary>
/// Interprète une <see cref="Hitbox"/> (chaîne d’os) en <see cref="BodyHitZone"/>.
/// Couvre les conventions courantes du citizen Source 2 ; noms inconnus → false.
/// </summary>
public static class HitboxBodyZoneMapper
{
	public static bool TryMap(Hitbox hitbox, out BodyHitZone zone)
	{
		zone = BodyHitZone.Torso;

		if (hitbox is null)
			return false;

		for (var bone = hitbox.Bone; bone != null; bone = bone.Parent)
		{
			var name = bone.Name;
			if (string.IsNullOrEmpty(name))
				continue;

			var n = name.ToLowerInvariant();

			if (IsHeadBoneName(n))
			{
				zone = BodyHitZone.Head;
				return true;
			}

			if (IsLegBoneName(n))
			{
				zone = BodyHitZone.Leg;
				return true;
			}

			if (IsArmBoneName(n))
			{
				zone = BodyHitZone.Arm;
				return true;
			}

			if (IsTorsoBoneName(n))
			{
				zone = BodyHitZone.Torso;
				return true;
			}
		}

		return false;
	}

	static bool IsHeadBoneName(string n) =>
		n.Contains("head")
		|| n.Contains("jaw")
		|| n.Contains("teeth")
		|| n.Contains("eye")
		|| n.Contains("brow")
		|| n.Contains("ear")
		|| n.Contains("neck");

	static bool IsLegBoneName(string n) =>
		n.Contains("leg")
		|| n.Contains("thigh")
		|| n.Contains("calf")
		|| n.Contains("knee")
		|| n.Contains("ankle")
		|| n.Contains("foot")
		|| n.Contains("toe")
		|| n.Contains("ball");

	static bool IsArmBoneName(string n) =>
		n.Contains("clavicle")
		|| n.Contains("shoulder")
		|| n.Contains("upperarm")
		|| n.Contains("forearm")
		|| n.Contains("hand")
		|| n.Contains("wrist")
		|| n.Contains("finger")
		|| n.Contains("thumb")
		|| n.Contains("elbow")
		|| n.Contains("ulna")
		|| n.Contains("radius");

	static bool IsTorsoBoneName(string n) =>
		n.Contains("spine")
		|| n.Contains("pelvis")
		|| n.Contains("chest")
		|| n.Contains("stomach")
		|| n.Contains("torso")
		|| n.Contains("abdomen")
		|| n.Contains("hips");
}
