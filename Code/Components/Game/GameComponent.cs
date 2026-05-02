namespace SniperVsRunners.Components.Game;

using Sandbox;
using SniperVsRunners.Managers;

public class GameComponent : Component, Component.INetworkListener
{
    static GameComponent _instance;

    public static GameComponent Session => _instance;

    [Property]
    public bool BeginMatchWhenSceneStarts { get; set; }

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
            _gameManager?.BeginMatch();
    }

    void Component.INetworkListener.OnActive(Connection connection)
    {
        _gameManager?.OnPlayerConnected(connection);
    }
}
