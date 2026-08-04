using System;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(ScaledRigidbody))]
public class ScaledObjectSync : NetworkBehaviour
{
    [Tooltip("Ship state snapshot rate in Hz.")]
    [SerializeField] private float snapshotRate = 20f;
    [SerializeField] private bool interpolate = true;
    [SerializeField] private float interpolateSpeed = 1f;
    public enum ControlType
    {
        Owner,
        Server,
        Client
    }
    [Tooltip("Determines whose simulation state is ground truth.")]
    [SerializeField] private ControlType controlType = ControlType.Server;
    private NetworkConnection controller = null;

    [SerializeField] private bool activeWhenOffline = true;
    [SerializeField] private bool syncMass = false;
    
    private ScaledRigidbody scaledRigidbody;
    private ScaledObjectState targetState;
    private float lastSnapshotTime;
    private float snapshotTimer;
    private float interpolationTimer;
    private float snapshotDuration;
    private bool hasInitState;
    private bool isController;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        if (controlType == ControlType.Client)
        {
            Debug.LogWarning(GameLog.ObjectLog(this, "Cannot initialize with Client control type. Defaulting to Owner."));
            controlType = ControlType.Owner;
        }

        if (InstanceFinder.IsOffline && activeWhenOffline && NetworkObject.GetIsNetworked())
        {
            Debug.Log(GameLog.ObjectLog(this, $"Setting IsNetworked to false to keep GameObject active for Offline mode."));
            NetworkObject.SetIsNetworked(false);
        }
    }
    
    private void FixedUpdate()
    {
        if (IsOffline)
            return;

        scaledRigidbody.affectedByGravity = false;

        switch (controlType)
        {
            case ControlType.Owner:
                if (!IsOwner)
                {
                    isController = false;
                    return;
                }
                break;
            case ControlType.Server:
                if (!IsServerInitialized)
                {
                    isController = false;
                    return;
                }
                break;
            case ControlType.Client:
                if (controller == null || LocalConnection != controller)
                {
                    isController = false;
                    return;
                }
                break;
        }
        isController = true;
        scaledRigidbody.affectedByGravity = true;

        float interval = 1f / Mathf.Max(1f, snapshotRate);
        snapshotTimer += Time.fixedDeltaTime;
        if (snapshotTimer >= interval)
        {
            snapshotTimer = 0f;
            ScaledObjectState snapshot = ScaledObjectState.From(scaledRigidbody);
            if (IsServerInitialized)
            {
                if (syncMass)
                {
                    SendSnapshotObserversRpc(snapshot, scaledRigidbody.mass, LocalConnection);
                }
                else
                {
                    SendSnapshotObserversRpc(snapshot, LocalConnection);
                }
            }
            else
            {
                if (syncMass)
                {
                    SendSnapshotServerRpc(snapshot, scaledRigidbody.mass, LocalConnection);
                }
                else
                {
                    SendSnapshotServerRpc(snapshot, LocalConnection);
                }
            }
        }
    }

    private void Update()
    {
        if (!hasInitState || IsOffline || isController)
            return;

        interpolationTimer += Time.deltaTime;
        
        float t = 1f;
        if (interpolate)
        {
            // t = Mathf.Clamp01((Time.realtimeSinceStartup - lastSnapshotTime) * interpolateSpeed * Time.deltaTime);
            t = interpolateSpeed * Time.deltaTime;
        }
        
        Vector3d position = Vector3d.Lerp(scaledRigidbody.scaledTransform.realPosition, targetState.Position, t);
        Quaternion rotation = Quaternion.Slerp(transform.rotation, targetState.Rotation, t);
        Vector3d velocity = Vector3d.Lerp(scaledRigidbody.velocity, targetState.Velocity, t);
        Vector3 angularVelocity = Vector3.Lerp(scaledRigidbody.angularVelocity, targetState.AngularVelocity, t);

        scaledRigidbody.scaledTransform.realPosition = position;
        transform.rotation = rotation;
        scaledRigidbody.velocity = velocity;
        scaledRigidbody.angularVelocity = angularVelocity;
    }

    private void UpdateState(ScaledObjectState state)
    {
        float now = Time.realtimeSinceStartup;

        hasInitState = true;
        targetState = state;
        interpolationTimer = 0f;
        snapshotDuration = hasInitState ? Mathf.Clamp(now - lastSnapshotTime, 0.01f, 0.5f) : 1f / Mathf.Max(1f, snapshotRate);
        lastSnapshotTime = now;
    }

    [ObserversRpc(BufferLast = true, ExcludeServer = true, RunLocally = false)]
    private void SendSnapshotObserversRpc(ScaledObjectState state, NetworkConnection sender)
    {
        if (LocalConnection == sender)
            return;
        UpdateState(state);
    }

    [ServerRpc(RunLocally = false, RequireOwnership = false)]
    private void SendSnapshotServerRpc(ScaledObjectState state, NetworkConnection sender)
    {
        if (sender != LocalConnection && hasInitState)
        {
            // TODO: Implement validation
            // Validate the state sent to this server isn't hacked
            // Ensure distance isn't crazy
            // if ((previousState.Position - state.Position).sqrMagnitude > 100.0
            // || (previousState.Velocity - state.Velocity).sqrMagnitude > 10000.0)
            // {
            //     // Reject the snapshot, and synchronize everyone to what the server has
            //     SendSnapshotObserversRpc(targetState, LocalConnection);
            //     return;
            // }
        }
        UpdateState(state);
        SendSnapshotObserversRpc(state, sender);
    }

    [ObserversRpc(BufferLast = true, ExcludeServer = true, RunLocally = false)]
    private void SendSnapshotObserversRpc(ScaledObjectState state, double mass, NetworkConnection sender)
    {
        if (LocalConnection == sender)
            return;
        UpdateState(state);
        scaledRigidbody.mass = mass;
    }

    [ServerRpc(RunLocally = false, RequireOwnership = false)]
    private void SendSnapshotServerRpc(ScaledObjectState state, double mass, NetworkConnection sender)
    {
        if (sender != LocalConnection && hasInitState)
        {
            // TODO: Implement validation
            // Validate the state sent to this server isn't hacked
            // Ensure distance isn't crazy
            // if ((previousState.Position - state.Position).sqrMagnitude > 100.0
            // || (previousState.Velocity - state.Velocity).sqrMagnitude > 10000.0)
            // {
            //     // Reject the snapshot, and synchronize everyone to what the server has
            //     SendSnapshotObserversRpc(targetState, LocalConnection);
            //     return;
            // }
        }
        UpdateState(state);
        scaledRigidbody.mass = mass;
        SendSnapshotObserversRpc(state, mass, sender);
    }

    public void SetControl(ControlType newControlType, NetworkConnection connection = null)
    {
        if (newControlType == ControlType.Client)
        {
            if (connection == null)
            {
                Debug.Log(GameLog.ObjectLog(this, "Cannot set control type to Client without specifying a controller connection."));
                return;
            }
            controller = connection;
        }
        controlType = newControlType;
    }
}
