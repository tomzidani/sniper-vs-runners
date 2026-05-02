namespace SniperVsRunners.Features.GameFlow;

using System;
using Sandbox;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Managers;

public sealed class MatchFlowComponent : Component
{
    /// <summary>
    /// Flux de match sur l’objet session (DontDestroyOnLoad + <see cref="GameComponent"/>).
    /// Ne pas utiliser un dernier-<c>OnAwake</c> gagnant : sur le client, un autre réveil peut pointer vers un composant non synchronisé (HUD bloqué 0/2, phase Lobby).
    /// </summary>
    public static MatchFlowComponent Current
    {
        get
        {
            var session = GameComponent.Session;
            if (session == null || !session.GameObject.IsValid())
                return null;
            return session.GameObject.Components.Get<MatchFlowComponent>();
        }
    }

    [Property]
    public int MinPlayersToStart { get; set; } = 2;

    [Property]
    public float CountdownDurationSeconds { get; set; } = 5f;

    [Property]
    public string GameplayScenePath { get; set; } = "scenes/play.scene";

    [Sync(SyncFlags.FromHost)]
    public MatchSessionPhase SessionPhase { get; set; } = MatchSessionPhase.WaitingForPlayers;

    [Sync(SyncFlags.FromHost)]
    public int CountdownWholeSeconds { get; set; }

    [Sync(SyncFlags.FromHost)]
    public string BannerTitle { get; set; } = "Lobby";

    [Sync(SyncFlags.FromHost)]
    public string BannerSubtitle { get; set; } = "";

    /// <summary>
    /// Nombre de joueurs côté hôte (liste <see cref="GameManager"/>). À utiliser pour l’UI sur les clients :
    /// <see cref="GameManager.PlayerCount"/> n’est rempli que sur l’hôte (callbacks réseau host-only).
    /// </summary>
    [Sync(SyncFlags.FromHost)]
    public int ConnectedPlayerCount { get; set; }

    float _countdownElapsed;
    bool _pendingBeginMatchAfterScene;

    protected override void OnUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        if (!Networking.IsActive)
        {
            BannerTitle = "Lobby";
            BannerSubtitle = "Connexion au réseau…";
            return;
        }

        if (Networking.IsHost)
            ConnectedPlayerCount = gm.PlayerCount;

        if (!Networking.IsHost)
            return;

        if (_pendingBeginMatchAfterScene && IsGameplaySceneActive())
        {
            _pendingBeginMatchAfterScene = false;
            gm.BeginMatch();
            SessionPhase = MatchSessionPhase.InMatch;
            BannerTitle = "Partie en cours";
            BannerSubtitle = string.Empty;
            CountdownWholeSeconds = 0;
            return;
        }

        var count = gm.PlayerCount;

        switch (SessionPhase)
        {
            case MatchSessionPhase.WaitingForPlayers:
                BannerTitle = "Lobby";
                BannerSubtitle = count >= MinPlayersToStart
                    ? $"Objectif atteint ({count} joueurs). Démarrage du compte à rebours."
                    : $"En attente de joueurs — {count} / {MinPlayersToStart}.";
                if (count >= MinPlayersToStart)
                    EnterCountdown();
                break;

            case MatchSessionPhase.Countdown:
                if (count < MinPlayersToStart)
                {
                    SessionPhase = MatchSessionPhase.WaitingForPlayers;
                    _countdownElapsed = 0f;
                    break;
                }

                _countdownElapsed += Time.Delta;
                var remaining = CountdownDurationSeconds - _countdownElapsed;
                CountdownWholeSeconds = Math.Max(0, (int)Math.Ceiling(remaining));
                BannerTitle = "Début de partie";
                BannerSubtitle = "Préparez-vous. La map va se charger.";
                if (_countdownElapsed >= CountdownDurationSeconds)
                    StartArenaTransition();
                break;

            case MatchSessionPhase.LoadingArena:
                break;

            case MatchSessionPhase.InMatch:
                break;
        }
    }

    void EnterCountdown()
    {
        SessionPhase = MatchSessionPhase.Countdown;
        _countdownElapsed = 0f;
        CountdownWholeSeconds = (int)Math.Ceiling(CountdownDurationSeconds);
    }

    void StartArenaTransition()
    {
        SessionPhase = MatchSessionPhase.LoadingArena;
        BannerTitle = "Chargement";
        BannerSubtitle = "Ouverture de la map de jeu…";

        var options = new SceneLoadOptions();
        options.SetScene(GameplayScenePath);

        if (!Game.ChangeScene(options))
        {
            Log.Error($"ChangeScene a échoué pour '{GameplayScenePath}'.");
            SessionPhase = MatchSessionPhase.Countdown;
            _countdownElapsed = 0f;
            return;
        }

        _pendingBeginMatchAfterScene = true;
    }

    bool IsGameplaySceneActive()
    {
        var scene = Game.ActiveScene;
        if (!scene.IsValid())
            return false;

        var want = NormalizeScenePath(GameplayScenePath);
        var src = scene.Source;
        if (src != null)
        {
            var path = NormalizeScenePath(src.ResourcePath);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(want, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var title = scene.Name;
        return !string.IsNullOrEmpty(title) && title.Contains("play", StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeScenePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        return path.Replace('\\', '/').TrimStart('/');
    }
}
