namespace SniperVsRunners.Features.Spawning;

using System;
using SniperVsRunners.Entities;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.Hud;
using SniperVsRunners.Features.Vitality;
using SniperVsRunners.Features.Weapons;
using SniperVsRunners.Managers;
using SniperVsRunners.Teams;

public sealed class SpawnService
{
    readonly GameObject _playerPrefab;
    readonly GameObject _spawnTransformFallback;
    readonly Func<GamePhase> _getPhase;

    public SpawnService(GameObject playerPrefab, GameObject spawnTransformFallback, Func<GamePhase> getPhase)
    {
        _playerPrefab = playerPrefab;
        _spawnTransformFallback = spawnTransformFallback;
        _getPhase = getPhase;
    }

    public void TrySpawnPawn(PlayerEntity player)
    {
        if (!_playerPrefab.IsValid())
        {
            Log.Warning("Player prefab is missing: assign it on the GameComponent in the inspector.");
            return;
        }

        var phase = _getPhase();

        if (phase == GamePhase.Playing && player.CurrentTeam == TeamTypes.Spectators)
            return;

        if (!TeamSpawnResolver.TryResolve(phase, player.CurrentTeam, Game.ActiveScene, _spawnTransformFallback, out var startTransform))
            return;

        startTransform = startTransform.WithScale(1f);
        var pawn = _playerPrefab.Clone(startTransform, name: $"Player - {player.Connection.DisplayName}");

        var combatInfo = pawn.Components.GetOrCreate<PlayerCombatInfoComponent>();
        combatInfo.Team = player.CurrentTeam;

        var weapon = pawn.Components.GetOrCreate<PlayerHitscanWeaponComponent>();
        weapon.ActiveProfile = player.CurrentTeam == TeamTypes.Sniper
            ? WeaponProfileKind.SniperRifle
            : WeaponProfileKind.Sidearm;

        pawn.Components.GetOrCreate<PlayerVitalityComponent>();
        // ScreenPanel : requis pour que les PanelComponent (HUD) soient rendus à l’écran (doc moteur).
        pawn.Components.GetOrCreate<ScreenPanel>();
        pawn.Components.GetOrCreate<PlayerVitalityHud>();
        pawn.Components.GetOrCreate<PlayerDeathScreenHud>();

        pawn.NetworkSpawn(player.Connection);
        player.Pawn = pawn;

        var vitality = pawn.Components.Get<PlayerVitalityComponent>();
        vitality?.ServerResetForSpawn();
    }
}
