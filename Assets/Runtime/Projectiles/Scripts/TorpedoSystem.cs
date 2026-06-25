using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SpaceStuff;
using UnityEngine.InputSystem;
using FishNet.Object;

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
        TryGetComponent(out targetingSystem);
        launchedTorpedoes = new Torpedo[torpedoPoints.Length];
        if (IsOffline)
            Init();
    }

    private void Init()
    {
        if (initialized)
            return;
        targetingSystem.OnTargetChange += SetTarget;
        GameManager.Instance.inputActions.Player.Secondary.performed += TryLaunchTorpedo;
        initialized = true;
    }

    private void OnDestroy()
    {
        targetingSystem.OnTargetChange -= SetTarget;
        GameManager.Instance.inputActions.Player.Secondary.performed -= TryLaunchTorpedo;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
            return;
        Init();
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
            ToggleBayDoorObserversRpc(torpedoBayDoorOpen);
    }

    private void LaunchTorpedo(int i)
    {
        launchAudio.ResetPlay(true);
        Vector3d launchPosition = ship.scaledRigidbody.scaledTransform.TransformRenderPoint(torpedoPoints[i].transform.position);
        if (IsOffline || IsServerInitialized)
            launchedTorpedoes[i] = torpedoPoints[i].LaunchTorpedo(launchPosition, ship.scaledRigidbody.velocity, targetingSystem.lockedTarget, i, ship.radarTarget.team);
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
    private void SetTargetServerRpc(uint targetId)
    {
        if (!RadarRegistry.TryGet(targetId, out var target))
            return;
        for (int i = 0; i < launchedTorpedoes.Length; i++)
        {
            if (launchedTorpedoes[i] == null)
                continue;
            launchedTorpedoes[i].SetTarget(target);
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
                SetTargetServerRpc(targetingSystem.lockedTarget.GetID());
            }
        }
    }
}
