using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine.UI;
using TMPro;

public class Radar : NetworkBehaviour
{
    public bool IsEnabled {get; private set;} = false;
    public bool IsActive => emitLevel > 0;

    [SerializeField] private Ship ship;
    [SerializeField] private AlertSystem alertSystem;
    [SerializeField] private RadarUI radarUI;

    [SerializeField, Tooltip("Maximum detection range of active radar at each emission level.")] private float[] radarRanges;
    [SerializeField, Tooltip("The smallest radius active radar can detect at the current emission level's maximum range.")] private float[] minDetectRadii;
    public int emitLevel {get; private set;} = 0;
    private List<uint> validTargets = new List<uint>();
    [SerializeField] private ScaledCollider activeRadarTrigger;

    [Serializable]
    public struct ConfigSetting
    {
        public string tag;
        public Toggle detectToggle;
        public bool detectOn;
        
        public Toggle alertToggle;
        public bool alertOn;

        public Toggle killToggle;
        public bool killOn;

        public TMP_InputField killRadiusInput;
        public double killRadius;
    }

    [SerializeField, Tooltip("Initial settings for radar configurations.")] private ConfigSetting[] configInits;
    private Dictionary<string, ConfigSetting> radarConfigs = new Dictionary<string, ConfigSetting>();
    [SerializeField] private Color onColor = Color.green;
    [SerializeField] private Color offColor = Color.red;

    public int radarLocks {get; private set;} = 0;
    public int missileLocks {get; private set;} = 0;

    private bool IsOwnerOrOffline => IsOwner || IsOffline;
    public Action OnRadarLockChange;
    public Action OnMissileLockChange;
    
    private void Init()
    {
        if (ship != null)
        {
            ship.scaledRigidbody.OnScaledTriggerEnter += OnScaledTriggerEnter;
            ship.scaledRigidbody.OnScaledTriggerExit += OnScaledTriggerExit;
            ship.OnStartupEnd.AddListener(EnableRadar);
            ship.OnShutdownStart.AddListener(DisableRadar);
        }
        for(int i = 0; i < configInits.Length; i++)
        {
            string tag = configInits[i].tag;
            radarConfigs.Add(tag, configInits[i]);

            configInits[i].alertToggle.onValueChanged.AddListener((x) => ToggleAlert(tag, x));
            configInits[i].alertToggle.SetIsOnWithoutNotify(configInits[i].detectOn && configInits[i].alertOn);
            ToggleAlert(tag, configInits[i].alertOn);

            configInits[i].killToggle.onValueChanged.AddListener((x) => ToggleKill(tag, x));
            configInits[i].killToggle.SetIsOnWithoutNotify(configInits[i].detectOn && configInits[i].killOn);
            ToggleKill(tag, configInits[i].killOn);

            configInits[i].detectToggle.onValueChanged.AddListener((x) => ToggleDetect(tag, x));
            configInits[i].detectToggle.SetIsOnWithoutNotify(configInits[i].detectOn);
            ToggleDetect(tag, configInits[i].detectOn);

            configInits[i].killRadiusInput.onEndEdit.AddListener((x) =>
            {
                if (double.TryParse(x, out double newRadius))
                {
                    SetKillRadius(tag, newRadius);
                }
            });
            configInits[i].killRadiusInput.gameObject.SetActive(configInits[i].detectOn && configInits[i].killOn);
            configInits[i].killRadiusInput.SetTextWithoutNotify(configInits[i].killRadius.ToString());
        }
    }

    private void Start()
    {
        if (!IsOffline)
            return;
        Init();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
            return;
        Init();
    }

    private void OnDestroy()
    {
        if (ship != null)
        {
            ship.OnStartupEnd.RemoveListener(EnableRadar);
            ship.OnShutdownStart.RemoveListener(DisableRadar);
            for (int i = 0; i < configInits.Length; i++)
            {
                // Not good to remove all listeners, but we can probably assume radar being destroyed means the radar config UI gets destroyed too
                configInits[i].detectToggle.onValueChanged.RemoveAllListeners();

                configInits[i].alertToggle.onValueChanged.RemoveAllListeners();

                configInits[i].killToggle.onValueChanged.RemoveAllListeners();
                configInits[i].killRadiusInput.onEndEdit.RemoveAllListeners();
            }
        }
    }

    private void EnableRadar()
    {
        IsEnabled = true;
        if(radarUI != null)
            radarUI.SetActive(true);
        activeRadarTrigger.enabled = true;
        SetActiveEmissionLevel(emitLevel);
    }

    private void DisableRadar()
    {
        IsEnabled = false;
        if (radarUI != null)
            radarUI.SetActive(false);
        activeRadarTrigger.enabled = false;
        foreach(uint targetId in validTargets)
        {
            if (!RadarRegistry.TryGet(targetId, out var radarTarget))
                continue;
            radarTarget.radarIndex = -1;
            radarTarget.collidersInActiveRadar = 0;
            radarTarget.passivelyDetected = false;
            radarTarget.activelyDetected = false;
        }
        validTargets.Clear();
    }

    public void SetActiveEmissionLevel(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        emitLevel = state;
        activeRadarTrigger.SetRadius(radarRanges[emitLevel]);
        if (radarUI != null)
            radarUI.SetRange();
    }

    private void TryContactAlert(RadarTarget radarTarget)
    {
        if (alertSystem != null && radarConfigs.TryGetValue(radarTarget.tag, out var config) && config.alertOn)
        {
            if (radarTarget.alertWhenTargeting)
            {
                alertSystem.NewContact();
            }
            else
            {
                alertSystem.NewSpecialContact();
            }
        }
    }

    private void AddValidTarget(RadarTarget radarTarget)
    {
        if (radarTarget.GetID() == ship.attachedRadarTarget.GetID() || radarTarget.radarIndex >= 0)
            return;
        radarTarget.radarIndex = validTargets.Count;
        validTargets.Add(radarTarget.GetID());
    }

    private void SwapRemoveValidTargetAt(int index)
    {
        // Swap item at index with item and end of list then pop the list for fast removal
        int lastIndex = validTargets.Count - 1;
        if (index != lastIndex)
        {
            uint lastId = validTargets[lastIndex];
            validTargets[index] = lastId;
            
            if (RadarRegistry.TryGet(lastId, out var lastRadarTarget))
            {
                lastRadarTarget.radarIndex = index;
            }
            else
            {
                Debug.LogWarning($"[Radar] Could not find RadarTarget associated with ID: {lastId}.");
            }
        }
        validTargets.RemoveAt(lastIndex);
    }

    private void RemoveValidTarget(RadarTarget radarTarget)
    {
        int index = radarTarget.radarIndex;
        if (index < 0 || index >= validTargets.Count)
        {
            Debug.LogWarning(
                $"[Radar] Invalid radarIndex {index} for " +
                $"{radarTarget.name}. Searching validTargets for removal. List count: {validTargets.Count}");
            if (!validTargets.Remove(radarTarget.GetID()))
                Debug.LogWarning($"[Radar] Failed to remove {radarTarget.name} with radar ID: {radarTarget.GetID()}.");
            radarTarget.radarIndex = -1;
            radarTarget.passivelyDetected = false;
            radarTarget.activelyDetected = false;
            radarTarget.collidersInActiveRadar = 0;
            return;
        }
        SwapRemoveValidTargetAt(index);
        radarTarget.radarIndex = -1;
        radarTarget.passivelyDetected = false;
        radarTarget.activelyDetected = false;
        radarTarget.collidersInActiveRadar = 0;
    }

    public void AddPassiveTarget(RadarTarget radarTarget)
    {
        Debug.Log($"[Radar] Adding {radarTarget.name} as passively detected target.");
        if (!radarTarget.passivelyDetected)
        {
            radarTarget.passivelyDetected = true;
            TryContactAlert(radarTarget);
        }
        AddValidTarget(radarTarget);
    }

    public void RemovePassiveTarget(RadarTarget radarTarget)
    {
        Debug.Log($"[Radar] Removing {radarTarget.name} as passively detected target.");
        radarTarget.passivelyDetected = false;
        if (radarTarget.collidersInActiveRadar > 0)
            return; // Still being actively detected, dont remove it
        RemoveValidTarget(radarTarget);
    }

    [TargetRpc]
    private void AddPassiveTargetRpc(NetworkConnection conn, int objectId)
    {
        if (!ClientManager.Objects.Spawned.TryGetValue(objectId, out var networkObject))
            return;
        if (!networkObject.TryGetComponent<RadarTarget>(out var radarTarget))
            return;
        AddPassiveTarget(radarTarget);
    }

    [TargetRpc]
    private void RemovePassiveTargetRpc(NetworkConnection conn, int objectId)
    {
        if (!ClientManager.Objects.Spawned.TryGetValue(objectId, out var networkObject))
            return;
        if (!networkObject.TryGetComponent<RadarTarget>(out var radarTarget))
            return;
        RemovePassiveTarget(radarTarget);
    }

    public void StartPing(int sourceObjectId)
    {
        AddPassiveTargetRpc(Owner, sourceObjectId);
    }

    public void StopPing(int sourceObjectId)
    {
        RemovePassiveTargetRpc(Owner, sourceObjectId);
    }

    private void OnScaledTriggerEnter(ScaledCollider source, ScaledCollider other)
    {
        if (!IsOwnerOrOffline || source.id != activeRadarTrigger.id)
            return;
        if (!other.scaledRigidbody.TryGetComponent(out RadarTarget otherRadarTarget))
            return;

        otherRadarTarget.collidersInActiveRadar++;

        AddValidTarget(otherRadarTarget);

        // Add to the other radar's passive list
        if (otherRadarTarget.attachedRadar != null)
            otherRadarTarget.attachedRadar.StartPing(NetworkObject.ObjectId);
    }

    private void OnScaledTriggerExit(ScaledCollider source, ScaledCollider other)
    {
        if (!IsOwnerOrOffline || source.id != activeRadarTrigger.id)
            return;
        if (!other.scaledRigidbody.TryGetComponent(out RadarTarget otherRadarTarget))
            return;
        
        otherRadarTarget.collidersInActiveRadar--;
        if (otherRadarTarget.collidersInActiveRadar > 0)
            return;
        otherRadarTarget.collidersInActiveRadar = 0;
        
        RemoveValidTarget(otherRadarTarget);

        // Remove from the other radar's passive detections
        if (otherRadarTarget.attachedRadar != null)
            otherRadarTarget.attachedRadar.StopPing(NetworkObject.ObjectId);
    }

    public bool IsDetectOn(string tag)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return false;
        return config.detectOn;
    }

    private void ToggleDetect(string tag, bool value)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return;
        config.detectOn = value;
        Transform frontElement = config.detectToggle.transform.GetChild(2);
        frontElement.GetComponent<Image>().color = value ? onColor : offColor;
        frontElement.GetChild(0).GetComponent<TextMeshProUGUI>().text = value ? "ON" : "OFF";
        radarConfigs[tag] = config;

        config.alertToggle.gameObject.SetActive(value);
        config.killToggle.gameObject.SetActive(value);
        config.killRadiusInput.gameObject.SetActive(value && config.killOn);
    }

    public bool IsAlertOn(string tag)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return false;
        return config.alertOn;
    }

    private void ToggleAlert(string tag, bool value)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return;
        config.alertOn = value;
        Transform frontElement = config.alertToggle.transform.GetChild(2);
        frontElement.GetComponent<Image>().color = value ? onColor : offColor;
        frontElement.GetChild(0).GetComponent<TextMeshProUGUI>().text = value ? "ON" : "OFF";
        radarConfigs[tag] = config;
    }

    public bool IsKillOn(string tag)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return false;
        return config.killOn;
    }

    private void ToggleKill(string tag, bool value)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return;
        config.killOn = value;
        Transform frontElement = config.killToggle.transform.GetChild(2);
        frontElement.GetComponent<Image>().color = value ? onColor : offColor;
        frontElement.GetChild(0).GetComponent<TextMeshProUGUI>().text = value ? "ON" : "OFF";
        radarConfigs[tag] = config;
        config.killRadiusInput.gameObject.SetActive(value);
    }

    public double GetKillRadius(string tag, double defaultValue = -1.0)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return defaultValue;
        return config.killRadius;
    }

    public void SetKillRadius(string tag, double radius)
    {
        if (!radarConfigs.TryGetValue(tag, out var config))
            return;
        config.killRadius = radius;
        radarConfigs[tag] = config;
    }

    public float GetCurrentRange()
    {
        return radarRanges[emitLevel];
    }

    [TargetRpc]
    private void SetRadarLockTargetRpc(NetworkConnection conn, int amount)
    {
        this.radarLocks += amount;
        OnRadarLockChange?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    private void TrySendRadarLockServerRpc(NetworkConnection target, int amount)
    {
        // TODO: Validate request
        SetRadarLockTargetRpc(target, amount);
    }

    public void IncrementRadarLock(int amount)
    {
        radarLocks += amount;
        if (IsOwnerOrOffline)
        {
            OnRadarLockChange?.Invoke();
        }
        else
        {
            TrySendRadarLockServerRpc(Owner, amount);
        }
    }

    [TargetRpc]
    private void SetMissileLockTargetRpc(NetworkConnection conn, int amount)
    {
        this.missileLocks += amount;
        OnMissileLockChange?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    private void TrySendMissileLockServerRpc(NetworkConnection target, int amount)
    {
        // TODO: Validate request
        SetMissileLockTargetRpc(target, amount);
    }

    public void IncrementMissileLock(int amount)
    {
        missileLocks += amount;
        if (IsOwnerOrOffline)
        {
            OnMissileLockChange?.Invoke();
        }
        else
        {
            TrySendMissileLockServerRpc(Owner, amount);
        }
    }

    private bool CanDetectTarget(RadarTarget radarTarget)
    {
        if (radarTarget.passivelyDetected)
            return true;
        if (!IsActive || radarTarget.collidersInActiveRadar <= 0)
            return false;
        Vector3d relativePosition = radarTarget.scaledRigidbody.scaledTransform.realPosition - ship.scaledRigidbody.scaledTransform.realPosition;
        double sqrDistance = relativePosition.sqrMagnitude;

        double maxRange = radarRanges[emitLevel];
        double minRadiusAtMaxRange = minDetectRadii[emitLevel];

        // Inverse square law falloff
        double minimumDetectableRadius = minRadiusAtMaxRange * sqrDistance / (maxRange * maxRange);
        double effectiveRadius = radarTarget.GetEffectiveRadarRadius();
        if (effectiveRadius < minimumDetectableRadius)
        {
            radarTarget.activelyDetected = false;
            return false;
        }
        if (!radarTarget.activelyDetected)
        {
            radarTarget.activelyDetected = true;
            TryContactAlert(radarTarget);
        }
        return true;
    }

    public IEnumerable<RadarTarget> GetAllDetectedTargets()
    {
        for (int i = validTargets.Count - 1; i >= 0; i--)
        {
            uint targetId = validTargets[i];
            if (!RadarRegistry.TryGet(targetId, out var radarTarget))
            {
                SwapRemoveValidTargetAt(i);
                continue;
            }
            if (radarConfigs.TryGetValue(radarTarget.tag, out var config) && !config.detectOn)
                continue;

            if (!CanDetectTarget(radarTarget))
                continue;
            yield return radarTarget;
        }
    }
}
