namespace SniperVsRunners.Features.Spawning;

using System;
using Sandbox;

public enum TeamSpawnAreaShape
{
    Circle,
    Rectangle
}

[Title("Team spawn (zone)")]
[Category("Spawning")]
public sealed class TeamSpawnArea : Component, ITeamSpawnShape
{
    [Property]
    public TeamSpawnAudience Audience { get; set; }

    [Property]
    public TeamSpawnAreaShape Shape { get; set; } = TeamSpawnAreaShape.Circle;

    [Property]
    public float Radius { get; set; } = 128f;

    [Property]
    public Vector3 RectangleHalfExtents { get; set; } = new Vector3(128f, 4f, 128f);

    public bool TryPickWorldTransform(out Transform worldTransform)
    {
        var rotation = WorldTransform.Rotation;
        var origin = WorldTransform.Position;

        var localOffset = Shape switch
        {
            TeamSpawnAreaShape.Circle => SampleCircleXZ(),
            TeamSpawnAreaShape.Rectangle => SampleBox(),
            _ => SampleBox()
        };

        var worldPos = origin + rotation * localOffset;
        worldTransform = new Transform(worldPos, rotation, 1f);
        return true;
    }

    Vector3 SampleCircleXZ()
    {
        var angle = (float)(Random.Shared.NextDouble() * Math.PI * 2d);
        var r = MathF.Sqrt((float)Random.Shared.NextDouble()) * Radius;
        return new Vector3(MathF.Cos(angle) * r, 0f, MathF.Sin(angle) * r);
    }

    Vector3 SampleBox()
    {
        static float Unit()
        {
            return (float)(Random.Shared.NextDouble() * 2d - 1d);
        }

        return new Vector3(
            Unit() * RectangleHalfExtents.x,
            Unit() * RectangleHalfExtents.y,
            Unit() * RectangleHalfExtents.z
        );
    }
}
