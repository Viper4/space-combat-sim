using FishNet.Object;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(DoubleRigidbody))]
public class DoubleRigidbodySync : NetworkBehaviour
{
    [Tooltip("Ship state snapshot rate in Hz.")]
    [SerializeField] private float snapshotRate = 20f;

    private DoubleRigidbody doubleRigidbody;
    private DoubleRigidbodyState previousState;
    private DoubleRigidbodyState targetState;
    private float lastSnapshotTime;
    private float snapshotTimer;
    private float interpolationTimer;
    private float snapshotDuration;
    private bool hasSnapshot;
    private bool remoteClient;

    private void Awake()
    {
        doubleRigidbody = GetComponent<DoubleRigidbody>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        remoteClient = IsClientInitialized && !IsOwner;

        if (remoteClient)
        {
            // Stop local physics on non-owner clients; server-authoritative snapshots drive movement.
            doubleRigidbody.isKinematic = true;
        }
    }

    private void FixedUpdate()
    {
        if (!IsServerInitialized)
            return;

        float interval = 1f / Mathf.Max(1f, snapshotRate);
        snapshotTimer += Time.fixedDeltaTime;
        if (snapshotTimer >= interval)
        {
            snapshotTimer -= interval;
            DoubleRigidbodyState snapshot = DoubleRigidbodyState.From(doubleRigidbody);
            ShipSnapshotObserversRpc(snapshot);
        }
    }

    private void Update()
    {
        if (!remoteClient || !hasSnapshot)
            return;

        interpolationTimer += Time.deltaTime;
        float t = snapshotDuration <= 0f ? 1f : Mathf.Clamp01(interpolationTimer / snapshotDuration);

        Vector3d position = Vector3d.Lerp(previousState.Position, targetState.Position, t);
        Quaternion rotation = Quaternion.Slerp(previousState.Rotation, targetState.Rotation, t);
        Vector3d velocity = Vector3d.Lerp(previousState.Velocity, targetState.Velocity, t);
        Vector3d angularVelocity = Vector3d.Lerp(previousState.AngularVelocity, targetState.AngularVelocity, t);

        doubleRigidbody.scaledTransform.realPosition = position;
        transform.rotation = rotation;
        doubleRigidbody.velocity = velocity;
        doubleRigidbody.angularVelocity = angularVelocity;
    }

    [ObserversRpc(ExcludeOwner = true, BufferLast = true)]
    private void ShipSnapshotObserversRpc(DoubleRigidbodyState state)
    {
        float now = Time.realtimeSinceStartup;
        if (!hasSnapshot)
        {
            previousState = state;
            targetState = state;
            hasSnapshot = true;
            interpolationTimer = 0f;
            snapshotDuration = hasSnapshot
                ? Mathf.Clamp(now - lastSnapshotTime, 0.01f, 0.5f)
                : 1f / Mathf.Max(1f, snapshotRate);
            lastSnapshotTime = now;
            state.ApplyTo(doubleRigidbody);
            return;
        }

        previousState = targetState;
        targetState = state;
        interpolationTimer = 0f;
        snapshotDuration = hasSnapshot
            ? Mathf.Clamp(now - lastSnapshotTime, 0.01f, 0.5f)
            : 1f / Mathf.Max(1f, snapshotRate);
        lastSnapshotTime = now;
    }
}
