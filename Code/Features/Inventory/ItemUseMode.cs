namespace SniperVsRunners.Features.Inventory;

public enum ItemUseMode
{
	ConsumeHeal,
	ThrowExplosive,
	ThrowFlash,
	/// <summary>Équipe l’ident d’arme <c>usp</c> (ne consomme pas le slot).</summary>
	EquipWeaponUsp,
	/// <summary>Équipe l’ident d’arme <c>m700</c> (ne consomme pas le slot).</summary>
	EquipWeaponM700
}
