using System;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;

public class AlertSystem : NetworkBehaviour
{
    [SerializeField] private AudioSource alertAudioSource;
    [SerializeField] private AudioClip radarLockClip;
    [SerializeField] private AudioClip torpedoLockClip;
    [SerializeField] private AudioClip contactClip;
    [SerializeField] private AudioClip specialContactClip;

    [SerializeField] private GameObject overGPanel;
    [SerializeField] private GameObject radarLockPanel;
    [SerializeField] private GameObject torpedoLockPanel;
    [SerializeField] private GameObject fuelPanel;

    private int radarLocks = 0;
    private int torpedoLocks = 0;

    private bool IsOwnerOrOffline => IsOwner || IsOffline;
    private bool IsServerOrOffline => IsServerInitialized || IsOffline;


    [TargetRpc]
    private void NewContactTargetRpc(NetworkConnection conn)
    {
        alertAudioSource.PlayOneShot(contactClip);
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
            alertAudioSource.PlayOneShot(contactClip);
        }
        else
        {
            TrySendNewContactServerRpc(Owner);
        }
    }

    [TargetRpc]
    private void NewSpecialContactTargetRpc(NetworkConnection conn)
    {
        alertAudioSource.PlayOneShot(specialContactClip);
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
            alertAudioSource.PlayOneShot(specialContactClip);
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
            if (alertAudioSource.clip != radarLockClip)
                alertAudioSource.clip = radarLockClip;
            if (!alertAudioSource.isPlaying)
                alertAudioSource.Play();
            if (radarLockPanel != null)
                radarLockPanel.SetActive(true);
        }
        else if (radarLocks <= 0)
        {
            radarLocks = 0;
            if (torpedoLocks <= 0)
            {
                alertAudioSource.Stop();
            }
            else
            {
                alertAudioSource.clip = torpedoLockClip;
            }
            if (radarLockPanel != null)
                radarLockPanel.SetActive(false);
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

    private void UpdateTorpedoLock()
    {
        if (!IsOwnerOrOffline)
            return;
        if (torpedoLocks > 0)
        {
            if (alertAudioSource.clip != torpedoLockClip)
                alertAudioSource.clip = torpedoLockClip;
            if (!alertAudioSource.isPlaying)
                alertAudioSource.Play();
            if (torpedoLockPanel != null)
                torpedoLockPanel.SetActive(true);
        }
        else if (torpedoLocks <= 0)
        {
            torpedoLocks = 0;
            if (radarLocks <= 0)
            {
                alertAudioSource.Stop();
            }
            else
            {
                alertAudioSource.clip = radarLockClip;
            }
            if (torpedoLockPanel != null)
                torpedoLockPanel.SetActive(false);
        }
    }

    [TargetRpc]
    private void SetTorpedoLockTargetRpc(NetworkConnection conn, int amount)
    {
        this.torpedoLocks += amount;
        UpdateTorpedoLock();
    }

    [ServerRpc(RequireOwnership = false)]
    private void TrySendTorpedoLockServerRpc(NetworkConnection target, int amount)
    {
        // TODO: Validate request
        SetRadarLockTargetRpc(target, amount);
    }

    public void IncrementTorpedoLock(int amount)
    {
        torpedoLocks += amount;
        if (IsOwnerOrOffline)
        {
            UpdateTorpedoLock();
        }
        else
        {
            TrySendTorpedoLockServerRpc(Owner, amount);
        }
    }

    public void ToggleOverGAlert(bool value)
    {
        if (overGPanel != null && overGPanel.activeSelf != value)
            overGPanel.SetActive(value);
    }

    public void ToggleLowFuelAlert(bool value)
    {
        if (fuelPanel != null && fuelPanel.activeSelf != value)
            fuelPanel.SetActive(value);
    }
}
