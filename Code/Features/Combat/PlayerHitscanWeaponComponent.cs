namespace SniperVsRunners.Features.Combat;

using System;
using System.Collections.Generic;
using Sandbox;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;
using SniperVsRunners.Teams;

/// <summary>
/// Tir hitscan : capacités issues de <see cref="WeaponDefinition"/> (ident sync côté hôte).
/// <see cref="FireFxSequence"/> incrémenté côté hôte à chaque tir valide pour synchroniser les impulsions d’anim (3P / corps).
/// <see cref="FireTracerAuthoritativeFlightSeconds"/> : durée de vol tracer (hôte), répliquée ; multijoueur : spawn du tracer via Rpc broadcast hôte sur le composant visuel du tireur.
/// Munitions + rechargement : autorité hôte, <see cref="ReloadFxSequence"/> pour les anims reload.
/// </summary>
public partial class PlayerHitscanWeaponComponent : Component
{
	/// <summary>Ident de la fiche <see cref="WeaponDefinition"/> (registre PostLoad).</summary>
	[Sync(SyncFlags.FromHost)]
	public string ActiveWeaponIdent { get; set; } = "usp";

	/// <summary>
	/// Incrémenté sur l’hôte à chaque tir accepté (même sans cible) ; tous les clients observent le même compteur pour pousser <c>b_attack</c> sur le Citizen.
	/// </summary>
	[Sync(SyncFlags.FromHost)]
	public uint FireFxSequence { get; set; }

	[Sync(SyncFlags.FromHost)]
	public int AmmoInMag { get; set; }

	[Sync(SyncFlags.FromHost)]
	public int AmmoReserve { get; set; }

	[Sync(SyncFlags.FromHost)]
	public bool IsReloading { get; set; }

	/// <summary>Horodatage moteur : fin prévue du rechargement (voir <see cref="Time.Now"/>).</summary>
	[Sync(SyncFlags.FromHost)]
	public float ReloadEndTime { get; set; }

	/// <summary>Incrémenté au début d’un rechargement accepté (sync anim reload).</summary>
	[Sync(SyncFlags.FromHost)]
	public uint ReloadFxSequence { get; set; }

	/// <summary>Fin du rayon du dernier tir (sync hôte) pour tracer / FX ; même trace que les dégâts.</summary>
	[Sync(SyncFlags.FromHost)]
	public Vector3 FireTracerEndWorld { get; set; }

	/// <summary>Direction initiale du tir validé (après spread + drop) pour tracer visuel courbe.</summary>
	[Sync(SyncFlags.FromHost)]
	public Vector3 FireTracerDirectionWorld { get; set; }

	/// <summary>Point de départ du dernier tir validé (source autoritaire hôte).</summary>
	[Sync(SyncFlags.FromHost)]
	public Vector3 FireTracerStartWorld { get; set; }

	/// <summary>
	/// Durée de vol du tracer (s) calculée sur l’hôte (émission bouche/œil → impact) ; répliquée pour le FX et alignée sur le retard des dégâts différés.
	/// </summary>
	[Sync(SyncFlags.FromHost)]
	public float FireTracerAuthoritativeFlightSeconds { get; set; }

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

	const int MaxPendingHitscanHits = 64;

	readonly List<PendingHitscanHit> _pendingHitscanHits = new();

	struct PendingHitscanHit
	{
		public float ApplyAtTime;
		public GameObject VictimRoot;
		public GameObject AttackerRoot;
		public BodyHitZone Zone;
		public string WeaponIdent;
	}

	/// <summary>Remplit chargeur + réserve depuis la définition active ; annule un rechargement en cours. À appeler sur l’hôte au spawn ou après changement d’arme.</summary>
	public void HostApplyEquippedWeaponAmmo()
	{
		if (Networking.IsActive && !Networking.IsHost)
			return;

		_pendingHitscanHits.Clear();

		IsReloading = false;
		ReloadEndTime = 0f;

		var def = WeaponDefinition.Resolve(ActiveWeaponIdent);
		if (def == null)
		{
			AmmoInMag = 0;
			AmmoReserve = 0;
			return;
		}

		AmmoInMag = Math.Max(0, def.MagazineSize);
		AmmoReserve = def.InfiniteReserve ? 0 : Math.Max(0, def.StartingReserveAmmo);
	}

	protected override void OnUpdate()
	{
		if (IsAuthorityForGameplay())
		{
			TickReloadAuthority();
			TickPendingHitscanHitsAuthority();
		}

		if (IsDeadLocally())
			return;

		if (!IsLocalPawn())
			return;

		if (!CanUseWeaponThisFrame())
			return;

		if (Input.Pressed("Reload"))
		{
			var reloadDef = ResolveActiveDefinition();
			if (reloadDef != null)
			{
				var cap = Math.Max(0, reloadDef.MagazineSize);
				if (AmmoInMag < cap
				    && (reloadDef.InfiniteReserve || AmmoReserve > 0)
				    && !IsReloading)
				{
					if (!Networking.IsActive)
						TryStartReloadAsAuthority();
					else
						HostRequestReload();
				}
			}
		}

		if (!Input.Pressed("Attack1"))
			return;

		if (IsReloading)
			return;

		if (AmmoInMag <= 0)
			return;

		if (Time.Now < _nextFireTime)
			return;

		var pc = Components.Get<PlayerController>();
		if (pc == null)
			return;

		var cd = EffectiveFireCooldown;
		_nextFireTime = Time.Now + cd;

		var start = pc.EyePosition;
		var forward = pc.EyeAngles.Forward;

		if (!Networking.IsActive)
			PerformPrimaryFireAsAuthority(start, forward);
		else
			HostRequestPrimaryFire(start, forward);
	}

	/// <summary>Vrai si cette machine applique la logique « serveur » pour ce pawn (hôte en multi, toujours en solo).</summary>
	static bool IsAuthorityForGameplay()
	{
		if (!Networking.IsActive)
			return true;
		return Networking.IsHost;
	}

	void TickReloadAuthority()
	{
		var v = Components.Get<PlayerVitalityComponent>();
		if (v != null && v.IsDead)
		{
			if (IsReloading)
				IsReloading = false;
			return;
		}

		if (!IsReloading)
			return;

		if (Time.Now < ReloadEndTime)
			return;

		var def = WeaponDefinition.Resolve(ActiveWeaponIdent);
		CompleteReload(def);
		IsReloading = false;
		ReloadEndTime = 0f;
	}

	void CompleteReload(WeaponDefinition def)
	{
		if (def == null)
			return;

		var cap = Math.Max(0, def.MagazineSize);
		var need = cap - AmmoInMag;
		if (need <= 0)
			return;

		if (def.InfiniteReserve)
		{
			AmmoInMag = cap;
			return;
		}

		var take = Math.Min(need, AmmoReserve);
		if (take <= 0)
			return;

		AmmoInMag += take;
		AmmoReserve -= take;
	}

	void TryStartReloadAsAuthority()
	{
		if (!IsAuthorityForGameplay())
			return;

		var v = Components.Get<PlayerVitalityComponent>();
		if (v != null && v.IsDead)
			return;

		if (!CanUseWeaponThisFrame())
			return;

		var def = WeaponDefinition.Resolve(ActiveWeaponIdent);
		if (def == null)
			return;

		if (IsReloading)
			return;

		var cap = Math.Max(0, def.MagazineSize);
		if (AmmoInMag >= cap)
			return;

		if (!def.InfiniteReserve && AmmoReserve <= 0)
			return;

		var duration = Math.Max(0.05f, def.ReloadTimeSeconds);
		IsReloading = true;
		ReloadEndTime = Time.Now + duration;

		unchecked
		{
			ReloadFxSequence++;
		}

		Components.Get<PlayerCitizenWeaponVisualComponent>()
			?.OnAuthorityReloadFx(ReloadFxSequence, def);
	}

	/// <summary>Exécute tir + FX sync sur l’autorité courante (solo ou hôte après RPC).</summary>
	void PerformPrimaryFireAsAuthority(Vector3 start, Vector3 forward)
	{
		if (!IsAuthorityForGameplay())
			return;

		if (!CanUseWeaponThisFrame())
			return;

		var def = WeaponDefinition.Resolve(ActiveWeaponIdent);
		if (def == null)
			return;

		if (IsReloading)
			return;

		if (AmmoInMag <= 0)
			return;

		AmmoInMag--;

		var pcFire = Components.Get<PlayerController>();
		var eyeStart = pcFire != null ? pcFire.EyePosition : start;
		var forwardBase = pcFire != null ? pcFire.EyeAngles.Forward : forward;
		var spreadMul = ResolveSpreadMultiplier(def);
		var aimDir = WeaponBallistics.ComputeFireDirection(forwardBase, def, spreadMul);

		TryApplyPrimaryFireDamage(eyeStart, aimDir, def, out var tracerEnd, out var flightAuth);
		FireTracerStartWorld = eyeStart;
		FireTracerEndWorld = tracerEnd;
		FireTracerDirectionWorld = aimDir;
		FireTracerAuthoritativeFlightSeconds = flightAuth;

		unchecked
		{
			FireFxSequence++;
		}

		Components.Get<PlayerCitizenWeaponVisualComponent>()
			?.OnAuthorityPrimaryFireFx(FireFxSequence, def, tracerEnd, aimDir, flightAuth);

		if (AmmoInMag == 0 && def.AutoReloadWhenEmpty)
			TryStartReloadAsAuthority();
	}

	float ResolveSpreadMultiplier(WeaponDefinition def)
	{
		if (def == null || !def.AimEnabled)
			return 1f;

		var aim = Components.Get<PlayerWeaponAimComponent>();
		if (aim == null || !aim.IsAimingForDefinition(def))
			return 1f;

		return Math.Max(0f, def.AimSpreadMultiplier);
	}

	void TryApplyPrimaryFireDamage(Vector3 start, Vector3 forward, WeaponDefinition def, out Vector3 tracerEndWorld, out float authoritativeTracerFlightSeconds)
	{
		var far = start + forward * def.MaxRange;
		tracerEndWorld = far;
		authoritativeTracerFlightSeconds = 0f;

		try
		{
			if (!CombatAimTrace.TryTraceDamageableTarget(
				    Scene,
				    GameObject,
				    start,
				    forward,
				    def.MaxRange,
				    out var trace,
				    out var victimRoot,
				    out var zone))
			{
				if (trace.Hit)
					tracerEndWorld = trace.HitPosition;
				return;
			}

			tracerEndWorld = trace.HitPosition;

			var vitality = victimRoot.Components.Get<PlayerVitalityComponent>();
			if (vitality == null || vitality.IsDead)
				return;

			if (ShouldDeferHitscanDamageForTracer(def))
			{
				var delay = ComputeAuthoritativeTracerFlightSeconds(start, tracerEndWorld, def, forward);
				EnqueueDeferredHitscanHit(victimRoot, zone, delay);
				return;
			}

			vitality.ServerApplyHit(GameObject, ActiveWeaponIdent, zone);
		}
		finally
		{
			authoritativeTracerFlightSeconds = ComputeAuthoritativeTracerFlightSeconds(start, tracerEndWorld, def, forward);
		}
	}

	float ComputeAuthoritativeTracerFlightSeconds(Vector3 syncedEyeStart, Vector3 tracerEndWorld, WeaponDefinition def, Vector3 shotForwardWorld)
	{
		var vis = Components.Get<PlayerCitizenWeaponVisualComponent>();
		var pc = Components.Get<PlayerController>();
		var emission = vis?.GetTracerEmissionWorld(pc, def, syncedEyeStart, syncedEyeStart) ?? syncedEyeStart;
		if (!WeaponTracerBeam.CanSpawnTracer(emission, tracerEndWorld, def))
			return 0f;
		var dir = shotForwardWorld.Length > 0.0001f ? shotForwardWorld.Normal : (tracerEndWorld - emission).Normal;
		return WeaponTracerBeam.ComputeTracerFlightSeconds(emission, tracerEndWorld, dir, def);
	}

	static bool ShouldDeferHitscanDamageForTracer(WeaponDefinition def)
	{
		if (def == null || !def.DelayHitscanDamageUntilTracerImpact)
			return false;
		return def.TracerTrajectory is WeaponDefinition.TracerTrajectoryStyle.StrictHitscanRay
		       or WeaponDefinition.TracerTrajectoryStyle.StrictHitscanDropArc;
	}

	void EnqueueDeferredHitscanHit(GameObject victimRoot, BodyHitZone zone, float delaySeconds)
	{
		if (_pendingHitscanHits.Count >= MaxPendingHitscanHits)
			_pendingHitscanHits.RemoveAt(0);

		_pendingHitscanHits.Add(new PendingHitscanHit
		{
			ApplyAtTime = Time.Now + delaySeconds,
			VictimRoot = victimRoot,
			AttackerRoot = GameObject,
			Zone = zone,
			WeaponIdent = ActiveWeaponIdent
		});
	}

	void TickPendingHitscanHitsAuthority()
	{
		if (_pendingHitscanHits.Count == 0)
			return;

		var now = Time.Now;
		for (var i = _pendingHitscanHits.Count - 1; i >= 0; i--)
		{
			var p = _pendingHitscanHits[i];
			if (now < p.ApplyAtTime)
				continue;

			_pendingHitscanHits.RemoveAt(i);
			ApplyDeferredHitscanHit(p);
		}
	}

	void ApplyDeferredHitscanHit(PendingHitscanHit p)
	{
		if (!p.VictimRoot.IsValid() || !p.AttackerRoot.IsValid())
			return;

		var vitality = p.VictimRoot.Components.Get<PlayerVitalityComponent>();
		if (vitality == null || vitality.IsDead)
			return;

		vitality.ServerApplyHit(p.AttackerRoot, p.WeaponIdent, p.Zone);
	}

	bool IsDeadLocally()
	{
		var v = Components.Get<PlayerVitalityComponent>();
		return v != null && v.IsDead;
	}

	bool CanUseWeaponThisFrame()
	{
		if (!GameComponent.AreWeaponsAndInventoryUnlockedForCurrentSession())
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

		var pc = Components.Get<PlayerController>();
		if (pc != null)
			PerformPrimaryFireAsAuthority(pc.EyePosition, pc.EyeAngles.Forward);
		else
			PerformPrimaryFireAsAuthority(start, forward);
	}

	[Rpc.Host]
	public void HostRequestReload()
	{
		if (!Networking.IsHost)
			return;

		TryStartReloadAsAuthority();
	}

	static bool ComputeIsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => ComputeIsLocalPawn(GameObject);
}
