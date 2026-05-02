namespace SniperVsRunners.Features.Vitality;

using System;
using Sandbox;
using SniperVsRunners.Features.Combat;
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
	}

	/// <summary>Applique un impact validé par l’hôte.</summary>
	public void ServerApplyHit(
		GameObject attackerRoot,
		WeaponProfileKind weapon,
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

		switch (weapon)
		{
			case WeaponProfileKind.SniperRifle:
				ApplySniperHit(zone);
				break;
			case WeaponProfileKind.Sidearm:
			default:
				ApplySidearmHit(zone);
				break;
		}
	}

	void ApplySniperHit(BodyHitZone zone)
	{
		if (zone is BodyHitZone.Head or BodyHitZone.Torso)
		{
			Health = 0f;
			ServerDieFromDamage();
			return;
		}

		if (zone == BodyHitZone.Leg)
		{
			Health = Math.Max(0f, Health - 70f);
			AddLegInjury(2);
			AddBleedContribution(9f);
			if (Health <= 0f)
				ServerDieFromDamage();
			return;
		}

		// Membre supérieur : blessure lourde, pas de boiterie.
		Health = Math.Max(0f, Health - 52f);
		AddBleedContribution(7f);
		if (Health <= 0f)
			ServerDieFromDamage();
	}

	void ApplySidearmHit(BodyHitZone zone)
	{
		var dmg = zone switch
		{
			BodyHitZone.Head => 55f,
			BodyHitZone.Torso => 32f,
			BodyHitZone.Arm => 26f,
			_ => 22f
		};

		Health = Math.Max(0f, Health - dmg);

		if (zone == BodyHitZone.Leg)
			AddLegInjury(1);

		var bleed = zone switch
		{
			BodyHitZone.Head => 4f,
			BodyHitZone.Torso => 2.5f,
			BodyHitZone.Arm => 2f,
			_ => 1.5f
		};
		AddBleedContribution(bleed);

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
		ApplyDeathPresentation("dégâts");
	}

	void ServerDieFromBleedout()
	{
		if (IsDead)
			return;

		IsDead = true;
		_bleedPerSecond = 0f;
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
