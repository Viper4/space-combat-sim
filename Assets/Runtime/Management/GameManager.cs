using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public InputActions inputActions;
    public GameSettings gameSettings;
    public float sensitivityScale = 0.02f;
    public bool IsPaused;
    public Volume globalVolume;
    public Vignette vignette;
    public ColorAdjustments colorAdjustments;
    public ScreenBlur screenBlur;
    public float volumeMultiplier = 1f;

    private Dictionary<string, int> rangedSettingMap = new Dictionary<string, int>();
    [SerializeField] private bool loadSettings = true;

    private Action[] rangedSettingsActions;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            inputActions = new InputActions();
            inputActions.Enable();
            SaveSystem.Init();

            if (loadSettings)
            {
                LoadSettings();
            }
            rangedSettingsActions = new Action[gameSettings.rangedSettings.Count];
            // int volumeIndex = GetRangedSettingIndex("Master Volume");
            // rangedSettingsActions[volumeIndex] += OnVolumeUpdated;
            globalVolume = GetComponentInChildren<Volume>();
            if(globalVolume != null)
            {
                globalVolume.profile.TryGet(out vignette);
                globalVolume.profile.TryGet(out colorAdjustments);
                globalVolume.profile.TryGet(out screenBlur);
            }
            Time.timeScale = 1f;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        inputActions?.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        if (inputActions.UI.Fullscreen.triggered)
        {
            Screen.fullScreen = !Screen.fullScreen;
            if (Screen.fullScreen)
            {
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            }
            else
            {
                Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
            }
        }

        AudioListener.volume = GetRangedSettingValue("Master Volume") * volumeMultiplier;
        Application.targetFrameRate = (int)GetRangedSettingValue("Target Frame Rate");
    }

    private void ClearActions()
    {
        for(int i = 0; i < rangedSettingsActions.Length; i++)
        {
            rangedSettingsActions[i] = null;
        }
    }

    public void AddRangedSettingListener(string settingName, Action listener)
    {
        if (rangedSettingMap.TryGetValue(settingName, out var index))
        {
            rangedSettingsActions[index] += listener;
        }
    }

    public void RemoveRangedSettingListener(string settingName, Action listener)
    {
        if (rangedSettingMap.TryGetValue(settingName, out var index))
        {
            rangedSettingsActions[index] -= listener;
        }
    }

    public void InvokeRangedSettingAction(string settingName)
    {
        if (rangedSettingMap.TryGetValue(settingName, out var index))
        {
            rangedSettingsActions[index]?.Invoke();
        }
    }

    public void SaveSettings()
    {
        string json = inputActions.asset.SaveBindingOverridesAsJson();
        gameSettings.bindingOverridesJson = json;
        SaveSystem.SaveGameSettings(gameSettings);
    }

    public void LoadSettings()
    {
        gameSettings = SaveSystem.LoadGameSettings();
        rangedSettingMap.Clear();
        for (int i = 0; i < gameSettings.rangedSettings.Count; i++)
        {
            rangedSettingMap.Add(gameSettings.rangedSettings[i].name, i);
        }
        if (!string.IsNullOrEmpty(gameSettings.bindingOverridesJson))
        {
            inputActions.asset.LoadBindingOverridesFromJson(gameSettings.bindingOverridesJson);
        }
    }

    public int GetRangedSettingIndex(string settingName)
    {
        if (rangedSettingMap.TryGetValue(settingName, out var index))
        {
            return index;
        }
        return -1;
    }

    public void SetRangedSetting(RangedSetting rangedSetting)
    {
        if (rangedSettingMap.TryGetValue(rangedSetting.name, out var index))
        {
            gameSettings.rangedSettings[index] = rangedSetting;
        }
    }

    public float GetRangedSettingValue(string settingName)
    {
        if (rangedSettingMap.TryGetValue(settingName, out var index))
        {
            return gameSettings.rangedSettings[index].value;
        }
        Debug.LogWarning($"Failed to get ranged setting value for: '{settingName}'");
        return 1.0f;
    }
}
