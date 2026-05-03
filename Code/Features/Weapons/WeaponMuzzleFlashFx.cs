namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

/// <summary>
/// Flash de bouche minimaliste : petit burst lumineux devant le canon.
/// Basé sur un modèle dev pour rester autonome sans asset VFX dédié.
/// </summary>
public static class WeaponMuzzleFlashFx
{
	const string FlashModel = "models/dev/box.vmdl";
	const float FlashModelDepthUnits = 50f;

	public static void SpawnIfEnabled(Scene scene, Vector3 muzzleWorld, Vector3 shotForwardWorld, WeaponDefinition def)
	{
		if (scene == null || def == null || !def.MuzzleFlashEnabled)
			return;

		var go = new GameObject(true);
		go.Name = "WeaponMuzzleFlash";
		// Position déjà ajustée par l’appelant (bouche + offset arme) ; pas de décalage forward ici pour éviter d’entrer dans le mesh.
		go.WorldPosition = muzzleWorld;
		go.WorldRotation = Rotation.LookAt(shotForwardWorld.Normal, Vector3.Up);

		var duration = Math.Max(0.01f, def.MuzzleFlashDuration);
		var scale = Math.Clamp(def.MuzzleFlashScale, 0.05f, 3f);
		go.WorldScale = new Vector3(scale, scale, Math.Max(0.05f, scale * 0.45f / FlashModelDepthUnits));

		var m = go.Components.Create<ModelRenderer>();
		m.Model = Model.Load(FlashModel);
		m.Tint = def.MuzzleFlashColor.WithAlpha(Math.Clamp(def.MuzzleFlashColor.a > 0f ? def.MuzzleFlashColor.a : 0.92f, 0.2f, 1f));

		var life = go.Components.Create<TimedDestroyComponent>();
		life.DestroyAtTime = Time.Now + duration;
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
}
