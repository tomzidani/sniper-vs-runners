namespace SniperVsRunners.Features.Combat;

using System;
using System.Collections.Generic;
using System.Text;
using Sandbox;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Teams;

/// <summary>
/// Aide dev : pour le pawn local, trace la visée chaque frame et dessine la hitbox touchée + le nom de zone (DrawGizmos).
/// Désactiver <see cref="ShowPreview"/> sur le prefab pour les builds joueur.
/// Les lignes <see cref="HudLine1"/> / <see cref="HudLine2"/> alimentent le HUD dev (visible dans la vue Game).
/// </summary>
public sealed class PlayerAimHitPreviewComponent : Component
{
	[Property] public bool ShowPreview { get; set; } = true;

	[Property] public bool ShowZoneLabel { get; set; } = true;

	/// <summary>Si vrai, la prévisualisation (et le HUD) ne tournent qu’en phase <see cref="MatchSessionPhase.InMatch"/>.</summary>
	[Property] public bool RestrictPreviewToInMatch { get; set; }

	[Property] public float MaxRangeFallback { get; set; } = 10_000f;

	/// <summary>Renseigné pour le panneau HUD dev quand une cible valide est sous la visée.</summary>
	public string HudLine1 { get; private set; } = "";

	/// <summary>Détail hitbox / os (vide sinon).</summary>
	public string HudLine2 { get; private set; } = "";

	bool _hasPreview;
	SceneTraceResult _trace;
	BodyHitZone _zone;

	protected override void OnUpdate()
	{
		_hasPreview = false;
		HudLine1 = "";
		HudLine2 = "";

		if (!ShowPreview || Scene == null)
			return;

		if (!IsLocalPawn())
			return;

		if (IsDeadLocally())
			return;

		if (!CanPreviewThisFrame())
			return;

		var pc = Components.Get<PlayerController>();
		if (pc == null)
			return;

		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		var maxRange = weapon != null ? weapon.MaxRange : MaxRangeFallback;

		if (!CombatAimTrace.TryTraceDamageableTarget(
			    Scene,
			    GameObject,
			    pc.EyePosition,
			    pc.EyeAngles.Forward,
			    maxRange,
			    out _trace,
			    out _,
			    out _zone))
			return;

		_hasPreview = true;
		HudLine1 = $"Visée : {ZoneLabel(_zone)}";
		HudLine2 = _trace.Hitbox != null
			? $"Hitbox : oui  ·  Os {FormatBoneChain(_trace.Hitbox)}"
			: "Hitbox : non (repli bbox) — zone approximative";
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if (!_hasPreview || !ShowPreview)
			return;

		var color = ZoneColor(_zone);
		using (Gizmo.Scope("svr_aim_hit_preview", Scene.Transform.World))
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.LineThickness = 2.5f;

			Gizmo.Draw.Color = color.WithAlpha(0.42f);
			DrawHitboxSolid(_trace);

			Gizmo.Draw.Color = color;
			DrawHitboxWire(_trace);

			if (ShowZoneLabel && _trace.Hit)
			{
				var pos = _trace.Hit ? _trace.HitPosition : _trace.EndPosition;
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.ScreenText(ZoneLabel(_zone), pos, new Vector2(0f, -48f), size: 13);
			}
		}
	}

	static void DrawHitboxWire(SceneTraceResult tr)
	{
		if (tr.Hitbox is not { Body: { } body })
		{
			if (tr.Hit)
				Gizmo.Draw.LineSphere(tr.HitPosition, 9f, rings: 10);
			return;
		}

		var drew = false;
		foreach (var shape in body.Shapes)
		{
			if (shape.IsCapsuleShape)
			{
				DrawWorldCapsuleWire(body, shape.Capsule);
				drew = true;
			}
			else if (shape.IsSphereShape)
			{
				DrawWorldSphereWire(body, shape.Sphere);
				drew = true;
			}
		}

		if (!drew && tr.Hit)
			Gizmo.Draw.LineSphere(tr.HitPosition, 9f, rings: 10);
	}

	static void DrawHitboxSolid(SceneTraceResult tr)
	{
		if (tr.Hitbox is not { Body: { } body })
			return;

		foreach (var shape in body.Shapes)
		{
			if (shape.IsCapsuleShape)
			{
				var (a, b, r) = WorldCapsule(body, shape.Capsule);
				Gizmo.Draw.SolidCapsule(a, b, r, hSegments: 10, vSegments: 6);
			}
			else if (shape.IsSphereShape)
			{
				var (c, rad) = WorldSphere(body, shape.Sphere);
				Gizmo.Draw.SolidSphere(c, rad, hSegments: 10, vSegments: 8);
			}
		}
	}

	static void DrawWorldCapsuleWire(PhysicsBody body, Capsule localCapsule)
	{
		var (a, b, r) = WorldCapsule(body, localCapsule);
		Gizmo.Draw.LineCapsule(new Capsule(a, b, r));
	}

	static void DrawWorldSphereWire(PhysicsBody body, Sphere localSphere)
	{
		var (c, rad) = WorldSphere(body, localSphere);
		Gizmo.Draw.LineSphere(c, rad, rings: 10);
	}

	static (Vector3 a, Vector3 b, float r) WorldCapsule(PhysicsBody body, Capsule localCapsule)
	{
		var t = body.Transform;
		var s = PhysicsShapeRadiusScale(body);
		var a = t.PointToWorld(localCapsule.CenterA);
		var b = t.PointToWorld(localCapsule.CenterB);
		var r = localCapsule.Radius * s;
		return (a, b, r);
	}

	static (Vector3 c, float r) WorldSphere(PhysicsBody body, Sphere localSphere)
	{
		var t = body.Transform;
		var s = PhysicsShapeRadiusScale(body);
		var c = t.PointToWorld(localSphere.Center);
		var r = localSphere.Radius * s;
		return (c, r);
	}

	/// <summary>Facteur radial pour passer du repère local du corps physique au monde (remplace l’API obsolète sur le corps).</summary>
	static float PhysicsShapeRadiusScale(PhysicsBody body)
	{
		var scale = body.GameObject.IsValid() ? body.GameObject.WorldScale : Vector3.One;
		var x = MathF.Abs(scale.x);
		var y = MathF.Abs(scale.y);
		var z = MathF.Abs(scale.z);
		var m = MathF.Max(x, MathF.Max(y, z));
		return m < 0.0001f ? 1f : m;
	}

	static Color ZoneColor(BodyHitZone z) =>
		z switch
		{
			BodyHitZone.Head => new Color(1f, 0.25f, 0.2f),
			BodyHitZone.Torso => new Color(1f, 0.55f, 0.12f),
			BodyHitZone.Arm => new Color(0.95f, 0.9f, 0.2f),
			_ => new Color(0.25f, 0.85f, 1f)
		};

	static string ZoneLabel(BodyHitZone z) =>
		z switch
		{
			BodyHitZone.Head => "Zone : tête",
			BodyHitZone.Torso => "Zone : torse",
			BodyHitZone.Arm => "Zone : bras",
			_ => "Zone : jambe"
		};

	static string FormatBoneChain(Hitbox hitbox)
	{
		if (hitbox?.Bone == null)
			return "—";

		var chain = new List<string>();
		for (var bone = hitbox.Bone; bone != null; bone = bone.Parent)
		{
			if (!string.IsNullOrEmpty(bone.Name))
				chain.Add(bone.Name);
		}

		chain.Reverse();
		if (chain.Count == 0)
			return "—";

		var sb = new StringBuilder();
		for (var i = 0; i < chain.Count; i++)
		{
			if (i > 0)
				sb.Append(" > ");
			sb.Append(chain[i]);
		}

		return sb.ToString();
	}

	bool IsDeadLocally()
	{
		var v = Components.Get<PlayerVitalityComponent>();
		return v != null && v.IsDead;
	}

	bool CanPreviewThisFrame()
	{
		if (RestrictPreviewToInMatch)
		{
			var flow = MatchFlowComponent.Current;
			if (flow != null && flow.SessionPhase != MatchSessionPhase.InMatch)
				return false;
		}

		var info = Components.Get<PlayerCombatInfoComponent>();
		if (info == null || info.Team == TeamTypes.Spectators)
			return false;

		return true;
	}

	static bool IsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => IsLocalPawn(GameObject);
}
