namespace SniperVsRunners.Features.Hud;

using Sandbox;
using Sandbox.UI;
using SniperVsRunners.Features.Combat;

/// <summary>
/// Affiche en jeu (vue Game) la zone et la chaîne d’os du hitbox visé — complète <see cref="PlayerAimHitPreviewComponent"/> (gizmos surtout vus dans la vue scène).
/// </summary>
public partial class PlayerAimHitDebugHud : PanelComponent
{
	[Property] public bool ShowHud { get; set; } = true;

	Label _line1;
	Label _line2;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		if (Panel is null)
			return;

		Panel.AddClass("player-aim-hit-debug-hud");
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = Length.Pixels(16);
		Panel.Style.Top = Length.Pixels(120);
		Panel.Style.Width = Length.Pixels(520);
		Panel.Style.FlexDirection = FlexDirection.Column;
		Panel.Style.BackgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.82f);
		Panel.Style.PaddingLeft = Length.Pixels(12);
		Panel.Style.PaddingRight = Length.Pixels(12);
		Panel.Style.PaddingTop = Length.Pixels(8);
		Panel.Style.PaddingBottom = Length.Pixels(8);
		Panel.Style.BorderWidth = 1;
		Panel.Style.BorderColor = new Color(0.4f, 0.75f, 1f, 0.35f);

		_line1 = Panel.AddChild(new Label("", "aim-debug-l1"));
		_line1.Style.FontSize = Length.Pixels(14);
		_line1.Style.FontWeight = 700;
		_line1.Style.FontColor = new Color(0.55f, 0.95f, 1f);

		_line2 = Panel.AddChild(new Label("", "aim-debug-l2"));
		_line2.Style.FontSize = Length.Pixels(12);
		_line2.Style.FontWeight = 500;
		_line2.Style.FontColor = new Color(0.88f, 0.92f, 1f, 0.92f);
		_line2.Style.MarginTop = Length.Pixels(4);
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (Panel is null || _line1 is null || _line2 is null)
			return;

		if (!ShowHud || !IsLocalPawn())
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		var preview = GameObject.Components.Get<PlayerAimHitPreviewComponent>();
		if (preview is not { ShowPreview: true } || string.IsNullOrEmpty(preview.HudLine1))
		{
			Panel.Style.Display = DisplayMode.None;
			return;
		}

		Panel.Style.Display = DisplayMode.Flex;
		_line1.Text = preview.HudLine1;
		_line2.Text = preview.HudLine2 ?? "";
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
