namespace SniperVsRunners.Features.Hud;

using System;
using Sandbox;
using Sandbox.UI;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Managers;

/// <summary>
/// HUD lobby / flux de partie : Razor minimal pour l’initialisation du panel, contenu en code.
/// </summary>
public partial class MatchStatusHud : PanelComponent
{
	MatchFlowComponent _flow;
	Label _bannerTitle;
	Label _bannerSubtitle;
	Panel _countdownWrap;
	Label _countdownDigit;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		TryBuildUi();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		TryBuildUi();

		if (_bannerTitle is null)
			return;

		_flow = MatchFlowComponent.Current;

		_bannerTitle.Text = GetBannerTitle();
		_bannerSubtitle.Text = GetBannerSubtitle();

		var inCountdown = _flow != null && _flow.SessionPhase == MatchSessionPhase.Countdown;
		_countdownWrap.Style.Display = inCountdown ? DisplayMode.Flex : DisplayMode.None;
		if (inCountdown)
			_countdownDigit.Text = _flow.CountdownWholeSeconds.ToString();
	}

	protected override int BuildHash()
	{
		var gm = GameManager.Instance;
		_flow ??= MatchFlowComponent.Current;
		var pc = _flow != null ? _flow.ConnectedPlayerCount : (gm?.PlayerCount ?? 0);

		if (_flow == null)
			return HashCode.Combine(pc, 0);

		return HashCode.Combine(
			_flow.SessionPhase,
			_flow.CountdownWholeSeconds,
			_flow.BannerTitle,
			_flow.BannerSubtitle,
			pc,
			_flow.MinPlayersToStart);
	}

	string GetBannerTitle()
	{
		if (_flow != null && !string.IsNullOrEmpty(_flow.BannerTitle))
			return _flow.BannerTitle;
		return "Lobby";
	}

	string GetBannerSubtitle()
	{
		if (_flow != null && !string.IsNullOrEmpty(_flow.BannerSubtitle))
			return _flow.BannerSubtitle;

		var gm = GameManager.Instance;
		if (gm == null)
			return "Chargement…";

		if (_flow == null)
			return "Recherche du flux de partie…";

		var count = _flow.ConnectedPlayerCount;
		if (count <= 0 && gm != null)
			count = gm.PlayerCount;

		return _flow.SessionPhase switch
		{
			MatchSessionPhase.WaitingForPlayers => $"En attente de joueurs — {count} / {_flow.MinPlayersToStart}.",
			MatchSessionPhase.Countdown => $"Départ dans {_flow.CountdownWholeSeconds} s.",
			MatchSessionPhase.LoadingArena => "Chargement de la map…",
			_ => string.Empty
		};
	}

	void TryBuildUi()
	{
		if (Panel is null || _bannerTitle is not null)
			return;

		Panel.AddClass("match-status-root");
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = 0;
		Panel.Style.Top = 0;
		Panel.Style.Right = 0;
		Panel.Style.Bottom = 0;
		Panel.Style.Width = Length.Fraction(1f);
		Panel.Style.Height = Length.Fraction(1f);
		Panel.Style.ZIndex = 10000;
		Panel.Style.FlexDirection = FlexDirection.Column;
		Panel.Style.AlignItems = Align.Center;
		Panel.Style.JustifyContent = Justify.FlexStart;
		Panel.Style.PaddingTop = Length.Pixels(40);

		_countdownWrap = Panel.AddChild<Panel>("countdown-stack");
		_countdownWrap.Style.Width = Length.Fraction(1f);
		_countdownWrap.Style.JustifyContent = Justify.Center;
		_countdownWrap.Style.AlignItems = Align.Center;
		_countdownDigit = _countdownWrap.AddChild(new Label("", "countdown-digit"));
		_countdownDigit.Style.FontSize = Length.Pixels(110);
		_countdownDigit.Style.FontWeight = 900;
		_countdownDigit.Style.FontColor = new Color(0.96f, 0.97f, 1f);
		_countdownWrap.Style.Display = DisplayMode.None;

		var card = Panel.AddChild<Panel>("banner-card");
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.MinWidth = Length.Pixels(420);
		card.Style.MaxWidth = Length.Pixels(720);
		card.Style.PaddingLeft = Length.Pixels(24);
		card.Style.PaddingRight = Length.Pixels(24);
		card.Style.PaddingTop = Length.Pixels(18);
		card.Style.PaddingBottom = Length.Pixels(18);
		card.Style.BackgroundColor = new Color(0.047f, 0.063f, 0.11f, 0.82f);

		var kicker = card.AddChild(new Label("Session", "banner-kicker"));
		kicker.Style.FontSize = Length.Pixels(11);
		kicker.Style.FontColor = new Color(1f, 1f, 1f, 0.45f);

		_bannerTitle = card.AddChild(new Label("Lobby", "banner-title"));
		_bannerTitle.Style.FontSize = Length.Pixels(22);
		_bannerTitle.Style.FontWeight = 800;
		_bannerTitle.Style.FontColor = Color.White;

		_bannerSubtitle = card.AddChild(new Label("", "banner-subtitle"));
		_bannerSubtitle.Style.FontSize = Length.Pixels(15);
		_bannerSubtitle.Style.FontColor = new Color(0.9f, 0.92f, 1f, 0.82f);
	}
}
