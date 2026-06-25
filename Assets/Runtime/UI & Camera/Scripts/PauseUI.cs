using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using FishNet;

/// <summary>
/// Pause menu activated by the UI/Pause input action.
/// Panels: Root → Main | Settings → KeybindList
///
/// Setup:
///   1. Attach to a persistent canvas (or a canvas that lives in your game scene).
///   2. Wire every serialised reference in the Inspector.
///   3. Ensure GameManager.Instance.inputActions is a generated C# InputActionAsset.
///
/// Keybind rebinding:
///   - SettingsPanel contains a ScrollRect whose Content is populated at runtime
///     with one KeybindRow prefab per action in the rebindable action maps.
///   - Rebinding uses InputAction.PerformInteractiveRebinding() and persists
///     overrides to PlayerPrefs via SaveBindingOverrides / LoadBindingOverrides.
///
/// Leave button visibility:
///   - Shown only when LobbyManager.Instance.State is Connected or Hosting.
///   - Pressing it calls LobbyManager.Instance.Disconnect() then hides the pause menu.
/// </summary>
public class PauseUI : MonoBehaviour
{
    public static PauseUI Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Root Canvas / Panel")]
    [Tooltip("The top-level GameObject that wraps the entire pause menu. Toggled on/off.")]
    [SerializeField] private GameObject pauseRoot;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Panel Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button leaveButton;         // Hidden when not in a lobby
    [SerializeField] private Button exitDesktopButton;

    [Header("Settings Panel")]
    [SerializeField] private Button settingsBackButton;
    [Tooltip("RectTransform that is the Content child of the ScrollRect in the settings panel.")]
    [SerializeField] private RectTransform keybindListContent;
    [SerializeField] private RangedSettingRow rangedSettingRowPrefab;
    [Tooltip("Prefab with: action-name label (TMP), binding label (TMP), rebind button, reset button.")]
    [SerializeField] private KeybindRow keybindRowPrefab;

    [Header("Rebinding")]
    [Tooltip("Action-map names whose actions will appear in the rebind list. Order is preserved.")]
    [SerializeField] private string[] rebindableMapNames = { "Player", "UI" };
    [Tooltip("Actions to skip even if they are in a rebindable map (e.g. internal composites).")]
    [SerializeField] private string[] excludedActionNames = { "Point", "ScrollWheel", "Click", "MiddleClick", "RightClick", "TrackedDeviceOrientation", "TrackedDevicePosition" };

    [Header("Overlay")]
    [Tooltip("Semi-transparent panel shown while waiting for a key press during rebinding.")]
    [SerializeField] private GameObject rebindOverlay;
    [SerializeField] private TextMeshProUGUI rebindOverlayLabel;

    // ── Private ────────────────────────────────────────────────────────────────

    private InputActionRebindingExtensions.RebindingOperation _currentRebind;
    private readonly List<GameObject> _spawnedRows = new();

    private CursorLockMode originalLockState;
    private bool originalCursorVisibility;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        pauseRoot.SetActive(false);
        rebindOverlay.SetActive(false);

        resumeButton     .onClick.AddListener(Resume);
        settingsButton   .onClick.AddListener(OpenSettings);
        leaveButton      .onClick.AddListener(Leave);
        exitDesktopButton.onClick.AddListener(ExitDesktop);
        settingsBackButton.onClick.AddListener(CloseSettings);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        _currentRebind?.Dispose();
    }

    private void Update()
    {
        // Guard: only poll after GameManager is ready.
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.inputActions.UI.Pause.WasPressedThisFrame())
            TogglePause();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void TogglePause()
    {
        if (GameManager.Instance.IsPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (GameManager.Instance.IsPaused)
            return;
        GameManager.Instance.IsPaused = true;

        originalLockState = Cursor.lockState;
        originalCursorVisibility = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshLeaveButtonVisibility();
        ShowPanel(mainPanel);
        pauseRoot.SetActive(true);

        if (InstanceFinder.NetworkManager.IsOffline)
            Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!GameManager.Instance.IsPaused)
            return;
        GameManager.Instance.IsPaused = false;
        Cursor.lockState = originalLockState;
        Cursor.visible = originalCursorVisibility;

        // Cancel any in-progress rebind so we don't leave listeners dangling.
        CancelCurrentRebind();

        pauseRoot.SetActive(false);

        if (InstanceFinder.NetworkManager.IsOffline)
            Time.timeScale = 1f;
    }

    // ── Button Handlers ────────────────────────────────────────────────────────

    private void OpenSettings()
    {
        BuildKeybindList();
        ShowPanel(settingsPanel);
    }

    private void CloseSettings()
    {
        CancelCurrentRebind();
        ShowPanel(mainPanel);
    }

    private void Leave()
    {
        Resume();
        LobbyManager.Instance.Disconnect();
    }

    private void ExitDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Leave Button ───────────────────────────────────────────────────────────

    private void RefreshLeaveButtonVisibility()
    {
        if (LobbyManager.Instance == null)
        {
            leaveButton.gameObject.SetActive(false);
            return;
        }
        var s = LobbyManager.Instance.State;
        leaveButton.gameObject.SetActive(
            s == LobbyManager.LobbyState.Connected ||
            s == LobbyManager.LobbyState.Hosting);
    }

    // ── Panel Helpers ──────────────────────────────────────────────────────────

    private void ShowPanel(GameObject target)
    {
        mainPanel    .SetActive(target == mainPanel);
        settingsPanel.SetActive(target == settingsPanel);
    }

    // ── Keybind List ───────────────────────────────────────────────────────────

    private void BuildKeybindList()
    {
        // Destroy stale rows from a previous open.
        foreach (var row in _spawnedRows)
            Destroy(row);
        _spawnedRows.Clear();

        InputActionAsset asset = GameManager.Instance.inputActions.asset;
        var excluded = new HashSet<string>(excludedActionNames);

        foreach (RangedSetting setting in GameManager.Instance.gameSettings.rangedSettings)
        {
            RangedSettingRow row = Instantiate(rangedSettingRowPrefab, keybindListContent);
            row.Initialize(setting.name);
            _spawnedRows.Add(row.gameObject);
        }

        foreach (string mapName in rebindableMapNames)
        {
            InputActionMap map = asset.FindActionMap(mapName, throwIfNotFound: false);
            if (map == null) continue;

            foreach (InputAction action in map.actions)
            {
                if (excluded.Contains(action.name)) continue;

                // One row per binding (skip composites parts; show the composite itself).
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];

                    // Skip composite parts — show only the composite header.
                    if (binding.isPartOfComposite) continue;

                    KeybindRow row = Instantiate(keybindRowPrefab, keybindListContent);
                    row.Initialize(action, i, this);
                    _spawnedRows.Add(row.gameObject);
                }
            }
        }
    }

    // ── Rebinding ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by a KeybindRow when the player clicks its Rebind button.
    /// </summary>
    public void StartRebind(InputAction action, int bindingIndex, KeybindRow row)
    {
        CancelCurrentRebind();

        // Disable the action map so the action itself doesn't fire during rebind.
        action.actionMap.Disable();

        ShowRebindOverlay(action.name);

        _currentRebind = action
            .PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op  => FinishRebind(op, action, row, cancelled: false))
            .OnCancel (op  => FinishRebind(op, action, row, cancelled: true))
            .Start();
    }

    private void FinishRebind(
        InputActionRebindingExtensions.RebindingOperation op,
        InputAction action,
        KeybindRow row,
        bool cancelled)
    {
        op.Dispose();
        _currentRebind = null;

        action.actionMap.Enable();
        HideRebindOverlay();

        if (!cancelled)
            GameManager.Instance.SaveSettings();

        row.Refresh();
    }

    /// <summary>Reset a single binding to its default and save.</summary>
    public void ResetBinding(InputAction action, int bindingIndex, KeybindRow row)
    {
        CancelCurrentRebind();
        action.RemoveBindingOverride(bindingIndex);
        GameManager.Instance.SaveSettings();
        row.Refresh();
    }

    private void CancelCurrentRebind()
    {
        if (_currentRebind == null) return;
        _currentRebind.Cancel();   // fires OnCancel → FinishRebind
        // FinishRebind disposes and nulls _currentRebind.
    }

    private void ShowRebindOverlay(string actionName)
    {
        rebindOverlay.SetActive(true);
        if (rebindOverlayLabel != null)
            rebindOverlayLabel.text = $"Press a key to bind\n<b>{actionName}</b>\n\n<size=70%>Escape to cancel</size>";
    }

    private void HideRebindOverlay() => rebindOverlay.SetActive(false);
}