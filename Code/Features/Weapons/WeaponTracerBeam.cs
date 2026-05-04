namespace SniperVsRunners.Features.Weapons;

using System;
using Sandbox;

/// <summary>
/// Tracer visuel : segment hitscan sync, arc hitscan (chute visuelle, mêmes extrémités), ou courbe legacy.
/// </summary>
public static class WeaponTracerBeam
{
	const string SegmentModel = "models/dev/box.vmdl";

	/// <summary>
	/// Référence d’échelle du modèle <c>dev/box</c> sur l’axe <strong>étiré pour la longueur du trait</strong>.
	/// Après <c>Rotation.LookAt(direction, up)</c>, le forward du transform suit l’axe <strong>local X</strong> (s&box) : la longueur du tir est <c>WorldScale.x</c>, la section sur <c>y</c> et <c>z</c>.
	/// </summary>
	const float SegmentModelDepthUnits = 50f;

	/// <summary>
	/// Durée de vol du tracer (s), identique au composant visuel — à utiliser pour retarder les dégâts hitscan si besoin.
	/// Pour <see cref="WeaponDefinition.TracerTrajectoryStyle.StrictHitscanDropArc"/>, préférer la surcharge avec direction de tir.
	/// </summary>
	public static float ComputeTracerFlightSeconds(Vector3 startWorld, Vector3 endWorld, WeaponDefinition def)
	{
		var chord = endWorld - startWorld;
		var fwd = chord.Length > 0.0001f ? chord.Normal : Vector3.Forward;
		return ComputeTracerFlightSeconds(startWorld, endWorld, fwd, def);
	}

	/// <summary>
	/// Même logique que l’autre surcharge ; <paramref name="shotForwardWorld"/> est requis pour l’arc strict (tangente au départ = rayon hitscan).
	/// </summary>
	public static float ComputeTracerFlightSeconds(Vector3 startWorld, Vector3 endWorld, Vector3 shotForwardWorld, WeaponDefinition def)
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

		if (def.TracerTrajectory == WeaponDefinition.TracerTrajectoryStyle.StrictHitscanDropArc
		    && WeaponTracerTrajectory.TryBuildStrictDropCubic(startWorld, endWorld, shotForwardWorld, def, out var p0, out var p1, out var p2, out var p3))
		{
			var arcLen = WeaponTracerTrajectory.ApproximateCubicBezierArcLength(p0, p1, p2, p3);
			var rawArc = arcLen / Math.Max(1f, def.TracerVisualSpeed);
			var tMinArc = Math.Max(0.0005f, def.TracerFlightTimeMinSeconds);
			var tMaxArc = Math.Max(tMinArc, def.TracerFlightTimeMaxSeconds);
			return Math.Clamp(rawArc, tMinArc, tMaxArc);
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

		var startFx = startWorld;
		NudgeTracerStartAwayFromCamera(ref startFx, endWorld, scene);

		var go = new GameObject(true);
		go.Name = "WeaponTracerProjectile";

		var c = go.Components.Create<TracerProjectileVisualComponent>();
		c.Configure(startFx, endWorld, shotForwardWorld, def, authoritativeFlightSeconds);
	}

	/// <summary>
	/// Si le départ est trop près de la caméra active, le pousse le long du rayon vers l’impact (évite clipping / trait collé à l’œil).
	/// </summary>
	static void NudgeTracerStartAwayFromCamera(ref Vector3 start, Vector3 end, Scene scene)
	{
		if (!TryGetPrimaryCameraWorldPosition(scene, out var camPos))
			return;

		const float minCamDist = 16f;
		var ray = end - start;
		var rayLen = ray.Length;
		if (rayLen < 1f)
			return;

		var dir = ray / rayLen;
		var dist = (start - camPos).Length;
		if (dist >= minCamDist)
			return;

		var push = minCamDist - dist + 3f;
		push = Math.Min(push, rayLen - 0.35f);
		if (push > 0.08f)
			start += dir * push;
	}

	static bool TryGetPrimaryCameraWorldPosition(Scene scene, out Vector3 worldPos)
	{
		worldPos = default;
		if (scene == null || !scene.IsValid())
			return false;

		foreach (var cam in scene.GetAllComponents<CameraComponent>())
		{
			if (cam == null || !cam.IsValid())
				continue;
			worldPos = cam.WorldPosition;
			return true;
		}

		return false;
	}

	static Color TintWithBrightness(Color baseColor, float rgbMul, float alpha)
	{
		var a = Math.Clamp(alpha, 0f, 1f);
		var m = Math.Max(0f, rgbMul);
		return new Color(baseColor.r * m, baseColor.g * m, baseColor.b * m, a);
	}

	const string TracerUnlitEmissiveMaterial = "materials/dev/primary_white_emissive_trans.vmat";
	const string TracerUnlitMaterialFallback = "materials/dev/primary_white.vmat";

	/// <summary>
	/// Pas d’ombres (cast/receive via pipeline standard) + matériau dev émissif pour couleur stable (hors lumières de scène).
	/// </summary>
	static void ConfigureTracerModelRenderer(ModelRenderer r)
	{
		if (r == null || !r.IsValid())
			return;

		r.RenderType = ModelRenderer.ShadowRenderType.Off;

		var mat = Material.Load(TracerUnlitEmissiveMaterial);
		var usedEmissiveTrans = mat.IsValid;
		if (!usedEmissiveTrans)
			mat = Material.Load(TracerUnlitMaterialFallback);
		if (mat.IsValid)
			r.MaterialOverride = mat;

		var so = r.SceneObject;
		if (so == null || !so.IsValid())
			return;

		so.Flags.CastShadows = false;
		so.Flags.NeedsLightProbe = false;
		so.Flags.NeedsEnvironmentMap = false;

		if (usedEmissiveTrans)
			so.Flags.IsTranslucent = true;
	}

	sealed class TracerProjectileVisualComponent : Component
	{
		const int StrictDropArcSamples = 96;

		Vector3 _start;
		Vector3 _end;
		Vector3 _controlA;
		Vector3 _controlB;
		float _flightSeconds;
		float _elapsed;
		float _segmentLength;
		float _thicknessX;
		float _thicknessY;
		ModelRenderer _coreRenderer;
		ModelRenderer _glowRenderer;
		bool _legacyBezier;
		bool _strictDropArc;
		float[] _strictDropCumArc;
		float _strictDropTotalArc;
		bool _renderersVisible;

		public void Configure(Vector3 start, Vector3 end, Vector3 shotForwardWorld, WeaponDefinition def, float? authoritativeFlightSeconds)
		{
			_start = start;
			_end = end;
			_legacyBezier = def.TracerTrajectory == WeaponDefinition.TracerTrajectoryStyle.LegacyBezier;

			var distance = (_end - _start).Length;
			var shotDir = shotForwardWorld.Length > 0.0001f
				? shotForwardWorld.Normal
				: (distance > 0.0001f ? (_end - _start).Normal : Vector3.Forward);

			_flightSeconds = authoritativeFlightSeconds is { } authSec && authSec > 0.000001f
				? authSec
				: ComputeTracerFlightSeconds(_start, _end, shotDir, def);

			_strictDropArc = false;
			_strictDropCumArc = null;
			_strictDropTotalArc = 0f;
			_controlA = default;
			_controlB = default;

			if (_legacyBezier)
			{
				var forward = shotDir;
				var dropT = Math.Clamp(def.BulletDropIntensity, 0f, 1f);
				var dropHeight = distance * dropT * 0.18f;
				_controlA = _start + forward * (distance * 0.42f);
				_controlB = _end + Vector3.Down * dropHeight;
			}
			else if (def.TracerTrajectory == WeaponDefinition.TracerTrajectoryStyle.StrictHitscanDropArc
			         && WeaponTracerTrajectory.TryBuildStrictDropCubic(_start, _end, shotForwardWorld, def, out _, out var p1, out var p2, out _))
			{
				_strictDropArc = true;
				_strictDropCumArc = new float[StrictDropArcSamples + 1];
				WeaponTracerTrajectory.BuildCumulativeArcLengthTable(_start, p1, p2, _end, StrictDropArcSamples, _strictDropCumArc);
				_strictDropTotalArc = _strictDropCumArc[StrictDropArcSamples];
				_controlA = p1;
				_controlB = p2;
			}

			_segmentLength = Math.Clamp(def.TracerSegmentWorldLength, 6f, Math.Max(7f, distance * 0.6f));
			_thicknessX = Math.Clamp(def.TracerWorldThickness, 0.008f, 2f);
			var ty = def.TracerWorldThicknessY;
			_thicknessY = ty > 0.0008f ? Math.Clamp(ty, 0.008f, 2f) : _thicknessX;

			var bright = Math.Clamp(def.TracerBrightness, 0.25f, 48f);
			var coreTint = TintWithBrightness(def.TracerColor, bright, def.TracerColor.a);

			var spread = def.TracerGlowSpread;
			var glowAlpha = Math.Clamp(def.TracerGlowAlpha, 0f, 1f);
			var hasGlow = spread > 1.01f && glowAlpha > 0.001f;
			var glowRgb = bright * Math.Max(0f, def.TracerGlowBrightnessMul);
			var glowTint = TintWithBrightness(def.TracerColor, glowRgb, glowAlpha);

			// Halo d’abord (derrière), puis cœur au-dessus pour un voile lumineux.
			if (hasGlow)
			{
				var glowGo = new GameObject(true);
				glowGo.Name = "TracerGlow";
				glowGo.SetParent(GameObject);
				glowGo.LocalPosition = Vector3.Zero;
				glowGo.LocalRotation = Rotation.Identity;
				var g = Math.Clamp(spread, 1.02f, 4f);
				// Parent : (longueur X, épaisseur Y, épaisseur Z) — halo un peu plus large en section (Y,Z), léger puff sur X.
				glowGo.LocalScale = new Vector3(1.02f, g, g);
				_glowRenderer = glowGo.Components.Create<ModelRenderer>();
				_glowRenderer.Model = Model.Load(SegmentModel);
				_glowRenderer.Tint = glowTint;
				ConfigureTracerModelRenderer(_glowRenderer);
			}

			var coreGo = new GameObject(true);
			coreGo.Name = "TracerCore";
			coreGo.SetParent(GameObject);
			coreGo.LocalPosition = Vector3.Zero;
			coreGo.LocalRotation = Rotation.Identity;
			coreGo.LocalScale = Vector3.One;
			_coreRenderer = coreGo.Components.Create<ModelRenderer>();
			_coreRenderer.Model = Model.Load(SegmentModel);
			_coreRenderer.Tint = coreTint;
			ConfigureTracerModelRenderer(_coreRenderer);
			_coreRenderer.Enabled = false;
			if (_glowRenderer != null && _glowRenderer.IsValid())
				_glowRenderer.Enabled = false;

			_renderersVisible = false;
			GameObject.WorldScale = new Vector3(1e-5f, 1e-5f, 1e-5f);
		}

		protected override void OnUpdate()
		{
			if (_coreRenderer == null || !_coreRenderer.IsValid())
				return;

			_elapsed += Time.Delta;
			var tHeadRaw = Math.Clamp(_elapsed / Math.Max(0.01f, _flightSeconds), 0f, 1f);
			var distance = (_end - _start).Length;
			var pathRefLen = _strictDropArc && _strictDropTotalArc > 0.001f ? _strictDropTotalArc : distance;
			var tailOffsetT = _segmentLength / Math.Max(1f, pathRefLen);
			var tTailRaw = Math.Max(0f, tHeadRaw - tailOffsetT);

			// À t≈0, tête et queue coïncident → longueur nulle : une frame au scale par défaut = « gros bloc ».
			const float minHeadT = 0.0022f;
			var tHead = Math.Max(tHeadRaw, minHeadT);
			var tTail = tTailRaw;
			if (tTail >= tHead - 0.00005f)
				tTail = Math.Max(0f, tHead - minHeadT);

			Vector3 head;
			Vector3 tail;
			if (_legacyBezier)
			{
				head = EvaluateBezier(tHead);
				tail = EvaluateBezier(tTail);
			}
			else if (_strictDropArc && _strictDropCumArc != null)
			{
				var tauH = WeaponTracerTrajectory.ArcLengthFractionToCurveParameter(_strictDropCumArc, StrictDropArcSamples, tHead);
				var tauT = WeaponTracerTrajectory.ArcLengthFractionToCurveParameter(_strictDropCumArc, StrictDropArcSamples, tTail);
				head = WeaponTracerTrajectory.EvaluateCubic(_start, _controlA, _controlB, _end, tauH);
				tail = WeaponTracerTrajectory.EvaluateCubic(_start, _controlA, _controlB, _end, tauT);
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
				var up = Vector3.Up;
				if (MathF.Abs(Vector3.Dot(dir, up)) > 0.98f)
					up = Vector3.Right;
				GameObject.WorldRotation = Rotation.LookAt(dir, up);
				var depthScale = len / SegmentModelDepthUnits;
				// Longueur sur local X (forward après LookAt) ; épaisseurs sur Y et Z.
				GameObject.WorldScale = new Vector3(Math.Max(depthScale, 0.02f), _thicknessX, _thicknessY);

				if (!_renderersVisible)
				{
					_renderersVisible = true;
					_coreRenderer.Enabled = true;
					if (_glowRenderer != null && _glowRenderer.IsValid())
						_glowRenderer.Enabled = true;
				}
			}
			else if (!_renderersVisible)
				GameObject.WorldScale = new Vector3(1e-5f, 1e-5f, 1e-5f);

			if (tHeadRaw >= 1f)
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
