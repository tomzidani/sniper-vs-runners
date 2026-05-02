namespace SniperVsRunners.Features.Hud;

using System;
using Sandbox;
using Sandbox.UI;
using SniperVsRunners.Features.Vitality;

/// <summary>
/// Barre d’état locale (propriétaire du pawn) : PV, saignement, jambe.
/// </summary>
public partial class PlayerVitalityHud : PanelComponent
{
	Label _status;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		if (Panel is null)
			return;

		Panel.AddClass("player-vitality-hud");
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = Length.Pixels(24);
		Panel.Style.Bottom = Length.Pixels(28);
		Panel.Style.Width = Length.Pixels(420);
		Panel.Style.Height = Length.Pixels(72);
		Panel.Style.JustifyContent = Justify.Center;
		Panel.Style.AlignItems = Align.FlexStart;
		Panel.Style.FlexDirection = FlexDirection.Column;
		Panel.Style.BackgroundColor = new Color(0.04f, 0.06f, 0.1f, 0.72f);
		Panel.Style.PaddingLeft = Length.Pixels(14);
		Panel.Style.PaddingRight = Length.Pixels(14);
		Panel.Style.PaddingTop = Length.Pixels(10);
		Panel.Style.PaddingBottom = Length.Pixels(10);
		Panel.Style.BorderWidth = 1;
		Panel.Style.BorderColor = new Color(1f, 1f, 1f, 0.12f);

		_status = Panel.AddChild(new Label("", "vitality-status"));
		_status.Style.FontSize = Length.Pixels(15);
		_status.Style.FontWeight = 600;
		_status.Style.FontColor = new Color(0.94f, 0.96f, 1f);
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (Panel is null || _status is null)
			return;

		if (!IsLocalPawn())
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		Panel.Style.Display = DisplayMode.Flex;

		var v = GameObject.Components.Get<PlayerVitalityComponent>();
		if (v is null)
		{
			_status.Text = "—";
			return;
		}

		if (v.IsDead)
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		Panel.Style.Display = DisplayMode.Flex;

		var bleedPct = v.BloodTrauma <= 0f ? 0f : 100f * (v.BloodTrauma / Math.Max(1f, v.BloodTraumaMax));
		_status.Text = $"PV {v.Health:0} / {v.MaxHealth:0}   ·   Hémorragie {bleedPct:0}%   ·   Jambe T{v.LegInjuryTier}";
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
