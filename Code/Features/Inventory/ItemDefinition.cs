namespace SniperVsRunners.Features.Inventory;

/// <summary>
/// Données statiques par type d’objet (fiche « définition »).
/// </summary>
public readonly struct ItemDefinition
{
	public ItemDefinition(
		string displayName,
		ItemUseMode useMode,
		int maxStack,
		float healAmount,
		float throwSpeed,
		float fragDamage,
		float fragRadius,
		float flashRadius,
		float flashDurationSeconds
	)
	{
		DisplayName = displayName;
		UseMode = useMode;
		MaxStack = maxStack;
		HealAmount = healAmount;
		ThrowSpeed = throwSpeed;
		FragDamage = fragDamage;
		FragRadius = fragRadius;
		FlashRadius = flashRadius;
		FlashDurationSeconds = flashDurationSeconds;
	}

	public string DisplayName { get; }
	public ItemUseMode UseMode { get; }
	public int MaxStack { get; }
	public float HealAmount { get; }
	public float ThrowSpeed { get; }
	public float FragDamage { get; }
	public float FragRadius { get; }
	public float FlashRadius { get; }
	public float FlashDurationSeconds { get; }

	public static ItemDefinition Get(ItemKind kind) => kind switch
	{
		ItemKind.Medkit => new ItemDefinition(
			"Kit de soin",
			ItemUseMode.ConsumeHeal,
			maxStack: 3,
			healAmount: 40f,
			throwSpeed: 0f,
			fragDamage: 0f,
			fragRadius: 0f,
			flashRadius: 0f,
			flashDurationSeconds: 0f
		),
		ItemKind.FragGrenade => new ItemDefinition(
			"Grenade",
			ItemUseMode.ThrowExplosive,
			maxStack: 3,
			healAmount: 0f,
			throwSpeed: 900f,
			fragDamage: 55f,
			fragRadius: 220f,
			flashRadius: 0f,
			flashDurationSeconds: 0f
		),
		ItemKind.FlashGrenade => new ItemDefinition(
			"Grenade flash",
			ItemUseMode.ThrowFlash,
			maxStack: 2,
			healAmount: 0f,
			throwSpeed: 850f,
			fragDamage: 0f,
			fragRadius: 0f,
			flashRadius: 400f,
			flashDurationSeconds: 2.2f
		),
		ItemKind.WeaponUsp => new ItemDefinition(
			"USP — équiper",
			ItemUseMode.EquipWeaponUsp,
			maxStack: 1,
			healAmount: 0f,
			throwSpeed: 0f,
			fragDamage: 0f,
			fragRadius: 0f,
			flashRadius: 0f,
			flashDurationSeconds: 0f
		),
		ItemKind.WeaponM700 => new ItemDefinition(
			"M700 — équiper",
			ItemUseMode.EquipWeaponM700,
			maxStack: 1,
			healAmount: 0f,
			throwSpeed: 0f,
			fragDamage: 0f,
			fragRadius: 0f,
			flashRadius: 0f,
			flashDurationSeconds: 0f
		),
		_ => default
	};

	public static bool IsDefined(ItemKind kind) => kind is ItemKind.Medkit or ItemKind.FragGrenade or ItemKind.FlashGrenade
		or ItemKind.WeaponUsp or ItemKind.WeaponM700;
}
