using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using System.Collections;

public class PlayerInfoRelay : NetworkBehaviour
{
    public static PlayerInfoRelay Instance { get; private set; }

    public Action OnPlayerInfoChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (InstanceFinder.IsClientStarted)
        {
            StartCoroutine(WaitToInitializePlayerData());
        }
    }

    private IEnumerator WaitToInitializePlayerData()
    {
        yield return new WaitUntil(() => IsClientInitialized);

        // Ask server for current player infos.
        if (!IsServerInitialized)
            GetInitialDataServerRpc(LocalConnection);
        // Then submit my player info
        LobbyManager.Instance.UpdatePlayerUsername();
        if (!PlayerRegistry.TryGetPlayer(LocalConnection.ClientId, out var thisInfo))
        {
            thisInfo = PlayerInfo.Default(LocalConnection.ClientId);
            thisInfo.Username = LobbyManager.Instance.GetLocalUsername();
        }
        if (!IsServerInitialized)
            SendPlayerInfoServerRpc(thisInfo, LocalConnection);

        // Also need to ensure LobbyManager stuff is properly setup
        LobbyManager.Instance.UpdatePlayerCount();
    }

    private void OnEnable()
    {
        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
    }

    private void OnDisable()
    {
        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
    }

    // ---------------------------------------------------------
    // CLIENT -> SERVER
    // ---------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void SendPlayerInfoServerRpc(PlayerInfo info, NetworkConnection sender = null)
    {
        if (sender == null)
            return;

        // Force correct client id.
        info.ClientId = sender.ClientId;
        PlayerRegistry.SetPlayer(info);
        Debug.Log(GameLog.ObjectLog(this, $"Updated PlayerInfo ({info.Username}) for client {sender.ClientId} on the Server."));

        BroadcastPlayerInfoObserversRpc(info);
    }

    [ServerRpc(RequireOwnership = false)]
    private void GetInitialDataServerRpc(NetworkConnection sender = null)
    {
        if (sender == null)
            return;

        List<PlayerInfo> infos = new();

        foreach (var pair in PlayerRegistry.Players)
        {
            infos.Add(pair.Value);
            Debug.Log(GameLog.ObjectLog(this, $"Server is sending {pair.Value.ClientId} {pair.Value.Username} to {sender.ClientId}"));
        }
        SendInitialDataTargetRpc(sender, infos.ToArray());
    }

    // ---------------------------------------------------------
    // SERVER -> ALL CLIENTS
    // ---------------------------------------------------------

    [ObserversRpc(BufferLast = false)]
    private void BroadcastPlayerInfoObserversRpc(PlayerInfo info)
    {
        PlayerRegistry.SetPlayer(info);
        OnPlayerInfoChanged?.Invoke();
        Debug.Log(GameLog.ObjectLog(this, $"Updated PlayerInfo {info.ClientId} {info.Username} for the local client."));
    }

    [ObserversRpc]
    public void SendMaxPlayersObserversRpc(int maxPlayers)
    {
        LobbyManager.Instance.SetMaxPlayers(maxPlayers);
    }

    // ---------------------------------------------------------
    // SERVER -> ONE CLIENT
    // ---------------------------------------------------------

    [TargetRpc]
    private void SendInitialDataTargetRpc(NetworkConnection conn, PlayerInfo[] infos)
    {
        PlayerRegistry.Clear();
        foreach (PlayerInfo info in infos)
        {
            Debug.Log(GameLog.ObjectLog(this, $"Received {info.ClientId} {info.Username} from server."));
            PlayerRegistry.SetPlayer(info);
        }
        OnPlayerInfoChanged?.Invoke();
    }

    // ---------------------------------------------------------
    // CONNECTS/DISCONNECTS FOR SERVER AND ALL CLIENTS
    // ---------------------------------------------------------

    private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        // Connecting players need to send their info 
        switch (args.ConnectionState)
        {
            case RemoteConnectionState.Stopped:
                PlayerRegistry.RemovePlayer(conn.ClientId);
                break;
        }
    }
}