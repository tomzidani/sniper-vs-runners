namespace SniperVsRunners.Features.Inventory;

using System;
using Sandbox;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Features.Vitality;

/// <summary>
/// Inventaire 9 slots (utilisables), autorité hôte + synchro <see cref="InvPayload"/>.
/// </summary>
public sealed class PlayerInventoryComponent : Component
{
	public const int SlotCount = 9;

	readonly ItemKind[] _kinds = new ItemKind[SlotCount];
	readonly int[] _counts = new int[SlotCount];

	[Sync(SyncFlags.FromHost)]
	public string InvPayload { get; set; } = "";

	bool _wheelOpen;
	int _localQuickSlot;

	public bool IsWheelOpen => _wheelOpen;
	public int LocalQuickSlotIndex => _localQuickSlot;

	protected override void OnUpdate()
	{
		if (!IsLocalPawn())
			return;

		var v = Components.Get<PlayerVitalityComponent>();
		if (v != null && v.IsDead)
			return;

		var flow = MatchFlowComponent.Current;
		if (flow != null
		    && flow.SessionPhase != MatchSessionPhase.InMatch
		    && flow.SessionPhase != MatchSessionPhase.LoadingArena)
			return;

		if (Input.Keyboard.Pressed("tab"))
			_wheelOpen = !_wheelOpen;

		var wheel = Input.MouseWheel.y;
		if (Math.Abs(wheel) > 0.08f)
		{
			_localQuickSlot = (_localQuickSlot + (wheel > 0 ? 8 : 1)) % SlotCount;
			_wheelOpen = true;
		}

		if (Input.Keyboard.Pressed("g"))
			HostRequestUseSlot(_localQuickSlot);

		for (var i = 0; i < 9; i++)
		{
			if (Input.Keyboard.Pressed($"Digit{i + 1}"))
				HostRequestUseSlot(i);
		}

		if (Input.Pressed("use"))
			HostRequestPickupNearby();
	}

	static bool IsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => IsLocalPawn(GameObject);

	[Rpc.Host]
	public void HostRequestPickupNearby()
	{
		if (!Networking.IsHost)
			return;

		var v = Components.Get<PlayerVitalityComponent>();
		if (v == null || v.IsDead)
			return;

		var pickup = InventoryWorldQueries.FindNearestPickup(WorldPosition + Vector3.Up * 24f, 96f);
		if (pickup == null)
			return;

		TryMergePickupHost(pickup);
	}

	void TryMergePickupHost(WorldPickupComponent pickup)
	{
		var kind = pickup.Kind;
		var count = pickup.Count;
		if (!ItemDefinition.IsDefined(kind) || count <= 0)
			return;

		var def = ItemDefinition.Get(kind);
		if (string.IsNullOrEmpty(def.DisplayName))
			return;

		var remaining = AddItemsReturnRemaining(kind, count);
		if (remaining == count)
			return;

		pickup.Count = remaining;
		if (pickup.Count <= 0)
			pickup.GameObject.Destroy();
	}

	int AddItemsReturnRemaining(ItemKind kind, int count)
	{
		var def = ItemDefinition.Get(kind);
		var max = def.MaxStack;
		if (!ItemDefinition.IsDefined(kind) || max <= 0 || count <= 0)
			return count;

		var left = count;
		while (left > 0)
		{
			var merge = FindMergeableSlot(kind, max);
			if (merge >= 0)
			{
				var space = max - _counts[merge];
				var take = Math.Min(space, left);
				_counts[merge] += take;
				left -= take;
				PushInventorySync();
				continue;
			}

			var empty = FindEmptySlot();
			if (empty < 0)
				return left;

			var takeEmpty = Math.Min(max, left);
			_kinds[empty] = kind;
			_counts[empty] = takeEmpty;
			left -= takeEmpty;
			PushInventorySync();
		}

		return 0;
	}

	int FindMergeableSlot(ItemKind kind, int maxStack)
	{
		for (var i = 0; i < SlotCount; i++)
		{
			if (_kinds[i] == kind && _counts[i] > 0 && _counts[i] < maxStack)
				return i;
		}

		return -1;
	}

	int FindEmptySlot()
	{
		for (var i = 0; i < SlotCount; i++)
		{
			if (_kinds[i] == ItemKind.None || _counts[i] <= 0)
				return i;
		}

		return -1;
	}

	[Rpc.Host]
	public void HostRequestUseSlot(int slot)
	{
		if (!Networking.IsHost)
			return;

		if (slot < 0 || slot >= SlotCount)
			return;

		var v = Components.Get<PlayerVitalityComponent>();
		if (v == null || v.IsDead)
			return;

		var kind = _kinds[slot];
		if (kind == ItemKind.None || _counts[slot] <= 0)
			return;

		var def = ItemDefinition.Get(kind);
		if (string.IsNullOrEmpty(def.DisplayName))
			return;

		switch (def.UseMode)
		{
			case ItemUseMode.ConsumeHeal:
				v.ServerApplyHeal(def.HealAmount);
				ConsumeOne(slot);
				break;

			case ItemUseMode.ThrowExplosive:
				ThrowFragHost(def);
				ConsumeOne(slot);
				break;

			case ItemUseMode.ThrowFlash:
				ThrowFlashHost(def);
				ConsumeOne(slot);
				break;

			case ItemUseMode.EquipWeaponUsp:
			{
				var hit = Components.Get<PlayerHitscanWeaponComponent>();
				if (hit != null)
				{
					hit.ActiveWeaponIdent = "usp";
					hit.HostApplyEquippedWeaponAmmo();
				}

				break;
			}

			case ItemUseMode.EquipWeaponM700:
			{
				var hit = Components.Get<PlayerHitscanWeaponComponent>();
				if (hit != null)
				{
					hit.ActiveWeaponIdent = "m700";
					hit.HostApplyEquippedWeaponAmmo();
				}

				break;
			}
		}
	}

	void ConsumeOne(int slot)
	{
		_counts[slot] = Math.Max(0, _counts[slot] - 1);
		if (_counts[slot] <= 0)
			_kinds[slot] = ItemKind.None;

		PushInventorySync();
	}

	void ThrowFragHost(ItemDefinition def)
	{
		var pc = Components.Get<PlayerController>();
		if (pc == null)
			return;

		var start = pc.EyePosition + pc.EyeAngles.Forward * 24f;
		var vel = pc.EyeAngles.Forward * def.ThrowSpeed;

		var go = new GameObject(true);
		go.Name = "Frag (host)";
		go.WorldPosition = start;
		go.WorldScale = 0.35f;

		var m = go.Components.Create<ModelRenderer>();
		m.Model = Model.Load("models/dev/box.vmdl");
		m.Tint = Color.Parse("#ff5722") ?? new Color(1f, 0.34f, 0.13f);

		var frag = go.Components.Create<ThrowableFragBall>();
		frag.AttackerRoot = GameObject;
		frag.Velocity = vel;
		frag.Damage = def.FragDamage;
		frag.Radius = def.FragRadius;
	}

	void ThrowFlashHost(ItemDefinition def)
	{
		var pc = Components.Get<PlayerController>();
		if (pc == null)
			return;

		var start = pc.EyePosition + pc.EyeAngles.Forward * 24f;
		var vel = pc.EyeAngles.Forward * def.ThrowSpeed;

		var go = new GameObject(true);
		go.Name = "Flash (host)";
		go.WorldPosition = start;
		go.WorldScale = 0.35f;

		var m = go.Components.Create<ModelRenderer>();
		m.Model = Model.Load("models/dev/box.vmdl");
		m.Tint = Color.Parse("#ffeb3b") ?? new Color(1f, 0.92f, 0.23f);

		var flash = go.Components.Create<ThrowableFlashBall>();
		flash.Velocity = vel;
		flash.FlashRadius = def.FlashRadius;
		flash.FlashDurationSeconds = def.FlashDurationSeconds;
	}

	public void ServerClearForSpawn()
	{
		if (!Networking.IsHost)
			return;

		for (var i = 0; i < SlotCount; i++)
		{
			_kinds[i] = ItemKind.None;
			_counts[i] = 0;
		}

		PushInventorySync();
	}

	/// <summary>
	/// Après <see cref="ServerClearForSpawn"/> : slots 1–2 = raccourcis pour équiper USP / M700 (tests rendu, touches 1 et 2).
	/// </summary>
	public void ServerGiveDefaultWeaponTestSlots()
	{
		if (!Networking.IsHost)
			return;

		_kinds[0] = ItemKind.WeaponUsp;
		_counts[0] = 1;
		_kinds[1] = ItemKind.WeaponM700;
		_counts[1] = 1;
		PushInventorySync();
	}

	public void ServerDropAllToWorld()
	{
		if (!Networking.IsHost)
			return;

		var origin = WorldPosition + Vector3.Up * 8f;
		var idx = 0;

		for (var i = 0; i < SlotCount; i++)
		{
			var k = _kinds[i];
			var c = _counts[i];
			if (k == ItemKind.None || c <= 0)
				continue;

			var ring = (Vector3.Right * MathF.Cos(idx * 0.9f) + Vector3.Forward * MathF.Sin(idx * 0.9f)) * 28f;
			InventoryPickupSpawner.SpawnPickupAt(k, c, origin + ring + Vector3.Up * (idx % 3) * 6f);
			idx++;
		}

		ServerClearForSpawn();
	}

	void PushInventorySync()
	{
		InvPayload = EncodeSlots();
	}

	string EncodeSlots()
	{
		var parts = new string[SlotCount];
		for (var i = 0; i < SlotCount; i++)
			parts[i] = $"{(int)_kinds[i]}:{_counts[i]}";

		return string.Join("|", parts);
	}

	public static void DecodeSlots(string payload, ItemKind[] kindsOut, int[] countsOut)
	{
		for (var i = 0; i < SlotCount; i++)
		{
			kindsOut[i] = ItemKind.None;
			countsOut[i] = 0;
		}

		if (string.IsNullOrWhiteSpace(payload))
			return;

		var segments = payload.Split('|');
		for (var i = 0; i < SlotCount && i < segments.Length; i++)
		{
			var seg = segments[i].Split(':');
			if (seg.Length < 2)
				continue;

			if (!int.TryParse(seg[0], out var k))
				continue;
			if (!int.TryParse(seg[1], out var c))
				continue;

			kindsOut[i] = (ItemKind)k;
			countsOut[i] = c;
		}
	}
}
