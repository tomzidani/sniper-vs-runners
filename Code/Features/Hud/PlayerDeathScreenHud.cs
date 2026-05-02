namespace SniperVsRunners.Features.Hud;

using Sandbox;
using Sandbox.UI;
using SniperVsRunners.Features.Vitality;

/// <summary>
/// Plein écran pour le propriétaire du pawn lorsque <see cref="PlayerVitalityComponent.IsDead"/> est vrai.
/// </summary>
public partial class PlayerDeathScreenHud : PanelComponent
{
	Label _title;
	Label _subtitle;
	Label _footer;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		if (Panel is null)
			return;

		Panel.AddClass("player-death-screen");
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = 0;
		Panel.Style.Top = 0;
		Panel.Style.Right = 0;
		Panel.Style.Bottom = 0;
		Panel.Style.Width = Length.Fraction(1f);
		Panel.Style.Height = Length.Fraction(1f);
		Panel.Style.ZIndex = 50_000;
		Panel.Style.JustifyContent = Justify.Center;
		Panel.Style.AlignItems = Align.Center;
		Panel.Style.FlexDirection = FlexDirection.Column;
		Panel.Style.BackgroundColor = new Color(0.02f, 0.02f, 0.04f, 0.88f);
		Panel.Style.Display = DisplayMode.None;

		_title = Panel.AddChild(new Label("Vous êtes mort", "death-title"));
		_title.Style.FontSize = Length.Pixels(42);
		_title.Style.FontWeight = 900;
		_title.Style.FontColor = new Color(0.98f, 0.35f, 0.32f);
		_title.Style.MarginBottom = Length.Pixels(12);

		_subtitle = Panel.AddChild(new Label("", "death-subtitle"));
		_subtitle.Style.FontSize = Length.Pixels(18);
		_subtitle.Style.FontColor = new Color(0.92f, 0.93f, 0.96f, 0.9f);
		_subtitle.Style.MarginBottom = Length.Pixels(28);

		_footer = Panel.AddChild(new Label("Vous reviendrez au lobby à la fin de la partie.", "death-footer"));
		_footer.Style.FontSize = Length.Pixels(14);
		_footer.Style.FontColor = new Color(1f, 1f, 1f, 0.45f);
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (Panel is null || _title is null || _subtitle is null || _footer is null)
			return;

		if (!IsLocalPawn())
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		var v = GameObject.Components.Get<PlayerVitalityComponent>();
		if (v is null || !v.IsDead)
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		Panel.Style.Display = DisplayMode.Flex;

		var detail = string.IsNullOrWhiteSpace(v.LastDeathMessage)
			? "Vous ne pouvez plus agir."
			: v.LastDeathMessage;
		_subtitle.Text = detail;
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
