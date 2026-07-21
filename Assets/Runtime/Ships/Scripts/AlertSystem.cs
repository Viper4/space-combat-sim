using System;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;

public class AlertSystem : NetworkBehaviour
{
    [SerializeField] private AudioSource lockAudioSource;
    [SerializeField] private AudioSource contactAudioSource;
    [SerializeField] private AudioClip radarLockClip;
    [SerializeField] private AudioClip missileLockClip;
    [SerializeField] private AudioClip contactClip;
    [SerializeField] private AudioClip specialContactClip;

    [SerializeField] private Button[] alertButtons;
    private bool[] alertMutes;
    private Dictionary<string, int> alertMap = new Dictionary<string, int>();

    [SerializeField] private Color unmutedColor = Color.red;
    [SerializeField] private Color mutedColor = Color.softRed;

    private int radarLocks = 0;
    private int missileLocks = 0;

    private bool IsOwnerOrOffline => IsOwner || IsOffline;

    private void Awake()
    {
        alertMutes = new bool[alertButtons.Length];
        foreach (Button button in alertButtons)
        {
            int index = Array.IndexOf(alertButtons, button);
            alertMap.Add(button.name, index);
            button.onClick.AddListener(() => OnClickAlertButton(index));
        }
    }

    private void OnDestroy()
    {
        alertMap.Clear();
        for(int i = 0; i < alertButtons.Length; i++)
        {
            alertButtons[i].onClick.RemoveAllListeners();
        }
    }

    [TargetRpc]
    private void NewContactTargetRpc(NetworkConnection conn)
    {
        contactAudioSource.Stop();
        contactAudioSource.clip = contactClip;
        contactAudioSource.Play();
    }

    [ServerRpc(RequireOwnership = false)]
    private void TrySendNewContactServerRpc(NetworkConnection target)
    {
        // TODO: Validate request
        NewContactTargetRpc(target);
    }

    public void NewContact()
    {
        if (IsOwnerOrOffline)
        {
            contactAudioSource.Stop();
            contactAudioSource.clip = contactClip;
            contactAudioSource.Play();
        }
        else
        {
            TrySendNewContactServerRpc(Owner);
        }
    }

    [TargetRpc]
    private void NewSpecialContactTargetRpc(NetworkConnection conn)
    {
        contactAudioSource.Stop();
        contactAudioSource.clip = specialContactClip;
        contactAudioSource.Play();
    }

    [ServerRpc(RequireOwnership = false)]
    private void TrySendNewSpecialContactServerRpc(NetworkConnection target)
    {
        // TODO: Validate request
        NewSpecialContactTargetRpc(target);
    }

    public void NewSpecialContact()
    {
        if (IsOwnerOrOffline)
        {
            contactAudioSource.Stop();
            contactAudioSource.clip = specialContactClip;
            contactAudioSource.Play();
        }
        else
        {
            TrySendNewSpecialContactServerRpc(Owner);
        }
    }

    private void UpdateRadarLock()
    {
        if (!IsOwnerOrOffline)
            return;
        if (radarLocks > 0)
        {
            if (lockAudioSource.clip != radarLockClip)
                lockAudioSource.clip = radarLockClip;
            if (alertMap.TryGetValue("Radar Lock", out int index))
            {
                alertButtons[index].gameObject.SetActive(true);
                lockAudioSource.mute = alertMutes[index];
            }
            if (!lockAudioSource.mute && !lockAudioSource.isPlaying)
                lockAudioSource.Play();
        }
        else if (radarLocks <= 0)
        {
            radarLocks = 0;
            if (missileLocks <= 0)
            {
                lockAudioSource.Stop();
            }
            else
            {
                UpdateMissileLock(); // Shouldn't cause stack overflow since it will take the >0 path
            }
            if (alertMap.TryGetValue("Radar Lock", out int index))
            {
                alertButtons[index].gameObject.SetActive(false);
            }
        }
    }

    [TargetRpc]
    private void SetRadarLockTargetRpc(NetworkConnection conn, int amount)
    {
        this.radarLocks += amount;
        UpdateRadarLock();
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
            UpdateRadarLock();
        }
        else
        {
            TrySendRadarLockServerRpc(Owner, amount);
        }
    }

    private void UpdateMissileLock()
    {
        if (!IsOwnerOrOffline)
            return;
        if (missileLocks > 0)
        {
            if (lockAudioSource.clip != missileLockClip)
                lockAudioSource.clip = missileLockClip;
            if (alertMap.TryGetValue("Missile Lock", out int index))
            {
                alertButtons[index].gameObject.SetActive(true);
                lockAudioSource.mute = alertMutes[index];
            }
            if (!lockAudioSource.mute && !lockAudioSource.isPlaying)
                lockAudioSource.Play();
        }
        else if (missileLocks <= 0)
        {
            missileLocks = 0;
            if (radarLocks <= 0)
            {
                lockAudioSource.Stop();
            }
            else
            {
                UpdateRadarLock(); // Shouldn't cause stack overflow since it will take the >0 path
            }
            if (alertMap.TryGetValue("Missile Lock", out int index))
            {
                alertButtons[index].gameObject.SetActive(false);
            }
        }
    }

    [TargetRpc]
    private void SetMissileLockTargetRpc(NetworkConnection conn, int amount)
    {
        this.missileLocks += amount;
        UpdateMissileLock();
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
            UpdateMissileLock();
        }
        else
        {
            TrySendMissileLockServerRpc(Owner, amount);
        }
    }

    public void SetAlert(string name, bool value)
    {
        if (alertMap.TryGetValue(name, out int index))
        {
            if (alertButtons[index].gameObject.activeSelf != value)
            {
                alertButtons[index].gameObject.SetActive(value);
                if (alertButtons[index].TryGetComponent<AudioSource>(out var alertAudioSource))
                {
                    if (alertAudioSource.isActiveAndEnabled && !alertAudioSource.isPlaying)
                    {
                        alertAudioSource.Play();
                    }
                }
            }
        }
    }

    private void OnClickAlertButton(int index)
    {
        if (index < 0 || index >= alertButtons.Length)
            return;
        alertMutes[index] = !alertMutes[index];
        alertButtons[index].image.color = alertMutes[index] ? mutedColor : unmutedColor;
        switch (alertButtons[index].name)
        {
            case "Radar Lock":
                lockAudioSource.mute = alertMutes[index];
                break;
            case "Missile Lock":
                lockAudioSource.mute = alertMutes[index];
                break;
            default:
                if (alertButtons[index].TryGetComponent<AudioSource>(out var alertAudioSource))
                {
                    alertAudioSource.mute = alertMutes[index];
                }
                break;
        }
    }
}
