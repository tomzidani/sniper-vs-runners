namespace SniperVsRunners.Features.Weapons;

using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Citizen;
using SniperVsRunners.Features.Combat;

/// <summary>
/// Fiche d’arme (données combat + visuel). Fichiers <c>.weapon</c> sous <c>Assets/weapons/...</c>.
/// Les propriétés sont regroupées dans l’inspecteur via l’attribut <c>[Group(…)]</c>.
/// </summary>
[AssetType(Name = "Weapon Definition", Extension = "weapon", Category = "svr")]
public partial class WeaponDefinition : GameResource
{
	static readonly Dictionary<string, WeaponDefinition> ByIdent = new();

	public static IReadOnlyDictionary<string, WeaponDefinition> AllByIdent => ByIdent;

	// --- General ---

	/// <summary>Ident stable (sync réseau, registre).</summary>
	[Property, Group("General")]
	public string Ident { get; set; } = "unnamed";

	[Property, Group("General")]
	public string DisplayName { get; set; } = "Arme";

	// --- Combat ---

	[Property, Group("Combat")]
	public float FireCooldownSeconds { get; set; } = 0.35f;

	[Property, Group("Combat")]
	public float MaxRange { get; set; } = 10_000f;

	/// <summary>Demi-angle de cône de dispersion (0 = rayon parfait).</summary>
	[Property, Group("Combat")]
	public float SpreadHalfAngleDegrees { get; set; }

	// --- Ballistics (hitscan) ---

	/// <summary>0 = pas de chute ; 1 = pleine chute (pitch max <see cref="BulletDropMaxPitchDegrees"/>).</summary>
	[Property, Group("Ballistics")]
	public float BulletDropIntensity { get; set; }

	/// <summary>À intensité 1 : rotation vers le bas (degrés) appliquée au rayon après le spread.</summary>
	[Property, Group("Ballistics")]
	public float BulletDropMaxPitchDegrees { get; set; } = 2.5f;

	// --- Ammo ---

	[Property, Group("Ammo")]
	public int MagazineSize { get; set; } = 12;

	/// <summary>Munitions hors chargeur à l’équipement / au spawn (ignoré si <see cref="InfiniteReserve"/>).</summary>
	[Property, Group("Ammo")]
	public int StartingReserveAmmo { get; set; } = 36;

	[Property, Group("Ammo")]
	public float ReloadTimeSeconds { get; set; } = 1.75f;

	/// <summary>Si vrai : la réserve est illimitée (HUD « ∞ ») ; le rechargement remplit toujours le chargeur.</summary>
	[Property, Group("Ammo")]
	public bool InfiniteReserve { get; set; }

	/// <summary>Après le dernier tir du chargeur, tente un rechargement automatique si la réserve le permet.</summary>
	[Property, Group("Ammo")]
	public bool AutoReloadWhenEmpty { get; set; } = true;

	// --- Damage — Head ---

	[Property, Group("Damage — Head")]
	public float ZoneHeadHealthDamage { get; set; } = 55f;

	[Property, Group("Damage — Head")]
	public float ZoneHeadBleedPerSecond { get; set; } = 4f;

	[Property, Group("Damage — Head")]
	public int ZoneHeadLegInjuryAdd { get; set; }

	[Property, Group("Damage — Head")]
	public bool ZoneHeadInstantKill { get; set; }

	// --- Damage — Torso ---

	[Property, Group("Damage — Torso")]
	public float ZoneTorsoHealthDamage { get; set; } = 32f;

	[Property, Group("Damage — Torso")]
	public float ZoneTorsoBleedPerSecond { get; set; } = 2.5f;

	[Property, Group("Damage — Torso")]
	public int ZoneTorsoLegInjuryAdd { get; set; }

	[Property, Group("Damage — Torso")]
	public bool ZoneTorsoInstantKill { get; set; }

	// --- Damage — Arm ---

	[Property, Group("Damage — Arm")]
	public float ZoneArmHealthDamage { get; set; } = 26f;

	[Property, Group("Damage — Arm")]
	public float ZoneArmBleedPerSecond { get; set; } = 2f;

	[Property, Group("Damage — Arm")]
	public int ZoneArmLegInjuryAdd { get; set; }

	[Property, Group("Damage — Arm")]
	public bool ZoneArmInstantKill { get; set; }

	// --- Damage — Leg ---

	[Property, Group("Damage — Leg")]
	public float ZoneLegHealthDamage { get; set; } = 22f;

	[Property, Group("Damage — Leg")]
	public float ZoneLegBleedPerSecond { get; set; } = 1.5f;

	[Property, Group("Damage — Leg")]
	public int ZoneLegLegInjuryAdd { get; set; } = 1;

	[Property, Group("Damage — Leg")]
	public bool ZoneLegInstantKill { get; set; }

	// --- Audio ---

	[Property, Group("Audio")]
	public string PrimaryFireSoundPath { get; set; } = "";

	[Property, Group("Audio")]
	public float PrimaryFireSoundVolume { get; set; } = 1f;

	[Property, Group("Audio")]
	public float PrimaryFireHearingRange { get; set; } = 3500f;

	[Property, Group("Audio")]
	public string ReloadSoundPath { get; set; } = "";

	[Property, Group("Audio")]
	public float ReloadSoundVolume { get; set; } = 0.85f;

	[Property, Group("Audio")]
	public float ReloadHearingRange { get; set; } = 2400f;

	// --- FX (tracers) ---

	/// <summary>
	/// <see cref="TracerTrajectoryStyle.StrictHitscanRay"/> : segment droit identique au rayon hitscan sync, durée <c>distance / <see cref="TracerVisualSpeed"/></c> (bornée).<br/>
	/// <see cref="TracerTrajectoryStyle.LegacyBezier"/> : ancienne courbe + bornes sur <see cref="TracerDrawSeconds"/>.
	/// </summary>
	public enum TracerTrajectoryStyle
	{
		StrictHitscanRay,
		LegacyBezier
	}

	[Property, Group("FX — Tracer")]
	public TracerTrajectoryStyle TracerTrajectory { get; set; } = TracerTrajectoryStyle.StrictHitscanRay;

	/// <summary>Mode strict : plafond bas durée de vol (s). Mode legacy : ignoré.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerFlightTimeMinSeconds { get; set; } = 0.015f;

	/// <summary>Mode strict : plafond haut durée de vol (s). Mode legacy : ignoré.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerFlightTimeMaxSeconds { get; set; } = 2.5f;

	/// <summary>Mode legacy uniquement : base pour les bornes de durée (0 = tracer désactivé en legacy).</summary>
	[Property, Group("FX — Tracer")]
	public float TracerDrawSeconds { get; set; } = 0.075f;

	[Property, Group("FX — Tracer")]
	public Color TracerColor { get; set; } = new Color(1f, 0.92f, 0.35f, 0.9f);

	/// <summary>Épaisseur monde du faisceau (modèle dev/box étiré).</summary>
	[Property, Group("FX — Tracer")]
	public float TracerWorldThickness { get; set; } = 0.22f;

	/// <summary>
	/// Vitesse du segment lumineux (unités monde / s). Mode strict : temps de vol ≈ <c>distance / vitesse</c> (borné min/max). Mode legacy : plancher 200 u/s pour le calcul de durée.
	/// </summary>
	[Property, Group("FX — Tracer")]
	public float TracerVisualSpeed { get; set; } = 13_000f;

	/// <summary>Longueur monde du segment visible (traînée) le long du trajet.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerSegmentWorldLength { get; set; } = 22f;

	/// <summary>
	/// Avec <see cref="TracerTrajectoryStyle.StrictHitscanRay"/> : retarde l’application des dégâts sur joueur à l’hôte du même délai que le temps de vol du tracer (<see cref="WeaponTracerBeam.ComputeTracerFlightSeconds"/>), pour que l’impact visuel coïncide avec le hit.<br/>
	/// Ignoré en <see cref="TracerTrajectoryStyle.LegacyBezier"/> (dégâts instantanés ; trajectoire FX ≠ rayon hitscan).
	/// </summary>
	[Property, Group("FX — Tracer")]
	public bool DelayHitscanDamageUntilTracerImpact { get; set; } = true;

	// --- FX (muzzle flash) — uniquement prefab ; rendu = contenu du prefab ---

	/// <summary>
	/// Prefab racine à cloner au point d’émission (particules, sprite, etc.). Vide = pas de flash de bouche.
	/// </summary>
	[ResourceType("prefab")]
	[Property, Group("FX — Muzzle flash")]
	public string MuzzleFlashPrefab { get; set; } = "";

	/// <summary>
	/// Si vrai : le clone du flash <strong>n’est pas auto-détruit</strong> après le tir (aucun <c>TimedDestroyComponent</c>).
	/// À activer seulement pour régler offsets / angles en jeu sous différentes vues ; laisser à <c>false</c> en production (sinon les clones s’accumulent).
	/// </summary>
	[Property, Group("FX — Muzzle flash")]
	public bool MuzzleFlashPersistForTuning { get; set; }

	/// <summary>
	/// Décalage dans l’espace <strong>local du HeldVisual 1P</strong> (mesh viewmodel). Le clone du flash est enfant de <c>FirstPersonHeldWeapon</c> ;
	/// cette valeur positionne le canon par rapport au mesh.
	/// </summary>
	[Property, Group("FX — Muzzle flash")]
	public Vector3 MuzzleFlashFirstPersonLocalOffset { get; set; }

	/// <summary>Rotation monde du flash au tir : appliquée après alignement sur la direction du tir (<c>LookAt</c> × ces angles).</summary>
	[Property, Group("FX — Muzzle flash")]
	public Angles MuzzleFlashFirstPersonLocalAngles { get; set; }

	/// <summary>Décalage dans l’espace <strong>local du HeldVisual monde</strong> (mesh <c>HeldWeapon</c> / 3P).</summary>
	[Property, Group("FX — Muzzle flash")]
	public Vector3 MuzzleFlashThirdPersonLocalOffset { get; set; }

	/// <summary>Rotation monde du flash au tir (3P), même convention que la 1P.</summary>
	[Property, Group("FX — Muzzle flash")]
	public Angles MuzzleFlashThirdPersonLocalAngles { get; set; }

	// --- World visual (third person) ---

	/// <summary>Modèle affiché sur le pawn (HeldWeapon). Préférer un <c>w_*.vmdl</c> monde ; un <c>v_*.vmdl</c> viewmodel est automatiquement remplacé par <see cref="FallbackWorldModel"/> si défini.</summary>
	[ResourceType("vmdl")]
	[Property, Group("World visual (third person)")]
	public string PrimaryModel { get; set; } = "";

	/// <summary>Modèle monde de secours ; sert aussi de remplacement si <see cref="PrimaryModel"/> pointe vers un viewmodel <c>v_</c>.</summary>
	[ResourceType("vmdl")]
	[Property, Group("World visual (third person)")]
	public string FallbackWorldModel { get; set; } = "";

	[Property, Group("World visual (third person)")]
	public bool UseAnimGraphOnPrimary { get; set; }

	[Property, Group("World visual (third person)")]
	public Vector3 HeldLocalPosition { get; set; }

	[Property, Group("World visual (third person)")]
	public Angles HeldLocalAngles { get; set; }

	[Property, Group("World visual (third person)")]
	public float HeldLocalScale { get; set; } = 1f;

	/// <summary>
	/// Os utilisé pour ancrer <c>HeldWeapon</c> en 3P (suivi d’os). Préférer <c>hold_R</c> sur le Citizen : os d’attache des objets tenus. Vide = liste par défaut du code (commence par <c>hold_R</c>).
	/// </summary>
	[Property, Group("World visual (third person)")]
	public string WorldHeldHandBoneName { get; set; } = "hold_R";

	/// <summary>
	/// Transform <strong>local</strong> de l’enfant <c>HeldVisual</c> (mesh monde) sous <c>HeldWeapon</c>.
	/// Le parent (<c>HeldLocalPosition</c> / <c>HeldLocalAngles</c>) reste l’ancrage sur le Citizen / IK ; ce bloc affine le mesh sans bouger ce parent.
	/// </summary>
	[Property, Group("World visual (third person)")]
	public Vector3 HeldVisualLocalPosition { get; set; }

	[Property, Group("World visual (third person)")]
	public Angles HeldVisualLocalAngles { get; set; }

	[Property, Group("World visual (third person)")]
	public float HeldVisualUniformScale { get; set; } = 1f;

	public enum CitizenHoldKind
	{
		Pistol,
		Rifle
	}

	[Property, Group("World visual (third person)")]
	public CitizenHoldKind CitizenHold { get; set; } = CitizenHoldKind.Pistol;

	[Property, Group("World visual (third person)")]
	public bool UseIkRightHandOnWeapon { get; set; } = true;

	// --- Citizen animation (world / anim graph) ---

	/// <summary>
	/// Active le pilotage « style Citizen » des paramètres d’anim graph sur le mesh <strong>monde</strong> (3P) si celui-ci utilise un graphe.
	/// Pour la 1P, voir aussi <see cref="FirstPersonDriveAnimGraphParameters"/>.
	/// </summary>
	[Property, Group("Citizen animation")]
	public bool UseCitizenFpAnimParameters { get; set; }

	/// <summary>
	/// Bras / skin Citizen pour le <strong>bonemerge monde</strong> uniquement quand <see cref="UseFirstPersonViewModel"/> est faux.
	/// Pour la 1P, préfère <see cref="FirstPersonArmsModel"/> (sinon bras Citizen par défaut dans le code).
	/// </summary>
	[ResourceType("vmdl")]
	[Property, Group("Citizen animation")]
	public string CitizenFpArmsModel { get; set; } = "models/first_person/v_first_person_arms_citizen.vmdl";

	// --- Animation — Citizen body (third person & graphe corps) ---

	/// <summary>
	/// Pousse sur le <c>SkinnedModelRenderer</c> du Citizen (corps) les paramètres de locomotion / arme du graphe officiel
	/// (<c>move_bob</c>, <c>b_twohanded</c>, etc.). Indispensable en 3P pour que le squelette suive marche + type d’arme.
	/// Le tir réseau utilise <see cref="BodyPrimaryFireParameterName"/> via <see cref="PlayerHitscanWeaponComponent.FireFxSequence"/>.
	/// </summary>
	[Property, Group("Animation — Citizen body")]
	public bool DriveCitizenBodyAnimGraph { get; set; } = true;

	/// <summary>Bool déclenché sur le graphe du corps à chaque tir (sync hôte). Graphe Citizen standard : <c>b_attack</c>.</summary>
	[Property, Group("Animation — Citizen body")]
	public string BodyPrimaryFireParameterName { get; set; } = "b_attack";

	// --- Animation — World weapon mesh ---

	/// <summary>
	/// Si l’arme monde utilise un anim graph : pousse aussi le bool de tir sur ce renderer (ex. culasse). Désactivé par défaut pour les <c>w_*</c> en séquence seule.
	/// </summary>
	[Property, Group("Animation — World weapon")]
	public bool DriveWorldWeaponPrimaryFireParameter { get; set; }

	/// <summary>Nom du paramètre sur le mesh monde (si <see cref="DriveWorldWeaponPrimaryFireParameter"/>).</summary>
	[Property, Group("Animation — World weapon")]
	public string WorldWeaponPrimaryFireParameterName { get; set; } = "b_attack";

	// --- Animation — Reload ---

	[Property, Group("Animation — Reload")]
	public bool DriveBodyReloadParameter { get; set; } = true;

	[Property, Group("Animation — Reload")]
	public string BodyReloadParameterName { get; set; } = "b_reload";

	[Property, Group("Animation — Reload")]
	public bool DriveWorldWeaponReloadParameter { get; set; }

	[Property, Group("Animation — Reload")]
	public string WorldWeaponReloadParameterName { get; set; } = "b_reload";

	/// <summary>Pulse bool reload sur le viewmodel 1P (en plus du sync <c>ReloadFxSequence</c>) pour réduire la latence locale.</summary>
	[Property, Group("Animation — Reload")]
	public bool DriveFirstPersonReloadParameter { get; set; } = true;

	[Property, Group("Animation — Reload")]
	public string FirstPersonReloadParameterName { get; set; } = "b_reload";

	// --- First person visual ---

	/// <summary>Si vrai : le joueur local en 1ʳᵉ personne affiche <see cref="FirstPersonViewModel"/> devant l’œil (position tirée du <c>PlayerController</c>).</summary>
	[Property, Group("First person visual")]
	public bool UseFirstPersonViewModel { get; set; }

	/// <summary>Modèle <c>v_*.vmdl</c> (viewmodel) pour la 1P locale uniquement.</summary>
	[ResourceType("vmdl")]
	[Property, Group("First person visual")]
	public string FirstPersonViewModel { get; set; } = "";

	[Property, Group("First person visual")]
	public bool FirstPersonUseAnimGraph { get; set; } = true;

	/// <summary>Décalage dans l’espace de la tête (axes : droite, haut, avant = <see cref="Angles.ToRotation"/> du regard).</summary>
	[Property, Group("First person visual")]
	public Vector3 FirstPersonLocalPosition { get; set; } = new Vector3(5f, 5f, 12f);

	[Property, Group("First person visual")]
	public Angles FirstPersonLocalAngles { get; set; }

	[Property, Group("First person visual")]
	public float FirstPersonUniformScale { get; set; } = 1f;

	/// <summary>
	/// Transform <strong>local</strong> de l’enfant <c>HeldVisual</c> sous <c>FirstPersonHeldWeapon</c> (mesh viewmodel).
	/// Le parent utilise <see cref="FirstPersonLocalPosition"/> dans l’espace tête ; ce bloc affine le modèle en local.
	/// </summary>
	[Property, Group("First person visual")]
	public Vector3 FirstPersonVisualLocalPosition { get; set; }

	[Property, Group("First person visual")]
	public Angles FirstPersonVisualLocalAngles { get; set; }

	[Property, Group("First person visual")]
	public float FirstPersonVisualUniformScale { get; set; } = 1f;

	/// <summary>Si vrai : en 1P locale, le mesh monde sur <c>HeldWeapon</c> est masqué (évite double rendu).</summary>
	[Property, Group("First person visual")]
	public bool FirstPersonHideWorldModelWhenLocalFirstPerson { get; set; } = true;

	/// <summary>
	/// Pousse <c>move_x</c>, <c>b_attack</c>, <c>b_twohanded</c>, etc. sur le viewmodel 1P quand il a un anim graph.
	/// Indépendant du bonemerge des bras Citizen : à activer pour les <c>v_*</c> dont les mains / bodygroups dépendent du graphe (ex. M700).
	/// </summary>
	[Property, Group("First person visual")]
	public bool FirstPersonDriveAnimGraphParameters { get; set; } = true;

	/// <summary>
	/// Si vrai : tente de bonemerge les bras Citizen sur le viewmodel 1P (souvent avec <c>v_usp</c>).
	/// À <b>false</b> si les mains sont déjà dans le viewmodel (ex. <c>v_m700</c>) pour éviter des doubles bras.
	/// </summary>
	[Property, Group("First person visual")]
	public bool FirstPersonMergeCitizenArms { get; set; } = true;

	/// <summary>Bras Citizen bonemergés sur le viewmodel 1P ; vide = bras Citizen par défaut du code (pas <see cref="CitizenFpArmsModel"/>).</summary>
	[ResourceType("vmdl")]
	[Property, Group("First person visual")]
	public string FirstPersonArmsModel { get; set; } = "";

	/// <summary>Remplit les valeurs d’impact pour <see cref="WeaponHitResolver"/>.</summary>
	public void GetZoneCombat(BodyHitZone zone, out float healthDamage, out float bleedPerSecond, out int legInjuryAdd, out bool instantKill)
	{
		switch (zone)
		{
			case BodyHitZone.Head:
				healthDamage = ZoneHeadHealthDamage;
				bleedPerSecond = ZoneHeadBleedPerSecond;
				legInjuryAdd = ZoneHeadLegInjuryAdd;
				instantKill = ZoneHeadInstantKill;
				return;
			case BodyHitZone.Torso:
				healthDamage = ZoneTorsoHealthDamage;
				bleedPerSecond = ZoneTorsoBleedPerSecond;
				legInjuryAdd = ZoneTorsoLegInjuryAdd;
				instantKill = ZoneTorsoInstantKill;
				return;
			case BodyHitZone.Arm:
				healthDamage = ZoneArmHealthDamage;
				bleedPerSecond = ZoneArmBleedPerSecond;
				legInjuryAdd = ZoneArmLegInjuryAdd;
				instantKill = ZoneArmInstantKill;
				return;
			default:
				healthDamage = ZoneLegHealthDamage;
				bleedPerSecond = ZoneLegBleedPerSecond;
				legInjuryAdd = ZoneLegLegInjuryAdd;
				instantKill = ZoneLegInstantKill;
				return;
		}
	}

	/// <summary>
	/// Les chemins copiés depuis l’éditeur finissent souvent par <c>.vmdl_c</c> ; le runtime charge en général le <c>.vmdl</c>.
	/// </summary>
	public static string ToRuntimeModelPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return "";

		var s = path.Replace('\\', '/').Trim();
		if (s.EndsWith(".vmdl_c", StringComparison.OrdinalIgnoreCase))
			return s[..^2];

		return s;
	}

	public static WeaponDefinition Resolve(string ident)
	{
		if (string.IsNullOrWhiteSpace(ident))
			ident = "usp";

		ident = ident.Trim();
		if (ByIdent.TryGetValue(ident, out var cached))
			return cached;

		var path = $"weapons/definitions/{ident}.weapon";
		if (ResourceLibrary.TryGet<WeaponDefinition>(path, out var loaded))
			return loaded;

		if (!string.Equals(ident, "usp", StringComparison.OrdinalIgnoreCase))
			return Resolve("usp");

		return ResourceLibrary.TryGet<WeaponDefinition>("weapons/definitions/usp.weapon", out var fallback)
			? fallback
			: null;
	}

	public static bool TryResolve(string ident, out WeaponDefinition def)
	{
		def = Resolve(ident);
		return def != null;
	}

	protected override void PostLoad()
	{
		base.PostLoad();

		if (string.IsNullOrWhiteSpace(Ident))
			return;

		ByIdent[Ident.Trim()] = this;
	}

	public CitizenAnimationHelper.HoldTypes GetCitizenHoldType() =>
		CitizenHold == CitizenHoldKind.Rifle
			? CitizenAnimationHelper.HoldTypes.Rifle
			: CitizenAnimationHelper.HoldTypes.Pistol;
}
