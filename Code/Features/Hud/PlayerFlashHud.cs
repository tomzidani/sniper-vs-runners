namespace SniperVsRunners.Features.Hud;

using System;
using Sandbox;
using Sandbox.UI;

/// <summary>
/// Flash plein écran bref (grenade flash) pour le pawn local.
/// </summary>
public partial class PlayerFlashHud : PanelComponent
{
	float _flashEndTime;
	float _flashDuration = 1f;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		if (Panel is null)
			return;

		Panel.AddClass("player-flash-hud");
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = 0;
		Panel.Style.Top = 0;
		Panel.Style.Right = 0;
		Panel.Style.Bottom = 0;
		Panel.Style.BackgroundColor = Color.White.WithAlpha(0f);
		Panel.Style.ZIndex = 60_000;
	}

	public void PlayFlash(float durationSeconds)
	{
		if (durationSeconds <= 0.05f)
			return;

		_flashDuration = Math.Max(0.05f, durationSeconds);
		_flashEndTime = Time.Now + _flashDuration;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if (Panel is null)
			return;

		if (!IsLocalPawn())
		{
			Panel.Style.BackgroundColor = Color.White.WithAlpha(0f);
			return;
		}

		if (Time.Now >= _flashEndTime)
		{
			Panel.Style.BackgroundColor = Color.White.WithAlpha(0f);
			return;
		}

		var remain = (float)(_flashEndTime - Time.Now);
		var t = 1f - remain / _flashDuration;
		var alpha = MathF.Sin(t * MathF.PI) * 0.92f;
		Panel.Style.BackgroundColor = Color.White.WithAlpha(alpha);
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
