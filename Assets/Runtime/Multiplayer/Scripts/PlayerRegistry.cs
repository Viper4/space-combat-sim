using System;
using System.Collections.Generic;

public static class PlayerRegistry
{
    private static readonly Dictionary<int, PlayerInfo> players = new();

    public static IReadOnlyDictionary<int, PlayerInfo> Players => players;

    public static event Action<int, PlayerInfo> OnPlayerUpdated;
    public static event Action<int> OnPlayerRemoved;

    public static void SetPlayer(PlayerInfo info)
    {
        players[info.ClientId] = info;
        OnPlayerUpdated?.Invoke(info.ClientId, info);
    }

    public static bool TryGetPlayer(int clientId, out PlayerInfo info)
    {
        return players.TryGetValue(clientId, out info);
    }

    public static void RemovePlayer(int clientId)
    {
        if (players.Remove(clientId))
            OnPlayerRemoved?.Invoke(clientId);
    }

    public static void Clear()
    {
        players.Clear();
    }
}