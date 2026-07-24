using System;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;

public class AlertSystem : MonoBehaviour
{
    [SerializeField] private Radar radar;

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

    private void Awake()
    {
        radar.OnRadarLockChange += UpdateRadarLock;
        radar.OnMissileLockChange += UpdateMissileLock;

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
        radar.OnRadarLockChange -= UpdateRadarLock;
        radar.OnMissileLockChange -= UpdateMissileLock;
        alertMap.Clear();
        for(int i = 0; i < alertButtons.Length; i++)
        {
            alertButtons[i].onClick.RemoveAllListeners();
        }
    }

    public void NewContact()
    {
        contactAudioSource.Stop();
        contactAudioSource.clip = contactClip;
        contactAudioSource.Play();
    }

    public void NewSpecialContact()
    {
        contactAudioSource.Stop();
        contactAudioSource.clip = specialContactClip;
        contactAudioSource.Play();
    }

    private void UpdateRadarLock()
    {
        if (radar.radarLocks > 0)
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
        else if (radar.radarLocks <= 0)
        {
            if (radar.missileLocks <= 0)
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

    private void UpdateMissileLock()
    {
        if (radar.missileLocks > 0)
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
        else if (radar.missileLocks <= 0)
        {
            if (radar.radarLocks <= 0)
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
