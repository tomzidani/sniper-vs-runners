namespace SniperVsRunners.Features.Hud;

using System;
using Sandbox;
using Sandbox.UI;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;

/// <summary>
/// HUD munitions style FPS (chargeur / réserve + barre de rechargement) pour le pawn local.
/// </summary>
public sealed class PlayerWeaponAmmoHud : PanelComponent
{
	Panel _frame;
	Panel _reloadTrack;
	Panel _reloadFill;
	Label _weaponName;
	Label _ammoMain;
	Label _ammoReserve;
	Label _reloadLabel;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		if (Panel is null)
			return;

		Panel.AddClass("weapon-ammo-hud-root");
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Right = Length.Pixels(28);
		Panel.Style.Bottom = Length.Pixels(108);
		Panel.Style.ZIndex = 11_500;
		Panel.Style.FlexDirection = FlexDirection.Column;
		Panel.Style.AlignItems = Align.FlexEnd;
		Panel.Style.JustifyContent = Justify.FlexEnd;

		_frame = Panel.AddChild<Panel>("ammo-frame");
		_frame.Style.FlexDirection = FlexDirection.Column;
		_frame.Style.AlignItems = Align.FlexEnd;
		_frame.Style.Padding = 14;
		_frame.Style.MinWidth = Length.Pixels(200);
		_frame.Style.BackgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.88f);
		_frame.Style.BorderWidth = 2;
		_frame.Style.BorderColor = new Color(0.2f, 0.85f, 0.95f, 0.55f);
		_frame.Style.BoxShadow.Add(new Shadow
		{
			Blur = 24f,
			Spread = 0f,
			Color = new Color(0.1f, 0.7f, 0.9f, 0.22f),
			Inset = false,
			OffsetX = 0f,
			OffsetY = 0f
		});

		var accent = _frame.AddChild<Panel>("ammo-accent-line");
		accent.Style.Width = Length.Percent(100);
		accent.Style.Height = Length.Pixels(3);
		accent.Style.MarginBottom = 10;
		accent.Style.BackgroundColor = new Color(0.15f, 0.95f, 1f, 0.85f);

		_weaponName = _frame.AddChild(new Label("—", "ammo-weapon-name"));
		_weaponName.Style.FontSize = Length.Pixels(11);
		_weaponName.Style.FontWeight = 700;
		_weaponName.Style.LetterSpacing = Length.Pixels(1);
		_weaponName.Style.FontColor = new Color(0.55f, 0.78f, 0.88f, 0.95f);
		_weaponName.Style.MarginBottom = 4;

		var row = _frame.AddChild<Panel>("ammo-count-row");
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.JustifyContent = Justify.FlexEnd;

		_ammoMain = row.AddChild(new Label("0", "ammo-mag"));
		_ammoMain.Style.FontSize = Length.Pixels(38);
		_ammoMain.Style.FontWeight = 900;
		_ammoMain.Style.FontColor = new Color(0.92f, 0.98f, 1f);
		_ammoMain.Style.TextShadow.Add(new Shadow
		{
			Blur = 12f,
			Color = new Color(0.1f, 0.75f, 0.95f, 0.45f),
			OffsetX = 0f,
			OffsetY = 0f
		});

		var sep = row.AddChild(new Label("/", "ammo-sep"));
		sep.Style.FontSize = Length.Pixels(22);
		sep.Style.FontWeight = 600;
		sep.Style.FontColor = new Color(0.35f, 0.5f, 0.58f, 0.9f);
		sep.Style.MarginLeft = 6;
		sep.Style.MarginRight = 6;
		sep.Style.MarginBottom = Length.Pixels(6);

		_ammoReserve = row.AddChild(new Label("0", "ammo-reserve"));
		_ammoReserve.Style.FontSize = Length.Pixels(22);
		_ammoReserve.Style.FontWeight = 700;
		_ammoReserve.Style.FontColor = new Color(0.65f, 0.82f, 0.9f, 0.92f);
		_ammoReserve.Style.MarginBottom = Length.Pixels(4);

		_reloadLabel = _frame.AddChild(new Label("", "ammo-reload-hint"));
		_reloadLabel.Style.FontSize = Length.Pixels(10);
		_reloadLabel.Style.FontWeight = 800;
		_reloadLabel.Style.LetterSpacing = Length.Pixels(2);
		_reloadLabel.Style.FontColor = new Color(0.35f, 0.95f, 0.75f, 0);
		_reloadLabel.Style.MarginTop = 8;
		_reloadLabel.Style.Height = Length.Pixels(0);
		_reloadLabel.Text = "RELOAD";

		_reloadTrack = _frame.AddChild<Panel>("ammo-reload-track");
		_reloadTrack.Style.Width = Length.Percent(100);
		_reloadTrack.Style.Height = Length.Pixels(5);
		_reloadTrack.Style.MarginTop = 6;
		_reloadTrack.Style.BackgroundColor = new Color(0.06f, 0.1f, 0.14f, 0.95f);
		_reloadTrack.Style.Overflow = OverflowMode.Hidden;
		_reloadTrack.Style.Display = DisplayMode.None;

		_reloadFill = _reloadTrack.AddChild<Panel>("ammo-reload-fill");
		_reloadFill.Style.Position = PositionMode.Absolute;
		_reloadFill.Style.Left = 0;
		_reloadFill.Style.Top = 0;
		_reloadFill.Style.Bottom = 0;
		_reloadFill.Style.Width = Length.Percent(0);
		_reloadFill.Style.BackgroundColor = new Color(0.2f, 0.92f, 0.72f, 0.92f);
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (Panel is null || _frame is null || _ammoMain is null || _ammoReserve is null
		    || _weaponName is null || _reloadTrack is null || _reloadFill is null || _reloadLabel is null)
			return;

		if (!IsLocalPawn())
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		var v = GameObject.Components.Get<PlayerVitalityComponent>();
		if (v is null || v.IsDead)
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		if (!GameComponent.AreWeaponsAndInventoryUnlockedForCurrentSession())
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		var weapon = GameObject.Components.Get<PlayerHitscanWeaponComponent>();
		if (weapon is null)
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		Panel.Style.Display = DisplayMode.Flex;

		var def = weapon.ResolveActiveDefinition();
		var ident = string.IsNullOrWhiteSpace(weapon.ActiveWeaponIdent) ? "usp" : weapon.ActiveWeaponIdent.Trim();
		var displayName = def != null && !string.IsNullOrWhiteSpace(def.DisplayName)
			? def.DisplayName.ToUpperInvariant()
			: ident.ToUpperInvariant();

		_weaponName.Text = displayName;

		var mag = Math.Max(0, weapon.AmmoInMag);
		var reserve = Math.Max(0, weapon.AmmoReserve);
		var infinite = def != null && def.InfiniteReserve;

		_ammoMain.Text = $"{mag}";
		_ammoReserve.Text = infinite ? "∞" : $"{reserve}";

		var lowMag = def != null && def.MagazineSize > 0 && mag <= Math.Max(1, def.MagazineSize / 4);
		_ammoMain.Style.FontColor = lowMag
			? new Color(1f, 0.55f, 0.35f, 1f)
			: new Color(0.92f, 0.98f, 1f);

		if (weapon.IsReloading && def != null)
		{
			var dur = Math.Max(0.05f, def.ReloadTimeSeconds);
			var progress = 1f - (float)(weapon.ReloadEndTime - Time.Now) / dur;
			progress = Math.Clamp(progress, 0f, 1f);

			_reloadTrack.Style.Display = DisplayMode.Flex;
			_reloadFill.Style.Width = Length.Fraction(progress);
			_reloadLabel.Style.Display = DisplayMode.Flex;
			_reloadLabel.Style.Height = Length.Auto;
			_reloadLabel.Style.FontColor = new Color(0.35f, 0.95f, 0.75f, 0.95f);
		}
		else
		{
			_reloadTrack.Style.Display = DisplayMode.None;
			_reloadFill.Style.Width = Length.Fraction(0);
			_reloadLabel.Style.Display = DisplayMode.None;
			_reloadLabel.Style.Height = Length.Pixels(0);
			_reloadLabel.Style.FontColor = new Color(0.35f, 0.95f, 0.75f, 0);
		}

		StateHasChanged();
	}

	static bool ComputeIsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => ComputeIsLocalPawn(GameObject);
}
