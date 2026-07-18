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
    
    private ScaledRigidbody scaledRigidbody;
    private ScaledObjectState previousState;
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
            Debug.LogWarning("[ScaledObjectSync] Cannot initialize with Client control type. Defaulting to Owner.");
            controlType = ControlType.Owner;
        }

        if (InstanceFinder.IsOffline && activeWhenOffline && NetworkObject.GetIsNetworked())
        {
            Debug.Log($"[ScaledObjectSync] Setting {name} IsNetworked to false for Offline mode");
            NetworkObject.SetIsNetworked(false);
        }
    }
    
    private void FixedUpdate()
    {
        if (IsOffline)
            return;

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

        float interval = 1f / Mathf.Max(1f, snapshotRate);
        snapshotTimer += Time.fixedDeltaTime;
        if (snapshotTimer >= interval)
        {
            snapshotTimer = 0f;
            ScaledObjectState snapshot = ScaledObjectState.From(scaledRigidbody);
            if (IsServerInitialized)
                SendSnapshotObserversRpc(snapshot, LocalConnection);
            else
                SendSnapshotServerRpc(snapshot, LocalConnection);
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
            t = snapshotDuration <= 0f ? 1f : Mathf.Clamp01(interpolationTimer / snapshotDuration);
        }
        
        Vector3d position = Vector3d.Lerp(previousState.Position, targetState.Position, t);
        Quaternion rotation = Quaternion.Slerp(previousState.Rotation, targetState.Rotation, t);
        Vector3d velocity = Vector3d.Lerp(previousState.Velocity, targetState.Velocity, t);
        Vector3d angularVelocity = Vector3d.Lerp(previousState.AngularVelocity, targetState.AngularVelocity, t);

        scaledRigidbody.scaledTransform.realPosition = position;
        transform.rotation = rotation;
        scaledRigidbody.velocity = velocity;
        scaledRigidbody.angularVelocity = angularVelocity;
    }

    private void UpdateState(ScaledObjectState state)
    {
        float now = Time.realtimeSinceStartup;

        if (hasInitState)
        {
            previousState = targetState;
        }
        else
        {
            previousState = state;
            hasInitState = true;
        }
        targetState = state;
        interpolationTimer = 0f;
        snapshotDuration = hasInitState
            ? Mathf.Clamp(now - lastSnapshotTime, 0.01f, 0.5f)
            : 1f / Mathf.Max(1f, snapshotRate);
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

    public void SetControl(ControlType newControlType, NetworkConnection connection = null)
    {
        if (newControlType == ControlType.Client)
        {
            if (connection == null)
            {
                Debug.LogWarning("[ScaledObjectSync] Cannot set control type to Client without specifying a controller connection.");
                return;
            }
            controller = connection;
        }
        controlType = newControlType;
    }
}
