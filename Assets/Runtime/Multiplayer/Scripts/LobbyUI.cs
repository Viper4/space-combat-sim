using System;
using FishNet;
using FishNet.Managing.Scened;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin UI layer over LobbyManager.
///
/// Scene setup:
///   Create four child GameObjects under your canvas:
///     MainMenuPanel   — shown on startup
///     HostPanel       — shown after clicking "Host"
///     JoinPanel       — shown after clicking "Join"
///     ConnectingPanel — shown during a connection attempt
///
///   Wire every serialized reference in the Inspector.
///   The whole canvas GameObject is disabled once the player successfully
///   connects (or is kicked back to Disconnected from a live session).
/// </summary>
public class LobbyUI : MonoBehaviour
{
    // ── Panels ─────────────────────────────────────────────────────────────────

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject connectingPanel;
    [SerializeField] private GameObject connectedPanel;

    // ── Main Menu ──────────────────────────────────────────────────────────────

    [Header("Main Menu Buttons")]
    [SerializeField] private Button connectMainServerButton;
    [SerializeField] private Button hostPrivateLobbyButton;
    [SerializeField] private Button joinPrivateLobbyButton;

    // ── Host Panel ─────────────────────────────────────────────────────────────

    [Header("Host Panel")]
    [Tooltip("Displays the base-64 invite code once the public IP is fetched.")]
    [SerializeField] private TextMeshProUGUI inviteCodeLabel;
    [SerializeField] private Button          copyInviteCodeButton;
    [SerializeField] private Button          stopHostingButton;

    [Tooltip("Optional: clamp 1-64. Leave blank or 0 for unlimited.")]
    [SerializeField] private TMP_InputField  maxPlayerCountInput;

    [Tooltip("Only the host sees this; loads the game scene for all connected clients.")]
    [SerializeField] private Button          startGameButton;

    // ── Join Panel ─────────────────────────────────────────────────────────────

    [Header("Join Panel")]
    [Tooltip("Paste the base-64 invite code from the host here.")]
    [SerializeField] private TMP_InputField inviteCodeInput;
    [SerializeField] private Button         joinByCodeButton;

    [Tooltip("Direct IP connection — useful for LAN play.")]
    [SerializeField] private TMP_InputField directIPInput;
    [SerializeField] private TMP_InputField directPortInput;
    [SerializeField] private Button         joinByIPButton;
    [SerializeField] private Button         cancelJoinButton;

    // ── Connecting Panel ───────────────────────────────────────────────────────

    [Header("Connecting Panel")]
    [SerializeField] private Button cancelConnectButton;

    // ── Connected Panel ───────────────────────────────────────────────────────

    [Header("Connected Panel")]
    [SerializeField] private Button disconnectButton;

    // ── Shared ─────────────────────────────────────────────────────────────────

    [Header("Shared")]
    [Tooltip("Small status / error label visible on every panel.")]
    [SerializeField] private GameObject statusParent;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private TMP_InputField[] usernameInputs;
    [Tooltip("Shows 'X / Y players' or 'X players' if max is 0.")]
    [SerializeField] private TextMeshProUGUI[] playerCountLabels;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        LobbyManager lm = LobbyManager.Instance;
        lm.OnStateChanged       += HandleStateChanged;
        lm.OnConnectionFailed   += HandleConnectionFailed;
        lm.OnInviteCodeReady    += HandleInviteCodeReady;
        lm.OnPlayerCountChanged += HandlePlayerCountChanged;

        connectMainServerButton.onClick.AddListener(OnClickMainServer);
        hostPrivateLobbyButton .onClick.AddListener(OnClickHost);
        joinPrivateLobbyButton .onClick.AddListener(() => ShowPanel(joinPanel));
        copyInviteCodeButton   .onClick.AddListener(OnClickCopyCode);
        stopHostingButton      .onClick.AddListener(OnClickStopHosting);
        startGameButton        .onClick.AddListener(OnClickStartGame);
        joinByCodeButton       .onClick.AddListener(OnClickJoinByCode);
        joinByIPButton         .onClick.AddListener(OnClickJoinByIP);
        cancelJoinButton       .onClick.AddListener(() => ShowPanel(mainMenuPanel));
        cancelConnectButton    .onClick.AddListener(Disconnect);
        disconnectButton       .onClick.AddListener(Disconnect);

        maxPlayerCountInput.onEndEdit.AddListener(OnMaxPlayersEndEdit);
        for(int i = 0; i < usernameInputs.Length; i++)
        {
            usernameInputs[i].onEndEdit.AddListener(OnUsernameEndEdit);
        }

        ShowPanel(mainMenuPanel);
    }

    private void OnDisable()
    {
        if (LobbyManager.Instance == null) return;
        LobbyManager lm = LobbyManager.Instance;
        lm.OnStateChanged       -= HandleStateChanged;
        lm.OnConnectionFailed   -= HandleConnectionFailed;
        lm.OnInviteCodeReady    -= HandleInviteCodeReady;
        lm.OnPlayerCountChanged -= HandlePlayerCountChanged;

        connectMainServerButton.onClick.RemoveListener(OnClickMainServer);
        hostPrivateLobbyButton .onClick.RemoveListener(OnClickHost);
        copyInviteCodeButton   .onClick.RemoveListener(OnClickCopyCode);
        stopHostingButton      .onClick.RemoveListener(OnClickStopHosting);
        startGameButton        .onClick.RemoveListener(OnClickStartGame);
        joinByCodeButton       .onClick.RemoveListener(OnClickJoinByCode);
        joinByIPButton         .onClick.RemoveListener(OnClickJoinByIP);
        cancelConnectButton    .onClick.RemoveListener(Disconnect);
        disconnectButton       .onClick.RemoveListener(Disconnect);

        maxPlayerCountInput.onEndEdit.RemoveListener(OnMaxPlayersEndEdit);
    }

    // ── Button Handlers ────────────────────────────────────────────────────────

    private void OnClickMainServer()
    {
        SetStatus("Connecting to main server...");
        ShowPanel(connectingPanel);
        LobbyManager.Instance.ConnectToMainServer();
    }

    private void OnClickHost()
    {
        int maxPlayers = ParseMaxPlayers();

        inviteCodeLabel.text = "Fetching your public IP...";
        for (int i = 0; i < playerCountLabels.Length; i++)
        {
            playerCountLabels[i].text = FormatPlayerCount(0, maxPlayers);
        }
        ShowPanel(hostPanel);

        LobbyManager.Instance.HostPrivateLobby(maxPlayers);
    }

    private void OnClickCopyCode()
    {
        GUIUtility.systemCopyBuffer = inviteCodeLabel.text;
        SetStatus("Invite code copied to clipboard.");
    }

    private void OnClickStopHosting()
    {
        LobbyManager.Instance.Disconnect();
        ShowPanel(mainMenuPanel);
    }

    private void OnClickStartGame()
    {
        // Only the server/host can initiate global scene loads
        if (!InstanceFinder.IsServerStarted)
            return;

        // ReplaceScenes = All tells FishNet to unload all current global scenes
        // (StartScene) on every client as it loads MainScene — no manual unload needed.
        SceneLoadData sld = new SceneLoadData("MainScene")
        {
            ReplaceScenes = ReplaceOption.All
        };
        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }

    private void OnClickJoinByCode()
    {
        string code = inviteCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) { SetStatus("Paste an invite code first."); return; }
        SetStatus("Connecting...");
        ShowPanel(connectingPanel);
        LobbyManager.Instance.JoinByInviteCode(code);
    }

    private void OnClickJoinByIP()
    {
        string ip = directIPInput.text.Trim();
        if (string.IsNullOrEmpty(ip)) { SetStatus("Enter an IP address."); return; }
        ushort port = ushort.TryParse(directPortInput.text.Trim(), out ushort p) ? p : (ushort)7771;
        SetStatus($"Connecting to {ip}:{port}...");
        ShowPanel(connectingPanel);
        LobbyManager.Instance.JoinByAddress(ip, port);
    }

    private void Disconnect()
    {
        LobbyManager.Instance.Disconnect();
        ShowPanel(joinPanel);
        ShowStatus(false);
    }

    // ── Input Validation ──────────────────────────────────────────────────────

    private void OnMaxPlayersEndEdit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // Empty = unlimited; clear placeholder text
            maxPlayerCountInput.text = "";
            return;
        }

        if (!int.TryParse(value, out int parsed))
        {
            // Non-numeric: reset to empty (unlimited)
            maxPlayerCountInput.text = "";
            SetStatus("Max players must be a number (leave blank or 0 for unlimited).");
            return;
        }

        // Clamp to [1, 64]; show the corrected value so the player knows it was clamped.
        int clamped = Mathf.Clamp(parsed, 0, 64);
        if (clamped != parsed)
        {
            maxPlayerCountInput.text = clamped.ToString();
            SetStatus(parsed < 0
                ? "Minimum 1 player (0 for unlimited)."
                : "Maximum 64 players.");
        }
        else
        {
            ShowStatus(false);
        }
        LobbyManager.Instance.SetMaxPlayers(clamped);
    }

    private void OnUsernameEndEdit(string value)
    {
        LobbyManager.Instance.SetUsername(value);
    }

    // ── LobbyManager Event Handlers ───────────────────────────────────────────

    private void HandleStateChanged(LobbyManager.LobbyState state)
    {
        switch (state)
        {
            case LobbyManager.LobbyState.Disconnected:
                gameObject.SetActive(true); // Re-show if kicked from an active session
                ShowPanel(mainMenuPanel);
                break;
            case LobbyManager.LobbyState.Hosting:
                SetStatus("Hosting. Waiting for players...");
                break;
            case LobbyManager.LobbyState.Connected:
                // Server loads the game scene; disable the lobby canvas.
                gameObject.SetActive(false);
                break;
        }
    }

    private void HandleConnectionFailed(string reason)
    {
        ShowPanel(mainMenuPanel);
        SetStatus(reason);
    }

    private void HandleInviteCodeReady(string code)
    {
        inviteCodeLabel.text = code;
    }

    private void HandlePlayerCountChanged(int current, int max)
    {
        for (int i = 0; i < playerCountLabels.Length; i++)
        {
            playerCountLabels[i].text = FormatPlayerCount(current, max);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void ShowPanel(GameObject target)
    {
        mainMenuPanel  .SetActive(target == mainMenuPanel);
        hostPanel      .SetActive(target == hostPanel);
        joinPanel      .SetActive(target == joinPanel);
        connectingPanel.SetActive(target == connectingPanel);
        ShowStatus(false);
    }

    private void SetStatus(string msg)
    {
        if (statusLabel != null)
        {
            statusLabel.text = msg;
            ShowStatus(true);
        }
    }

    private void ShowStatus(bool active)
    {
        if (statusParent.activeSelf != active)
            statusParent.SetActive(active);
    }

    /// <summary>
    /// Reads and validates maxPlayerCountInput.
    /// Returns 0 (unlimited) if blank, and clamps to [0, 64] otherwise.
    /// </summary>
    private int ParseMaxPlayers()
    {
        string raw = maxPlayerCountInput.text.Trim();
        if (string.IsNullOrEmpty(raw))
            return 0;
        return int.TryParse(raw, out int v) ? Mathf.Clamp(v, 0, 64) : 0;
    }

    /// <summary>
    /// Formats the player-count label.
    /// max == 0 → "2 players" (unlimited)
    /// max > 0  → "2 / 8 players"
    /// </summary>
    private static string FormatPlayerCount(int current, int max) => max > 0 ? $"{current} / {max} players" : $"{current} players";
}