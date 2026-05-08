namespace SniperVsRunners.Features.Spawning;

using System;
using SniperVsRunners.Components.Game;
using SniperVsRunners.Entities;
using SniperVsRunners.Features.Combat;
using SniperVsRunners.Features.Hud;
using SniperVsRunners.Features.Inventory;
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
        pawn.Components.GetOrCreate<PlayerWeaponAimComponent>();
        pawn.Components.GetOrCreate<PlayerAimCameraFovComponent>();
        var session = GameComponent.Session;
        var forcePistol = session?.DevForceEveryonePistol == true;
        weapon.ActiveWeaponIdent = WeaponSpawnIds.ResolvePrimaryIdent(forcePistol, player.CurrentTeam, session);
        weapon.HostApplyEquippedWeaponAmmo();

        pawn.Components.GetOrCreate<PlayerCitizenWeaponVisualComponent>();
        pawn.Components.GetOrCreate<PlayerVitalityComponent>();
        pawn.Components.GetOrCreate<PlayerInventoryComponent>();
        // ScreenPanel : requis pour que les PanelComponent (HUD) soient rendus à l’écran (doc moteur).
        pawn.Components.GetOrCreate<ScreenPanel>();
        pawn.Components.GetOrCreate<PlayerVitalityHud>();
        pawn.Components.GetOrCreate<PlayerDeathScreenHud>();
        pawn.Components.GetOrCreate<ItemWheelHud>();
        pawn.Components.GetOrCreate<PlayerFlashHud>();
        pawn.Components.GetOrCreate<PlayerWeaponAmmoHud>();
        pawn.Components.GetOrCreate<PlayerCrosshairHud>();

        pawn.NetworkSpawn(player.Connection);
        player.Pawn = pawn;

        var vitality = pawn.Components.Get<PlayerVitalityComponent>();
        vitality?.ServerResetForSpawn();
        pawn.Components.Get<PlayerInventoryComponent>()?.ServerGiveDefaultWeaponTestSlots();
    }
}
