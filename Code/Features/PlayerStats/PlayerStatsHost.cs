namespace SniperVsRunners.Features.PlayerStats;

using System;
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

		try
		{
			var shouldSave = false;
			try
			{
				shouldSave = !Networking.IsActive || Networking.IsHost;
			}
			catch (Exception e)
			{
				Log.Warning(e, "Networking indisponible pendant PlayerStatsHost.Shutdown ; sauvegarde ignorée.");
			}

			if (shouldSave)
				_service.SaveToDisk();
		}
		catch (Exception e)
		{
			Log.Error(e, "Échec de la sauvegarde des stats joueur à l’arrêt de la session.");
		}
		finally
		{
			_service = null;
		}
	}
}
