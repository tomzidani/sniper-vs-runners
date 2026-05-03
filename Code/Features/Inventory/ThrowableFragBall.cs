namespace SniperVsRunners.Features.Inventory;

using Sandbox;
using SniperVsRunners.Features.Vitality;

/// <summary>
/// Grenade à fragmentation : simulation simple côté hôte uniquement.
/// </summary>
public sealed class ThrowableFragBall : Component
{
	public GameObject AttackerRoot { get; set; }
	public Vector3 Velocity;
	public float FuseSeconds = 2.4f;
	public float Damage;
	public float Radius;

	float _elapsed;

	protected override void OnFixedUpdate()
	{
		if (!Networking.IsHost)
			return;

		_elapsed += Time.Delta;
		Velocity += new Vector3(0f, 0f, -1200f) * Time.Delta;
		WorldPosition += Velocity * Time.Delta;

		if (_elapsed < FuseSeconds)
			return;

		FragExplosion.ApplyHost(AttackerRoot, WorldPosition, Radius, Damage);
		DestroyGameObject();
	}
}

static class FragExplosion
{
	public static void ApplyHost(GameObject attackerRoot, Vector3 center, float radius, float damage)
	{
		if (!Networking.IsHost)
			return;

		foreach (var vitality in Game.ActiveScene.GetAllComponents<PlayerVitalityComponent>())
		{
			if (!vitality.GameObject.IsValid() || vitality.IsDead)
				continue;

			var dist = (vitality.WorldPosition - center).Length;
			if (dist > radius)
				continue;

			vitality.ServerApplyExplosiveDamage(damage, attackerRoot);
		}
	}
}
