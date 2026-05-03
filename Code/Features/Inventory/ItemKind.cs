namespace SniperVsRunners.Features.Inventory;

/// <summary>
/// Types d’objets utilisables (0 = slot vide).
/// </summary>
public enum ItemKind : int
{
	None = 0,
	Medkit = 1,
	FragGrenade = 2,
	FlashGrenade = 3,
	/// <summary>Utiliser le slot : équipe l’USP (test visuel / arme).</summary>
	WeaponUsp = 4,
	/// <summary>Utiliser le slot : équipe le M700 (test visuel / arme).</summary>
	WeaponM700 = 5
}
