using FishNet;
using FishNet.Connection;
using FishNet.Object;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(ScaledTransform))]
public class NetworkScaledObject : NetworkBehaviour
{
    /// <summary>Everything needed to describe this object's physical state at a point in time.</summary>
    public struct StateSnapshot
    {
        public Vector3d RealPosition;
        public Vector3d RealScale;
        public Quaternion Rotation;
        public Vector3 Velocity; // float to reduce bytes sent over network
        public Vector3 AngularVelocity;

        /// <summary>Reads the current live state from this object's ScaledTransform/ScaledRigidbody.</summary>
        public static StateSnapshot Capture(ScaledTransform st, ScaledRigidbody rb)
        {
            return new StateSnapshot
            {
                RealPosition = st.realPosition,
                RealScale = st.realScale,
                Rotation = st.transform.localRotation,
                Velocity = rb != null ? rb.velocity.ToVector3() : Vector3.zero,
                AngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero
            };
        }

        public readonly void ApplyTo(ScaledTransform st)
        {
            st.realPosition = RealPosition;
            st.realScale = RealScale;
            st.transform.localRotation = Rotation;
        }

        public readonly void ApplyTo(ScaledRigidbody rb)
        {
            rb.velocity = Velocity.ToVector3d();
            rb.angularVelocity = AngularVelocity;
        }
    }

    [SerializeField]
    private bool keepActiveWhenOffline;

    [Tooltip("If true, the Owner simulates locally and reports state to the server for validation. If false, the server is the sole simulator and just broadcasts its state.")]
    [SerializeField]
    private bool clientAuthoritative = false;

    [Tooltip("How many times per second to send state updates.")]
    [SerializeField]
    private float sendRate = 20f;

    [SerializeField]
    private bool interpolateTransform;

    [SerializeField]
    private bool interpolateRigidbody;

    [SerializeField]
    private float interpolationSpeed = 1f;

    [ConditionalHide("clientAuthoritative")]
    [Header("Client-authoritative validation")]
    [Tooltip("Max acceleration (units/sec^2) allowed on each LOCAL axis in the positive direction.")]
    [SerializeField]
    private Vector3 maxAccelerationPositive = new(10.0f, 10.0f, 100.0f);

    [ConditionalHide("clientAuthoritative")]
    [Tooltip("Max acceleration (units/sec^2) allowed on each LOCAL axis in the negative direction.")]
    [SerializeField]
    private Vector3 maxAccelerationNegative = new(10.0f, 10.0f, 10.0f);

    [ConditionalHide("clientAuthoritative")]
    [Tooltip("Extra multiplier on max possible acceleration/distance, to give leniency for lag/jitter.")]
    [SerializeField]
    private float leniency = 1.5f;

    /// <summary>
    /// Lets the object's controller (e.g. Ship swapping cruise/combat mode) change how much
    /// thrust is considered legitimate on each local axis. Positive/negative let a single
    /// engine, like a main engine that only pushes +Z, have an asymmetric limit.
    /// </summary>
    public void SetMaxAcceleration(Vector3 positive, Vector3 negative)
    {
        maxAccelerationPositive = positive;
        maxAccelerationNegative = negative;
    }

    private ScaledTransform scaledTransform;
    private ScaledRigidbody scaledRigidbody; // optional - null if this object has no physics
    private float sendTimer;
    private float inverseSendRate;

    /// <summary>The client/server's last accepted/simulated state. This is what corrections are sent from.</summary>
    private StateSnapshot targetState;
    private bool receivedInitState;

    private void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        scaledRigidbody = GetComponent<ScaledRigidbody>(); // may be null, that's fine
        inverseSendRate = 1f / sendRate;
        if (InstanceFinder.IsOffline && keepActiveWhenOffline && NetworkObject.GetIsNetworked())
        {
            Debug.Log(GameLog.ObjectLog(this, $"Setting IsNetworked to false to keep GameObject active for Offline mode."));
            NetworkObject.SetIsNetworked(false);
        }
    }

    public override void OnStartServer()
    {
        targetState = StateSnapshot.Capture(scaledTransform, scaledRigidbody);
    }

    public override void OnSpawnServer(NetworkConnection connection)
    {
        base.OnSpawnServer(connection);
        // Late joiner: give them the current state immediately instead of waiting for the next tick.
        TargetReceiveState(connection, targetState);
    }

    private void FixedUpdate()
    {
        if (IsOffline)
            return;
        if (clientAuthoritative && IsOwner)
            return;
        if (!clientAuthoritative && IsServerInitialized)
            return;
        float t = Time.fixedDeltaTime * interpolationSpeed;
        if (interpolateTransform)
        {
            scaledTransform.realPosition = Vector3d.Lerp(scaledTransform.realPosition, targetState.RealPosition, t);
            scaledTransform.transform.localRotation = Quaternion.Slerp(scaledTransform.transform.localRotation, targetState.Rotation, t);
            scaledTransform.realScale = Vector3d.Lerp(scaledTransform.realScale, targetState.RealScale, t);
        }

        if (interpolateRigidbody && scaledRigidbody != null)
        {
            scaledRigidbody.velocity = Vector3d.Lerp(scaledRigidbody.velocity, targetState.Velocity.ToVector3d(), t);
            scaledRigidbody.angularVelocity = Vector3.Lerp(scaledRigidbody.angularVelocity, targetState.AngularVelocity, t);
        }
    }

    private void Update()
    {
        sendTimer += Time.deltaTime;
        if (sendTimer < inverseSendRate)
            return;
        sendTimer = 0f;

        if (clientAuthoritative)
        {
            // Owner simulates locally and reports up. Server never sends here on its own
            // initiative - it only responds when it receives a report (see ServerReceiveState).
            if (IsOwner && !IsServerInitialized)
                SendStateToServer(StateSnapshot.Capture(scaledTransform, scaledRigidbody));
        }
        else
        {
            // Server simulates and broadcasts to everyone, including the owner if any.
            if (IsServerInitialized)
            {
                targetState = StateSnapshot.Capture(scaledTransform, scaledRigidbody);
                ObserversReceiveState(targetState);
            }
        }
    }

    // --- Client-authoritative: Owner -> Server ---

    private void SendStateToServer(StateSnapshot state)
    {
        ServerReceiveState(state);
    }

    [ServerRpc]
    private void ServerReceiveState(StateSnapshot reported)
    {
        float timeSinceLastUpdate = inverseSendRate;

        if (!receivedInitState || IsPlausible(targetState, reported, timeSinceLastUpdate))
        {
            receivedInitState = true;
            // Accept: adopt the owner's reported state as the server's own, then tell everyone else.
            targetState = reported;
            if (!interpolateTransform)
                reported.ApplyTo(scaledTransform);
            if (!interpolateRigidbody && scaledRigidbody != null)
                reported.ApplyTo(scaledRigidbody);
            ObserversReceiveState(targetState, excludeOwner: true);
        }
        else
        {
            // Reject: ignore the report entirely, correct the owner back to server truth, and
            // make sure everyone else also has the correct (server) state.
            TargetReceiveState(Owner, targetState);
            ObserversReceiveState(targetState, excludeOwner: true);
        }
    }

    private bool IsPlausible(StateSnapshot previous, StateSnapshot reported, float timeSinceLastUpdate)
    {
        if (reported.Velocity.sqrMagnitude > ScaledSpacePhysics.speedOfLight * ScaledSpacePhysics.speedOfLight)
            return false;

        if (!IsAccelerationPlausible(previous, reported, timeSinceLastUpdate))
        {
            Debug.Log(GameLog.ObjectLog(this, $"Acceleration not plausible {previous.Velocity} {reported.Velocity} | Max: {maxAccelerationNegative} {maxAccelerationPositive}."));
            return false;
        }
        
        if (!IsPositionDeltaPlausible(previous, reported, timeSinceLastUpdate))
        {
            Debug.Log(GameLog.ObjectLog(this, $"Position delta not plausible {previous.RealPosition} {reported.RealPosition} | Max: {maxAccelerationNegative} {maxAccelerationPositive}."));
            return false;
        }

        return true;
    }

    private bool IsAccelerationPlausible(StateSnapshot previous, StateSnapshot reported, float timeSinceLastUpdate)
    {
        Vector3 worldVelocityDelta = reported.Velocity - previous.Velocity;
        worldVelocityDelta -= scaledRigidbody.GetGravity().ToVector3() * timeSinceLastUpdate; // Remove velocity change due to gravity to only consider the acceleration the owner tried applying
        Vector3 localVelocityDelta = WorldToLocal(worldVelocityDelta, previous.Rotation);

        return IsWithinAxisLimit(localVelocityDelta.x, maxAccelerationPositive.x, maxAccelerationNegative.x, timeSinceLastUpdate)
            && IsWithinAxisLimit(localVelocityDelta.y, maxAccelerationPositive.y, maxAccelerationNegative.y, timeSinceLastUpdate)
            && IsWithinAxisLimit(localVelocityDelta.z, maxAccelerationPositive.z, maxAccelerationNegative.z, timeSinceLastUpdate);
    }

    private bool IsWithinAxisLimit(float velocityDelta, double maxPositiveAccel, double maxNegativeAccel, float timeSinceLastUpdate)
    {
        if (velocityDelta >= 0)
            return velocityDelta <= maxPositiveAccel * timeSinceLastUpdate * leniency;
        else
            return -velocityDelta <= maxNegativeAccel * timeSinceLastUpdate * leniency;
    }

    private bool IsPositionDeltaPlausible(StateSnapshot previous, StateSnapshot reported, float timeSinceLastUpdate)
    {
        Vector3 localVelocity = WorldToLocal(previous.Velocity, previous.Rotation);

        float maxDeltaX = GetMaxPositionDelta(localVelocity.x, maxAccelerationPositive.x, maxAccelerationNegative.x, timeSinceLastUpdate);
        float maxDeltaY = GetMaxPositionDelta(localVelocity.y, maxAccelerationPositive.y, maxAccelerationNegative.y, timeSinceLastUpdate);
        float maxDeltaZ = GetMaxPositionDelta(localVelocity.z, maxAccelerationPositive.z, maxAccelerationNegative.z, timeSinceLastUpdate);

        Vector3d gravityDelta = 0.5f * timeSinceLastUpdate * timeSinceLastUpdate * scaledRigidbody.GetGravity();
        Vector3d localPositionDelta = WorldToLocal(reported.RealPosition - previous.RealPosition - gravityDelta, previous.Rotation);
        // Allow position deltas where the object accelerated with max acceleration in any direction between the previous state and reported state
        bool plausible = System.Math.Abs(localPositionDelta.x) <= maxDeltaX 
            && System.Math.Abs(localPositionDelta.y) <= maxDeltaY 
            && System.Math.Abs(localPositionDelta.z) <= maxDeltaZ;
        if (!plausible)
            Debug.Log(GameLog.ObjectLog(this, $"localPosDelta: {localPositionDelta} maxs: {maxDeltaX} {maxDeltaY} {maxDeltaZ}"));

        return plausible;
    }

    private float GetMaxPositionDelta(float localVelocity, float maxPositiveAcceleration, float maxNegativeAcceleration, float time)
    {
        float positiveDistance = localVelocity * time + 0.5f * maxPositiveAcceleration * time * time;
        float negativeDistance = localVelocity * time - 0.5f * maxNegativeAcceleration * time * time;

        return Mathf.Max(Mathf.Abs(positiveDistance), Mathf.Abs(negativeDistance)) * leniency;
    }

    /// <summary>Projects a world-space Vector3 onto a rotation's local axes (same idea as
    /// Ship.AddRelativeForce's basis-vector projection, just the inverse direction).</summary>
    private static Vector3 WorldToLocal(Vector3 worldVector, Quaternion rotation)
    {
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;

        return new Vector3(
            worldVector.x * right.x + worldVector.y * right.y + worldVector.z * right.z,
            worldVector.x * up.x + worldVector.y * up.y + worldVector.z * up.z,
            worldVector.x * forward.x + worldVector.y * forward.y + worldVector.z * forward.z
        );
    }

    /// <summary>Projects a world-space Vector3d onto a rotation's local axes (same idea as
    /// Ship.AddRelativeForce's basis-vector projection, just the inverse direction).</summary>
    private static Vector3d WorldToLocal(Vector3d worldVector, Quaternion rotation)
    {
        Vector3d right = Vector3d.right.Rotate(rotation);
        Vector3d up = Vector3d.up.Rotate(rotation);
        Vector3d forward = Vector3d.forward.Rotate(rotation);

        return new Vector3d(
            worldVector.x * right.x + worldVector.y * right.y + worldVector.z * right.z,
            worldVector.x * up.x + worldVector.y * up.y + worldVector.z * up.z,
            worldVector.x * forward.x + worldVector.y * forward.y + worldVector.z * forward.z
        );
    }

    // --- Receiving state (both modes) ---

    [ObserversRpc]
    private void ObserversReceiveState(StateSnapshot state, bool excludeOwner = false)
    {
        if (IsServerInitialized)
            return; // server already has authoritative state, never overwrite itself
        if (excludeOwner && IsOwner)
            return; // owner already simulated this locally, don't stomp on it

        targetState = state;
        if (!interpolateTransform)
            targetState.ApplyTo(scaledTransform);
        if (!interpolateRigidbody && scaledRigidbody != null)
            targetState.ApplyTo(scaledRigidbody);
    }

    [TargetRpc(ValidateTarget = false)]
    private void TargetReceiveState(NetworkConnection connection, StateSnapshot state)
    {
        targetState = state;
        targetState.ApplyTo(scaledTransform);
        if (scaledRigidbody != null)
            targetState.ApplyTo(scaledRigidbody);
    }
}