namespace SniperVsRunners.Components.Game;

using System;
using System.Threading.Tasks;
using Sandbox;
using SniperVsRunners.Features.GameFlow;
using SniperVsRunners.Features.Inventory;
using SniperVsRunners.Features.PlayerStats;
using SniperVsRunners.Features.Weapons;
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

    /// <summary>
    /// Si vrai : tous les joueurs reçoivent le profil pistolet (test 2 joueurs). Si faux : sniper = fusil, runners = pistolet.
    /// </summary>
    [Property]
    public bool DevForceEveryonePistol { get; set; } = true;

    /// <summary>Arme des runners (référence asset <c>.weapon</c>). Si null, ident <c>usp</c>.</summary>
    [Property]
    public WeaponDefinition RunnerPrimary { get; set; }

    /// <summary>Arme du sniper. Si null, ident <c>m700</c>.</summary>
    [Property]
    public WeaponDefinition SniperPrimary { get; set; }

    /// <summary>Si renseigné avec <see cref="DevForceEveryonePistol"/>, remplace l’arme pour tout le monde.</summary>
    [Property]
    public WeaponDefinition DevEveryonePrimary { get; set; }

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
        PlayerStatsHost.Initialize();
        GameObject.Components.GetOrCreate<InventoryFxComponent>();
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

        try
        {
            PlayerStatsHost.Shutdown();
        }
        catch (Exception e)
        {
            Log.Error(e, "PlayerStatsHost.Shutdown a échoué pendant GameComponent.OnDestroy (souvent I/O ou état réseau en cours d’arrêt).");
        }
        finally
        {
            try
            {
                _gameManager?.ClearSingletonIfThis();
            }
            catch (Exception e)
            {
                Log.Error(e, "GameManager.ClearSingletonIfThis a échoué pendant GameComponent.OnDestroy.");
            }
        }
    }

    protected override void OnStart()
    {
        if (BeginMatchWhenSceneStarts && GameObject.Components.Get<MatchFlowComponent>() == null)
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
