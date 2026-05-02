namespace SniperVsRunners.Features.Combat;

using Sandbox;
using SniperVsRunners.Features.Vitality;

/// <summary>
/// Trace « visée / tir » partagée : même rayon que le hitscan, résolution de victime et de zone.
/// Ignore le tag <see cref="PlayerMovementColliderTag"/> (capsule/boîte de déplacement du prefab joueur)
/// pour que le rayon atteigne les hitboxes du modèle avant le grossier collider de contrôleur.
/// </summary>
public static class CombatAimTrace
{
	public const string PlayerMovementColliderTag = "svc_player_movement";
	public static bool TryTraceDamageableTarget(
		Scene scene,
		GameObject attackerRoot,
		Vector3 start,
		Vector3 forward,
		float maxRange,
		out SceneTraceResult trace,
		out GameObject victimRoot,
		out BodyHitZone zone
	)
	{
		trace = default;
		victimRoot = default;
		zone = BodyHitZone.Torso;

		if (scene == null || !attackerRoot.IsValid())
			return false;

		var end = start + forward * maxRange;
		trace = scene.Trace
			.Ray(start, end)
			.UseHitboxes(true)
			.UseHitPosition(true)
			.WithoutTags("trigger", PlayerMovementColliderTag)
			.IgnoreGameObjectHierarchy(attackerRoot)
			.Run();

		if (!trace.Hit || trace.GameObject == null || !trace.GameObject.IsValid())
			return false;

		victimRoot = FindDamageableRoot(trace.GameObject);
		if (!victimRoot.IsValid() || victimRoot == attackerRoot)
			return false;

		var vitality = victimRoot.Components.Get<PlayerVitalityComponent>();
		if (vitality == null || vitality.IsDead)
			return false;

		zone = BodyZoneResolver.Resolve(victimRoot, trace);
		return true;
	}

	public static GameObject FindDamageableRoot(GameObject hitObject)
	{
		var go = hitObject;
		while (go.IsValid())
		{
			if (go.Components.Get<PlayerVitalityComponent>() != null)
				return go;
			go = go.Parent;
		}

		return default;
	}
}
