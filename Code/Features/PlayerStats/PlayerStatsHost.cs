namespace SniperVsRunners.Features.PlayerStats;

using Sandbox;

/// <summary>
/// Point d’accès hôte pour les stats persistantes (initialisé par <see cref="Components.Game.GameComponent"/>).
/// </summary>
public static class PlayerStatsHost
{
	static PlayerStatsService _service;

	public static PlayerStatsService Service => _service;

	public static void Initialize()
	{
		if (_service != null)
			return;

		_service = new PlayerStatsService(new FileSystemPlayerStatsStore());
		_service.LoadFromDisk();
	}

	public static void Shutdown()
	{
		if (_service == null)
			return;

		if (!Networking.IsActive || Networking.IsHost)
			_service.SaveToDisk();

		_service = null;
	}
}
