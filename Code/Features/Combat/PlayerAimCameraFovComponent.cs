namespace SniperVsRunners.Features.Combat;

using System;
using Sandbox;
using SniperVsRunners.Features.Weapons;

/// <summary>
/// Effet caméra ADS : interpolation du FOV local selon l’avancement de la visée.
/// </summary>
public sealed class PlayerAimCameraFovComponent : Component
{
	float _defaultFov = 60f;
	bool _hasDefaultFov;
	CameraComponent _activeCamera;

	protected override void OnUpdate()
	{
		if (!IsLocalPawn())
			return;

		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		var def = weapon?.ResolveActiveDefinition();
		var aim = Components.Get<PlayerWeaponAimComponent>();
		var pc = Components.Get<PlayerController>();
		if (pc == null || pc.ThirdPerson)
		{
			TryRestoreDefaultFov();
			return;
		}

		var aimAlpha = def != null && def.AimEnabled ? (aim?.AimAlphaVisual ?? 0f) : 0f;
		aimAlpha = Math.Clamp(aimAlpha, 0f, 1f);

		var camActive = ResolveStableCamera();
		if (camActive == null || !camActive.IsValid())
			return;

		if (!_hasDefaultFov)
		{
			_defaultFov = camActive.FieldOfView;
			_hasDefaultFov = true;
		}

		var canZoom = def != null && def.AimEnabled && def.AimFovDegrees > 1f;
		var targetFov = canZoom
			? _defaultFov + (def.AimFovDegrees - _defaultFov) * aimAlpha
			: _defaultFov;
		camActive.FieldOfView = targetFov;
	}

	void TryRestoreDefaultFov()
	{
		if (!_hasDefaultFov || _activeCamera == null || !_activeCamera.IsValid())
			return;

		_activeCamera.FieldOfView = _defaultFov;
	}

	CameraComponent ResolveStableCamera()
	{
		if (_activeCamera != null && _activeCamera.IsValid())
			return _activeCamera;

		foreach (var cam in Scene.GetAllComponents<CameraComponent>())
		{
			if (cam == null || !cam.IsValid())
				continue;
			_activeCamera = cam;
			return cam;
		}

		return null;
	}

	static bool ComputeIsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => ComputeIsLocalPawn(GameObject);
}
