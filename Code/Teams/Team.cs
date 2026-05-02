namespace SniperVsRunners.Teams;

using SniperVsRunners.Entities;

public abstract class Team
{
    protected Team(string name, TeamTypes type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }
    public TeamTypes Type { get; }
    public List<PlayerEntity> Players { get; } = new List<PlayerEntity>();

    public virtual void AddPlayer(PlayerEntity player)
    {
        if (Players.Any(p => p.Connection.Id == player.Connection.Id))
            return;

        Players.Add(player);
    }

    public void RemovePlayer(PlayerEntity player)
    {
        Players.RemoveAll(p => p.Connection.Id == player.Connection.Id);
    }
}
