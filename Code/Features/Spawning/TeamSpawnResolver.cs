namespace SniperVsRunners.Features.Spawning;

using System;
using System.Collections.Generic;
using System.Linq;
using SniperVsRunners.Managers;
using SniperVsRunners.Teams;

public static class TeamSpawnResolver
{
    public static bool TryResolve(
        GamePhase phase,
        TeamTypes team,
        Scene scene,
        GameObject fallbackTransformSource,
        out Transform worldTransform
    )
    {
        worldTransform = default;

        if (phase == GamePhase.Playing && team == TeamTypes.Spectators)
            return false;

        var audience = MapAudience(phase, team);
        var pickers = CollectPickers(scene).Where(p => p.Audience == audience).ToList();

        if (pickers.Count == 0)
            return TryFallback(fallbackTransformSource, out worldTransform);

        var chosen = pickers[Random.Shared.Next(pickers.Count)];
        if (!chosen.TryPickWorldTransform(out worldTransform))
            return TryFallback(fallbackTransformSource, out worldTransform);

        return true;
    }

    static TeamSpawnAudience MapAudience(GamePhase phase, TeamTypes team)
    {
        if (phase == GamePhase.Lobby)
            return TeamSpawnAudience.Lobby;

        return team switch
        {
            TeamTypes.Sniper => TeamSpawnAudience.Sniper,
            TeamTypes.Runners => TeamSpawnAudience.Runners,
            TeamTypes.Spectators => TeamSpawnAudience.Lobby,
            _ => TeamSpawnAudience.Lobby
        };
    }

    static IEnumerable<ITeamSpawnShape> CollectPickers(Scene scene)
    {
        if (!scene.IsValid())
            yield break;

        foreach (var pin in scene.GetAllComponents<TeamSpawnPin>())
            yield return pin;

        foreach (var zone in scene.GetAllComponents<TeamSpawnArea>())
            yield return zone;
    }

    static bool TryFallback(GameObject fallbackTransformSource, out Transform worldTransform)
    {
        if (fallbackTransformSource.IsValid())
        {
            worldTransform = fallbackTransformSource.WorldTransform;
            return true;
        }

        worldTransform = new Transform(new Vector3(0f, 0f, 128f), Rotation.Identity, 1f);
        return true;
    }
}
