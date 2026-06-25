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
public class RangedSettingRow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Slider slider;
    [SerializeField] private Button resetButton;

    // ── State ──────────────────────────────────────────────────────────────────

    private string settingName;
    private float originalValue;
    private bool hasOverride;

    // ── Initialization ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called once by PauseUI after instantiation.
    /// </summary>
    public void Initialize(string settingName)
    {
        this.settingName = settingName;
        int index = GameManager.Instance.GetRangedSettingIndex(settingName);
        if (index < 0)
        {
            Debug.LogError($"Failed to initialize RangedSettingRow with key: '{settingName}'");
            return;
        }
        RangedSetting setting = GameManager.Instance.gameSettings.rangedSettings[index];
        originalValue = setting.value;

        slider.minValue = setting.min;
        slider.maxValue = setting.max;
        slider.value = setting.value;

        if (setting.integer)
        {
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            slider.wholeNumbers = true;
        }
        else
        {
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
            slider.wholeNumbers = false;
        }

        inputField.onEndEdit.AddListener(OnEndEdit);
        slider.onValueChanged.AddListener(OnSliderChanged);
        resetButton.onClick.AddListener(OnResetClicked);

        Refresh();
    }

    // ── Public ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-reads the current effective binding path and updates the label.
    /// Called by PauseUI after a rebind or reset completes.
    /// </summary>
    public void Refresh()
    {
        int index = GameManager.Instance.GetRangedSettingIndex(settingName);
        if (index < 0)
            return;
        RangedSetting setting = GameManager.Instance.gameSettings.rangedSettings[index];
        nameLabel.text = setting.name;
        if (setting.integer)
            inputField.SetTextWithoutNotify(setting.value.ToString("F0"));
        else
            inputField.SetTextWithoutNotify(setting.value.ToString("F2"));
        slider.SetValueWithoutNotify(setting.value);

        GameManager.Instance.InvokeRangedSettingAction(settingName);
        GameManager.Instance.SaveSettings();

        // Dim the reset button if there is no override to undo.
        resetButton.interactable = hasOverride;
    }

    // ── Private ────────────────────────────────────────────────────────────────
    
    private void OnEndEdit(string value)
    {
        int index = GameManager.Instance.GetRangedSettingIndex(settingName);
        if (index < 0)
            return;
        RangedSetting setting = GameManager.Instance.gameSettings.rangedSettings[index];
        if (float.TryParse(value, out float result))
        {
            float clamped = Mathf.Clamp(result, setting.min, setting.max);
            setting.value = clamped;
            GameManager.Instance.gameSettings.rangedSettings[index] = setting;
        }
        hasOverride = true;
        Refresh();
    }

    private void OnSliderChanged(float value)
    {
        int index = GameManager.Instance.GetRangedSettingIndex(settingName);
        if (index < 0)
            return;
        RangedSetting setting = GameManager.Instance.gameSettings.rangedSettings[index];
        setting.value = value;
        GameManager.Instance.gameSettings.rangedSettings[index] = setting;
        hasOverride = true;
        Refresh();
    }

    private void OnResetClicked()
    {
        int index = GameManager.Instance.GetRangedSettingIndex(settingName);
        if (index < 0)
            return;
        RangedSetting setting = GameManager.Instance.gameSettings.rangedSettings[index];
        setting.value = originalValue;
        GameManager.Instance.gameSettings.rangedSettings[index] = setting;
        hasOverride = false;
        Refresh();
    }
}