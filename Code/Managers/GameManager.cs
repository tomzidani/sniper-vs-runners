namespace SniperVsRunners.Managers;

using System;
using System.Collections.Generic;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Entities;
using SniperVsRunners.Features.Spawning;
using SniperVsRunners.Teams;

public class GameManager
{
    public static GameManager Instance { get; private set; }

    public GamePhase Phase { get; private set; } = GamePhase.Lobby;

    public bool IsGameStarted => Phase == GamePhase.Playing;

    public int PlayerCount => _playerManager?.Players.Count ?? 0;

    /// <summary>Liste des joueurs connectés côté hôte (pour snapshot fin de manche / stats).</summary>
    public IReadOnlyList<PlayerEntity> AllPlayers => _playerManager?.Players ?? Array.Empty<PlayerEntity>();

    bool _sessionInitialized;

    protected PlayerManager _playerManager;
    protected TeamManager _teamManager;
    SpawnService _spawnService;

    public GameManager()
    {
        _playerManager = new PlayerManager();
        _teamManager = new TeamManager(this);
    }

    internal void MarkActiveSingleton()
    {
        Instance = this;
    }

    internal void ClearSingletonIfThis()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ConfigureSpawning(GameObject playerPrefab, GameObject spawnTransformFallback)
    {
        _spawnService = new SpawnService(playerPrefab, spawnTransformFallback, () => Phase);
    }

    public void Initialize()
    {
        if (_sessionInitialized)
            return;

        _sessionInitialized = true;
        _teamManager.CreateTeams();
        Phase = GamePhase.Lobby;
    }

    public void BeginMatch()
    {
        Phase = GamePhase.Playing;
        RespawnEveryoneForCurrentRules();
    }

    public void ReturnToLobby()
    {
        PrepareReturnToLobby();
        FinalizeReturnToLobby();
    }

    public void PrepareReturnToLobby(bool reassignTeams = true)
    {
        Phase = GamePhase.Lobby;

        if (reassignTeams)
            _teamManager.ReassignForLobby(_playerManager.Players);
    }

    public void FinalizeReturnToLobby()
    {
        RespawnEveryoneForCurrentRules();
    }

    void RespawnEveryoneForCurrentRules()
    {
        if (_spawnService == null)
            return;

        foreach (var player in _playerManager.Players.ToArray())
        {
            var existing = player.Pawn;
            if (existing.IsValid())
                existing.Destroy();

            player.Pawn = null;
            _spawnService.TrySpawnPawn(player);
        }
    }

    public void OnPlayerConnected(Connection connection)
    {
        // OnActive (host-only) peut se rappeler après un ChangeScene (ex. lobby → play) : ne pas
        // recréer un PlayerEntity ni respawner, sinon doublon de pawn pour la même connexion.
        if (_playerManager.FindByConnection(connection) != null)
            return;

        var player = _playerManager.CreatePlayer(connection);
        _teamManager.AssignPlayerToTeam(player);
        _spawnService?.TrySpawnPawn(player);
    }

    public void OnPlayerDisconnected(Connection connection)
    {
        var player = _playerManager.FindByConnection(connection);
        if (player == null)
            return;

        var pawn = player.Pawn;
        if (pawn.IsValid())
            pawn.Destroy();

        _teamManager.RemovePlayerFromAllTeams(player);
        _playerManager.RemovePlayer(player);
    }

    public int GetTeamPlayerCount(TeamTypes teamType)
    {
        return _teamManager.GetTeamPlayerCount(teamType);
    }

    public int GetAliveTeamPlayerCount(TeamTypes teamType)
    {
        var players = _teamManager.GetPlayersInTeam(teamType);
        var alive = 0;

        foreach (var player in players)
        {
            var pawn = player.Pawn;
            if (!pawn.IsValid())
                continue;

            var vitality = pawn.Components.Get<PlayerVitalityComponent>();
            if (vitality == null || !vitality.IsDead)
                alive++;
        }

        return alive;
    }
}
