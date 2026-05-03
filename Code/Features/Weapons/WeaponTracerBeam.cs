namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

/// <summary>
/// Tracer visuel : soit le segment hitscan sync (<see cref="WeaponDefinition.TracerTrajectoryStyle.StrictHitscanRay"/>),
/// soit l’ancienne courbe legacy (<see cref="WeaponDefinition.TracerTrajectoryStyle.LegacyBezier"/>).
/// </summary>
public static class WeaponTracerBeam
{
	const string SegmentModel = "models/dev/box.vmdl";
	const float SegmentModelDepthUnits = 50f;

	/// <summary>
	/// Durée de vol du tracer (s), identique au composant visuel — à utiliser pour retarder les dégâts hitscan si besoin.
	/// </summary>
	public static float ComputeTracerFlightSeconds(Vector3 startWorld, Vector3 endWorld, WeaponDefinition def)
	{
		if (def == null)
			return 0.01f;

		var distance = (endWorld - startWorld).Length;

		if (def.TracerTrajectory == WeaponDefinition.TracerTrajectoryStyle.LegacyBezier)
		{
			var speed = Math.Max(200f, def.TracerVisualSpeed);
			var flight = distance / speed;
			var minTime = Math.Max(0.01f, def.TracerDrawSeconds * 0.35f);
			var maxTime = Math.Max(minTime, def.TracerDrawSeconds * 1.6f);
			return Math.Clamp(flight, minTime, maxTime);
		}

		var rawFlight = distance / Math.Max(1f, def.TracerVisualSpeed);
		var tMin = Math.Max(0.0005f, def.TracerFlightTimeMinSeconds);
		var tMax = Math.Max(tMin, def.TracerFlightTimeMaxSeconds);
		return Math.Clamp(rawFlight, tMin, tMax);
	}

	/// <summary>Vrai si un tracer serait créé pour ce segment (mêmes gardes que <see cref="SpawnIfEnabled"/>).</summary>
	public static bool CanSpawnTracer(Vector3 startWorld, Vector3 endWorld, WeaponDefinition def)
	{
		if (def == null)
			return false;

		var legacy = def.TracerTrajectory == WeaponDefinition.TracerTrajectoryStyle.LegacyBezier;
		if (legacy)
		{
			if (def.TracerDrawSeconds <= 0.01f)
				return false;
		}
		else if (def.TracerVisualSpeed < 1f)
			return false;

		return (endWorld - startWorld).Length >= 2f;
	}

	public static void SpawnIfEnabled(Scene scene, Vector3 startWorld, Vector3 endWorld, Vector3 shotForwardWorld, WeaponDefinition def, float? authoritativeFlightSeconds = null)
	{
		if (scene == null || def == null)
			return;

		if (!CanSpawnTracer(startWorld, endWorld, def))
			return;

		var go = new GameObject(true);
		go.Name = "WeaponTracerProjectile";

		var c = go.Components.Create<TracerProjectileVisualComponent>();
		c.Configure(startWorld, endWorld, shotForwardWorld, def, authoritativeFlightSeconds);
	}

	sealed class TracerProjectileVisualComponent : Component
	{
		Vector3 _start;
		Vector3 _end;
		Vector3 _controlA;
		Vector3 _controlB;
		float _flightSeconds;
		float _elapsed;
		float _segmentLength;
		float _thickness;
		Color _color;
		ModelRenderer _renderer;
		bool _legacyBezier;

		public void Configure(Vector3 start, Vector3 end, Vector3 shotForwardWorld, WeaponDefinition def, float? authoritativeFlightSeconds)
		{
			_start = start;
			_end = end;
			_legacyBezier = def.TracerTrajectory == WeaponDefinition.TracerTrajectoryStyle.LegacyBezier;

			var distance = (_end - _start).Length;
			_flightSeconds = authoritativeFlightSeconds is { } authSec && authSec > 0.000001f
				? authSec
				: ComputeTracerFlightSeconds(_start, _end, def);

			if (_legacyBezier)
			{
				var forward = shotForwardWorld.Length > 0.0001f ? shotForwardWorld.Normal : (_end - _start).Normal;
				var dropT = Math.Clamp(def.BulletDropIntensity, 0f, 1f);
				var dropHeight = distance * dropT * 0.18f;
				_controlA = _start + forward * (distance * 0.42f);
				_controlB = _end + Vector3.Down * dropHeight;
			}
			else
			{
				_controlA = default;
				_controlB = default;
			}

			_segmentLength = Math.Clamp(def.TracerSegmentWorldLength, 6f, Math.Max(7f, distance * 0.6f));
			_thickness = Math.Clamp(def.TracerWorldThickness, 0.04f, 2f);
			_color = def.TracerColor.WithAlpha(0.82f);

			_renderer = GameObject.Components.Create<ModelRenderer>();
			_renderer.Model = Model.Load(SegmentModel);
			_renderer.Tint = _color;
		}

		protected override void OnUpdate()
		{
			if (_renderer == null || !_renderer.IsValid())
				return;

			_elapsed += Time.Delta;
			var tHead = Math.Clamp(_elapsed / Math.Max(0.01f, _flightSeconds), 0f, 1f);
			var distance = (_end - _start).Length;
			var tailOffsetT = _segmentLength / Math.Max(1f, distance);
			var tTail = Math.Max(0f, tHead - tailOffsetT);

			Vector3 head;
			Vector3 tail;
			if (_legacyBezier)
			{
				head = EvaluateBezier(tHead);
				tail = EvaluateBezier(tTail);
			}
			else
			{
				head = WeaponTracerTrajectory.SampleHitscanSegment(_start, _end, tHead);
				tail = WeaponTracerTrajectory.SampleHitscanSegment(_start, _end, tTail);
			}

			var seg = head - tail;
			var len = seg.Length;
			if (len > 0.0001f)
			{
				var dir = seg / len;
				GameObject.WorldPosition = (head + tail) * 0.5f;
				GameObject.WorldRotation = Rotation.LookAt(dir, Vector3.Up);
				var depthScale = len / SegmentModelDepthUnits;
				GameObject.WorldScale = new Vector3(_thickness, _thickness, Math.Max(depthScale, 0.02f));
			}

			if (tHead >= 1f)
				GameObject.Destroy();
		}

		Vector3 EvaluateBezier(float t)
		{
			var u = 1f - t;
			var uu = u * u;
			var tt = t * t;
			return _start * (uu * u)
				   + _controlA * (3f * uu * t)
				   + _controlB * (3f * u * tt)
				   + _end * (tt * t);
		}
	}
}
