using System;

/// <summary>
/// Serializable player data synced to all clients via PlayerInfoSync.
/// FishNet auto-generates serializer code for structs containing primitives and strings.
/// Kills and deaths are server-authoritative — only PlayerInfoSync.AddKill / AddDeath write them.
/// </summary>
[Serializable]
public struct PlayerInfo
{
    public int    ClientId;
    public string Username;
    public int    Kills;
    public int    Deaths;

    public static PlayerInfo Default(int clientId) => new PlayerInfo
    {
        ClientId = clientId,
        Username = $"Player {clientId}",
        Kills    = 0,
        Deaths   = 0,
    };
}