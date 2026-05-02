namespace SniperVsRunners.Managers;

using SniperVsRunners.Entities;

public class PlayerManager
{
    readonly List<PlayerEntity> _players = new List<PlayerEntity>();

    public IReadOnlyList<PlayerEntity> Players => _players;

    public PlayerEntity CreatePlayer(Connection connection)
    {
        var player = new PlayerEntity { Connection = connection };
        _players.Add(player);
        return player;
    }

    public PlayerEntity FindByConnection(Connection connection)
    {
        return _players.FirstOrDefault(p => p.Connection.Id == connection.Id);
    }

    public void RemovePlayer(PlayerEntity player)
    {
        _players.Remove(player);
    }
}
