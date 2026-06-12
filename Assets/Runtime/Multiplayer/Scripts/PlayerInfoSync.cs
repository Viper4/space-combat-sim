using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

/// <summary>
/// Owns the SyncDictionary that replicates player info to all peers,
/// and provides the ServerRpcs that clients use to submit their data.
///
/// LobbyManager is a MonoBehaviour and cannot own SyncObjects or send RPCs,
/// so this NetworkBehaviour provides that capability.
///
/// Scene setup:
///   1. Create a GameObject in your lobby scene (StartScene).
///   2. Add a NetworkObject component to it (makes it a scene network object —
///      no manual Spawn() call needed; FishNet registers it automatically).
///   3. Add this PlayerInfoSync component to the same GameObject.
///   4. If you want player info to persist into the game scene, add a second
///      instance of this prefab to your game scene as well. OnStartClient()
///      re-submits the stored username from LobbyManager.LocalUsername so the
///      data is automatically restored after the scene transition.
/// </summary>
[AddComponentMenu("Game/Player Info Sync")]
public class PlayerInfoSync : NetworkBehaviour
{
    public static PlayerInfoSync Instance { get; private set; }

    // ── Sync Data ──────────────────────────────────────────────────────────────

    // Key: ClientId (int).  NetworkConnection cannot be a sync key — it is a
    // runtime object that FishNet's serializer does not know how to encode.
    private readonly SyncDictionary<int, PlayerInfo> _players = new SyncDictionary<int, PlayerInfo>();

    /// <summary>Read-only view of all current player infos, available on every peer.</summary>
    public IReadOnlyDictionary<int, PlayerInfo> Players => _players;

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on all peers when any player's info is added or updated.
    /// arg1 = ClientId, arg2 = the new Player.
    /// </summary>
    public event Action<int, PlayerInfo> OnPlayerChanged;

    /// <summary>
    /// Fired on all peers when a player leaves and their info is removed.
    /// arg = the ClientId that was removed.
    /// </summary>
    public event Action<int> OnPlayerLeft;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Multiple instances can coexist if both lobby and game scenes have one.
            // The most recently-awoken instance wins; the old one is still valid while alive.
        }
        Instance = this;
        _players.OnChange += HandleDictionaryChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        _players.OnChange -= HandleDictionaryChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionStateServer;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionStateServer;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Submit the stored username immediately on connect (or reconnect after a
        // scene transition). Falls back to "Player N" if none has been set yet.
        string name = LobbyManager.Instance != null && !string.IsNullOrEmpty(LobbyManager.Instance.LocalPlayer.Username)
            ? LobbyManager.Instance.LocalPlayer.Username
            : $"Player {LocalConnection.ClientId}";

        SubmitUsernameServerRpc(name);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    public void SubmitMaxPlayersServerRpc(int maxPlayers)
    {
        LobbyManager.Instance.UpdateMaxPlayers(maxPlayers);
    }

    /// <summary>
    /// Call from any client to set or update their display name.
    /// The server always uses the sender's actual ClientId regardless of what
    /// the client passes in, so the ClientId field in Player cannot be spoofed.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SubmitUsernameServerRpc(string username, NetworkConnection sender = null)
    {
        // Preserve existing kills / deaths if the player is already registered.
        PlayerInfo info = _players.TryGetValue(sender.ClientId, out PlayerInfo existing)
            ? existing
            : PlayerInfo.Default(sender.ClientId);

        info.Username = string.IsNullOrWhiteSpace(username)
            ? $"Player {sender.ClientId}"
            : username;

        _players[sender.ClientId] = info;
    }

    /// <summary>
    /// Increments the kill count for a player.
    /// Server-authoritative — call from game logic (e.g. damage resolution), not from clients.
    /// </summary>
    [Server]
    public void AddKill(int clientId)
    {
        if (!_players.TryGetValue(clientId, out PlayerInfo info)) return;
        info.Kills++;
        _players[clientId] = info;
    }

    /// <summary>
    /// Increments the death count for a player.
    /// Server-authoritative — call from game logic, not from clients.
    /// </summary>
    [Server]
    public void AddDeath(int clientId)
    {
        if (!_players.TryGetValue(clientId, out PlayerInfo info)) return;
        info.Deaths++;
        _players[clientId] = info;
    }

    /// <summary>
    /// Returns the Player for a given ClientId, or false if they are not registered.
    /// Works on all peers (reads the local SyncDictionary copy).
    /// </summary>
    public bool TryGetPlayer(int clientId, out PlayerInfo info)
        => _players.TryGetValue(clientId, out info);

    // ── Server: clean up on disconnect ─────────────────────────────────────────

    private void HandleRemoteConnectionStateServer(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
            _players.Remove(conn.ClientId);
    }

    // ── SyncDictionary callback ────────────────────────────────────────────────

    private void HandleDictionaryChanged(
        SyncDictionaryOperation op, int key, PlayerInfo value, bool asServer)
    {
        switch (op)
        {
            case SyncDictionaryOperation.Add:
            case SyncDictionaryOperation.Set:
                OnPlayerChanged?.Invoke(key, value);
                break;

            case SyncDictionaryOperation.Remove:
                OnPlayerLeft?.Invoke(key);
                break;

            // Complete fires at the end of a full sync batch (e.g. a late-joining client
            // receiving the entire dictionary at once). Re-fire everything so late
            // subscribers get the current state rather than missing the initial burst.
            case SyncDictionaryOperation.Complete:
                foreach (var kvp in _players)
                    OnPlayerChanged?.Invoke(kvp.Key, kvp.Value);
                break;
        }
    }
}