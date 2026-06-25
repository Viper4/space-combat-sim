using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;

public class PlayerInfoRelay : NetworkBehaviour
{
    public static PlayerInfoRelay Instance { get; private set; }

    // Server-side authoritative copy.
    private readonly Dictionary<int, PlayerInfo> serverPlayers = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
    }

    private void OnDisable()
    {
        if (InstanceFinder.ServerManager != null)
            InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
            return;

        // Ask server for current player list.
        RequestInitialPlayerDataServerRpc();
        // Submit my player info
        if (!PlayerRegistry.TryGetPlayer(LocalConnection.ClientId, out PlayerInfo thisInfo))
            thisInfo = PlayerInfo.Default(LocalConnection.ClientId);
        SubmitPlayerInfoServerRpc(thisInfo);
    }

    // ---------------------------------------------------------
    // CLIENT -> SERVER
    // ---------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void SubmitPlayerInfoServerRpc(PlayerInfo info, NetworkConnection sender = null)
    {
        if (sender == null)
            return;

        // Force correct client id.
        info.ClientId = sender.ClientId;
        serverPlayers[sender.ClientId] = info;

        BroadcastPlayerInfoObserversRpc(info);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInitialPlayerDataServerRpc(NetworkConnection sender = null)
    {
        if (sender == null)
            return;

        List<PlayerInfo> infos = new();

        foreach (var pair in PlayerRegistry.Players)
            infos.Add(pair.Value);

        TargetReceiveInitialData(sender, infos.ToArray());
    }

    // ---------------------------------------------------------
    // SERVER -> ALL CLIENTS
    // ---------------------------------------------------------

    [ObserversRpc(BufferLast = false)]
    private void BroadcastPlayerInfoObserversRpc(PlayerInfo info)
    {
        PlayerRegistry.SetPlayer(info);
    }

    // ---------------------------------------------------------
    // SERVER -> ONE CLIENT
    // ---------------------------------------------------------

    [TargetRpc]
    private void TargetReceiveInitialData(NetworkConnection conn, PlayerInfo[] infos)
    {
        foreach (PlayerInfo info in infos)
            PlayerRegistry.SetPlayer(info);
    }

    // ---------------------------------------------------------
    // DISCONNECTS FOR SERVER AND ALL CLIENTS
    // ---------------------------------------------------------

    private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        // Connecting players need to send their info themselves
        if (args.ConnectionState != RemoteConnectionState.Stopped)
            return;

        PlayerRegistry.RemovePlayer(conn.ClientId);
    }
}