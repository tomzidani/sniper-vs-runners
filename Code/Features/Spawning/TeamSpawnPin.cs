namespace SniperVsRunners.Features.Spawning;

using Sandbox;

[Title("Team spawn (point)")]
[Category("Spawning")]
public sealed class TeamSpawnPin : Component, ITeamSpawnShape
{
    [Property]
    public TeamSpawnAudience Audience { get; set; }

    public bool TryPickWorldTransform(out Transform worldTransform)
    {
        worldTransform = WorldTransform;
        return true;
    }
}
