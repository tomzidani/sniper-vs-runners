namespace SniperVsRunners.Features.Vitality;

using System;
using Sandbox;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.Inventory;
using SniperVsRunners.Features.PlayerStats;
using SniperVsRunners.Features.Weapons;
using SniperVsRunners.Teams;

/// <summary>
/// Santé, saignement, blessures de jambe et mort — logique autoritaire sur l’hôte.
/// </summary>
public sealed class PlayerVitalityComponent : Component
{
	public const bool FriendlyFire = false;

	[Property] public float MaxHealth { get; set; } = 100f;

	/// <summary>0 = pas de risque d’exsanguination, 100 = mort par saignement.</summary>
	[Property] public float BloodTraumaMax { get; set; } = 100f;

	[Sync(SyncFlags.FromHost)]
	public float Health { get; set; } = 100f;

	[Sync(SyncFlags.FromHost)]
	public float BloodTrauma { get; set; }

	[Sync(SyncFlags.FromHost)]
	public bool IsDead { get; set; }

	[Sync(SyncFlags.FromHost)]
	public string LastDeathMessage { get; set; } = "";

	/// <summary>0 = normal, jusqu’à 3 = forte boiterie.</summary>
	[Sync(SyncFlags.FromHost)]
	public int LegInjuryTier { get; set; }

	float _bleedPerSecond;
	float _baseWalk;
	float _baseRun;
	bool _cachedMovement;
	GameObject _lastDamageAttackerRoot;

	protected override void OnAwake()
	{
		Health = MaxHealth;
	}

	protected override void OnStart()
	{
		CacheMovementBaselines();
	}

	void CacheMovementBaselines()
	{
		if (_cachedMovement)
			return;

		var pc = Components.Get<PlayerController>();
		if (pc == null)
			return;

		_baseWalk = pc.WalkSpeed;
		_baseRun = pc.RunSpeed;
		_cachedMovement = true;
	}

	protected override void OnUpdate()
	{
		if (!_cachedMovement)
			CacheMovementBaselines();

		var pc = Components.Get<PlayerController>();
		if (pc != null && _cachedMovement && !IsDead)
		{
			var legMul = GetLegSpeedMultiplier();
			pc.WalkSpeed = _baseWalk * legMul;
			pc.RunSpeed = _baseRun * legMul;
		}

		if (!Networking.IsHost)
			return;

		if (IsDead)
			return;

		if (_bleedPerSecond > 0.001f)
		{
			BloodTrauma += _bleedPerSecond * Time.Delta;
			if (BloodTrauma >= BloodTraumaMax)
				ServerDieFromBleedout();
		}

		if (Health <= 0f && !IsDead)
			ServerDieFromDamage();
	}

	/// <summary>Réinitialise l’état pour un nouveau spawn (appel hôte).</summary>
	public void ServerResetForSpawn()
	{
		if (!Networking.IsHost)
			return;

		Health = MaxHealth;
		BloodTrauma = 0f;
		IsDead = false;
		LastDeathMessage = "";
		LegInjuryTier = 0;
		_bleedPerSecond = 0f;
		_lastDamageAttackerRoot = null;

		var pc = Components.Get<PlayerController>();
		if (pc != null && _cachedMovement)
		{
			pc.WalkSpeed = _baseWalk;
			pc.RunSpeed = _baseRun;
			pc.UseInputControls = true;
			pc.UseLookControls = true;
			pc.UseCameraControls = true;
		}

		var rb = Components.Get<Rigidbody>();
		if (rb != null)
			rb.MotionEnabled = true;

		Components.Get<PlayerInventoryComponent>()?.ServerClearForSpawn();
	}

	public void ServerApplyHeal(float amount)
	{
		if (!Networking.IsHost || IsDead || amount <= 0f)
			return;

		Health = Math.Min(MaxHealth, Health + amount);
	}

	public void ServerApplyExplosiveDamage(float damage, GameObject attackerRoot)
	{
		if (!Networking.IsHost || IsDead || damage <= 0f)
			return;

		if (attackerRoot.IsValid())
			_lastDamageAttackerRoot = attackerRoot;

		Health = Math.Max(0f, Health - damage);
		if (Health <= 0f)
			ServerDieFromDamage();
	}

	/// <summary>Applique un impact validé par l’hôte (dégâts pilotés par <see cref="WeaponDefinition"/>).</summary>
	public void ServerApplyHit(
		GameObject attackerRoot,
		string weaponIdent,
		BodyHitZone zone
	)
	{
		if (!Networking.IsHost || IsDead)
			return;

		if (!attackerRoot.IsValid())
			return;

		var victimInfo = Components.Get<PlayerCombatInfoComponent>();
		var attackerInfo = attackerRoot.Components.Get<PlayerCombatInfoComponent>();
		if (victimInfo == null || attackerInfo == null)
			return;

		if (!FriendlyFire && attackerInfo.Team == victimInfo.Team)
			return;

		if (attackerInfo.Team == TeamTypes.Spectators || victimInfo.Team == TeamTypes.Spectators)
			return;

		_lastDamageAttackerRoot = attackerRoot;

		var def = WeaponDefinition.Resolve(weaponIdent);
		if (def == null)
		{
			Log.Warning($"Arme inconnue pour dégâts : '{weaponIdent}'");
			return;
		}

		var o = WeaponHitResolver.Compute(def, zone);

		if (o.InstantKill)
		{
			Health = 0f;
			ServerDieFromDamage();
			return;
		}

		Health = Math.Max(0f, Health - o.HealthDamage);

		if (o.LegInjuryAdd > 0)
			AddLegInjury(o.LegInjuryAdd);

		if (o.BleedPerSecondAdd > 0.001f)
			AddBleedContribution(o.BleedPerSecondAdd);

		if (Health <= 0f)
			ServerDieFromDamage();
	}

	void AddLegInjury(int amount)
	{
		LegInjuryTier = Math.Min(3, LegInjuryTier + amount);
	}

	void AddBleedContribution(float perSecond)
	{
		_bleedPerSecond = Math.Min(25f, _bleedPerSecond + perSecond);
	}

	float GetLegSpeedMultiplier()
	{
		return LegInjuryTier switch
		{
			0 => 1f,
			1 => 0.78f,
			2 => 0.6f,
			_ => 0.45f
		};
	}

	void ServerDieFromDamage()
	{
		if (IsDead)
			return;

		IsDead = true;
		_bleedPerSecond = 0f;
		PlayerStatsKillBridge.NotifyKillFromDamage(_lastDamageAttackerRoot, GameObject);
		_lastDamageAttackerRoot = null;
		Components.Get<PlayerInventoryComponent>()?.ServerDropAllToWorld();
		ApplyDeathPresentation("dégâts");
	}

	void ServerDieFromBleedout()
	{
		if (IsDead)
			return;

		IsDead = true;
		_bleedPerSecond = 0f;
		PlayerStatsKillBridge.NotifyKillFromDamage(_lastDamageAttackerRoot, GameObject);
		_lastDamageAttackerRoot = null;
		Components.Get<PlayerInventoryComponent>()?.ServerDropAllToWorld();
		ApplyDeathPresentation("saignement");
	}

	void ApplyDeathPresentation(string reason)
	{
		LastDeathMessage = reason switch
		{
			"saignement" => "Exsanguination",
			"dégâts" => "Blessures mortelles",
			_ => reason
		};

		Log.Info($"{GameObject.Name} est mort ({reason}).");

		var pc = Components.Get<PlayerController>();
		if (pc != null)
		{
			pc.UseInputControls = false;
			pc.UseLookControls = false;
			pc.UseCameraControls = false;
		}

		var rb = Components.Get<Rigidbody>();
		if (rb != null)
			rb.MotionEnabled = false;
	}
}
