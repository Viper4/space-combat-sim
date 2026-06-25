using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    //public Dictionary<ulong, NetworkObject> trackedNetworkObjects = new Dictionary<ulong, NetworkObject>();

    public InputActions inputActions;
    public GameSettings gameSettings;
    public float sensitivityScale = 0.02f;
    public bool IsPaused;

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
            int volumeIndex = GetRangedSettingIndex("Master Volume");
            rangedSettingsActions[volumeIndex] += OnVolumeUpdated;
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
    }

    // public int RegisterSpaceLight(SpaceLight light)
    // {
    //     spaceLights.Add(light);
    //     return spaceLights.Count-1;
    // }

    // public void UpdateLightIntensity(int index)
    // {
    //     if (spaceLights == null || spaceLights.Count == 0)
    //         return;
    //     // Just use first index to reset highest rather than iterate in a update loop
    //     if (index == 0)
    //     {
    //         highestSpaceLightIntensity = spaceLights[index].intensity;
    //     }
    //     else
    //     {
    //         if (spaceLights[index].intensity > highestSpaceLightIntensity)
    //         {
    //             highestSpaceLightIntensity = spaceLights[index].intensity;
    //         }
    //     }
    // }

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

    private void OnVolumeUpdated()
    {
        AudioListener.volume = GetRangedSettingValue("Master Volume");
    }
}
