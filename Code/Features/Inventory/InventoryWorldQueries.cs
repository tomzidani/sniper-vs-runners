namespace SniperVsRunners.Features.Inventory;

using Sandbox;

static class InventoryWorldQueries
{
	public static WorldPickupComponent FindNearestPickup(Vector3 from, float maxDistance)
	{
		WorldPickupComponent best = null;
		var bestDist = maxDistance;

		foreach (var pickup in Game.ActiveScene.GetAllComponents<WorldPickupComponent>())
		{
			if (!pickup.GameObject.IsValid())
				continue;

			if (pickup.Kind == ItemKind.None || pickup.Count <= 0)
				continue;

			var d = pickup.WorldPosition - from;
			var dist = d.Length;
			if (dist < bestDist)
			{
				bestDist = dist;
				best = pickup;
			}
		}

		return best;
	}
}
