using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.Networking;
using FishNet.Object.Synchronizing;
using FishNet.Object;

/// <summary>
/// Central lobby system. Manages the always-on main server connection,
/// hosting private lobbies, and joining via invite code or direct IP.
///
/// Setup:
///   1. Attach to a persistent (DontDestroyOnLoad) GameObject in your bootstrap scene.
///   2. Set mainServerAddress / mainServerPort to your dedicated server.
///   3. Set privateServerPort to any open port for player-hosted lobbies (default 7771).
///   4. Set connectionTimeoutSeconds to match Tugboat's ClientConnectTimeout / 1000 (default 5).
///   5. LobbyUI wires its buttons to the public methods here.
///
/// Failure message taxonomy:
///   - Unknown host:    DNS resolution failed before a connection was even attempted.
///   - Timed out:       We never reached LocalConnectionState.Started and the full
///                      timeout elapsed — server offline, wrong port, or firewalled.
///   - Unreachable:     Failed faster than the timeout (rare on UDP; ICMP unreachable).
///   - Server full:     We briefly reached Started then were immediately kicked — the
///                      server enforced its max-player cap.
///   - Lost connection: We were in Connected/Hosting state and got an unexpected Stopped.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    // -- Inspector --------------------------------------------------------------

    [Header("Main Server")]
    [SerializeField] private string mainServerAddress = "your-server.example.com";
    [SerializeField] private ushort mainServerPort    = 7770;

    [Header("Private Lobby")]
    [SerializeField] private ushort privateServerPort = 7771;

    [Header("Connection Diagnostics")]
    [Tooltip("Match this to Tugboat's ClientConnectTimeout / 1000. Default Tugboat value is 5.")]
    [SerializeField] private float connectionTimeoutSeconds = 5f;
    [Tooltip("If LocalConnectionState reaches Started but drops again within this window, "
           + "we report 'server full' rather than a generic disconnect.")]
    [SerializeField] private float kickWindowSeconds = 2f;

    // -- State ------------------------------------------------------------------

    public enum LobbyState
    {
        Disconnected,
        Connecting,  // DNS check or connection attempt in progress
        Hosting,     // This machine is running a listen-server + local client
        Connected    // Fully connected to a remote server
    }

    public LobbyState State  { get; private set; } = LobbyState.Disconnected;
    public bool       IsHosting => _isHosting;

    // -- Player Info ------------------------------------------------------------
    public PlayerInfo LocalPlayer;

    // -- Events -----------------------------------------------------------------

    /// <summary>Fired whenever State changes.</summary>
    public event Action<LobbyState> OnStateChanged;

    /// <summary>
    /// Fired on any unintentional connection failure or drop.
    /// The string is a human-readable, display-ready message.
    /// </summary>
    public event Action<string> OnConnectionFailed;

    /// <summary>
    /// Fired on the hosting client after HostPrivateLobby() once the public IP
    /// is resolved.  The string is a base-64 invite code.
    /// </summary>
    public event Action<string> OnInviteCodeReady;

    /// <summary>
    /// Fired on the hosting client whenever a remote player joins or leaves.
    /// arg1 = current player count (includes the host).
    /// arg2 = max players (0 = unlimited).
    /// </summary>
    public event Action<int, int> OnPlayerCountChanged;

    // -- Private ----------------------------------------------------------------

    private bool _isHosting;
    private int  _maxPlayers; // 0 = unlimited

    // Connection diagnostics — set on every attempt, read in ClassifyAndFireFailure()
    private enum ConnectTarget { MainServer, PrivateLobby }
    private ConnectTarget _connectTarget;
    private string  _lastAttemptedAddress;
    private ushort  _lastAttemptedPort;
    private float   _connectionStartTime;  // Time.time when StartConnection() was called
    private bool    _everReachedStarted;   // true once LocalConnectionState.Started fires
    private float   _connectedStartTime;   // Time.time when Started first fired
    private bool    _disconnectingIntentionally; // Set by Disconnect() to suppress error messages

    // -- Lifecycle --------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        InstanceFinder.ClientManager.OnClientConnectionState += HandleClientState;
        InstanceFinder.ServerManager.OnServerConnectionState += HandleServerState;
    }

    private void OnDisable()
    {
        if (InstanceFinder.ClientManager != null)
            InstanceFinder.ClientManager.OnClientConnectionState -= HandleClientState;
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnServerConnectionState  -= HandleServerState;
            InstanceFinder.ServerManager.OnRemoteConnectionState  -= HandleRemoteConnectionServer;
        }
    }

    // -- Public API -------------------------------------------------------------

    /// <summary>Connect to the always-on main server.</summary>
    public void ConnectToMainServer()
    {
        if (State != LobbyState.Disconnected) return;
        SetState(LobbyState.Connecting);
        StartCoroutine(ValidateAndConnect(mainServerAddress, mainServerPort, ConnectTarget.MainServer));
    }

    /// <summary>
    /// Start a private listen-server lobby on this machine.
    /// Fires OnInviteCodeReady once the public IP is resolved.
    /// </summary>
    /// <param name="maxPlayers">
    /// Maximum concurrent players including the host.  0 = unlimited.
    /// </param>
    public void HostPrivateLobby(int maxPlayers = 0)
    {
        if (State != LobbyState.Disconnected)
            return;

        _isHosting              = true;
        _maxPlayers             = Mathf.Max(0, maxPlayers);
        _everReachedStarted     = false;
        _disconnectingIntentionally = false;

        // Server starts first, then the local client connects to loopback.
        SetPort(privateServerPort);
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionServer;

        SetTransport("localhost", privateServerPort);
        _connectionStartTime = Time.time;
        InstanceFinder.ClientManager.StartConnection();

        SetState(LobbyState.Hosting);
        StartCoroutine(FetchPublicIPAndFireCode(privateServerPort));
    }

    public void UpdateMaxPlayers(int newMax)
    {
        _maxPlayers = Mathf.Max(0, newMax);
        OnPlayerCountChanged?.Invoke(InstanceFinder.ServerManager.Clients.Count, _maxPlayers);
    }

    public void SetMaxPlayers(int maxPlayers)
    {
        UpdateMaxPlayers(maxPlayers);
        if (PlayerInfoSync.Instance != null)
            PlayerInfoSync.Instance.SubmitMaxPlayersServerRpc(_maxPlayers);
    }

    /// <summary>
    /// Stores the local player's display name and forwards it to PlayerInfoSync if connected.
    /// Safe to call before connecting — PlayerInfoSync.OnStartClient() will re-submit it.
    /// </summary>
    public void SetUsername(string username)
    {
        LocalPlayer.Username = string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim();
        if (PlayerInfoSync.Instance != null)
            PlayerInfoSync.Instance.SubmitUsernameServerRpc(LocalPlayer.Username);
    }

    /// <summary>Join using a base-64 invite code from HostPrivateLobby.</summary>
    public void JoinByInviteCode(string code)
    {
        if (!TryDecodeInviteCode(code, out string ip, out ushort port))
        {
            OnConnectionFailed?.Invoke("Invalid invite code — double-check and try again.");
            return;
        }
        JoinByAddress(ip, port);
    }

    /// <summary>Join a server at an explicit IP and port.</summary>
    public void JoinByAddress(string ip, ushort port)
    {
        if (State != LobbyState.Disconnected) return;
        SetState(LobbyState.Connecting);
        StartCoroutine(ValidateAndConnect(ip, port, ConnectTarget.PrivateLobby));
    }

    /// <summary>
    /// Disconnect the client; also stops the server if this machine is hosting.
    /// Suppresses OnConnectionFailed — this is an intentional action.
    /// </summary>
    public void Disconnect()
    {
        _disconnectingIntentionally = true;
        StopAllCoroutines(); // Cancel any in-progress ValidateAndConnect

        if (InstanceFinder.IsClientStarted)
            InstanceFinder.ClientManager.StopConnection();

        if (_isHosting && InstanceFinder.IsServerStarted)
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionServer;
            InstanceFinder.ServerManager.StopConnection(sendDisconnectMessage: true);
        }

        _isHosting = false;
        SetState(LobbyState.Disconnected);
    }

    // -- Invite Code ------------------------------------------------------------

    /// <summary>
    /// Encodes an IP:port pair as a trimmed base-64 string.
    /// "192.168.1.5:7771" → "MTkyLjE2OC4xLjU6Nzc3MQ"
    /// </summary>
    public static string GenerateInviteCode(string ip, ushort port)
    {
        byte[] raw = Encoding.UTF8.GetBytes($"{ip}:{port}");
        return Convert.ToBase64String(raw).TrimEnd('=');
    }

    /// <summary>Decodes a code produced by GenerateInviteCode.  Returns false if malformed.</summary>
    public static bool TryDecodeInviteCode(string code, out string ip, out ushort port)
    {
        ip   = null;
        port = 0;
        try
        {
            string padded  = code.PadRight(code.Length + (4 - code.Length % 4) % 4, '=');
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            int    colon   = decoded.LastIndexOf(':');
            if (colon < 0) return false;
            ip   = decoded.Substring(0, colon);
            port = ushort.Parse(decoded.Substring(colon + 1));
            return true;
        }
        catch { return false; }
    }

    // -- Connection Validation --------------------------------------------------

    /// <summary>
    /// Validates the address/port before calling StartConnection, and sets up
    /// all diagnostic fields so ClassifyAndFireFailure can produce a precise message.
    /// </summary>
    private IEnumerator ValidateAndConnect(string address, ushort port, ConnectTarget target)
    {
        _connectTarget               = target;
        _lastAttemptedAddress        = address;
        _lastAttemptedPort           = port;
        _everReachedStarted          = false;
        _disconnectingIntentionally  = false;

        // -- 1. Port sanity check -----------------------------------------------
        if (port == 0)
        {
            Fail("Invalid port number. Port must be between 1 and 65535.");
            yield break;
        }

        // -- 2. DNS resolution (skipped for raw IPs) ----------------------------
        if (!IPAddress.TryParse(address, out _))
        {
            bool? resolved = null; // null = pending, true = success, false = failure

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try   { Dns.GetHostAddresses(address); resolved = true;  }
                catch { resolved = false; }
            });

            // Yield until the thread finishes or 5 s passes.
            float waited = 0f;
            while (resolved == null && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (resolved != true)
            {
                string dnsMsg = target == ConnectTarget.MainServer
                    ? $"Could not resolve the main server address \"{address}\". Check your internet connection."
                    : $"Unknown host — \"{address}\" could not be found. Check the address for typos.";
                Fail(dnsMsg);
                yield break;
            }
        }

        // -- 3. All checks passed — start the connection ------------------------
        _connectionStartTime = Time.time;
        SetTransport(address, port);
        InstanceFinder.ClientManager.StartConnection();
    }

    /// <summary>
    /// Called when a connection attempt ends in Stopped without being intentional.
    /// Uses the elapsed time and _everReachedStarted to pick the most accurate message.
    /// </summary>
    private void ClassifyAndFireFailure()
    {
        float elapsed = Time.time - _connectionStartTime;
        bool  isMain  = _connectTarget == ConnectTarget.MainServer;
        string at     = isMain ? "the main server" : $"{_lastAttemptedAddress}:{_lastAttemptedPort}";
        string msg;

        if (_everReachedStarted && Time.time - _connectedStartTime < kickWindowSeconds)
        {
            // Connected briefly then kicked — server enforced max players
            // (or some other pre-game rejection).
            msg = isMain
                ? "The main server is full or rejected the connection."
                : $"Server at {at} is full or rejected the connection.";
        }
        else if (elapsed >= connectionTimeoutSeconds - 0.5f)
        {
            // Waited the full timeout with no response — offline, wrong port, or NAT.
            msg = isMain
                ? "Main server timed out. The server may be down or overloaded — try again later."
                : $"Connection to {at} timed out. "
                + $"Confirm the server is running and that port {_lastAttemptedPort} is open (UDP).";
        }
        else
        {
            // Failed before the full timeout — unreachable at the OS/network level.
            msg = isMain
                ? "Could not reach the main server. Check your internet connection."
                : $"Host at {at} is unreachable. Check the address and that the server is online.";
        }

        OnConnectionFailed?.Invoke(msg);
    }

    /// <summary>Sets state to Disconnected and fires OnConnectionFailed with a pre-built message.</summary>
    private void Fail(string message)
    {
        SetState(LobbyState.Disconnected);
        OnConnectionFailed?.Invoke(message);
    }

    // -- Max Players ------------------------------------------------------------

    /// <summary>
    /// Server-side: runs only on the hosting machine.
    /// Enforces the max-player cap and fires OnPlayerCountChanged for the host UI.
    /// </summary>
    private void HandleRemoteConnectionServer(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // ServerManager.Clients already includes the newly connected client here.
            if (_maxPlayers > 0 && InstanceFinder.ServerManager.Clients.Count > _maxPlayers)
            {
                Debug.Log($"[LobbyManager] Server full ({_maxPlayers} max); kicking client {conn.ClientId}.");
                InstanceFinder.ServerManager.Kick(conn, KickReason.Unset);
                // A second OnRemoteConnectionState (Stopped) will follow and update the count.
                return;
            }
        }

        OnPlayerCountChanged?.Invoke(InstanceFinder.ServerManager.Clients.Count, _maxPlayers);
    }

    // -- Transport --------------------------------------------------------------

    private static void SetTransport(string address, ushort port)
    {
        Transport t = InstanceFinder.NetworkManager.TransportManager.Transport;
        t.SetClientAddress(address);
        SetPort(port, t);
    }

    private static void SetPort(ushort port)
        => SetPort(port, InstanceFinder.NetworkManager.TransportManager.Transport);

    private static void SetPort(ushort port, Transport transport)
    {
        // Tugboat exposes SetPort(ushort). Reflection avoids a compile-time transport dependency.
        var method = transport.GetType().GetMethod(
            "SetPort",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null, new[] { typeof(ushort) }, null);

        if (method != null)
            method.Invoke(transport, new object[] { port });
        else
            Debug.LogWarning($"[LobbyManager] Transport '{transport.GetType().Name}' has no "
                           + "SetPort(ushort) method — port will not be changed.");
    }

    private IEnumerator FetchPublicIPAndFireCode(ushort port)
    {
        using UnityWebRequest req = UnityWebRequest.Get("https://api.ipify.org");
        yield return req.SendWebRequest();
        bool ok = req.result == UnityWebRequest.Result.Success;
        if (!ok)
            Debug.LogWarning("[LobbyManager] Could not fetch public IP — invite code will use 127.0.0.1 (LAN only).");
        string ip = ok ? req.downloadHandler.text.Trim() : "127.0.0.1";
        OnInviteCodeReady?.Invoke(GenerateInviteCode(ip, port));
    }

    // -- Internal State ---------------------------------------------------------

    private void SetState(LobbyState state)
    {
        if (State == state) return;
        State = state;
        OnStateChanged?.Invoke(state);
    }

    // -- FishNet Callbacks ------------------------------------------------------

    private void HandleClientState(ClientConnectionStateArgs args)
    {
        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                _everReachedStarted = true;
                _connectedStartTime = Time.time;
                // Hosting stays in Hosting; a pure client moves to Connected.
                if (State == LobbyState.Connecting)
                    SetState(LobbyState.Connected);
                break;

            case LocalConnectionState.Stopped:
            {
                bool wasConnecting = State == LobbyState.Connecting;
                bool wasLive       = State == LobbyState.Connected || State == LobbyState.Hosting;

                // If the server is also gone (or we were never hosting), go fully Disconnected.
                if (!_isHosting || !InstanceFinder.IsServerStarted)
                {
                    _isHosting = false;
                    SetState(LobbyState.Disconnected);
                }

                if (!_disconnectingIntentionally)
                {
                    if (wasConnecting)
                        ClassifyAndFireFailure();
                    else if (wasLive)
                        OnConnectionFailed?.Invoke("Lost connection to the server.");
                }
                _disconnectingIntentionally = false;
                break;
            }
        }
    }

    private void HandleServerState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped && _isHosting)
            _isHosting = false; // Client Stopped will follow and call SetState(Disconnected)
    }
}