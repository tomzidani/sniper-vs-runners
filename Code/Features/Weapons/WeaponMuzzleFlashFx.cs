namespace SniperVsRunners.Features.Weapons;

using System;
using System.Collections.Generic;
using Sandbox;

/// <summary>
/// Flash de bouche : clone du prefab <strong>enfant direct</strong> de <c>HeldWeapon</c> (3P) ou <c>FirstPersonHeldWeapon</c> (1P),
/// en mode bone-only (os obligatoire). Les particules sont forcées en espace local pour suivre le mouvement.
/// </summary>
public static class WeaponMuzzleFlashFx
{
	const float PrefabInstanceDestroyHoldSeconds = 0.35f;

	const string MuzzleFlashParticleSpriteResourcePath = "textures/fx/muzzle-flash/muzzle-flash.sprite";

	/// <param name="followParent">
	/// <c>HeldWeapon</c> / <c>HeldVisual</c> (monde ou 1P) : parent du clone ; doit suivre l’arme.
	/// </param>
	/// <param name="boneSource">Squelette qui fournit l’os de bouche (souvent le renderer du mesh arme).</param>
	public static void SpawnIfEnabled(
		Scene scene,
		WeaponDefinition def,
		GameObject followParent,
		Angles visualLocalAngles,
		SkinnedModelRenderer boneSource = null,
		string followBoneName = null)
	{
		if (scene == null || def == null)
			return;

		var prefab = def.MuzzleFlashPrefab?.Trim() ?? "";
		if (prefab.Length == 0)
			return;

		if (boneSource == null || !boneSource.IsValid() || string.IsNullOrWhiteSpace(followBoneName))
			return;

		var boneName = followBoneName.Trim();
		if (!boneSource.TryGetBoneTransform(boneName, out var initialBone))
			return;

		SpawnPrefabFlash(
			scene,
			def,
			followParent,
			visualLocalAngles,
			prefab,
			boneSource,
			boneName,
			initialBone);
	}

	static void SpawnPrefabFlash(
		Scene scene,
		WeaponDefinition def,
		GameObject followParent,
		Angles visualLocalAngles,
		string prefabPath,
		SkinnedModelRenderer boneSource,
		string followBoneName,
		Transform initialBone)
	{
		WarmupMuzzleFlashParticleSprite();
		var fine = visualLocalAngles.ToRotation();
		var worldPosInst = initialBone.Position;
		var worldRotInst = initialBone.Rotation * fine;

		GameObject cloneParent = null;
		var cloneXf = new Transform(worldPosInst, worldRotInst, 1f);

		if (followParent != null && followParent.IsValid())
		{
			var lp = followParent.WorldRotation.Inverse * (worldPosInst - followParent.WorldPosition);
			var lr = followParent.WorldRotation.Inverse * worldRotInst;
			cloneXf = new Transform(lp, lr, 1f);
			cloneParent = followParent;
		}

		GameObject inst = default;
		var tried = new List<string>();
		foreach (var path in BuildPrefabPathCandidates(prefabPath))
		{
			tried.Add(path);
			try
			{
				if (TryClonePrefabInstance(scene, path, cloneParent, cloneXf, out inst) && inst.IsValid())
					break;
			}
			catch (Exception e)
			{
				Log.Warning($"WeaponMuzzleFlashFx: essai « {path} » : {e.Message}");
			}
		}

		if (!inst.IsValid())
		{
			Log.Warning(
				$"WeaponMuzzleFlashFx: prefab introuvable — « {prefabPath} ». Chemins essayés : {string.Join(" | ", tried)}. "
				+ "Vérifie la .weapon et la compilation du .prefab.");
			return;
		}

		FinalizeMuzzleFxInstance(inst, def);

		var follow = inst.Components.Create<MuzzleFlashBoneFollowComponent>();
		follow.Skin = boneSource;
		follow.BoneName = followBoneName;
		follow.FineRotation = fine;
	}

	static List<string> BuildPrefabPathCandidates(string raw)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var list = new List<string>();
		void Add(string p)
		{
			p = (p ?? "").Replace('\\', '/').Trim().TrimStart('/');
			if (p.Length == 0 || !seen.Add(p))
				return;
			list.Add(p);
		}

		var s = raw.Replace('\\', '/').Trim();
		Add(s);
		Add(s.TrimStart('/'));
		if (!s.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
			Add("assets/" + s.TrimStart('/'));

		return list;
	}

	/// <summary>
	/// <paramref name="parent"/> null : clone racine scène avec <paramref name="xf"/> monde. Sinon : clone enfant avec transform <strong>local</strong> sous le parent.
	/// </summary>
	static bool TryClonePrefabInstance(Scene scene, string path, GameObject parent, Transform xf, out GameObject inst)
	{
		inst = default;

		var template = GameObject.GetPrefab(path);
		if (template.IsValid())
		{
			if (scene != null && scene.IsValid())
			{
				using (scene.Push())
					inst = parent != null && parent.IsValid()
						? template.Clone(xf, parent, true, "WeaponMuzzleFlashFx")
						: template.Clone(xf, name: "WeaponMuzzleFlashFx");
			}
			else
				inst = parent != null && parent.IsValid()
					? template.Clone(xf, parent, true, "WeaponMuzzleFlashFx")
					: template.Clone(xf, name: "WeaponMuzzleFlashFx");

			return inst.IsValid();
		}

		if (ResourceLibrary.TryGet<PrefabFile>(path, out var pf) && pf != null)
		{
			if (scene != null && scene.IsValid())
			{
				using (scene.Push())
					inst = parent != null && parent.IsValid()
						? GameObject.Clone(pf, xf, parent, true, "WeaponMuzzleFlashFx")
						: GameObject.Clone(pf, xf, null, true, "WeaponMuzzleFlashFx");
			}
			else
				inst = parent != null && parent.IsValid()
					? GameObject.Clone(pf, xf, parent, true, "WeaponMuzzleFlashFx")
					: GameObject.Clone(pf, xf, null, true, "WeaponMuzzleFlashFx");

			return inst.IsValid();
		}

		return false;
	}

	static void FinalizeMuzzleFxInstance(GameObject inst, WeaponDefinition def)
	{
		inst.NetworkMode = NetworkMode.Never;
		ForceParticleEffectsFollowParentTransform(inst);
		KickstartParticleHierarchy(inst);
		RebindClonedParticleSprites(inst);

		if (def != null && def.MuzzleFlashPersistForTuning)
			return;

		var life = inst.Components.Get<TimedDestroyComponent>();
		if (life == null)
			life = inst.Components.Create<TimedDestroyComponent>();
		life.DestroyAtTime = Time.Now + PrefabInstanceDestroyHoldSeconds;
	}

	/// <summary>
	/// Avec <c>LocalSpace</c> à 0, les particules restent en coordonnées monde et ne suivent pas le parent qui bouge. On force 1 (doc : suivi relatif à l’émetteur).
	/// </summary>
	static void ForceParticleEffectsFollowParentTransform(GameObject root)
	{
		if (!root.IsValid())
			return;

		var one = new ParticleFloat(1f, 1f);
		foreach (var pe in root.Components.GetAll<ParticleEffect>(FindMode.EverythingInSelfAndDescendants))
		{
			if (pe == null || !pe.IsValid())
				continue;
			pe.LocalSpace = one;
		}
	}

	static void KickstartParticleHierarchy(GameObject root)
	{
		if (!root.IsValid())
			return;

		var rootKeep = root.Enabled;
		root.Enabled = false;
		root.Enabled = rootKeep;
	}

	static void WarmupMuzzleFlashParticleSprite()
	{
		ResourceLibrary.TryGet<Sprite>(MuzzleFlashParticleSpriteResourcePath, out _);
		if (!MuzzleFlashParticleSpriteResourcePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
			ResourceLibrary.TryGet<Sprite>("assets/" + MuzzleFlashParticleSpriteResourcePath.TrimStart('/'), out _);
	}

	static void RebindClonedParticleSprites(GameObject root)
	{
		if (!root.IsValid())
			return;

		Sprite resolved = null;
		if (!ResourceLibrary.TryGet<Sprite>(MuzzleFlashParticleSpriteResourcePath, out resolved) || resolved == null)
		{
			var alt = "assets/" + MuzzleFlashParticleSpriteResourcePath.TrimStart('/');
			ResourceLibrary.TryGet<Sprite>(alt, out resolved);
		}

		if (resolved == null)
			return;

		foreach (var r in root.Components.GetAll<ParticleSpriteRenderer>(FindMode.EverythingInSelfAndDescendants))
		{
			if (r == null || !r.IsValid())
				continue;
			r.Sprite = resolved;
		}
	}

	sealed class TimedDestroyComponent : Component
	{
		public float DestroyAtTime { get; set; }

		protected override void OnUpdate()
		{
			if (Time.Now >= DestroyAtTime)
				GameObject.Destroy();
		}
	}

	/// <summary>Aligne le flash sur un os du viewmodel pour qu’il suive recoil / anim.</summary>
	sealed class MuzzleFlashBoneFollowComponent : Component
	{
		public SkinnedModelRenderer Skin { get; set; }
		public string BoneName { get; set; } = "";
		public Rotation FineRotation { get; set; }

		protected override void OnUpdate()
		{
			if (Skin == null || !Skin.IsValid() || string.IsNullOrWhiteSpace(BoneName))
			{
				GameObject.Enabled = false;
				return;
			}

			if (!Skin.TryGetBoneTransform(BoneName, out var bone))
			{
				GameObject.Enabled = false;
				return;
			}

			GameObject.Enabled = true;
			GameObject.WorldPosition = bone.Position;
			GameObject.WorldRotation = bone.Rotation * FineRotation;
		}
	}
}
