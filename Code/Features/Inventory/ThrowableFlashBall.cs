namespace SniperVsRunners.Features.Inventory;

using Sandbox;
using SniperVsRunners.Components.Game;

/// <summary>
/// Grenade flash : déclenche un flash d’écran côté clients dans le rayon.
/// </summary>
public sealed class ThrowableFlashBall : Component
{
	public Vector3 Velocity;
	public float FuseSeconds = 1.8f;
	public float FlashRadius = 400f;
	public float FlashDurationSeconds = 2.2f;

	float _elapsed;

	protected override void OnFixedUpdate()
	{
		if (!Networking.IsHost)
			return;

		_elapsed += Time.Delta;
		Velocity += new Vector3(0f, 0f, -900f) * Time.Delta;
		WorldPosition += Velocity * Time.Delta;

		if (_elapsed < FuseSeconds)
			return;

		var session = GameComponent.Session;
		session?.GameObject.Components.Get<InventoryFxComponent>()?.RpcFlashAt(
			WorldPosition,
			FlashRadius,
			FlashDurationSeconds);

		DestroyGameObject();
	}
}
