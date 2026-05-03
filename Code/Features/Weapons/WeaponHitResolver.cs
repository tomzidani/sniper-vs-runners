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

/// <summary>Calcule dégâts / saignement / blessure jambe à partir du profil par zone de <see cref="WeaponDefinition"/>.</summary>
public static class WeaponHitResolver
{
	public static WeaponHitOutcome Compute(WeaponDefinition w, BodyHitZone zone)
	{
		if (w == null)
			return default;

		w.GetZoneCombat(zone, out var health, out var bleed, out var leg, out var instant);
		if (instant)
			return new WeaponHitOutcome(true, 0f, 0f, 0);

		return new WeaponHitOutcome(false, health, bleed, leg);
	}
}
