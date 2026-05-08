namespace SniperVsRunners.Features.Hud;

using System;
using Sandbox;
using Sandbox.UI;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Teams;

/// <summary>
/// Crosshair gameplay de base : visible quand le joueur local peut utiliser une arme, masqué en ADS.
/// </summary>
public sealed class PlayerCrosshairHud : PanelComponent
{
	Panel _root;
	Panel _dot;
	Panel _up;
	Panel _down;
	Panel _left;
	Panel _right;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		if (Panel is null)
			return;

		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = Length.Percent(50);
		Panel.Style.Top = Length.Percent(50);
		Panel.Style.Width = Length.Pixels(1);
		Panel.Style.Height = Length.Pixels(1);
		Panel.Style.ZIndex = 11_600;

		_root = Panel.AddChild<Panel>("crosshair-root");
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Left = Length.Pixels(0);
		_root.Style.Top = Length.Pixels(0);

		_dot = CreateLine("crosshair-dot");
		_up = CreateLine("crosshair-up");
		_down = CreateLine("crosshair-down");
		_left = CreateLine("crosshair-left");
		_right = CreateLine("crosshair-right");
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (!CanShowCrosshair())
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		var def = weapon?.ResolveActiveDefinition();
		var aim = Components.Get<PlayerWeaponAimComponent>();
		var aimAlpha = def != null && def.AimEnabled ? (aim?.AimAlphaVisual ?? 0f) : 0f;
		aimAlpha = Math.Clamp(aimAlpha, 0f, 1f);

		var opacity = 1f - aimAlpha;
		if (opacity <= 0.01f)
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		Panel.Style.Display = DisplayMode.Flex;
		Panel.Style.Opacity = opacity;
		UpdateCrosshairGeometry(def);
		StateHasChanged();
	}

	void UpdateCrosshairGeometry(Weapons.WeaponDefinition def)
	{
		var spreadDeg = Math.Max(0f, def?.SpreadHalfAngleDegrees ?? 0f);
		var gap = Math.Clamp(8f + spreadDeg * 24f, 8f, 24f);
		var armLength = 8f;
		var thickness = 2f;

		ApplyRect(_dot, -1f, -1f, 2f, 2f);
		ApplyRect(_up, -thickness * 0.5f, -gap - armLength, thickness, armLength);
		ApplyRect(_down, -thickness * 0.5f, gap, thickness, armLength);
		ApplyRect(_left, -gap - armLength, -thickness * 0.5f, armLength, thickness);
		ApplyRect(_right, gap, -thickness * 0.5f, armLength, thickness);
	}

	static void ApplyRect(Panel p, float x, float y, float w, float h)
	{
		if (p is null)
			return;

		p.Style.Position = PositionMode.Absolute;
		p.Style.Left = Length.Pixels(x);
		p.Style.Top = Length.Pixels(y);
		p.Style.Width = Length.Pixels(w);
		p.Style.Height = Length.Pixels(h);
		p.Style.BackgroundColor = new Color(0.92f, 0.97f, 1f, 0.95f);
	}

	Panel CreateLine(string cls)
	{
		var p = _root.AddChild<Panel>(cls);
		return p;
	}

	bool CanShowCrosshair()
	{
		if (Panel is null)
			return false;

		if (!IsLocalPawn())
			return false;

		if (!GameComponent.AreWeaponsAndInventoryUnlockedForCurrentSession())
			return false;

		var v = Components.Get<PlayerVitalityComponent>();
		if (v != null && v.IsDead)
			return false;

		var info = Components.Get<PlayerCombatInfoComponent>();
		if (info == null || info.Team == TeamTypes.Spectators)
			return false;

		var weapon = Components.Get<PlayerHitscanWeaponComponent>();
		return weapon != null;
	}

	static bool ComputeIsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => ComputeIsLocalPawn(GameObject);
}
