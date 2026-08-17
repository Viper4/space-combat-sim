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
        if (!IsOwnerOrOffline || !ship.isStarted)
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

    private void LaunchTorpedo(int i, RadarTarget lockedTarget)
    {
        launchAudio.ResetPlay(true);

        if (IsOffline || IsServerInitialized)
            launchedTorpedoes[i] = torpedoPoints[i].LaunchTorpedo(ship.scaledRigidbody.scaledTransform, ship.scaledRigidbody.velocity, ship.scaledRigidbody.angularVelocity, lockedTarget, i, ship.radarTarget.team);
        UpdateTorpedoUI(i, false);
        if (lockedTarget == null)
            Debug.Log(GameLog.ObjectLog(this, "Launched torpedo with no target."));
        else
            Debug.Log(GameLog.ObjectLog(this, $"Launched torpedo at {lockedTarget.name}."));
    }

    private void LaunchTorpedo(int i, int targetObjectId)
    {
        if (targetObjectId == -1)
        {
            LaunchTorpedo(i, null);
            return;
        }
        NetworkObject networkObject;
        if (IsClientInitialized)
        {
            if (!ClientManager.Objects.Spawned.TryGetValue(targetObjectId, out networkObject))
            {
                Debug.LogWarning(GameLog.NetworkObjectNotFound(this, $"LaunchTorpedo at {targetObjectId}", targetObjectId));
                LaunchTorpedo(i, null);
                return;
            }
        }
        else if (IsServerInitialized)
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(targetObjectId, out networkObject))
            {
                Debug.LogWarning(GameLog.NetworkObjectNotFound(this, $"LaunchTorpedo at {targetObjectId}", targetObjectId));
                LaunchTorpedo(i, null);
                return;
            }
        }
        else
        {
            Debug.LogWarning(GameLog.ObjectLog(this, "Cannot launch torpedo using network object ID as neither client nor server are initialized."));
            return;
        }
        
        if (!networkObject.TryGetComponent<RadarTarget>(out var radarTarget))
        {
            Debug.LogWarning(GameLog.ComponentNotFound(this, $"LaunchTorpedo at {targetObjectId} ({networkObject.name})", networkObject, "RadarTarget"));
            LaunchTorpedo(i, null);
            return;
        }
        LaunchTorpedo(i, radarTarget);
    }

    [ObserversRpc(ExcludeServer = true)]
    private void LaunchTorpedoObserversRpc(int i, int targetObjectId)
    {
        LaunchTorpedo(i, targetObjectId);
    }

    [ServerRpc]
    private void LaunchTorpedoServerRpc(int targetObjectId)
    {
        if (!torpedoBayDoorOpen || !canLaunch)
            return;
        for (int i = 0; i < torpedoPoints.Length; i++)
        {
            if (torpedoPoints[i].hasTorpedo)
            {
                LaunchTorpedoObserversRpc(i, targetObjectId);
                LaunchTorpedo(i, targetObjectId);
                break;
            }
        }
        StartCoroutine(LaunchCooldown());
    }

    private void TryLaunchTorpedo(InputAction.CallbackContext context)
    {
        if (!torpedoBayDoorOpen || !canLaunch || !ship.isStarted)
            return;
        if (IsOffline)
        {
            for (int i = 0; i < torpedoPoints.Length; i++)
            {
                if (torpedoPoints[i].hasTorpedo)
                {
                    LaunchTorpedo(i, targetingSystem.lockedTarget);
                    break;
                }
            }
            StartCoroutine(LaunchCooldown());
        }
        else if (IsOwner)
        {
            if (targetingSystem.lockedTarget == null)
            {
                LaunchTorpedoServerRpc(-1);
            }
            else if (!targetingSystem.lockedTarget.TryGetComponent<NetworkObject>(out var targetNetworkObject))
            {
                Debug.LogWarning(GameLog.ComponentNotFound(this, "TryLaunchTorpedo at lockedTarget", targetingSystem.lockedTarget, "NetworkObject"));
                LaunchTorpedoServerRpc(-1);
            }
            else
            {
                LaunchTorpedoServerRpc(targetNetworkObject.ObjectId);
            }
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
            Debug.LogWarning(GameLog.NetworkObjectNotFound(this, "SetTargetServerRpc", targetObjectId));
            return;
        }

        if (!targetNetObject.TryGetComponent<RadarTarget>(out var radarTarget))
        {
            Debug.LogWarning(GameLog.ComponentNotFound(this, "SetTargetServerRpc", targetNetObject, "RadarTarget"));
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
                    Debug.LogWarning(GameLog.ComponentNotFound(this, "SetTarget to lockedTarget", targetingSystem.lockedTarget, "NetworkObject"));
                    return;
                }
                SetTargetServerRpc(targetNetObject.ObjectId);
            }
        }
    }
}
