namespace SniperVsRunners.Features.Inventory;

using System.Linq;
using Sandbox;
using SniperVsRunners.Features.Spawning;
using SniperVsRunners.Managers;
using SniperVsRunners.Teams;

/// <summary>
/// Spawn automatique de pickups de test sur la map (aucune config éditeur).
/// </summary>
public static class InventoryPickupSpawner
{
	public static void SpawnDefaultPickupsIfHost()
	{
		if (!Networking.IsHost || !Game.ActiveScene.IsValid())
			return;

		if (Game.ActiveScene.GetAllComponents<InventoryMatchPickupsMarker>().Any())
			return;

		var anchor = ResolveSpawnAnchor();

		var markerGo = new GameObject(true);
		markerGo.Name = "Inventory_Pickups";
		markerGo.WorldPosition = anchor;
		markerGo.Components.Create<InventoryMatchPickupsMarker>();

		SpawnPickupAt(ItemKind.Medkit, 2, anchor + new Vector3(80f, 0f, 0f));
		SpawnPickupAt(ItemKind.Medkit, 1, anchor + new Vector3(110f, 0f, 20f));
		SpawnPickupAt(ItemKind.FragGrenade, 2, anchor + new Vector3(140f, 0f, -10f));
		SpawnPickupAt(ItemKind.FlashGrenade, 2, anchor + new Vector3(170f, 0f, 30f));
	}

	static Vector3 ResolveSpawnAnchor()
	{
		var fallback = new Vector3(0f, 0f, 128f);
		if (!TeamSpawnResolver.TryResolve(
			    GamePhase.Playing,
			    TeamTypes.Runners,
			    Game.ActiveScene,
			    null,
			    out var t))
			return fallback;

		return t.Position + new Vector3(0f, 0f, 40f);
	}

	public static void SpawnPickupAt(ItemKind kind, int count, Vector3 worldPosition)
	{
		if (!Networking.IsHost)
			return;

		var go = new GameObject(true);
		go.Name = $"Pickup {kind} x{count}";
		go.WorldPosition = worldPosition;

		var pickup = go.Components.Create<WorldPickupComponent>();
		pickup.Kind = kind;
		pickup.Count = count;

		var box = go.Components.Create<ModelRenderer>();
		box.Model = Model.Load("models/dev/box.vmdl");
		box.Tint = kind switch
		{
			ItemKind.Medkit => (Color.Parse("#4caf50") ?? new Color(0.3f, 0.69f, 0.31f)).WithAlpha(1f),
			ItemKind.FragGrenade => (Color.Parse("#ff5722") ?? new Color(1f, 0.34f, 0.13f)).WithAlpha(1f),
			ItemKind.FlashGrenade => (Color.Parse("#ffeb3b") ?? new Color(1f, 0.92f, 0.23f)).WithAlpha(1f),
			ItemKind.WeaponUsp => (Color.Parse("#42a5f5") ?? new Color(0.26f, 0.65f, 0.96f)).WithAlpha(1f),
			ItemKind.WeaponM700 => (Color.Parse("#8d6e63") ?? new Color(0.55f, 0.43f, 0.39f)).WithAlpha(1f),
			_ => Color.White
		};

		var collider = go.Components.Create<BoxCollider>();
		collider.IsTrigger = false;
		collider.Scale = new Vector3(20f, 20f, 20f);

		go.NetworkSpawn();
	}
}
