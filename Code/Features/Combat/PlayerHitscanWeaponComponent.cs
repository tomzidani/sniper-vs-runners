namespace SniperVsRunners.Features.Combat;

using Sandbox;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;
using SniperVsRunners.Teams;

/// <summary>
/// Tir hitscan : capacités issues de <see cref="WeaponDefinition"/> (ident sync côté hôte).
/// </summary>
public partial class PlayerHitscanWeaponComponent : Component
{
	/// <summary>Ident de la fiche <see cref="WeaponDefinition"/> (registre PostLoad).</summary>
	[Sync(SyncFlags.FromHost)]
	public string ActiveWeaponIdent { get; set; } = "usp";

	string _cachedIdent;
	WeaponDefinition _cachedDef;

	/// <summary>Résout la définition active (cache local par ident).</summary>
	public WeaponDefinition ResolveActiveDefinition()
	{
		if (ActiveWeaponIdent == _cachedIdent && _cachedDef != null)
			return _cachedDef;

		_cachedIdent = ActiveWeaponIdent;
		_cachedDef = WeaponDefinition.Resolve(ActiveWeaponIdent);
		return _cachedDef;
	}

	public float EffectiveMaxRange => ResolveActiveDefinition()?.MaxRange ?? 10_000f;

	public float EffectiveFireCooldown => ResolveActiveDefinition()?.FireCooldownSeconds ?? 0.35f;

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

		// Ne pas exiger WeaponDefinition en local : ResourceLibrary peut différer client/hôte ; l’hôte résout au RPC.
		var cd = EffectiveFireCooldown;
		_nextFireTime = Time.Now + cd;
		HostRequestPrimaryFire(pc.EyePosition, pc.EyeAngles.Forward);
	}

	bool IsDeadLocally()
	{
		var v = Components.Get<PlayerVitalityComponent>();
		return v != null && v.IsDead;
	}

	bool CanFireThisFrame()
	{
		var flow = MatchFlowComponent.Current;
		if (flow != null)
		{
			// InMatch + court créneau LoadingArena (même frame / ordre composants après chargement map).
			if (flow.SessionPhase != MatchSessionPhase.InMatch
			    && flow.SessionPhase != MatchSessionPhase.LoadingArena)
				return false;
		}

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

		// Le RPC est invoqué sur le composant du pawn du tireur ; pas de garde OwnerId (souvent vide / incohérent selon timing listen-server).
		if (!CanFireThisFrame())
			return;

		var def = WeaponDefinition.Resolve(ActiveWeaponIdent);
		if (def == null)
			return;

		forward = WeaponSpread.ApplyCone(forward, def.SpreadHalfAngleDegrees);

		if (!CombatAimTrace.TryTraceDamageableTarget(
			    Scene,
			    GameObject,
			    start,
			    forward,
			    def.MaxRange,
			    out var tr,
			    out var victimRoot,
			    out var zone))
			return;

		var vitality = victimRoot.Components.Get<PlayerVitalityComponent>();
		if (vitality == null || vitality.IsDead)
			return;

		vitality.ServerApplyHit(GameObject, ActiveWeaponIdent, zone);
	}

	static bool ComputeIsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => ComputeIsLocalPawn(GameObject);
}
