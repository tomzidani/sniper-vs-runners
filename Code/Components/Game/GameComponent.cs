namespace SniperVsRunners.Components.Game;

using System.Threading.Tasks;
using Sandbox;
using SniperVsRunners.Managers;

public class GameComponent : Component, Component.INetworkListener
{
    static GameComponent _instance;

    public static GameComponent Session => _instance;

    [Property]
    public bool BeginMatchWhenSceneStarts { get; set; }

    [Property]
    public GameObject PlayerPrefab { get; set; }

    [Property]
    public GameObject SpawnTransformFallback { get; set; }

    [Property]
    public bool CreateLobbyIfNone { get; set; } = true;

    public GameManager Game => _gameManager;

    GameManager _gameManager;

    protected override void OnAwake()
    {
        if (_instance != null && _instance != this)
        {
            DestroyGameObject();
            return;
        }

        _instance = this;
        GameObject.Flags |= GameObjectFlags.DontDestroyOnLoad;

        _gameManager = new GameManager();
        _gameManager.MarkActiveSingleton();
        _gameManager.Initialize();

        var fallback = SpawnTransformFallback;
        if (!fallback.IsValid())
            fallback = GameObject;

        _gameManager.ConfigureSpawning(PlayerPrefab, fallback);
    }

    protected override async Task OnLoad()
    {
        if (!CreateLobbyIfNone || Networking.IsActive)
            return;

        await Task.DelayRealtimeSeconds(0.1f);
        LoadingScreen.Title = "Lobby";
        Networking.CreateLobby(new());
    }

    protected override void OnDestroy()
    {
        if (_instance != this)
            return;

        _instance = null;
        _gameManager?.ClearSingletonIfThis();
    }

    protected override void OnStart()
    {
        if (BeginMatchWhenSceneStarts)
            _ = DeferBeginMatchAfterConnections();
    }

    async Task DeferBeginMatchAfterConnections()
    {
        await Task.DelayRealtimeSeconds(0.05f);
        _gameManager?.BeginMatch();
    }

    void Component.INetworkListener.OnActive(Connection connection)
    {
        _gameManager?.OnPlayerConnected(connection);
    }

    void Component.INetworkListener.OnDisconnected(Connection connection)
    {
        _gameManager?.OnPlayerDisconnected(connection);
    }
}
