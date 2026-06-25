using System;

/// <summary>
/// Serializable player data synced to all clients via PlayerInfoSync.
/// FishNet auto-generates serializer code for structs containing primitives and strings.
/// PlayerInfo Username should only be updated by the local client, and updates are then sent to Server who then distributes that update to all clients.
/// Other fields like Kills and Deaths should be server-authoritative.
/// </summary>
[Serializable]
public struct PlayerInfo
{
    public int ClientId;
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