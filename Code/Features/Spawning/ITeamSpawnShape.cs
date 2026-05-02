namespace SniperVsRunners.Features.Spawning;

public interface ITeamSpawnShape
{
    TeamSpawnAudience Audience { get; }

    bool TryPickWorldTransform(out Transform worldTransform);
}
