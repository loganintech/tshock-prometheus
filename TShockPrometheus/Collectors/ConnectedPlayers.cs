using System.Collections.Generic;
using Prometheus;
using TerrariaApi.Server;
using TShockAPI;
using System.Text.RegularExpressions;

namespace TShockPrometheus.Collectors {
  class ConnectedPlayers : BaseCollector {

    /// <summary>
    /// The Prometheus Collector that will hold this metric. This is static
    /// because names are unique, so we couldn't create a copy for each class
    /// instance.
    /// </summary>
    static readonly Gauge collector = Metrics.CreateGauge(Prefix("connected_player_count"), "connected players");

    public ConnectedPlayers (TerrariaPlugin plugin) : base(plugin) {
    }

    #region Initialize/Dispose
    /// <summary>
    /// Hook into Terraria, but first check if we should enable or not
    /// </summary>
    public override void Initialize () {
      if (enabled) return;

      ServerApi.Hooks.ServerJoin.Register(plugin, OnJoin);
      ServerApi.Hooks.ServerLeave.Register(plugin, OnLeave);

      enabled = true;
    }

    public override void Dispose () {
      if (!enabled) return;

      ServerApi.Hooks.ServerJoin.Deregister(plugin, OnJoin);
      ServerApi.Hooks.ServerLeave.Deregister(plugin, OnLeave);

      enabled = false;
    }
    #endregion

    private Dictionary<string, Gauge> playersToGague = new Dictionary<string, Gauge>();

    private string labelNameFromPlayerName(string playerName)
    {
      // Replace all non-alphanumeric characters with an underscore
      string pattern = "[^a-zA-Z0-9]";
      string replacement = "_";
      return Regex.Replace(playerName, pattern, replacement);
    }

    #region Hooks
    /// <summary>
    /// Called when a player joins the server
    /// </summary>
    /// <param name="args">event arguments passed by hook</param>
    private void OnJoin (JoinEventArgs args)
    {
      collector.Inc(1);

      TSPlayer player = TShock.Players[args.Who];
      if (player == null) {
        return;
      }

      Gauge gauge;
      bool hasGauge = playersToGague.TryGetValue(player.Name, out gauge);
      if (hasGauge && gauge != null)
      {
        gauge.Set(1);
        return;
      }

      Gauge newGauge = Metrics.CreateGauge(Prefix("connected_player"), "connected player", "name");
      newGauge.WithLabels(labelNameFromPlayerName(player.Name)).Set(1);
      playersToGague.Add(player.Name, newGauge);
    }

    /// <summary>
    /// Called when a player leaves the server
    /// </summary>
    /// <param name="args">event arguments passed by hook</param>
    private void OnLeave (LeaveEventArgs args) {
      if (TShock.Players[args.Who] == null) return;
      collector.Dec(1);
      
      TSPlayer player = TShock.Players[args.Who];
      Gauge gauge;
      bool hasGauge = playersToGague.TryGetValue(player.Name, out gauge);
      if (hasGauge && gauge != null)
      {
        gauge?.WithLabels(labelNameFromPlayerName(player.Name)).Set(0);
      }
    }
    #endregion
  }
}
