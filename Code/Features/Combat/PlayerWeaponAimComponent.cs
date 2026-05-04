namespace SniperVsRunners.Features.Combat;

using System;
using Sandbox;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;
using SniperVsRunners.Teams;

/// <summary>
/// État de visée (ADS) : input local (hold/toggle), autorité hôte pour le gameplay, interpolation visuelle.
/// </summary>
public sealed class PlayerWeaponAimComponent : Component
{
	[Sync(SyncFlags.FromHost)]
	public bool IsAiming { get; set; }

	/// <summary>Alpha local de transition ADS [0..1], utilisé pour viewmodel/HUD.</summary>
	public float AimAlphaVisual { get; private set; }

	bool _localDesiredAim;
	bool _lastSentAim;

	protected override void OnUpdate()
	{
		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		var def = weapon?.ResolveActiveDefinition();
		var canAim = CanAimThisFrame(weapon, def);

		if (IsLocalPawn())
		{
			UpdateLocalDesiredAim(def, canAim);
			PushDesiredAimToAuthority(canAim);
		}
		else if (!canAim && IsAuthorityForGameplay())
		{
			IsAiming = false;
		}

		UpdateVisualAlpha(def, canAim);
	}

	public bool IsAimingForDefinition(WeaponDefinition def)
	{
		if (def == null || !def.AimEnabled)
			return false;

		return IsAiming;
	}

	void UpdateLocalDesiredAim(WeaponDefinition def, bool canAim)
	{
		if (!canAim || def == null || !def.AimEnabled)
		{
			_localDesiredAim = false;
			return;
		}

		if (def.AimActivation == WeaponDefinition.AimActivationMode.Toggle)
		{
			if (IsSecondaryAttackPressed())
				_localDesiredAim = !_localDesiredAim;
			return;
		}

		_localDesiredAim = IsSecondaryAttackDown();
	}

	void PushDesiredAimToAuthority(bool canAim)
	{
		var desired = canAim && _localDesiredAim;
		if (!canAim)
			desired = false;

		if (desired == _lastSentAim)
			return;

		_lastSentAim = desired;

		if (!Networking.IsActive)
		{
			if (IsAuthorityForGameplay())
				IsAiming = desired;
			return;
		}

		HostSetAiming(desired);
	}

	void UpdateVisualAlpha(WeaponDefinition def, bool canAim)
	{
		var targetAiming = ResolveVisualIsAiming(def, canAim);
		var target = targetAiming ? 1f : 0f;

		var inSeconds = Math.Max(0.01f, def?.AimTransitionInSeconds ?? 0.12f);
		var outSeconds = Math.Max(0.01f, def?.AimTransitionOutSeconds ?? 0.1f);

		var current = AimAlphaVisual;
		if (Math.Abs(target - current) < 0.0001f)
		{
			AimAlphaVisual = target;
			return;
		}

		var speed = target > current ? (1f / inSeconds) : (1f / outSeconds);
		AimAlphaVisual = Math.Clamp(current + Time.Delta * speed * MathF.Sign(target - current), 0f, 1f);
	}

	bool ResolveVisualIsAiming(WeaponDefinition def, bool canAim)
	{
		if (!canAim || def == null || !def.AimEnabled)
			return false;

		// Pour le rendu local (viewmodel/FOV/HUD), on suit l’intention du joueur
		// afin d’éviter un pompage visuel dû aux allers-retours réseau.
		if (IsLocalPawn())
			return _localDesiredAim;

		return IsAiming;
	}

	bool CanAimThisFrame(PlayerHitscanWeaponComponent weapon, WeaponDefinition def)
	{
		if (weapon == null || def == null || !def.AimEnabled)
			return false;

		if (!GameComponent.AreWeaponsAndInventoryUnlockedForCurrentSession())
			return false;

		var v = Components.Get<PlayerVitalityComponent>();
		if (v != null && v.IsDead)
			return false;

		if (weapon.IsReloading)
			return false;

		var info = Components.Get<PlayerCombatInfoComponent>();
		if (info == null || info.Team == TeamTypes.Spectators)
			return false;

		return true;
	}

	[Rpc.Host]
	public void HostSetAiming(bool aiming)
	{
		if (!Networking.IsHost)
			return;

		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		var def = weapon?.ResolveActiveDefinition();
		var canAim = CanAimThisFrame(weapon, def);
		IsAiming = canAim && aiming;
	}

	static bool IsAuthorityForGameplay()
	{
		if (!Networking.IsActive)
			return true;
		return Networking.IsHost;
	}

	static bool ComputeIsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => ComputeIsLocalPawn(GameObject);

	static bool IsSecondaryAttackDown()
		=> Input.Down("Attack2") || Input.Down("attack2");

	static bool IsSecondaryAttackPressed()
		=> Input.Pressed("Attack2") || Input.Pressed("attack2");
}
