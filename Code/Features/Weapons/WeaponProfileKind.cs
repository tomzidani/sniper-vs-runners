namespace SniperVsRunners.Features.Weapons;

/// <summary>
/// Profil d’arme pour les tables de dégâts / létalité (données gameplay, pas le modèle 3D).
/// </summary>
public enum WeaponProfileKind
{
	/// <summary>Tir précis, torse/tête létaux, membres = blessure lourde.</summary>
	SniperRifle,

	/// <summary>Arme secondaire / futur fusil : dégâts modérés, saignement possible.</summary>
	Sidearm
}
