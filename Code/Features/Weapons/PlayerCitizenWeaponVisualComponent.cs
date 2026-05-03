namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;
using Sandbox.Citizen;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Teams;

/// <summary>
/// Arme visible monde (3P + autres clients) + viewmodel 1P local piloté par <see cref="WeaponDefinition"/>.
/// </summary>
public sealed class PlayerCitizenWeaponVisualComponent : Component
{
	/// <summary>
	/// Si vrai : bras Citizen bonemergés sur le <strong>viewmodel 1P</strong> lorsque celui-ci est actif (<see cref="WeaponDefinition.UseFirstPersonViewModel"/>).
	/// </summary>
	[Property] public bool BonemergeCitizenFirstPersonArms { get; set; } = true;

	[Property] public bool ForceDisableFirstPersonViewmodel { get; set; }

	/// <summary>
	/// En 3P : pose monde de <c>HeldWeapon</c> = os main droite du Citizen + <see cref="WeaponDefinition.HeldLocalPosition"/> / angles (espace main), au lieu d’un ancrage local figé sur le root du pawn.
	/// </summary>
	[Property] public bool SyncWorldHeldWeaponToHandBone { get; set; } = true;

	/// <summary>
	/// Si vrai et que le suivi d’os est actif : pas d’<c>IkRightHand</c> vers l’arme (évite que le squelette tire la main vers une cible déjà sur l’os).
	/// </summary>
	[Property] public bool DisableRightHandIkWhenSyncedToHandBone { get; set; } = true;

	/// <summary>Nom d’os forcé pour tout le pawn ; vide = <see cref="WeaponDefinition.WorldHeldHandBoneName"/> puis repli <c>hold_R</c> (attache objets Citizen), <c>hand_R</c>, …</summary>
	[Property] public string WorldHeldWeaponHandBoneNameOverride { get; set; } = "";

	/// <summary>Ordre de repli si aucun nom n’est forcé : d’abord l’os d’attache <c>hold_R</c>, puis la main.</summary>
	static readonly string[] DefaultWorldHeldHandBoneNames =
	{
		"hold_R", "hold_r", "Hold_R",
		"hand_R", "hand_r", "Hand_R",
		"weapon_hand_R"
	};

	// Repli si WeaponDefinition / ResourceLibrary indisponible.
	const string FallbackUspView = "models/weapons/sbox_pistol_usp/v_usp.vmdl";
	const string FallbackUspWorld = "models/weapons/sbox_pistol_usp/w_usp.vmdl";
	const string FallbackM700World = "models/weapons/sbox_sniper_m700/w_m700.vmdl";
	const string DefaultFpCitizenArms = "models/first_person/v_first_person_arms_citizen.vmdl";
	/// <summary>Enfant sous <c>HeldWeapon</c> / <c>FirstPersonHeldWeapon</c> : mesh + offset local sans toucher au parent (IK / tête).</summary>
	const string HeldVisualChildName = "HeldVisual";

	static readonly Vector3 FallbackUspHeldPos = new Vector3(5f, 18f, 2f);
	static readonly Angles FallbackUspHeldAng = new Angles(0f, 90f, 0f);
	static readonly Vector3 FallbackM700HeldPos = new Vector3(4f, 12f, -4f);
	static readonly Angles FallbackM700HeldAng = new Angles(0f, 90f, 0f);

	CitizenAnimationHelper _anim;
	SkinnedModelRenderer _bodySkinned;
	GameObject _weaponRoot;
	GameObject _weaponVisualRoot;
	SkinnedModelRenderer _weaponSkinned;
	GameObject _armsMergeObject;

	GameObject _fpWeaponRoot;
	GameObject _fpWeaponVisualRoot;
	SkinnedModelRenderer _fpWeaponSkinned;
	GameObject _fpArmsMergeObject;

	string _lastWeaponIdent = "";
	TeamTypes _lastCombatTeam = TeamTypes.Spectators;
	bool _visualReady;
	bool _fpGraphPrimed;

	protected override void OnStart()
	{
		TrySetupVisual();
	}

	protected override void OnDestroy()
	{
		DestroyFpPresentation();
		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if (!_visualReady)
			TrySetupVisual();

		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		var vitality = Components.Get<PlayerVitalityComponent>();
		var combat = Components.Get<PlayerCombatInfoComponent>();

		if (vitality != null && vitality.IsDead)
		{
			if (_weaponRoot.IsValid())
				_weaponRoot.Enabled = false;
			if (_fpWeaponRoot.IsValid())
				_fpWeaponRoot.Enabled = false;
			if (_anim.IsValid())
			{
				_anim.HoldType = CitizenAnimationHelper.HoldTypes.None;
				_anim.IkRightHand = null;
			}

			return;
		}

		if (_weaponRoot.IsValid())
			_weaponRoot.Enabled = true;

		if (!_visualReady || weapon == null)
			return;

		var ident = string.IsNullOrWhiteSpace(weapon.ActiveWeaponIdent) ? "usp" : weapon.ActiveWeaponIdent.Trim();
		var def = WeaponDefinition.Resolve(ident);
		if (ident != _lastWeaponIdent)
		{
			_lastWeaponIdent = ident;
			_fpGraphPrimed = false;
			ApplyWeaponModel(def, ident);
			TrySetupWorldArmsBonemerge(def);
			ApplyFirstPersonWeaponModel(def, ident);
			TrySetupFpArmsBonemerge(def);
		}

		if (!_anim.IsValid())
			return;

		var team = combat?.Team ?? TeamTypes.Spectators;
		if (team != _lastCombatTeam)
		{
			_lastCombatTeam = team;
			_fpGraphPrimed = false;
		}

		if (_weaponVisualRoot.IsValid())
			ApplyHeldVisualLocalTransform(def);

		var pc = Components.Get<PlayerController>();
		if (pc != null)
		{
			_anim.WithVelocity(pc.Velocity);
			_anim.AimAngle = Rotation.LookAt(pc.EyeAngles.Forward, Vector3.Up);
			_anim.AimEyesWeight = 1f;
			_anim.AimHeadWeight = 0.75f;
			_anim.AimBodyWeight = 0.72f;
			_anim.IsWeaponLowered = false;
		}

		SyncWorldHeldWeaponFromHandBone(def, ident);
		ApplyHoldType(def, ident, combat);

		if (pc == null)
			return;

		var activeDef = weapon.ResolveActiveDefinition();
		UpdateFirstPersonPresentation(pc, activeDef, combat);

		var animTarget = GetActiveFpSkinnedForAnimGraph(pc, activeDef);
		if (animTarget.IsValid() && activeDef != null && animTarget.UseAnimGraph && ShouldDriveWeaponAnimGraph(animTarget, activeDef, pc))
			UpdateCitizenFpWeaponAnimGraph(animTarget, pc, activeDef);
	}

	/// <summary>
	/// Viewmodel 1P : graphe souvent obligatoire pour afficher mains / bodygroups (ex. <c>v_m700</c>).
	/// Monde 3P : inchangé, piloté par <see cref="WeaponDefinition.UseCitizenFpAnimParameters"/>.
	/// </summary>
	bool ShouldDriveWeaponAnimGraph(SkinnedModelRenderer animTarget, WeaponDefinition def, PlayerController pc)
	{
		if (!def.UseFirstPersonViewModel || !IsLocalPawn() || pc == null || pc.ThirdPerson
		    || !_fpWeaponSkinned.IsValid() || animTarget != _fpWeaponSkinned)
			return def.UseCitizenFpAnimParameters;

		return def.FirstPersonDriveAnimGraphParameters || def.UseCitizenFpAnimParameters;
	}

	void TrySetupVisual()
	{
		if (_visualReady)
			return;

		var skinned = FindCitizenSkinnedRenderer(GameObject);
		if (skinned == null)
			return;

		_visualReady = true;
		_bodySkinned = skinned;

		_anim = Components.GetOrCreate<CitizenAnimationHelper>();
		_anim.Target = skinned;
		_anim.EyeSource = GameObject;

		_weaponRoot = FindHeldWeaponRoot(GameObject);
		if (_weaponRoot == null)
		{
			_weaponRoot = new GameObject(true);
			_weaponRoot.Name = "HeldWeapon";
			_weaponRoot.SetParent(GameObject);
		}

		EnsureWorldWeaponHierarchy();

		if (IsLocalPawn())
			EnsureFirstPersonWeaponObjects();

		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		var combat = Components.Get<PlayerCombatInfoComponent>();
		if (weapon != null)
		{
			_lastWeaponIdent = string.IsNullOrWhiteSpace(weapon.ActiveWeaponIdent) ? "usp" : weapon.ActiveWeaponIdent.Trim();
			var def = WeaponDefinition.Resolve(_lastWeaponIdent);
			ApplyWeaponModel(def, _lastWeaponIdent);
			TrySetupWorldArmsBonemerge(def);
			ApplyFirstPersonWeaponModel(def, _lastWeaponIdent);
			TrySetupFpArmsBonemerge(def);
			ApplyHoldType(def, _lastWeaponIdent, combat);
		}
		else
		{
			_lastWeaponIdent = "usp";
			var def = WeaponDefinition.Resolve("usp");
			ApplyWeaponModel(def, "usp");
			TrySetupWorldArmsBonemerge(def);
			ApplyFirstPersonWeaponModel(def, "usp");
			TrySetupFpArmsBonemerge(def);
			ApplyHoldType(def, "usp", combat);
		}
	}

	void EnsureWorldWeaponHierarchy()
	{
		if (!_weaponRoot.IsValid())
			return;

		_weaponVisualRoot = FindNamedChild(_weaponRoot, HeldVisualChildName);
		if (_weaponVisualRoot == null)
		{
			_weaponVisualRoot = new GameObject(true);
			_weaponVisualRoot.Name = HeldVisualChildName;
			_weaponVisualRoot.SetParent(_weaponRoot);
		}

		var smRoot = _weaponRoot.Components.Get<SkinnedModelRenderer>();
		if (smRoot != null)
		{
			var preservedModel = smRoot.Model;
			var preservedAg = smRoot.UseAnimGraph;
			smRoot.Destroy();
			_weaponSkinned = _weaponVisualRoot.Components.Get<SkinnedModelRenderer>();
			if (_weaponSkinned == null)
				_weaponSkinned = _weaponVisualRoot.Components.Create<SkinnedModelRenderer>();
			if (preservedModel.IsValid)
			{
				_weaponSkinned.Model = preservedModel;
				_weaponSkinned.UseAnimGraph = preservedAg;
			}
		}
		else
		{
			_weaponSkinned = _weaponVisualRoot.Components.Get<SkinnedModelRenderer>();
			if (_weaponSkinned == null)
				_weaponSkinned = _weaponVisualRoot.Components.Create<SkinnedModelRenderer>();
		}
	}

	void EnsureFirstPersonWeaponObjects()
	{
		if (_fpWeaponRoot.IsValid())
			return;

		_fpWeaponRoot = new GameObject(true);
		_fpWeaponRoot.Name = "FirstPersonHeldWeapon";
		_fpWeaponRoot.SetParent(GameObject);

		_fpWeaponVisualRoot = new GameObject(true);
		_fpWeaponVisualRoot.Name = HeldVisualChildName;
		_fpWeaponVisualRoot.SetParent(_fpWeaponRoot);

		_fpWeaponSkinned = _fpWeaponVisualRoot.Components.Create<SkinnedModelRenderer>();
		_fpWeaponRoot.Enabled = false;
	}

	void DestroyFpPresentation()
	{
		DestroyFpArmsBonemerge();
		if (_fpWeaponRoot.IsValid())
		{
			_fpWeaponRoot.Destroy();
			_fpWeaponRoot = null;
			_fpWeaponVisualRoot = null;
			_fpWeaponSkinned = null;
		}
	}

	static GameObject FindNamedChild(GameObject parent, string childName)
	{
		if (!parent.IsValid())
			return null;

		foreach (var c in parent.Children)
		{
			if (c.IsValid() && c.Name == childName)
				return c;
		}

		return null;
	}

	GameObject WorldArmsAttachParent() => _weaponVisualRoot.IsValid() ? _weaponVisualRoot : _weaponRoot;

	GameObject FpArmsAttachParent() => _fpWeaponVisualRoot.IsValid() ? _fpWeaponVisualRoot : _fpWeaponRoot;

	void ApplyHeldVisualLocalTransform(WeaponDefinition def)
	{
		if (!_weaponVisualRoot.IsValid())
			return;

		var pos = def?.HeldVisualLocalPosition ?? default;
		var ang = def?.HeldVisualLocalAngles ?? default;
		var sc = def?.HeldVisualUniformScale ?? 1f;
		if (float.IsNaN(sc) || float.IsInfinity(sc) || sc <= 0f)
			sc = 1f;
		sc = Math.Max(sc, 0.001f);

		_weaponVisualRoot.LocalPosition = pos;
		_weaponVisualRoot.LocalRotation = ang.ToRotation();
		_weaponVisualRoot.LocalScale = Vector3.One * sc;
	}

	void ApplyFirstPersonVisualLocalTransform(WeaponDefinition def)
	{
		if (!_fpWeaponVisualRoot.IsValid() || def == null)
			return;

		var pos = def.FirstPersonVisualLocalPosition;
		var ang = def.FirstPersonVisualLocalAngles;
		var sc = def.FirstPersonVisualUniformScale;
		if (float.IsNaN(sc) || float.IsInfinity(sc) || sc <= 0f)
			sc = 1f;
		sc = Math.Max(sc, 0.001f);

		_fpWeaponVisualRoot.LocalPosition = pos;
		_fpWeaponVisualRoot.LocalRotation = ang.ToRotation();
		_fpWeaponVisualRoot.LocalScale = Vector3.One * sc;
	}

	void UpdateFirstPersonPresentation(PlayerController pc, WeaponDefinition def, PlayerCombatInfoComponent combat)
	{
		if (!_weaponSkinned.IsValid())
			return;

		var local = IsLocalPawn();
		var spec = combat != null && combat.Team == TeamTypes.Spectators;

		if (!local || ForceDisableFirstPersonViewmodel || spec || !_fpWeaponRoot.IsValid() || !_fpWeaponSkinned.IsValid())
		{
			if (_fpWeaponRoot.IsValid())
				_fpWeaponRoot.Enabled = false;
			_weaponSkinned.Enabled = _weaponRoot.IsValid() && _weaponRoot.Enabled;
			return;
		}

		var fpOk = def != null
		           && def.UseFirstPersonViewModel
		           && _fpWeaponSkinned.Model.IsValid;

		var firstPerson = !pc.ThirdPerson;
		var showFp = fpOk && firstPerson;

		if (!showFp)
		{
			_fpWeaponRoot.Enabled = false;
			_weaponSkinned.Enabled = _weaponRoot.IsValid() && _weaponRoot.Enabled;
			return;
		}

		var hideWorld = def.FirstPersonHideWorldModelWhenLocalFirstPerson;
		_weaponSkinned.Enabled = !hideWorld;

		var rot = pc.EyeAngles.ToRotation();
		var lp = def.FirstPersonLocalPosition;
		var worldPos = pc.EyePosition + rot.Right * lp.x + rot.Up * lp.y + rot.Forward * lp.z;
		var worldRot = rot * def.FirstPersonLocalAngles.ToRotation();

		var sc = def.FirstPersonUniformScale;
		if (float.IsNaN(sc) || float.IsInfinity(sc) || sc <= 0f)
			sc = 1f;

		_fpWeaponRoot.WorldPosition = worldPos;
		_fpWeaponRoot.WorldRotation = worldRot;
		_fpWeaponRoot.WorldScale = Vector3.One * sc;
		_fpWeaponRoot.Enabled = true;
		ApplyFirstPersonVisualLocalTransform(def);
	}

	SkinnedModelRenderer GetActiveFpSkinnedForAnimGraph(PlayerController pc, WeaponDefinition def)
	{
		if (!IsLocalPawn() || ForceDisableFirstPersonViewmodel || pc == null)
			return _weaponSkinned;

		if (def != null && def.UseFirstPersonViewModel && !pc.ThirdPerson && _fpWeaponSkinned.IsValid() && _fpWeaponSkinned.Enabled && _fpWeaponSkinned.Model.IsValid)
			return _fpWeaponSkinned;

		return _weaponSkinned;
	}

	static GameObject FindHeldWeaponRoot(GameObject root)
	{
		if (!root.IsValid())
			return null;

		foreach (var child in root.Children)
		{
			if (child.IsValid() && child.Name == "HeldWeapon")
				return child;
		}

		return null;
	}

	static SkinnedModelRenderer FindCitizenSkinnedRenderer(GameObject root)
	{
		foreach (var child in root.Children)
		{
			if (!child.IsValid() || child.Name != "Body")
				continue;

			var sm = child.Components.Get<SkinnedModelRenderer>();
			if (sm != null)
				return sm;
		}

		return FindSkinnedRecursive(root);
	}

	static SkinnedModelRenderer FindSkinnedRecursive(GameObject root)
	{
		if (!root.IsValid())
			return null;

		var sm = root.Components.Get<SkinnedModelRenderer>();
		if (sm != null)
			return sm;

		foreach (var child in root.Children)
		{
			var found = FindSkinnedRecursive(child);
			if (found != null)
				return found;
		}

		return null;
	}

	void ApplyWeaponModel(WeaponDefinition def, string weaponIdent)
	{
		if (!_weaponSkinned.IsValid())
			return;

		DestroyWorldArmsBonemerge();

		Model primary = default;
		var useAnimGraph = false;

		if (def != null)
		{
			primary = string.IsNullOrWhiteSpace(def.PrimaryModel)
				? default
				: Model.Load(WeaponDefinition.ToRuntimeModelPath(def.PrimaryModel));
			if (!primary.IsValid && !string.IsNullOrWhiteSpace(def.FallbackWorldModel))
			{
				primary = Model.Load(WeaponDefinition.ToRuntimeModelPath(def.FallbackWorldModel));
				if (primary.IsValid)
					useAnimGraph = false;
			}
			else if (primary.IsValid)
				useAnimGraph = def.UseAnimGraphOnPrimary;

			if (primary.IsValid
			    && !string.IsNullOrWhiteSpace(def.FallbackWorldModel)
			    && IsLikelyFirstPersonViewModelPath(def.PrimaryModel))
			{
				var world = Model.Load(WeaponDefinition.ToRuntimeModelPath(def.FallbackWorldModel));
				if (world.IsValid)
				{
					primary = world;
					useAnimGraph = false;
				}
			}

			if (primary.IsValid)
			{
				if (!SyncWorldHeldWeaponToHandBone)
					ApplyWeaponRootTransform(def.HeldLocalPosition, def.HeldLocalAngles.ToRotation(), def.HeldLocalScale);
				ConfigureWeaponSkinnedRenderer(_weaponSkinned, primary, useAnimGraph);
				ApplyHeldVisualLocalTransform(def);
				if (SyncWorldHeldWeaponToHandBone)
					SyncWorldHeldWeaponFromHandBone(def, weaponIdent);
				return;
			}

			Log.Warning($"Impossible de charger le modèle d’arme ({def.Ident}), repli ident « {weaponIdent} ».");
		}
		else
			Log.Warning($"WeaponDefinition introuvable pour « {weaponIdent} », repli visuel dur.");

		if (TryApplyHardcodedWeaponModel(weaponIdent, out var m, out var anim, out var pos, out var ang, out var scale, out _))
		{
			if (!SyncWorldHeldWeaponToHandBone)
				ApplyWeaponRootTransform(pos, ang.ToRotation(), scale);
			ConfigureWeaponSkinnedRenderer(_weaponSkinned, m, anim);
			ApplyHeldVisualLocalTransform(def);
			if (SyncWorldHeldWeaponToHandBone)
				SyncWorldHeldWeaponFromHandBone(def, weaponIdent);
		}
	}

	void ApplyFirstPersonWeaponModel(WeaponDefinition def, string weaponIdent)
	{
		if (!_fpWeaponSkinned.IsValid())
			return;

		DestroyFpArmsBonemerge();

		if (!IsLocalPawn() || ForceDisableFirstPersonViewmodel)
		{
			_fpWeaponSkinned.Model = default;
			return;
		}

		if (def == null || !def.UseFirstPersonViewModel)
		{
			_fpWeaponSkinned.Model = default;
			return;
		}

		var path = ResolveFirstPersonViewModelPath(def);
		if (string.IsNullOrWhiteSpace(path))
		{
			_fpWeaponSkinned.Model = default;
			return;
		}

		var vm = Model.Load(WeaponDefinition.ToRuntimeModelPath(path));
		if (!vm.IsValid)
		{
			Log.Warning($"Viewmodel 1P introuvable ({path}) pour « {weaponIdent} ».");
			_fpWeaponSkinned.Model = default;
			return;
		}

		ConfigureWeaponSkinnedRenderer(_fpWeaponSkinned, vm, def.FirstPersonUseAnimGraph);
		ApplyFirstPersonVisualLocalTransform(def);
	}

	static string ResolveFirstPersonViewModelPath(WeaponDefinition def)
	{
		if (!string.IsNullOrWhiteSpace(def.FirstPersonViewModel))
			return def.FirstPersonViewModel.Trim();

		// Compat : anciennes fiches qui mettaient le vmdl arme dans CitizenFpArmsModel.
		if (IsLikelyFirstPersonViewModelPath(def.CitizenFpArmsModel))
			return def.CitizenFpArmsModel.Trim();

		return "";
	}

	void ApplyWeaponRootTransform(Vector3 localPosition, Rotation localRotation, float uniformScale)
	{
		if (!_weaponRoot.IsValid())
			return;

		_weaponRoot.LocalPosition = localPosition;
		_weaponRoot.LocalRotation = localRotation;

		var s = uniformScale;
		if (float.IsNaN(s) || float.IsInfinity(s) || s <= 0f)
			s = 1f;

		s = Math.Max(s, 0.001f);
		_weaponRoot.LocalScale = Vector3.One * s;
	}

	bool TryResolveRightHandBoneTransform(WeaponDefinition def, out Transform handWorld)
	{
		handWorld = default;
		if (_bodySkinned == null || !_bodySkinned.IsValid())
			return false;

		if (!string.IsNullOrWhiteSpace(WorldHeldWeaponHandBoneNameOverride))
		{
			var o = WorldHeldWeaponHandBoneNameOverride.Trim();
			if (_bodySkinned.TryGetBoneTransform(o, out handWorld))
				return true;
		}

		if (def != null && !string.IsNullOrWhiteSpace(def.WorldHeldHandBoneName))
		{
			var n = def.WorldHeldHandBoneName.Trim();
			if (_bodySkinned.TryGetBoneTransform(n, out handWorld))
				return true;
		}

		foreach (var name in DefaultWorldHeldHandBoneNames)
		{
			if (_bodySkinned.TryGetBoneTransform(name, out handWorld))
				return true;
		}

		return false;
	}

	void GetWorldHeldRootOffsets(WeaponDefinition def, string weaponIdent, out Vector3 heldPos, out Rotation heldRot, out float heldScale)
	{
		if (def != null)
		{
			heldPos = def.HeldLocalPosition;
			heldRot = def.HeldLocalAngles.ToRotation();
			heldScale = def.HeldLocalScale;
			return;
		}

		var id = weaponIdent.Trim();
		if (id.Equals("m700", StringComparison.OrdinalIgnoreCase)
		    || id.Contains("m700", StringComparison.OrdinalIgnoreCase)
		    || id.Contains("sniper", StringComparison.OrdinalIgnoreCase))
		{
			heldPos = FallbackM700HeldPos;
			heldRot = FallbackM700HeldAng.ToRotation();
			heldScale = 1.15f;
			return;
		}

		heldPos = FallbackUspHeldPos;
		heldRot = FallbackUspHeldAng.ToRotation();
		heldScale = 1f;
	}

	void SyncWorldHeldWeaponFromHandBone(WeaponDefinition def, string weaponIdent)
	{
		if (!_weaponRoot.IsValid() || !SyncWorldHeldWeaponToHandBone)
			return;

		GetWorldHeldRootOffsets(def, weaponIdent, out var heldPos, out var heldRot, out var heldScale);

		if (!TryResolveRightHandBoneTransform(def, out var hand))
		{
			ApplyWeaponRootTransform(heldPos, heldRot, heldScale);
			return;
		}

		_weaponRoot.WorldPosition = hand.Position + hand.Rotation * heldPos;
		_weaponRoot.WorldRotation = hand.Rotation * heldRot;

		var s = heldScale;
		if (float.IsNaN(s) || float.IsInfinity(s) || s <= 0f)
			s = 1f;
		s = Math.Max(s, 0.001f);
		_weaponRoot.WorldScale = Vector3.One * s;
	}

	/// <summary>
	/// Sans anim graph, le SkinnedModelRenderer n’évalue souvent pas le squelette : aucun mesh visible malgré un <see cref="Model"/> assigné.
	/// </summary>
	static void ConfigureWeaponSkinnedRenderer(SkinnedModelRenderer renderer, Model model, bool useAnimGraph)
	{
		if (!renderer.IsValid() || !model.IsValid)
			return;

		renderer.Model = model;
		renderer.UseAnimGraph = useAnimGraph;

		if (useAnimGraph)
			return;

		var names = model.AnimationNames;
		if (names == null || names.Count == 0)
		{
			// Beaucoup de w_* / props Facepunch n’exposent pas de séquences listées mais ont un anim graph par défaut.
			renderer.UseAnimGraph = true;
			return;
		}

		renderer.Sequence.Name = names[0];
		renderer.Sequence.Looping = true;
	}

	static bool IsLikelyFirstPersonViewModelPath(string modelPath)
	{
		if (string.IsNullOrWhiteSpace(modelPath))
			return false;

		var s = modelPath.Replace('\\', '/').Trim();
		var slash = s.LastIndexOf('/');
		var file = slash >= 0 ? s[(slash + 1)..] : s;
		return file.StartsWith("v_", StringComparison.OrdinalIgnoreCase)
		       || s.Contains("/v_", StringComparison.OrdinalIgnoreCase);
	}

	static bool TryApplyHardcodedWeaponModel(
		string weaponIdent,
		out Model model,
		out bool useAnimGraph,
		out Vector3 heldPos,
		out Angles heldAng,
		out float heldScale,
		out bool ikToWeapon)
	{
		model = default;
		useAnimGraph = false;
		heldPos = FallbackUspHeldPos;
		heldAng = FallbackUspHeldAng;
		heldScale = 1f;
		ikToWeapon = true;

		var id = weaponIdent.Trim();
		if (id.Equals("m700", StringComparison.OrdinalIgnoreCase)
		    || id.Contains("m700", StringComparison.OrdinalIgnoreCase)
		    || id.Contains("sniper", StringComparison.OrdinalIgnoreCase))
		{
			model = Model.Load(FallbackM700World);
			if (!model.IsValid)
				return false;

			useAnimGraph = false;
			heldPos = FallbackM700HeldPos;
			heldAng = FallbackM700HeldAng;
			heldScale = 1.15f;
			ikToWeapon = false;
			return true;
		}

		var wm = Model.Load(FallbackUspWorld);
		if (wm.IsValid)
		{
			model = wm;
			useAnimGraph = false;
			return true;
		}

		var vm = Model.Load(FallbackUspView);
		if (!vm.IsValid)
			return false;

		model = vm;
		useAnimGraph = true;
		return true;
	}

	void UpdateCitizenFpWeaponAnimGraph(SkinnedModelRenderer skinned, PlayerController pc, WeaponDefinition def)
	{
		if (!skinned.IsValid())
			return;

		var p = skinned.Parameters;

		p.Set("skeleton", 1);
		p.Set("b_grounded", pc.IsOnGround);
		p.Set("b_jump", false);

		var horiz = pc.Velocity.WithZ(0).Length;
		var run = Math.Max(1f, pc.RunSpeed);
		p.Set("move_bob", Math.Clamp(horiz / run, 0f, 1f));
		p.Set("b_sprint", horiz > run * 0.82f);

		if (IsLocalPawn())
		{
			var m = Input.AnalogMove;
			p.Set("move_x", m.x);
			p.Set("move_y", m.y);
			p.Set("move_z", m.z);

			if (Input.Pressed("Attack1"))
				p.Set("b_attack", true);
		}
		else
		{
			p.Set("move_x", 0f);
			p.Set("move_y", 0f);
			p.Set("move_z", 0f);
		}

		var twoHanded = def != null && def.CitizenHold == WeaponDefinition.CitizenHoldKind.Rifle;
		p.Set("b_twohanded", twoHanded);
		p.Set("b_lower_weapon", false);

		if (!_fpGraphPrimed)
		{
			_fpGraphPrimed = true;
			p.Set("deploy_type", 1);
			p.Set("b_deploy_skip", true);
		}
	}

	/// <summary>Bonemerge bras sur le mesh <strong>monde</strong> (uniquement si pas de viewmodel 1P dédié).</summary>
	void TrySetupWorldArmsBonemerge(WeaponDefinition def)
	{
		DestroyWorldArmsBonemerge();

		if (def == null || def.UseFirstPersonViewModel)
			return;

		if (!BonemergeCitizenFirstPersonArms || !def.UseCitizenFpAnimParameters)
			return;

		if (!_weaponSkinned.IsValid() || !_weaponSkinned.UseAnimGraph)
			return;

		var armsPath = string.IsNullOrWhiteSpace(def.CitizenFpArmsModel)
			? DefaultFpCitizenArms
			: def.CitizenFpArmsModel;

		if (IsLikelyFirstPersonViewModelPath(armsPath))
			return;

		TryCreateArmsMergeOnTarget(WorldArmsAttachParent(), _weaponSkinned, armsPath, ref _armsMergeObject);
	}

	void TrySetupFpArmsBonemerge(WeaponDefinition def)
	{
		DestroyFpArmsBonemerge();

		if (def == null || !def.UseFirstPersonViewModel)
			return;

		if (!BonemergeCitizenFirstPersonArms || !def.FirstPersonMergeCitizenArms)
			return;

		if (!_fpWeaponSkinned.IsValid() || !_fpWeaponSkinned.UseAnimGraph)
			return;

		var armsPath = string.IsNullOrWhiteSpace(def.FirstPersonArmsModel)
			? DefaultFpCitizenArms
			: def.FirstPersonArmsModel.Trim();

		TryCreateArmsMergeOnTarget(FpArmsAttachParent(), _fpWeaponSkinned, armsPath, ref _fpArmsMergeObject);
	}

	static void TryCreateArmsMergeOnTarget(GameObject parentRoot, SkinnedModelRenderer weaponSkinned, string armsPath, ref GameObject armsGoSlot)
	{
		if (!parentRoot.IsValid() || !weaponSkinned.IsValid())
			return;

		var armsModel = Model.Load(WeaponDefinition.ToRuntimeModelPath(armsPath));
		if (!armsModel.IsValid)
		{
			Log.Warning($"Bras FP citizen introuvables : {armsPath}");
			return;
		}

		armsGoSlot = new GameObject(true);
		armsGoSlot.Name = "FP_Arms_BoneMerge";
		armsGoSlot.SetParent(parentRoot);

		var arms = armsGoSlot.Components.Create<SkinnedModelRenderer>();
		arms.Model = armsModel;
		arms.BoneMergeTarget = weaponSkinned;
		arms.UseAnimGraph = true;
	}

	void DestroyWorldArmsBonemerge()
	{
		if (_armsMergeObject.IsValid())
		{
			_armsMergeObject.Destroy();
			_armsMergeObject = null;
		}
	}

	void DestroyFpArmsBonemerge()
	{
		if (_fpArmsMergeObject.IsValid())
		{
			_fpArmsMergeObject.Destroy();
			_fpArmsMergeObject = null;
		}
	}

	void ApplyHoldType(WeaponDefinition def, string weaponIdent, PlayerCombatInfoComponent combat)
	{
		if (!_anim.IsValid())
			return;

		if (combat != null && combat.Team == TeamTypes.Spectators)
		{
			_anim.HoldType = CitizenAnimationHelper.HoldTypes.None;
			_anim.IkRightHand = null;
			if (_weaponRoot.IsValid())
				_weaponRoot.Enabled = false;
			if (_fpWeaponRoot.IsValid())
				_fpWeaponRoot.Enabled = false;
			return;
		}

		if (_weaponRoot.IsValid())
			_weaponRoot.Enabled = true;

		_anim.Handedness = CitizenAnimationHelper.Hand.Right;
		if (def != null)
			_anim.HoldType = def.GetCitizenHoldType();
		else if (IsSniperStyleIdent(weaponIdent))
			_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
		else
			_anim.HoldType = CitizenAnimationHelper.HoldTypes.Pistol;

		var boneFollow = SyncWorldHeldWeaponToHandBone
		                 && _weaponRoot.IsValid()
		                 && TryResolveRightHandBoneTransform(def, out _);
		var ikOff = boneFollow && DisableRightHandIkWhenSyncedToHandBone;
		var ikTarget = !ikOff && def != null && def.UseIkRightHandOnWeapon && _weaponRoot.IsValid()
			? _weaponRoot
			: null;
		_anim.IkRightHand = ikTarget;
	}

	static bool IsSniperStyleIdent(string weaponIdent)
	{
		var id = weaponIdent.Trim();
		return id.Equals("m700", StringComparison.OrdinalIgnoreCase)
		       || id.Contains("m700", StringComparison.OrdinalIgnoreCase)
		       || id.Contains("sniper", StringComparison.OrdinalIgnoreCase);
	}

	static bool IsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => IsLocalPawn(GameObject);
}
