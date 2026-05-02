namespace SniperVsRunners.Managers;

using SniperVsRunners.Teams;
using SniperVsRunners.Entities;

public class TeamManager
{
    protected GameManager _gameManager;
    protected List<Team> _teams = new List<Team>();

    public TeamManager(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void CreateTeams()
    {
        _teams.Clear();
        _teams.Add(new SniperTeam());
        _teams.Add(new RunnersTeam());
        _teams.Add(new SpectatorsTeam());
    }

    public void AssignPlayerToTeam(PlayerEntity player)
    {
        RemovePlayerFromAllTeams(player);

        var teamType = ResolveTeamTypeForIncomingPlayer();
        AddPlayerToTeam(player, teamType);
    }

    TeamTypes ResolveTeamTypeForIncomingPlayer()
    {
        if (_gameManager.Phase == GamePhase.Playing)
            return TeamTypes.Spectators;

        var sniper = GetTeam(TeamTypes.Sniper);
        if (sniper != null && sniper.Players.Count == 0)
            return TeamTypes.Sniper;

        return TeamTypes.Runners;
    }

    public void RemovePlayerFromAllTeams(PlayerEntity player)
    {
        foreach (var team in _teams)
            team.RemovePlayer(player);
    }

    Team GetTeam(TeamTypes teamType)
    {
        return _teams.FirstOrDefault(t => t.Type == teamType);
    }

    public void AddPlayerToTeam(PlayerEntity player, TeamTypes teamType)
    {
        var team = GetTeam(teamType);

        if (team == null)
        {
            Log.Error($"Team {teamType} not found when adding player to team");
            return;
        }

        team.AddPlayer(player);
    }
}
