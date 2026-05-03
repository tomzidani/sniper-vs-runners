namespace SniperVsRunners.Features.Combat;

using System;
using Sandbox;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;
using SniperVsRunners.Teams;

/// <summary>
/// Tir hitscan : capacités issues de <see cref="WeaponDefinition"/> (ident sync côté hôte).
/// <see cref="FireFxSequence"/> incrémenté côté hôte à chaque tir valide pour synchroniser les impulsions d’anim (3P / corps).
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

	/// <summary>Remplit chargeur + réserve depuis la définition active ; annule un rechargement en cours. À appeler sur l’hôte au spawn ou après changement d’arme.</summary>
	public void HostApplyEquippedWeaponAmmo()
	{
		if (Networking.IsActive && !Networking.IsHost)
			return;

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
			TickReloadAuthority();

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
		var aimDir = WeaponBallistics.ComputeFireDirection(forwardBase, def);

		TryApplyPrimaryFireDamage(eyeStart, aimDir, def, out var tracerEnd);
		FireTracerStartWorld = eyeStart;
		FireTracerEndWorld = tracerEnd;
		FireTracerDirectionWorld = aimDir;

		unchecked
		{
			FireFxSequence++;
		}

		Components.Get<PlayerCitizenWeaponVisualComponent>()
			?.OnAuthorityPrimaryFireFx(FireFxSequence, def, tracerEnd, aimDir);

		if (AmmoInMag == 0 && def.AutoReloadWhenEmpty)
			TryStartReloadAsAuthority();
	}

	void TryApplyPrimaryFireDamage(Vector3 start, Vector3 forward, WeaponDefinition def, out Vector3 tracerEndWorld)
	{
		var far = start + forward * def.MaxRange;
		tracerEndWorld = far;

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

		vitality.ServerApplyHit(GameObject, ActiveWeaponIdent, zone);
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
