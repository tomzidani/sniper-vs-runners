namespace SniperVsRunners.Managers;

using SniperVsRunners.Entities;

public class GameManager
{
    public static GameManager Instance { get; private set; }

    public GamePhase Phase { get; private set; } = GamePhase.Lobby;

    public bool IsGameStarted => Phase == GamePhase.Playing;

    bool _sessionInitialized;

    protected PlayerManager _playerManager;
    protected TeamManager _teamManager;

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
    }

    public void ReturnToLobby()
    {
        Phase = GamePhase.Lobby;
    }

    public void OnPlayerConnected(Connection connection)
    {
        var player = _playerManager.CreatePlayer(connection);

        _teamManager.AssignPlayerToTeam(player);
    }
}
