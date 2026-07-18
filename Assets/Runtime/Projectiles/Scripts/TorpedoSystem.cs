using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SpaceStuff;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet;

[RequireComponent(typeof(Ship))]
public class TorpedoSystem : NetworkBehaviour
{
    private Ship ship;
    private TargetingSystem targetingSystem;
    [SerializeField] private AudioManipulation launchAudio;

    [SerializeField] private TorpedoPoint[] torpedoPoints;
    [SerializeField] private Image[] torpedoIcons;
    [SerializeField] private Color baseUIColor;
    [SerializeField] private Animation bayDoorAnimation;
    [SerializeField] private float launchCooldown = 0.25f;
    private bool torpedoBayDoorOpen = false;
    private bool canLaunch = true;

    private Torpedo[] launchedTorpedoes;

    private bool IsOwnerOrOffline => IsOwner || IsOffline;

    private bool initialized = false;

    private void Start()
    {
        ship = GetComponent<Ship>();
        launchedTorpedoes = new Torpedo[torpedoPoints.Length];
        if (IsOffline)
            Init();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
            return;
        Init();
    }

    private void Init()
    {
        if (initialized)
            return;
        if (TryGetComponent(out targetingSystem))
            targetingSystem.OnTargetChange += SetTarget;
        GameManager.Instance.inputActions.Player.Secondary.performed += TryLaunchTorpedo;
        initialized = true;
    }

    private void OnDestroy()
    {
        if (targetingSystem != null)
            targetingSystem.OnTargetChange -= SetTarget;
        GameManager.Instance.inputActions.Player.Secondary.performed -= TryLaunchTorpedo;
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void ToggleBayDoorObserversRpc(bool open)
    {
        torpedoBayDoorOpen = open;
        if (open)
        {
            bayDoorAnimation.Play("OpenTorpedoBay");
        }
        else
        {
            bayDoorAnimation.Play("CloseTorpedoBay");
        }
    }

    [ServerRpc]
    private void ToggleBayDoorServerRpc(bool open)
    {
        ToggleBayDoorObserversRpc(open);
    }

    public void TorpedoBaySwitch(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        torpedoBayDoorOpen = state == 1;
        if (torpedoBayDoorOpen)
        {
            bayDoorAnimation.Play("OpenTorpedoBay");
        }
        else
        {
            bayDoorAnimation.Play("CloseTorpedoBay");
        }
        if (!IsOffline)
            ToggleBayDoorServerRpc(torpedoBayDoorOpen);
    }

    private void LaunchTorpedo(int i)
    {
        launchAudio.ResetPlay(true);

        if (IsOffline || IsServerInitialized)
            launchedTorpedoes[i] = torpedoPoints[i].LaunchTorpedo(ship.scaledRigidbody.scaledTransform, ship.scaledRigidbody.velocity, targetingSystem.lockedTarget, i, ship.radarTarget.team);
        UpdateTorpedoUI(i, false);
    }

    [ObserversRpc(ExcludeServer = true)]
    private void NonServerLaunchTorpedo(int i)
    {
        LaunchTorpedo(i);
    }

    [ServerRpc]
    private void LaunchTorpedoServerRpc()
    {
        if (!torpedoBayDoorOpen || !canLaunch)
            return;
        for (int i = 0; i < torpedoPoints.Length; i++)
        {
            if (torpedoPoints[i].hasTorpedo)
            {
                NonServerLaunchTorpedo(i);
                LaunchTorpedo(i);
                break;
            }
        }
        StartCoroutine(LaunchCooldown());
    }

    private void TryLaunchTorpedo(InputAction.CallbackContext context)
    {
        if (!torpedoBayDoorOpen || !canLaunch)
            return;
        if (IsOffline)
        {
            for (int i = 0; i < torpedoPoints.Length; i++)
            {
                if (torpedoPoints[i].hasTorpedo)
                {
                    LaunchTorpedo(i);
                    break;
                }
            }
            StartCoroutine(LaunchCooldown());
        }
        else if (IsOwner)
        {
            LaunchTorpedoServerRpc();
            if (!IsServerInitialized)
                StartCoroutine(LaunchCooldown());
        }
    }

    private IEnumerator LaunchCooldown()
    {
        canLaunch = false;
        yield return new WaitForSeconds(launchCooldown);
        canLaunch = true;
    }

    public void UpdateTorpedoUI(int torpedoIndex, bool active)
    {
        if (!IsOwnerOrOffline)
            return;
        torpedoIcons[torpedoIndex].color = active ? baseUIColor : Color.black;
    }

    [ServerRpc]
    private void SetTargetNullServerRpc()
    {
        for (int i = 0; i < launchedTorpedoes.Length; i++)
        {
            if (launchedTorpedoes[i] == null)
                continue;
            launchedTorpedoes[i].SetTarget(null);
        }
    }

    [ServerRpc]
    private void SetTargetServerRpc(int targetObjectId)
    {
        if (!ServerManager.Objects.Spawned.TryGetValue(targetObjectId, out NetworkObject targetNetObject))
        {
            Debug.LogWarning($"[TorpedoSystem] Server could not find target with net object ID: {targetObjectId}");
            return;
        }

        if (!targetNetObject.TryGetComponent<RadarTarget>(out var radarTarget))
        {
            Debug.LogWarning($"[TorpedoSystem] Server could not find RadarTarget component on net object ID: {targetObjectId}");
            return;
        }

        for (int i = 0; i < launchedTorpedoes.Length; i++)
        {
            if (launchedTorpedoes[i] == null)
                continue;
            launchedTorpedoes[i].SetTarget(radarTarget);
        }
    }

    private void SetTarget()
    {
        if (!IsOwnerOrOffline)
            return;
        
        if (IsOffline)
        {
            for (int i = 0; i < launchedTorpedoes.Length; i++)
            {
                if (launchedTorpedoes[i] == null)
                    continue;
                launchedTorpedoes[i].SetTarget(targetingSystem.lockedTarget);
            }
        }
        else if (IsOwner)
        {
            if (targetingSystem.lockedTarget == null)
            {
                SetTargetNullServerRpc();
            }
            else
            {
                if (!targetingSystem.lockedTarget.TryGetComponent<NetworkObject>(out var targetNetObject))
                {
                    Debug.LogWarning("[TorpedoSystem] Cannot send RPC to server as locked target does not have attached NetworkObject component.");
                    return;
                }
                SetTargetServerRpc(targetNetObject.ObjectId);
            }
        }
    }
}
