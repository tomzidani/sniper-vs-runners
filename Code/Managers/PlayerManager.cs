namespace SniperVsRunners.Managers;

using SniperVsRunners.Entities;

public class PlayerManager
{
    public PlayerEntity CreatePlayer(Connection connection)
    {
        return new PlayerEntity { Connection = connection };
    }
}
