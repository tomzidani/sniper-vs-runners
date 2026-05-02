namespace SniperVsRunners.Entities;

using SniperVsRunners.Teams;

public class PlayerEntity
{
    public Connection Connection { get; init; }
    public int Health { get; set; } = 100;
    public GameObject Pawn { get; set; }
    public TeamTypes CurrentTeam { get; set; } = TeamTypes.Spectators;
}
