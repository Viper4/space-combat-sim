using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// A single row in the keybind settings list.
/// 
/// Prefab layout (all children of this GameObject):
///   ┌─────────────────────────────────────────────────────┐
///   │  [ActionNameLabel]  [BindingLabel]  [Rebind] [Reset]│
///   └─────────────────────────────────────────────────────┘
///
/// Setup:
///   1. Create a prefab with this script on the root.
///   2. Add a horizontal layout group and wire the four references below.
///   3. Assign to PauseUI.keybindRowPrefab.
///
/// The row is initialized by PauseUI.BuildKeybindList() and talks back to
/// PauseUI when the player clicks Rebind or Reset.
/// </summary>
public class KeybindRow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI actionNameLabel;
    [SerializeField] private TextMeshProUGUI bindingLabel;
    [SerializeField] private Button          rebindButton;
    [SerializeField] private Button          resetButton;

    // ── State ──────────────────────────────────────────────────────────────────

    private InputAction _action;
    private int         _bindingIndex;
    private PauseUI     _owner;

    // ── Initialization ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called once by PauseUI after instantiation.
    /// </summary>
    public void Initialize(InputAction action, int bindingIndex, PauseUI owner)
    {
        _action       = action;
        _bindingIndex = bindingIndex;
        _owner        = owner;

        // Human-readable map prefix only when multiple maps are shown.
        actionNameLabel.text = FormatActionName(action, bindingIndex);

        rebindButton.onClick.AddListener(OnRebindClicked);
        resetButton .onClick.AddListener(OnResetClicked);

        Refresh();
    }

    // ── Public ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-reads the current effective binding path and updates the label.
    /// Called by PauseUI after a rebind or reset completes.
    /// </summary>
    public void Refresh()
    {
        bindingLabel.text = GetDisplayString();

        // Dim the reset button if there is no override to undo.
        bool hasOverride = (_action.bindings[_bindingIndex].overridePath != null);
        resetButton.interactable = hasOverride;
    }

    // ── Private ────────────────────────────────────────────────────────────────

    private void OnRebindClicked() => _owner.StartRebind(_action, _bindingIndex, this);
    private void OnResetClicked()  => _owner.ResetBinding(_action, _bindingIndex, this);

    private string GetDisplayString()
    {
        // InputBinding.ToDisplayString gives the human-readable key name
        // (e.g. "Space", "Left Ctrl", "Mouse Left") rather than the raw path.
        return _action.GetBindingDisplayString(_bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
    }

    /// <summary>
    /// Formats "Jump" or (if binding is a composite) "Move/Up" style names.
    /// </summary>
    private static string FormatActionName(InputAction action, int bindingIndex)
    {
        InputBinding binding = action.bindings[bindingIndex];
        if (binding.isComposite)
        {
            // Show "Move" for the composite header.
            return action.name;
        }
        return action.name;
    }
}