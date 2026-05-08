namespace SniperVsRunners.Features.Weapons;

using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Citizen;
using SniperVsRunners.Features.Combat;

/// <summary>
/// Fiche d’une arme : tout ce que le jeu doit savoir pour la faire tirer, infliger des dégâts, jouer des sons et l’afficher.
/// </summary>
/// <remarks>
/// <para>
/// Imagine une « carte de personnage » mais pour une arme : ce n’est pas le modèle 3D lui‑même, c’est la <b>liste de réglages</b>
/// (vitesse de tir, dégâts à la tête, son du coup, chemin du modèle, etc.). Ces fiches sont enregistrées dans des fichiers
/// <c>.weapon</c> sous <c>Assets/weapons/definitions/</c>. Dans l’éditeur s&amp;box, les champs sont rangés en <b>groupes</b> (Combat, Ammo, …).
/// </para>
/// <para>
/// <b>Concepts utiles :</b> le <b>hitscan</b> = le jeu calcule tout de suite où le tir arrive (rayon invisible), sans une balle physique qui vole.
/// Le <b>spread</b> = imprécision en cône. La <b>réserve</b> = munitions en poche ; le <b>chargeur</b> = ce qui est prêt à tirer.
/// </para>
/// </remarks>
[AssetType(Name = "Weapon Definition", Extension = "weapon", Category = "svr")]
public partial class WeaponDefinition : GameResource
{
	static readonly Dictionary<string, WeaponDefinition> ByIdent = new();

	public static IReadOnlyDictionary<string, WeaponDefinition> AllByIdent => ByIdent;

	// --- General ---

	/// <summary>Nom technique court de l’arme, sans espaces (ex. <c>usp</c>, <c>m700</c>).</summary>
	/// <remarks>
	/// C’est l’<b>identifiant stable</b> : le code et le réseau s’en servent pour dire « quel joueur tient quelle arme ».
	/// Il doit correspondre au fichier <c>{Ident}.weapon</c>. Ce n’est <b>pas</b> le joli nom affiché au joueur.
	/// </remarks>
	[Property, Group("General")]
	public string Ident { get; set; } = "unnamed";

	/// <summary>Nom lisible pour les menus ou le HUD (ex. « USP », « M700 »).</summary>
	/// <remarks>
	/// Purement <b>cosmétique</b> pour l’interface : ça ne change ni les dégâts ni le réseau.
	/// </remarks>
	[Property, Group("General")]
	public string DisplayName { get; set; } = "Arme";

	// --- Combat ---

	/// <summary>Temps minimum entre deux coups, en <b>secondes</b>.</summary>
	/// <remarks>
	/// Plus la valeur est <b>grande</b>, plus l’arme tire <b>lentement</b> (style fusil à verrou). Plus elle est <b>petite</b>, plus on peut spammer.
	/// Ex. <c>0.35</c> ≈ environ 2,8 tirs par seconde au mieux.
	/// </remarks>
	[Property, Group("Combat")]
	public float FireCooldownSeconds { get; set; } = 0.35f;

	/// <summary>Distance maximale du rayon de tir, en <b>unités du monde</b> (souvent assimilables à des cm dans Source).</summary>
	/// <remarks>
	/// Au‑delà, le tir ne touche rien. Une valeur énorme (ex. 10 000) = « portée quasi illimitée » pour un hitscan.
	/// </remarks>
	[Property, Group("Combat")]
	public float MaxRange { get; set; } = 10_000f;

	/// <summary>Imprécision du tir : demi‑angle du cône, en <b>degrés</b>.</summary>
	/// <remarks>
	/// <c>0</c> = chaque tir part exactement au centre du réticule (sniper parfait). Plus tu augmentes, plus le cône est large :
	/// les impacts se dispersent <b>autour</b> de la visée. Souvent réduit quand le joueur est en visée (voir <see cref="AimSpreadMultiplier"/>).
	/// </remarks>
	[Property, Group("Combat")]
	public float SpreadHalfAngleDegrees { get; set; }

	// --- Ballistics (hitscan) ---

	/// <summary>Combien la « chute de balle » s’applique, entre <c>0</c> (aucune) et <c>1</c> (pleine).</summary>
	/// <remarks>
	/// En hitscan il n’y a pas une vraie balle qui tombe : le jeu <b>incline</b> légèrement le rayon vers le bas pour simuler la gravité.
	/// <c>0</c> = laser droit. <c>1</c> = inclinaison max donnée par <see cref="BulletDropMaxPitchDegrees"/>.
	/// </remarks>
	[Property, Group("Ballistics")]
	public float BulletDropIntensity { get; set; }

	/// <summary>À intensité <c>1</c> : combien de <b>degrés</b> le rayon penche vers le bas après le spread.</summary>
	/// <remarks>
	/// Plus c’est élevé, plus il faut viser <b>au‑dessus</b> des cibles lointaines pour compenser (comme une vraie arme qui tombe).
	/// </remarks>
	[Property, Group("Ballistics")]
	public float BulletDropMaxPitchDegrees { get; set; } = 2.5f;

	// --- Ammo ---

	/// <summary>Nombre de coups que tu peux tirer <b>sans recharger</b> (taille du chargeur).</summary>
	[Property, Group("Ammo")]
	public int MagazineSize { get; set; } = 12;

	/// <summary>Munitions en réserve au moment où tu reçois l’arme (hors chargeur), si la réserve n’est pas infinie.</summary>
	/// <remarks>
	/// Le chargeur est rempli en général au max au départ ; le reste attend en « poche » pour les rechargements.
	/// Ignoré si <see cref="InfiniteReserve"/> est activé.
	/// </remarks>
	[Property, Group("Ammo")]
	public int StartingReserveAmmo { get; set; } = 36;

	/// <summary>Durée d’une animation / blocage de tir pendant un rechargement, en <b>secondes</b>.</summary>
	/// <remarks>
	/// Plus c’est long, plus le joueur est vulnérable pendant qu’il recharge. Doit coller à peu près à la durée de l’anim si tu veux que ça « feel » bien.
	/// </remarks>
	[Property, Group("Ammo")]
	public float ReloadTimeSeconds { get; set; } = 1.75f;

	/// <summary>Si coché : réserve infinie (souvent affichée <c>∞</c>) ; recharger remplit toujours le chargeur sans épuiser une réserve.</summary>
	/// <remarks>
	/// Pratique pour un mode arcade ou un fusil sans gestion de munitions réaliste.
	/// </remarks>
	[Property, Group("Ammo")]
	public bool InfiniteReserve { get; set; }

	/// <summary>Si coché : quand le dernier coup part, le jeu tente de recharger tout seul si la réserve le permet.</summary>
	/// <remarks>
	/// Évite de rester bloqué avec une arme vide si tu n’appuies pas sur la touche recharge.
	/// </remarks>
	[Property, Group("Ammo")]
	public bool AutoReloadWhenEmpty { get; set; } = true;

	// --- Damage — Head ---

	/// <summary>Dégâts directs à la <b>barre de vie</b> quand la tête est touchée (un seul coup, instantané).</summary>
	/// <remarks>
	/// Plus c’est haut, moins il faut de tirs pour tuer. Les autres lignes du groupe (saignement, jambes, instant kill) s’ajoutent ou modifient le résultat.
	/// </remarks>
	[Property, Group("Damage — Head")]
	public float ZoneHeadHealthDamage { get; set; } = 55f;

	/// <summary>Saignement : points (ou fraction) de vie perdus <b>par seconde</b> après un tir à la tête, en plus du coup direct.</summary>
	/// <remarks>
	/// <c>0</c> = pas de saignement. Utile pour un style « réaliste » où la blessure continue de faire mal.
	/// </remarks>
	[Property, Group("Damage — Head")]
	public float ZoneHeadBleedPerSecond { get; set; } = 4f;

	/// <summary>Ajoute ce nombre au compteur de blessure à la <b>jambe</b> quand la tête est touchée (logique métier du projet).</summary>
	/// <remarks>
	/// Souvent <c>0</c> pour la tête. Sert si ton gameplay lie plusieurs zones du corps à la mobilité.
	/// </remarks>
	[Property, Group("Damage — Head")]
	public int ZoneHeadLegInjuryAdd { get; set; }

	/// <summary>Si coché : cette zone déclenche une mort immédiate du personnage touché (override des dégâts normaux).</summary>
	[Property, Group("Damage — Head")]
	public bool ZoneHeadInstantKill { get; set; }

	// --- Damage — Torso ---

	/// <summary>Dégâts directs à la vie pour un impact au <b>torse</b> (buste).</summary>
	[Property, Group("Damage — Torso")]
	public float ZoneTorsoHealthDamage { get; set; } = 32f;

	/// <summary>Saignement par seconde après un tir au torse.</summary>
	[Property, Group("Damage — Torso")]
	public float ZoneTorsoBleedPerSecond { get; set; } = 2.5f;

	/// <summary>Blessure de jambe ajoutée lors d’un tir au torse.</summary>
	[Property, Group("Damage — Torso")]
	public int ZoneTorsoLegInjuryAdd { get; set; }

	/// <summary>Mort instantanée si le torse est touché (rare ; souvent réservé au headshot).</summary>
	[Property, Group("Damage — Torso")]
	public bool ZoneTorsoInstantKill { get; set; }

	// --- Damage — Arm ---

	/// <summary>Dégâts directs quand un <b>bras</b> est touché.</summary>
	/// <remarks>
	/// En jeu, toucher un membre fait souvent un peu moins mal que le torse ou la tête.
	/// </remarks>
	[Property, Group("Damage — Arm")]
	public float ZoneArmHealthDamage { get; set; } = 26f;

	/// <summary>Saignement par seconde après un tir au bras.</summary>
	[Property, Group("Damage — Arm")]
	public float ZoneArmBleedPerSecond { get; set; } = 2f;

	/// <summary>Blessure de jambe ajoutée lors d’un tir au bras.</summary>
	[Property, Group("Damage — Arm")]
	public int ZoneArmLegInjuryAdd { get; set; }

	/// <summary>Mort instantanée sur impact au bras (très rare).</summary>
	[Property, Group("Damage — Arm")]
	public bool ZoneArmInstantKill { get; set; }

	// --- Damage — Leg ---

	/// <summary>Dégâts directs quand une <b>jambe</b> est touchée.</summary>
	[Property, Group("Damage — Leg")]
	public float ZoneLegHealthDamage { get; set; } = 22f;

	/// <summary>Saignement par seconde après un tir à la jambe.</summary>
	[Property, Group("Damage — Leg")]
	public float ZoneLegBleedPerSecond { get; set; } = 1.5f;

	/// <summary>Combien on ajoute au malus « jambe blessée » (ralentissement, etc.) pour un tir à la jambe.</summary>
	/// <remarks>
	/// Ex. <c>1</c> = chaque impact à la jambe aggrave l’état de blessure des jambes dans ton système.
	/// </remarks>
	[Property, Group("Damage — Leg")]
	public int ZoneLegLegInjuryAdd { get; set; } = 1;

	/// <summary>Mort instantanée sur tir à la jambe (presque jamais utilisé).</summary>
	[Property, Group("Damage — Leg")]
	public bool ZoneLegInstantKill { get; set; }

	// --- Audio ---

	/// <summary>Chemin du fichier son joué à chaque <b>tir</b> (souvent un <c>.sound</c> ou ressource audio du projet).</summary>
	/// <remarks>
	/// Vide = pas de son de tir défini ici (le code peut avoir un fallback ou le silence).
	/// </remarks>
	[Property, Group("Audio")]
	public string PrimaryFireSoundPath { get; set; } = "";

	/// <summary>Volume relatif du tir : <c>1</c> = niveau de base, <c>0.5</c> = plus discret.</summary>
	[Property, Group("Audio")]
	public float PrimaryFireSoundVolume { get; set; } = 1f;

	/// <summary>À quelle distance (unités monde) les autres entendent encore clairement le tir.</summary>
	/// <remarks>
	/// Plus c’est grand, plus le bruit porte loin (important pour le PvP « on entend d’où ça tire »).
	/// </remarks>
	[Property, Group("Audio")]
	public float PrimaryFireHearingRange { get; set; } = 3500f;

	/// <summary>Chemin du son de <b>rechargement</b>.</summary>
	[Property, Group("Audio")]
	public string ReloadSoundPath { get; set; } = "";

	/// <summary>Volume du son de rechargement.</summary>
	[Property, Group("Audio")]
	public float ReloadSoundVolume { get; set; } = 0.85f;

	/// <summary>Portée d’écoute du bruit de rechargement (souvent plus courte que le tir).</summary>
	[Property, Group("Audio")]
	public float ReloadHearingRange { get; set; } = 2400f;

	// --- FX (tracers) ---

	/// <summary>Choix de la <b>forme du trait lumineux</b> (tracer) par rapport au vrai rayon de tir.</summary>
	/// <remarks>
	/// <para>
	/// Le <b>tracer</b> est presque toujours <b>purement visuel</b> : une lumière qui montre où part le coup. Le vrai hitscan peut être instantané dans le code.
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="StrictHitscanRay"/> : ligne <b>droite</b> qui colle au tir ; durée ≈ distance ÷ vitesse (avec min/max).</description></item>
	/// <item><description><see cref="StrictHitscanDropArc"/> : même départ et même impact que le tir, mais le trait <b>courbe</b> comme si la balle tombait.</description></item>
	/// <item><description><see cref="LegacyBezier"/> : ancien mode courbe ; peut ne pas matcher exactement le rayon (dégâts souvent instantanés).</description></item>
	/// </list>
	/// </remarks>
	public enum TracerTrajectoryStyle
	{
		StrictHitscanRay,
		StrictHitscanDropArc,
		LegacyBezier
	}

	/// <summary>Quel style de trajectoire utiliser pour le tracer (voir l’enum ci‑dessus).</summary>
	[Property, Group("FX — Tracer")]
	public TracerTrajectoryStyle TracerTrajectory { get; set; } = TracerTrajectoryStyle.StrictHitscanRay;

	/// <summary>Durée de vol <b>minimale</b> du tracer en secondes (modes stricts).</summary>
	/// <remarks>
	/// Empêche un trait trop bref sur les tirs très proches (sinon à peine visible).
	/// </remarks>
	[Property, Group("FX — Tracer")]
	public float TracerFlightTimeMinSeconds { get; set; } = 0.015f;

	/// <summary>Durée de vol <b>maximale</b> du tracer en secondes (modes stricts).</summary>
	/// <remarks>
	/// Évite d’attendre trois secondes avant de voir l’impact sur une très longue distance.
	/// </remarks>
	[Property, Group("FX — Tracer")]
	public float TracerFlightTimeMaxSeconds { get; set; } = 2.5f;

	/// <summary>Mode <see cref="LegacyBezier"/> uniquement : base de temps d’affichage ; <c>0</c> peut désactiver le tracer.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerDrawSeconds { get; set; } = 0.075f;

	/// <summary>Couleur du cœur du trait (RVB + transparence).</summary>
	[Property, Group("FX — Tracer")]
	public Color TracerColor { get; set; } = new Color(1f, 0.92f, 0.35f, 0.9f);

	/// <summary>Épaisseur « largeur » du trait dans le monde, sur un axe du mesh (perpendiculaire au tir).</summary>
	/// <remarks>
	/// Plus c’est gros, plus le laser est épais. Réglage purement visuel.
	/// </remarks>
	[Property, Group("FX — Tracer")]
	public float TracerWorldThickness { get; set; } = 0.08f;

	/// <summary>Épaisseur sur l’autre axe ; <c>≤ 0</c> = même valeur que <see cref="TracerWorldThickness"/> (trait rond).</summary>
	[Property, Group("FX — Tracer")]
	public float TracerWorldThicknessY { get; set; }

	/// <summary>Intensité lumineuse : &gt; <c>1</c> = plus brillant / halo HDR.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerBrightness { get; set; } = 3f;

	/// <summary>Opacité d’un <b>second</b> mesh plus large autour du trait (lueur). <c>0</c> = pas de halo.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerGlowAlpha { get; set; } = 0.28f;

	/// <summary>Combien le halo est plus large que le cœur. <c>≤ 1</c> = halo désactivé.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerGlowSpread { get; set; } = 1.45f;

	/// <summary>Le halo peut être un peu moins ou plus lumineux que le centre (multiplicateur couleur).</summary>
	[Property, Group("FX — Tracer")]
	public float TracerGlowBrightnessMul { get; set; } = 0.9f;

	/// <summary>Vitesse à laquelle le segment lumineux « voyage » le long du trajet (unités monde par seconde).</summary>
	/// <remarks>
	/// Plus c’est rapide, plus le trait file vite. Le jeu en déduit un temps de vol ≈ distance ÷ vitesse (avec les bornes min/max).
	/// </remarks>
	[Property, Group("FX — Tracer")]
	public float TracerVisualSpeed { get; set; } = 13_000f;

	/// <summary>Longueur de la <b>traînée</b> visible : morceau de ligne qui se déplace, pas toute la distance d’un coup.</summary>
	[Property, Group("FX — Tracer")]
	public float TracerSegmentWorldLength { get; set; } = 22f;

	/// <summary>Si coché (modes stricts) : les dégâts sur le joueur <b>local</b> attendent d’arriver quand le tracer touche visuellement.</summary>
	/// <remarks>
	/// Ça synchronise « ce que tu vois » avec « quand tu perds des PV ». Si décoché, les dégâts peuvent arriver avant le flash du tracer.
	/// En <see cref="LegacyBezier"/> ce réglage ne s’applique pas comme en mode strict.
	/// </remarks>
	[Property, Group("FX — Tracer")]
	public bool DelayHitscanDamageUntilTracerImpact { get; set; } = true;

	// --- FX (muzzle flash) — uniquement prefab ; rendu = contenu du prefab ---

	/// <summary>Prefab (petit objet prêt à l’emploi) cloné à chaque tir pour l’effet de <b>feu à la bouche</b>.</summary>
	/// <remarks>
	/// Souvent des particules ou un flash lumineux. Vide = aucun effet. Le rendu exact dépend du contenu du prefab, pas de ce script.
	/// </remarks>
	[ResourceType("prefab")]
	[Property, Group("FX — Muzzle flash")]
	public string MuzzleFlashPrefab { get; set; } = "";

	/// <summary>Debug : si coché, le flash <b>reste dans la scène</b> au lieu de disparaître tout seul.</summary>
	/// <remarks>
	/// Utile pour ajuster position/rotation en jeu. En production laisse <b>décoché</b> sinon les effets s’empilent et tu perds des FPS.
	/// </remarks>
	[Property, Group("FX — Muzzle flash")]
	public bool MuzzleFlashPersistForTuning { get; set; }

	/// <summary>Petite rotation supplémentaire du flash en 1P après alignement sur l’os défini.</summary>
	[Property, Group("FX — Muzzle flash")]
	public Angles MuzzleFlashFirstPersonLocalAngles { get; set; }

	/// <summary>Rotation supplémentaire du flash en 3P.</summary>
	[Property, Group("FX — Muzzle flash")]
	public Angles MuzzleFlashThirdPersonLocalAngles { get; set; }

	/// <summary>Nom d’un <b>os</b> du squelette 1P (ex. <c>muzzle</c>) : le flash colle à cet os à chaque image.</summary>
	/// <remarks>
	/// En mode bone-only, ce champ est obligatoire : vide = pas de muzzle flash.
	/// </remarks>
	[Property, Group("FX — Muzzle flash")]
	public string MuzzleFlashFirstPersonFollowBoneName { get; set; } = "";

	/// <summary>Os du modèle monde 3P à suivre pour le flash (comme <c>muzzle</c> sur le mesh vu par les autres). Obligatoire en bone-only.</summary>
	[Property, Group("FX — Muzzle flash")]
	public string MuzzleFlashThirdPersonFollowBoneName { get; set; } = "";

	// --- World visual (third person) ---

	/// <summary>Fichier <c>.vmdl</c> du modèle 3D que les <b>autres joueurs</b> voient dans ta main (vue troisième personne).</summary>
	/// <remarks>
	/// On utilise souvent un préfixe <c>w_</c> pour « world ». Si tu mets par erreur un viewmodel <c>v_</c> (pour la 1P), le jeu peut le remplacer par <see cref="FallbackWorldModel"/>.
	/// </remarks>
	[ResourceType("vmdl")]
	[Property, Group("World visual (third person)")]
	public string PrimaryModel { get; set; } = "";

	/// <summary>Modèle de repli si le principal est vide ou inadapté (ex. viewmodel en 3P).</summary>
	[ResourceType("vmdl")]
	[Property, Group("World visual (third person)")]
	public string FallbackWorldModel { get; set; } = "";

	/// <summary>Si coché : le modèle monde utilise un <b>graphe d’animation</b> (blend d’animations pilotées par le code).</summary>
	[Property, Group("World visual (third person)")]
	public bool UseAnimGraphOnPrimary { get; set; }

	/// <summary>Position de l’arme par rapport à la main / au point d’attache du personnage (repère local).</summary>
	/// <remarks>
	/// Sert à ce que le fusil ne traverse pas le torse : tu le déplaces en X/Y/Z jusqu’à ce que ça ait l’air naturel.
	/// </remarks>
	[Property, Group("World visual (third person)")]
	public Vector3 HeldLocalPosition { get; set; }

	/// <summary>Orientation de l’arme dans la même main (pitch / yaw / roll locaux).</summary>
	[Property, Group("World visual (third person)")]
	public Angles HeldLocalAngles { get; set; }

	/// <summary>Échelle du modèle monde dans la main (<c>1</c> = taille du fichier, <c>0.9</c> = un peu plus petit).</summary>
	[Property, Group("World visual (third person)")]
	public float HeldLocalScale { get; set; } = 1f;

	/// <summary>Nom de l’<b>os de la main</b> sur le squelette du Citizen où l’arme est attachée (souvent <c>hold_R</c> pour la main droite).</summary>
	/// <remarks>
	/// Vide = le code essaie une liste par défaut. Si tu te trompes d’os, l’arme peut apparaître au pied ou à la hanche.
	/// </remarks>
	[Property, Group("World visual (third person)")]
	public string WorldHeldHandBoneName { get; set; } = "hold_R";

	/// <summary>Second petit réglage de position : déplace le mesh à l’intérieur de son parent sans changer où la main se trouve.</summary>
	/// <remarks>
	/// Utile quand l’ancrage général (<see cref="HeldLocalPosition"/>) est bon mais le mesh du fichier est décalé par rapport au manche.
	/// </remarks>
	[Property, Group("World visual (third person)")]
	public Vector3 HeldVisualLocalPosition { get; set; }

	/// <summary>Rotation locale supplémentaire du mesh monde sous son parent.</summary>
	[Property, Group("World visual (third person)")]
	public Angles HeldVisualLocalAngles { get; set; }

	/// <summary>Échelle uniforme du mesh enfant (sans toucher au parent).</summary>
	[Property, Group("World visual (third person)")]
	public float HeldVisualUniformScale { get; set; } = 1f;

	/// <summary>Type de prise « officielle » du Citizen : <b>pistolet</b> une main vs <b>fusil</b> deux mains (pose du corps différente).</summary>
	public enum CitizenHoldKind
	{
		Pistol,
		Rifle
	}

	/// <summary>Quelle pose de corps utiliser pour tenir l’arme en 3P.</summary>
	[Property, Group("World visual (third person)")]
	public CitizenHoldKind CitizenHold { get; set; } = CitizenHoldKind.Pistol;

	/// <summary>Si coché : la main droite est aidée par l’<b>IK</b> (cinématique inverse) pour coller au fusil.</summary>
	/// <remarks>
	/// L’IK = le moteur recalcule coudes / poignets pour que les mains touchent le modèle proprement.
	/// </remarks>
	[Property, Group("World visual (third person)")]
	public bool UseIkRightHandOnWeapon { get; set; } = true;

	// --- Citizen animation (world / anim graph) ---

	/// <summary>Si coché : envoie au modèle <b>monde</b> (3P) les mêmes sortes de paramètres d’anim que le Citizen attend (déplacement, style d’arme, etc.).</summary>
	/// <remarks>
	/// À activer si ton mesh monde a un graphe qui réagit à ces noms. Pour la vue à la première personne, voir <see cref="FirstPersonDriveAnimGraphParameters"/>.
	/// </remarks>
	[Property, Group("Citizen animation")]
	public bool UseCitizenFpAnimParameters { get; set; }

	/// <summary>Modèle de <b>bras</b> Citizen utilisé en bonemerge sur le mesh monde quand tu <b>n’utilises pas</b> de viewmodel 1P dédié.</summary>
	/// <remarks>
	/// En 1P avec <see cref="UseFirstPersonViewModel"/>, préfère <see cref="FirstPersonArmsModel"/> pour choisir les bras sur le viewmodel.
	/// </remarks>
	[ResourceType("vmdl")]
	[Property, Group("Citizen animation")]
	public string CitizenFpArmsModel { get; set; } = "models/first_person/v_first_person_arms_citizen.vmdl";

	// --- Animation — Citizen body (third person & graphe corps) ---

	/// <summary>Si coché : le jeu met à jour le <b>graphe d’animation du corps</b> du personnage (marche, bob, une ou deux mains, etc.).</summary>
	/// <remarks>
	/// En 3P, sans ça le corps peut rester figé ou ne pas savoir que tu tiens un fusil. Le nom du bool de tir est <see cref="BodyPrimaryFireParameterName"/>.
	/// </remarks>
	[Property, Group("Animation — Citizen body")]
	public bool DriveCitizenBodyAnimGraph { get; set; } = true;

	/// <summary>Nom du paramètre <b>booléen</b> sur le graphe du corps qui signifie « je tire maintenant » (souvent <c>b_attack</c>).</summary>
	/// <remarks>
	/// Une courte impulsion à <c>true</c> déclenche l’anim de recul / tir vue par les autres.
	/// </remarks>
	[Property, Group("Animation — Citizen body")]
	public string BodyPrimaryFireParameterName { get; set; } = "b_attack";

	/// <summary>Si coché : le corps reçoit un bool du style « je suis en visée » pour adopter une posture visée en 3P.</summary>
	/// <remarks>
	/// Le graphe doit prévoir une anim ADS ; sinon le paramètre ne fait rien de visible.
	/// </remarks>
	[Property, Group("Animation — Citizen body")]
	public bool DriveBodyAimParameter { get; set; } = true;

	/// <summary>Nom exact du bool « visée » sur le graphe du corps (souvent <c>b_aim</c>).</summary>
	[Property, Group("Animation — Citizen body")]
	public string BodyAimParameterName { get; set; } = "b_aim";

	// --- Animation — World weapon mesh ---

	/// <summary>Si coché : le <b>mesh de l’arme dans le monde</b> reçoit aussi un signal de tir (culasse, levier, etc.).</summary>
	/// <remarks>
	/// Souvent laissé <b>décoché</b> pour les armes simples sans anim sur le modèle <c>w_</c>.
	/// </remarks>
	[Property, Group("Animation — World weapon")]
	public bool DriveWorldWeaponPrimaryFireParameter { get; set; }

	/// <summary>Nom du bool de tir sur le modèle monde (si l’option ci‑dessus est activée).</summary>
	[Property, Group("Animation — World weapon")]
	public string WorldWeaponPrimaryFireParameterName { get; set; } = "b_attack";

	// --- Animation — Reload ---

	/// <summary>Si coché : le <b>corps</b> joue une anim de rechargement (paramètre bool ou pulse selon le graphe).</summary>
	[Property, Group("Animation — Reload")]
	public bool DriveBodyReloadParameter { get; set; } = true;

	/// <summary>Nom du paramètre de rechargement sur le graphe du corps (souvent <c>b_reload</c>).</summary>
	[Property, Group("Animation — Reload")]
	public string BodyReloadParameterName { get; set; } = "b_reload";

	/// <summary>Si coché : le mesh <b>arme monde</b> reçoit aussi un signal de reload.</summary>
	[Property, Group("Animation — Reload")]
	public bool DriveWorldWeaponReloadParameter { get; set; }

	/// <summary>Nom du reload sur le mesh monde.</summary>
	[Property, Group("Animation — Reload")]
	public string WorldWeaponReloadParameterName { get; set; } = "b_reload";

	/// <summary>Si coché : le viewmodel 1P reçoit tout de suite le signal reload (moins de décalage ressenti en local).</summary>
	[Property, Group("Animation — Reload")]
	public bool DriveFirstPersonReloadParameter { get; set; } = true;

	/// <summary>Nom du reload sur le viewmodel 1P.</summary>
	[Property, Group("Animation — Reload")]
	public string FirstPersonReloadParameterName { get; set; } = "b_reload";

	// --- Animation — Magazine empty ---

	/// <summary>Si coché : quand il ne reste <b>aucune balle dans le chargeur</b>, le corps passe en pose « arme vide ».</summary>
	[Property, Group("Animation — Magazine empty")]
	public bool DriveBodyMagazineEmptyParameter { get; set; } = true;

	/// <summary>Nom du paramètre « chargeur vide » sur le corps (souvent <c>b_empty</c>).</summary>
	[Property, Group("Animation — Magazine empty")]
	public string BodyMagazineEmptyParameterName { get; set; } = "b_empty";

	/// <summary>Même idée pour le viewmodel 1P : main qui tient l’arme comme vide.</summary>
	[Property, Group("Animation — Magazine empty")]
	public bool DriveFirstPersonMagazineEmptyParameter { get; set; } = true;

	/// <summary>Nom du paramètre vide sur le viewmodel 1P.</summary>
	[Property, Group("Animation — Magazine empty")]
	public string FirstPersonMagazineEmptyParameterName { get; set; } = "b_empty";

	/// <summary>Si coché : le mesh monde de l’arme peut aussi afficher l’état vide (boulon ouvert, etc.).</summary>
	[Property, Group("Animation — Magazine empty")]
	public bool DriveWorldWeaponMagazineEmptyParameter { get; set; }

	/// <summary>Nom du paramètre vide sur le mesh monde.</summary>
	[Property, Group("Animation — Magazine empty")]
	public string WorldWeaponMagazineEmptyParameterName { get; set; } = "b_empty";

	// --- First person visual ---

	/// <summary>Si coché : <b>toi</b> (joueur local) vois un modèle d’arme spécial « collé à la caméra » au lieu de seulement l’arme sur ton personnage.</summary>
	/// <remarks>
	/// C’est le classique FPS : l’arme est grosse devant l’écran et ne correspond pas forcément à ce que les autres voient en 3P.
	/// </remarks>
	[Property, Group("First person visual")]
	public bool UseFirstPersonViewModel { get; set; }

	/// <summary>Chemin du <b>viewmodel</b> (<c>v_*.vmdl</c>) : version « cinéma » de l’arme pour ta vue à la première personne.</summary>
	[ResourceType("vmdl")]
	[Property, Group("First person visual")]
	public string FirstPersonViewModel { get; set; } = "";

	/// <summary>Si coché : ce viewmodel peut utiliser un graphe d’animation (mains qui bougent, culasse, etc.).</summary>
	[Property, Group("First person visual")]
	public bool FirstPersonUseAnimGraph { get; set; } = true;

	/// <summary>Où placer l’arme par rapport à ta <b>tête / caméra</b> (repère local : droite, haut, devant).</summary>
	/// <remarks>
	/// C’est le gros réglage pour que le viseur et le canon aient l’air alignés avec ton écran.
	/// </remarks>
	[Property, Group("First person visual")]
	public Vector3 FirstPersonLocalPosition { get; set; } = new Vector3(5f, 5f, 12f);

	/// <summary>Orientation de l’arme dans ce même repère (pencher, pivoter le modèle).</summary>
	[Property, Group("First person visual")]
	public Angles FirstPersonLocalAngles { get; set; }

	/// <summary>Zoom visuel du viewmodel entier (<c>1</c> = taille normale).</summary>
	[Property, Group("First person visual")]
	public float FirstPersonUniformScale { get; set; } = 1f;

	/// <summary>
	/// Petit décalage <b>à l’intérieur</b> du parent : affine la position du mesh sans tout recalculer depuis la tête.
	/// </summary>
	/// <remarks>
	/// Même idée que <see cref="HeldVisualLocalPosition"/> mais pour la 1P.
	/// </remarks>
	[Property, Group("First person visual")]
	public Vector3 FirstPersonVisualLocalPosition { get; set; }

	/// <summary>Rotation locale supplémentaire du viewmodel.</summary>
	[Property, Group("First person visual")]
	public Angles FirstPersonVisualLocalAngles { get; set; }

	/// <summary>Échelle locale du mesh viewmodel (sous-objet).</summary>
	[Property, Group("First person visual")]
	public float FirstPersonVisualUniformScale { get; set; } = 1f;

	/// <summary>Si coché : l’arme « monde » dans ta main est <b>cachée pour toi</b> en 1P pour éviter de voir deux armes en même temps.</summary>
	[Property, Group("First person visual")]
	public bool FirstPersonHideWorldModelWhenLocalFirstPerson { get; set; } = true;

	/// <summary>Si coché : le code envoie au graphe du viewmodel les infos de déplacement / tir / une ou deux mains (noms type Citizen).</summary>
	/// <remarks>
	/// Nécessaire si ton <c>v_*</c> a des anims qui dépendent de ces paramètres (ex. fusil avec mains intégrées).
	/// </remarks>
	[Property, Group("First person visual")]
	public bool FirstPersonDriveAnimGraphParameters { get; set; } = true;

	/// <summary>Si coché : colle les <b>bras du Citizen</b> sur le squelette du viewmodel (souvent pistolet avec mains séparées).</summary>
	/// <remarks>
	/// Mets <b>décoché</b> si le viewmodel contient déjà des mains (sinon tu vois quatre bras).
	/// </remarks>
	[Property, Group("First person visual")]
	public bool FirstPersonMergeCitizenArms { get; set; } = true;

	/// <summary>Fichier de bras à utiliser pour le bonemerge 1P ; vide = défaut du code.</summary>
	[ResourceType("vmdl")]
	[Property, Group("First person visual")]
	public string FirstPersonArmsModel { get; set; } = "";

	// --- Aim / ADS ---

	/// <summary>Comment le joueur <b>active</b> la visée (iron sights / ADS).</summary>
	public enum AimActivationMode
	{
		/// <summary>Tu dois <b>garder</b> la touche enfoncée pour rester en visée (comme beaucoup de FPS).</summary>
		Hold,
		/// <summary>Un <b>appui</b> entre en visée, un second appui en sort (pratique sur manette ou accessibilité).</summary>
		Toggle
	}

	/// <summary>Si coché : cette arme peut utiliser la <b>visée</b> (réduire le spread, zoom caméra, anims ADS, etc. selon les autres champs).</summary>
	/// <remarks>
	/// ADS = « Aim Down Sights », viser le long du canon / la mire. Si décoché, tout le bloc visée est ignoré pour cette arme.
	/// </remarks>
	[Property, Group("Aim")]
	public bool AimEnabled { get; set; }

	/// <summary><see cref="AimActivationMode.Hold"/> ou <see cref="AimActivationMode.Toggle"/> : voir l’enum.</summary>
	[Property, Group("Aim")]
	public AimActivationMode AimActivation { get; set; } = AimActivationMode.Hold;

	/// <summary>Durée en <b>secondes</b> pour <b>entrer</b> en visée (zoom + alpha visuelle).</summary>
	/// <remarks>
	/// Plus c’est long, plus la transition est lente. Le « feeling » suit aussi la courbe Bézier ci‑dessous.
	/// </remarks>
	[Property, Group("Aim")]
	public float AimTransitionInSeconds { get; set; } = 0.12f;

	/// <summary>Durée pour <b>quitter</b> la visée et revenir à la hanche.</summary>
	[Property, Group("Aim")]
	public float AimTransitionOutSeconds { get; set; } = 0.1f;

	/// <summary>Premier point de contrôle (coordonnée <b>X</b>) de la courbe <c>cubic-bezier</c> comme en CSS.</summary>
	/// <remarks>
	/// Tu n’as pas besoin de comprendre les maths : ces quatre nombres décrivent <b>comment accélérer / ralentir</b> le zoom et les blends liés à la visée.
	/// Défaut <c>0.25</c> = courbe « smooth » proche des sites web.
	/// </remarks>
	[Property, Group("Aim")]
	public float AimTransitionBezierX1 { get; set; } = 0.25f;

	/// <summary>Premier point de contrôle (coordonnée <b>Y</b>) de la courbe.</summary>
	[Property, Group("Aim")]
	public float AimTransitionBezierY1 { get; set; } = 0.1f;

	/// <summary>Deuxième point de contrôle (<b>X</b>).</summary>
	[Property, Group("Aim")]
	public float AimTransitionBezierX2 { get; set; } = 0.25f;

	/// <summary>Deuxième point de contrôle (<b>Y</b>).</summary>
	[Property, Group("Aim")]
	public float AimTransitionBezierY2 { get; set; } = 1f;

	/// <summary>En visée, on multiplie le <see cref="SpreadHalfAngleDegrees"/> par ce nombre.</summary>
	/// <remarks>
	/// <c>0</c> = tir parfaitement au centre du réticule. <c>1</c> = aussi imprécis qu’à la hanche. <c>0.5</c> = deux fois plus précis qu’à la hanche.
	/// </remarks>
	[Property, Group("Aim")]
	public float AimSpreadMultiplier { get; set; } = 0.35f;

	/// <summary>Si coché : envoie un <b>bool</b> « je vise » au graphe du viewmodel 1P (souvent <c>b_aim</c>).</summary>
	[Property, Group("Aim animation")]
	public bool DriveFirstPersonAimParameter { get; set; } = true;

	/// <summary>Nom exact du booléen de visée sur ton anim graph 1P.</summary>
	[Property, Group("Aim animation")]
	public string FirstPersonAimParameterName { get; set; } = "b_aim";

	/// <summary>Si coché : envoie aussi un paramètre de type <b>liste / enum</b> (souvent <c>ironsights</c>) pour choisir une pose parmi plusieurs.</summary>
	[Property, Group("Aim animation")]
	public bool DriveFirstPersonIronsightsParameter { get; set; } = true;

	/// <summary>Nom de cet enum sur le graphe (doit correspondre à ce que le modélisateur a créé).</summary>
	[Property, Group("Aim animation")]
	public string FirstPersonIronsightsParameterName { get; set; } = "ironsights";

	/// <summary>Valeur numérique quand la visée est <b>active</b> (souvent <c>1</c>).</summary>
	[Property, Group("Aim animation")]
	public int FirstPersonIronsightsEnabledValue { get; set; } = 1;

	/// <summary>Valeur numérique quand tu ne vises <b>pas</b> (souvent <c>0</c>).</summary>
	[Property, Group("Aim animation")]
	public int FirstPersonIronsightsDisabledValue { get; set; }

	/// <summary>Si coché : envoie un <b>float</b> de vitesse pour que le graphe sache à quelle vitesse jouer la transition visée / non visée.</summary>
	[Property, Group("Aim animation")]
	public bool DriveFirstPersonAimSpeedParameter { get; set; } = true;

	/// <summary>Nom du float (ex. <c>speed_ironsights</c>) sur le graphe 1P.</summary>
	[Property, Group("Aim animation")]
	public string FirstPersonAimSpeedParameterName { get; set; } = "speed_ironsights";

	/// <summary><b>Champ de vision</b> (FOV) de la caméra en pleine visée, en <b>degrés</b>.</summary>
	/// <remarks>
	/// Plus le nombre est <b>petit</b>, plus tu « zoomes » (image grossit). Le jeu interpole entre le FOV normal et celui‑ci pendant que tu vises.
	/// <c>0</c> ou négatif = pas de zoom caméra pour cette arme (anims seulement).
	/// </remarks>
	[Property, Group("Aim — Camera")]
	public float AimFovDegrees { get; set; }

	/// <summary>Méthode interne : donne les dégâts / saignement / jambe / kill instantané pour une <b>zone du corps</b> touchée.</summary>
	/// <remarks>
	/// Utilisé par le code de résolution des impacts ; tu ne l’appelles pas depuis l’éditeur.
	/// </remarks>
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

	/// <summary>Normalise un chemin de modèle copié‑collé depuis l’éditeur vers ce que le moteur attend au chargement.</summary>
	/// <remarks>
	/// Souvent les assets compilés finissent par <c>.vmdl_c</c> ; en jeu on utilise plutôt la ressource <c>.vmdl</c>. Tu n’as généralement pas à appeler ça à la main.
	/// </remarks>
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
