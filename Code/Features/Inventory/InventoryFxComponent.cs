namespace SniperVsRunners.Features.Inventory;

using Sandbox;
using SniperVsRunners.Features.Hud;

/// <summary>
/// Sur l’objet session : effets globaux (flash) via RPC.
/// </summary>
public sealed class InventoryFxComponent : Component
{
	[Rpc.Broadcast]
	public void RpcFlashAt(Vector3 worldPos, float radius, float durationSeconds)
	{
		foreach (var pc in Game.ActiveScene.GetAllComponents<PlayerController>())
		{
			if (!pc.Network.IsOwner)
				continue;

			if (!pc.GameObject.IsValid())
				continue;

			var dist = (pc.WorldPosition - worldPos).Length;
			if (dist > radius)
				continue;

			pc.GameObject.Components.GetOrCreate<PlayerFlashHud>()?.PlayFlash(durationSeconds);
		}
	}
}
