namespace SniperVsRunners.Features.Hud;

using System;
using Sandbox;
using Sandbox.UI;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.Inventory;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;

/// <summary>
/// Roue / barre d’inventaire (9 slots) pour le pawn local.
/// </summary>
public partial class ItemWheelHud : PanelComponent
{
	readonly ItemKind[] _decodeKinds = new ItemKind[PlayerInventoryComponent.SlotCount];
	readonly int[] _decodeCounts = new int[PlayerInventoryComponent.SlotCount];

	Panel _backdrop;
	Panel _card;
	Label _weaponLine;
	Label _footer;
	Label[] _slotLabels = Array.Empty<Label>();

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		if (Panel is null)
			return;

		Panel.AddClass("item-wheel-root");
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = 0;
		Panel.Style.Top = 0;
		Panel.Style.Right = 0;
		Panel.Style.Bottom = 0;
		Panel.Style.ZIndex = 12_000;

		_backdrop = Panel.AddChild<Panel>("wheel-backdrop");
		_backdrop.Style.Position = PositionMode.Absolute;
		_backdrop.Style.Left = 0;
		_backdrop.Style.Top = 0;
		_backdrop.Style.Right = 0;
		_backdrop.Style.Bottom = 0;
		_backdrop.Style.BackgroundColor = new Color(0f, 0f, 0f, 0.55f);
		_backdrop.Style.Display = DisplayMode.None;
		_backdrop.Style.JustifyContent = Justify.Center;
		_backdrop.Style.AlignItems = Align.Center;
		_backdrop.Style.FlexDirection = FlexDirection.Column;

		_card = _backdrop.AddChild<Panel>("wheel-card");
		_card.Style.MinWidth = Length.Pixels(440);
		_card.Style.Padding = 16;
		_card.Style.BackgroundColor = new Color(0.05f, 0.07f, 0.12f, 0.92f);
		_card.Style.FlexDirection = FlexDirection.Column;
		_card.Style.Display = DisplayMode.None;

		var title = _card.AddChild(new Label("Inventaire — objets utilisables (9 slots)", "wheel-title"));
		title.Style.FontSize = Length.Pixels(18);
		title.Style.FontWeight = 800;
		title.Style.FontColor = Color.White;
		title.Style.MarginBottom = Length.Pixels(8);

		_weaponLine = _card.AddChild(new Label("", "wheel-weapon-line"));
		_weaponLine.Style.FontSize = Length.Pixels(13);
		_weaponLine.Style.FontColor = new Color(0.75f, 0.9f, 1f, 0.95f);
		_weaponLine.Style.MarginBottom = Length.Pixels(10);

		var grid = _card.AddChild<Panel>("wheel-grid");
		grid.Style.FlexDirection = FlexDirection.Column;
		grid.Style.Width = Length.Pixels(408);
		grid.Style.JustifyContent = Justify.Center;

		_slotLabels = new Label[PlayerInventoryComponent.SlotCount];
		for (var row = 0; row < 3; row++)
		{
			var rowPanel = grid.AddChild<Panel>($"wheel-row-{row}");
			rowPanel.Style.FlexDirection = FlexDirection.Row;
			rowPanel.Style.JustifyContent = Justify.Center;

			for (var col = 0; col < 3; col++)
			{
				var i = row * 3 + col;
				var cell = rowPanel.AddChild<Panel>($"slot-{i}");
				cell.Style.Width = Length.Pixels(128);
				cell.Style.Height = Length.Pixels(72);
				cell.Style.Margin = 4;
				cell.Style.BackgroundColor = new Color(1f, 1f, 1f, 0.06f);
				cell.Style.JustifyContent = Justify.Center;
				cell.Style.AlignItems = Align.Center;
				cell.Style.FlexDirection = FlexDirection.Column;

				var lab = cell.AddChild(new Label("", "wheel-slot"));
				lab.Style.FontSize = Length.Pixels(13);
				lab.Style.FontColor = new Color(0.92f, 0.94f, 1f, 0.95f);
				lab.Style.TextAlign = TextAlign.Center;
				_slotLabels[i] = lab;
			}
		}

		_footer = _card.AddChild(new Label("", "wheel-footer"));
		_footer.Style.MarginTop = Length.Pixels(12);
		_footer.Style.FontSize = Length.Pixels(12);
		_footer.Style.FontColor = new Color(1f, 1f, 1f, 0.55f);
		_footer.Text =
			"Slots 1–2 : équiper USP / M700 (test, ne consomme pas). · Tir : clic gauche · Tab : roue · 1–9 : utiliser · G : slot rapide (molette) · E : ramasser";
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (Panel is null || _backdrop is null || _card is null || _weaponLine is null || _slotLabels.Length == 0)
			return;

		if (!IsLocalPawn())
		{
			_backdrop.Style.Display = DisplayMode.None;
			_card.Style.Display = DisplayMode.None;
			return;
		}

		var v = GameObject.Components.Get<PlayerVitalityComponent>();
		if (v != null && v.IsDead)
		{
			_backdrop.Style.Display = DisplayMode.None;
			_card.Style.Display = DisplayMode.None;
			return;
		}

		if (!GameComponent.AreWeaponsAndInventoryUnlockedForCurrentSession())
		{
			_backdrop.Style.Display = DisplayMode.None;
			_card.Style.Display = DisplayMode.None;
			return;
		}

		var hitscan = GameObject.Components.Get<PlayerHitscanWeaponComponent>();
		if (hitscan != null)
		{
			var wid = string.IsNullOrWhiteSpace(hitscan.ActiveWeaponIdent) ? "usp" : hitscan.ActiveWeaponIdent.Trim();
			var wdef = WeaponDefinition.Resolve(wid);
			var wname = wdef != null && !string.IsNullOrWhiteSpace(wdef.DisplayName) ? wdef.DisplayName : wid;
			_weaponLine.Text = $"Arme principale : {wname}  ·  ident « {wid} »  ·  touches 1 / 2 pour USP / M700 si en inventaire";
		}
		else
			_weaponLine.Text = "Arme équipée : —";

		var inv = GameObject.Components.Get<PlayerInventoryComponent>();
		if (inv == null)
			return;

		PlayerInventoryComponent.DecodeSlots(inv.InvPayload, _decodeKinds, _decodeCounts);

		var open = inv.IsWheelOpen;
		_backdrop.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;
		_card.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;

		for (var i = 0; i < PlayerInventoryComponent.SlotCount; i++)
		{
			var k = _decodeKinds[i];
			var c = _decodeCounts[i];
			var def = ItemDefinition.Get(k);
			var name = k == ItemKind.None ? $"[{i + 1}] —" : $"[{i + 1}] {def.DisplayName}";
			var qty = k == ItemKind.None ? "" : $"  x{c}";
			_slotLabels[i].Text = name + qty;

			var highlight = inv.LocalQuickSlotIndex == i;
			_slotLabels[i].Parent.Style.BackgroundColor = highlight
				? new Color(0.25f, 0.45f, 0.95f, 0.35f)
				: new Color(1f, 1f, 1f, 0.06f);
		}

		StateHasChanged();
	}

	static bool IsLocalPawn(GameObject go)
	{
		if (!Networking.IsActive)
			return true;
		return go.Network.IsOwner;
	}

	bool IsLocalPawn() => IsLocalPawn(GameObject);
}
