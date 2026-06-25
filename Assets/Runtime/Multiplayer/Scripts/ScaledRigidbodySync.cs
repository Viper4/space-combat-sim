using FishNet.Object;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(ScaledRigidbody))]
public class ScaledRigidbodySync : NetworkBehaviour
{
    [Tooltip("Ship state snapshot rate in Hz.")]
    [SerializeField] private float snapshotRate = 20f;

    private ScaledRigidbody scaledRigidbody;
    private ScaledRigidbodyState previousState;
    private ScaledRigidbodyState targetState;
    private float lastSnapshotTime;
    private float snapshotTimer;
    private float interpolationTimer;
    private float snapshotDuration;
    private bool hasSnapshot;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
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
            ScaledRigidbodyState snapshot = ScaledRigidbodyState.From(scaledRigidbody);
            ShipSnapshotObserversRpc(snapshot);
        }
    }

    private void Update()
    {
        if (!IsServerInitialized || !hasSnapshot)
            return;

        interpolationTimer += Time.deltaTime;
        float t = snapshotDuration <= 0f ? 1f : Mathf.Clamp01(interpolationTimer / snapshotDuration);

        Vector3d position = Vector3d.Lerp(previousState.Position, targetState.Position, t);
        Quaternion rotation = Quaternion.Slerp(previousState.Rotation, targetState.Rotation, t);
        Vector3d velocity = Vector3d.Lerp(previousState.Velocity, targetState.Velocity, t);
        Vector3d angularVelocity = Vector3d.Lerp(previousState.AngularVelocity, targetState.AngularVelocity, t);

        scaledRigidbody.scaledTransform.realPosition = position;
        transform.rotation = rotation;
        scaledRigidbody.velocity = velocity;
        scaledRigidbody.angularVelocity = angularVelocity;
    }

    [ObserversRpc(ExcludeServer = true, BufferLast = true)]
    private void ShipSnapshotObserversRpc(ScaledRigidbodyState state)
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
            state.ApplyTo(scaledRigidbody);
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
