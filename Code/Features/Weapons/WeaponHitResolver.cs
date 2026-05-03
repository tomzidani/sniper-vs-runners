namespace SniperVsRunners.Features.Weapons;

using SniperVsRunners.Features.Combat;

/// <summary>Résultat d’un impact hitscan à appliquer sur la vitalité.</summary>
public readonly struct WeaponHitOutcome
{
	public WeaponHitOutcome(bool instantKill, float healthDamage, float bleedPerSecondAdd, int legInjuryAdd)
	{
		InstantKill = instantKill;
		HealthDamage = healthDamage;
		BleedPerSecondAdd = bleedPerSecondAdd;
		LegInjuryAdd = legInjuryAdd;
	}

	public bool InstantKill { get; }
	public float HealthDamage { get; }
	public float BleedPerSecondAdd { get; }
	public int LegInjuryAdd { get; }
}

/// <summary>Calcule dégâts / saignement / blessure jambe à partir d’une <see cref="WeaponDefinition"/>.</summary>
public static class WeaponHitResolver
{
	public static WeaponHitOutcome Compute(WeaponDefinition w, BodyHitZone zone)
	{
		if (w == null)
			return default;

		return w.DamageStyle switch
		{
			WeaponDefinition.DamageStyleKind.SniperZones => ComputeSniper(w, zone),
			_ => ComputePistol(w, zone)
		};
	}

	static WeaponHitOutcome ComputePistol(WeaponDefinition w, BodyHitZone zone)
	{
		var dmg = zone switch
		{
			BodyHitZone.Head => w.PistolDamageHead,
			BodyHitZone.Torso => w.PistolDamageTorso,
			BodyHitZone.Arm => w.PistolDamageArm,
			_ => w.PistolDamageLeg
		};

		var bleed = zone switch
		{
			BodyHitZone.Head => w.PistolBleedHead,
			BodyHitZone.Torso => w.PistolBleedTorso,
			BodyHitZone.Arm => w.PistolBleedArm,
			_ => w.PistolBleedLeg
		};

		var leg = zone == BodyHitZone.Leg ? w.PistolLegInjuryOnLegHit : 0;
		return new WeaponHitOutcome(false, dmg, bleed, leg);
	}

	static WeaponHitOutcome ComputeSniper(WeaponDefinition w, BodyHitZone zone)
	{
		if (w.SniperHeadTorsoInstantKill && zone is BodyHitZone.Head or BodyHitZone.Torso)
			return new WeaponHitOutcome(true, 0f, 0f, 0);

		if (zone == BodyHitZone.Leg)
			return new WeaponHitOutcome(false, w.SniperLegDamage, w.SniperLegBleed, w.SniperLegInjury);

		if (zone == BodyHitZone.Arm)
			return new WeaponHitOutcome(false, w.SniperArmDamage, w.SniperArmBleed, 0);

		// Membre / jambe fallback : traiter comme bras pour les zones non tête/torse.
		return new WeaponHitOutcome(false, w.SniperArmDamage, w.SniperArmBleed, 0);
	}
}
