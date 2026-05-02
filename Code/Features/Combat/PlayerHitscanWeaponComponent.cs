namespace SniperVsRunners.Features.Combat;

using Sandbox;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;
using SniperVsRunners.Teams;

/// <summary>
/// Tir hitscan : le propriétaire envoie une requête à l’hôte, qui trace et applique les dégâts.
/// </summary>
public partial class PlayerHitscanWeaponComponent : Component
{
	[Property] public float MaxRange { get; set; } = 10_000f;

	[Property] public float FireCooldownSeconds { get; set; } = 0.35f;

	[Sync(SyncFlags.FromHost)]
	public WeaponProfileKind ActiveProfile { get; set; } = WeaponProfileKind.Sidearm;

	float _nextFireTime;

	protected override void OnUpdate()
	{
		if (IsDeadLocally())
			return;

		if (!CanFireThisFrame())
			return;

		if (!IsLocalPawn())
			return;

		if (!Input.Pressed("Attack1"))
			return;

		if (Time.Now < _nextFireTime)
			return;

		var pc = Components.Get<PlayerController>();
		if (pc == null)
			return;

		var forward = pc.EyeAngles.Forward;
		_nextFireTime = Time.Now + FireCooldownSeconds;
		HostRequestPrimaryFire(pc.EyePosition, forward);
	}

	bool IsDeadLocally()
	{
		var v = Components.Get<PlayerVitalityComponent>();
		return v != null && v.IsDead;
	}

	bool CanFireThisFrame()
	{
		var flow = MatchFlowComponent.Current;
		if (flow != null && flow.SessionPhase != MatchSessionPhase.InMatch)
			return false;

		var info = Components.Get<PlayerCombatInfoComponent>();
		if (info == null || info.Team == TeamTypes.Spectators)
			return false;

		return true;
	}

	[Rpc.Host]
	public void HostRequestPrimaryFire(Vector3 start, Vector3 forward)
	{
		if (!Networking.IsHost)
			return;

		if (Rpc.Caller != GameObject.Network.Owner)
			return;

		if (!CanFireThisFrame())
			return;

		if (!CombatAimTrace.TryTraceDamageableTarget(
			    Scene,
			    GameObject,
			    start,
			    forward,
			    MaxRange,
			    out var tr,
			    out var victimRoot,
			    out var zone))
			return;

		var vitality = victimRoot.Components.Get<PlayerVitalityComponent>();
		if (vitality == null || vitality.IsDead)
			return;

		vitality.ServerApplyHit(GameObject, ActiveProfile, zone);
	}

	static bool ComputeIsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => ComputeIsLocalPawn(GameObject);
}
