namespace SniperVsRunners.Features.GameFlow;

using System;
using Sandbox;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Features.Inventory;
using SniperVsRunners.Features.MatchResults;
using SniperVsRunners.Features.PlayerStats;
using SniperVsRunners.Managers;
using SniperVsRunners.Teams;

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

    [Property]
    public string LobbyScenePath { get; set; } = "scenes/lobby.scene";

    [Property]
    public float MatchDurationSeconds { get; set; } = 180f;

    [Property]
    public float ReturnToLobbyDelaySeconds { get; set; } = 4f;

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
    float _matchElapsed;
    float _returnToLobbyElapsed;
    bool _pendingBeginMatchAfterScene;
    bool _pendingFinalizeReturnToLobby;

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
            InventoryPickupSpawner.SpawnDefaultPickupsIfHost();
            SessionPhase = MatchSessionPhase.InMatch;
            _matchElapsed = 0f;
            BannerTitle = "Partie en cours";
            BannerSubtitle = "Le sniper doit éliminer tous les runners.";
            CountdownWholeSeconds = 0;
            return;
        }

        if (_pendingFinalizeReturnToLobby && IsLobbySceneActive())
        {
            _pendingFinalizeReturnToLobby = false;
            gm.FinalizeReturnToLobby();
            SessionPhase = MatchSessionPhase.WaitingForPlayers;
            BannerTitle = "Lobby";
            BannerSubtitle = "Nouvelles équipes assignées. En attente de joueurs…";
            CountdownWholeSeconds = 0;
            _countdownElapsed = 0f;
            _returnToLobbyElapsed = 0f;
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
                RunInMatchRules(gm, count);
                break;

            case MatchSessionPhase.ReturningToLobby:
                _returnToLobbyElapsed += Time.Delta;
                var seconds = Math.Max(0, (int)Math.Ceiling(ReturnToLobbyDelaySeconds - _returnToLobbyElapsed));
                CountdownWholeSeconds = seconds;
                BannerTitle = "Fin de partie";
                if (string.IsNullOrWhiteSpace(BannerSubtitle))
                    BannerSubtitle = "Retour au lobby…";

                if (_returnToLobbyElapsed >= ReturnToLobbyDelaySeconds)
                    StartLobbyTransition(gm);
                break;
        }
    }

    void RunInMatchRules(GameManager gm, int connectedCount)
    {
        _matchElapsed += Time.Delta;
        var remaining = Math.Max(0f, MatchDurationSeconds - _matchElapsed);
        CountdownWholeSeconds = (int)Math.Ceiling(remaining);
        BannerTitle = "Partie en cours";
        BannerSubtitle = $"Temps restant : {CountdownWholeSeconds}s";

        if (connectedCount < MinPlayersToStart)
        {
            EndMatchAndReturnToLobby(
                gm,
                MatchEndReason.NotEnoughPlayers,
                "Partie interrompue",
                "Pas assez de joueurs pour continuer.");
            return;
        }

        var sniperCount = gm.GetTeamPlayerCount(TeamTypes.Sniper);
        if (sniperCount == 0)
        {
            EndMatchAndReturnToLobby(
                gm,
                MatchEndReason.SniperDisconnected,
                "Partie interrompue",
                "Le sniper a quitté la partie.");
            return;
        }

        var runnerCount = gm.GetTeamPlayerCount(TeamTypes.Runners);
        if (runnerCount == 0)
        {
            EndMatchAndReturnToLobby(
                gm,
                MatchEndReason.AllRunnersDisconnected,
                "Victoire Sniper",
                "Tous les runners ont quitté la partie.");
            return;
        }

        var aliveSnipers = gm.GetAliveTeamPlayerCount(TeamTypes.Sniper);
        if (aliveSnipers <= 0)
        {
            EndMatchAndReturnToLobby(
                gm,
                MatchEndReason.SniperEliminated,
                "Victoire Runners",
                "Le sniper a été éliminé.");
            return;
        }

        var aliveRunners = gm.GetAliveTeamPlayerCount(TeamTypes.Runners);
        if (aliveRunners <= 0)
        {
            EndMatchAndReturnToLobby(
                gm,
                MatchEndReason.AllRunnersEliminated,
                "Victoire Sniper",
                "Tous les runners sont éliminés.");
            return;
        }

        if (_matchElapsed >= MatchDurationSeconds)
        {
            EndMatchAndReturnToLobby(
                gm,
                MatchEndReason.TimeoutSniperAlive,
                "Victoire Sniper",
                "Temps écoulé : le sniper est toujours en vie.");
        }
    }

    void EndMatchAndReturnToLobby(GameManager gm, MatchEndReason reason, string title, string subtitle)
    {
        if (SessionPhase != MatchSessionPhase.InMatch)
            return;

        if (Networking.IsHost && PlayerStatsHost.Service != null)
        {
            var snaps = PlayerStatsService.BuildParticipantSnapshots(gm.AllPlayers);
            PlayerStatsHost.Service.ApplyMatchOutcomes(reason, snaps);
        }

        SessionPhase = MatchSessionPhase.ReturningToLobby;
        BannerTitle = title;
        BannerSubtitle = subtitle;
        _returnToLobbyElapsed = 0f;
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

    void StartLobbyTransition(GameManager gm)
    {
        BannerTitle = "Retour lobby";
        BannerSubtitle = "Chargement du lobby…";
        gm.PrepareReturnToLobby(reassignTeams: true);

        var options = new SceneLoadOptions();
        options.SetScene(LobbyScenePath);

        if (!Game.ChangeScene(options))
        {
            Log.Error($"ChangeScene a échoué pour '{LobbyScenePath}'.");
            gm.ReturnToLobby();
            SessionPhase = MatchSessionPhase.WaitingForPlayers;
            BannerTitle = "Lobby";
            BannerSubtitle = "Retour local au lobby (fallback).";
            CountdownWholeSeconds = 0;
            return;
        }

        _pendingFinalizeReturnToLobby = true;
    }

    bool IsGameplaySceneActive()
    {
        var scene = Game.ActiveScene;
        if (!scene.IsValid())
            return false;

        var want = NormalizeScenePath(GameplayScenePath);
        var wantFile = SceneFileName(want);

        var src = scene.Source;
        if (src != null)
        {
            var path = NormalizeScenePath(src.ResourcePath);
            if (!string.IsNullOrEmpty(path))
            {
                if (path.EndsWith(want, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrEmpty(wantFile)
                    && SceneFileName(path).Equals(wantFile, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        var title = scene.Name;
        return !string.IsNullOrEmpty(title) && title.Contains("play", StringComparison.OrdinalIgnoreCase);
    }

    static string SceneFileName(string normalizedPath)
    {
        if (string.IsNullOrEmpty(normalizedPath))
            return string.Empty;
        var i = normalizedPath.LastIndexOf('/');
        return i >= 0 && i < normalizedPath.Length - 1
            ? normalizedPath[(i + 1)..]
            : normalizedPath;
    }

    bool IsLobbySceneActive()
    {
        var scene = Game.ActiveScene;
        if (!scene.IsValid())
            return false;

        var want = NormalizeScenePath(LobbyScenePath);
        var wantFile = SceneFileName(want);

        var src = scene.Source;
        if (src != null)
        {
            var path = NormalizeScenePath(src.ResourcePath);
            if (!string.IsNullOrEmpty(path))
            {
                if (path.EndsWith(want, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrEmpty(wantFile)
                    && SceneFileName(path).Equals(wantFile, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        var title = scene.Name;
        return !string.IsNullOrEmpty(title) && title.Contains("lobby", StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeScenePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        return path.Replace('\\', '/').TrimStart('/');
    }
}
