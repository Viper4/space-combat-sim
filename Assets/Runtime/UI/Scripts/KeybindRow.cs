using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Represents a single editable binding.
///
/// Examples:
///
/// Move      Up        W
///           Down      S
///           Left      A
///           Right     D
///
/// Jump                 Space
///
/// Fire                 Left Mouse
/// </summary>
public class KeybindRow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI actionNameLabel;
    [SerializeField] private TextMeshProUGUI bindingNameLabel;
    [SerializeField] private TextMeshProUGUI bindingLabel;

    [SerializeField] private Button rebindButton;
    [SerializeField] private Button resetButton;

    private InputAction action;
    private int bindingIndex;
    private PauseUI owner;

    public void Initialize(InputAction inputAction, int inputBindingIndex, string bindingName, string actionName, PauseUI pauseUI)
    {
        action = inputAction;
        bindingIndex = inputBindingIndex;
        owner = pauseUI;

        actionNameLabel.text = actionName;
        bindingNameLabel.text = bindingName;

        rebindButton.onClick.RemoveAllListeners();
        resetButton.onClick.RemoveAllListeners();

        rebindButton.onClick.AddListener(OnRebindClicked);
        resetButton.onClick.AddListener(OnResetClicked);

        Refresh();
    }

    public void Refresh()
    {
        bindingLabel.text = action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);

        resetButton.interactable = !string.IsNullOrEmpty(action.bindings[bindingIndex].overridePath);
    }

    private void OnRebindClicked()
    {
        owner.StartRebind(action, bindingIndex, this);
    }

    public void OnResetClicked()
    {
        owner.ResetBinding(action, bindingIndex, this);
    }
}