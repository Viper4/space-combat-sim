using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(ScaledTransform))]
public class NetworkScaledObject : NetworkBehaviour
{
    /// <summary>Everything needed to describe this object's physical state at a point in time.</summary>
    public struct StateSnapshot
    {
        /// <summary>FishNet local tick this snapshot was captured on. Used to discard
        /// out-of-order/duplicate/stale updates when arriving over an unreliable channel.</summary>
        public uint Tick;
        public Vector3d RealPosition;
        public Vector3d RealScale;
        public Quaternion Rotation;
        public Vector3 Velocity; // float to reduce bytes sent over network
        public Vector3 AngularVelocity;

        /// <summary>Reads the current live state from this object's ScaledTransform/ScaledRigidbody.</summary>
        public static StateSnapshot Capture(ScaledTransform st, ScaledRigidbody rb, uint tick)
        {
            return new StateSnapshot
            {
                Tick = tick,
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

    [Header("Bandwidth reduction")]
    [Tooltip("Skip sending a tick's update if position hasn't changed by at least this much (real-space units) since the last send.")]
    [SerializeField]
    private float positionChangeThreshold = 0.01f;

    [Tooltip("Skip sending a tick's update if the dot product between rotation and last send's rotation is less than this.")]
    [SerializeField, Range(0f, 1f)]
    private float rotationChangeThreshold = 0.99f; // Quaternion.dot of 1 = 0 degrees, 0 = 180 degrees, -1 = 360 degrees

    [Tooltip("Skip sending a tick's update if velocity/angular velocity haven't changed by at least this much since the last send.")]
    [SerializeField]
    private float velocityChangeThreshold = 0.01f;

    [Header("Snap correction")]
    [Tooltip("If the receiver's current real-space position is farther than this from a newly received target, teleport straight to it instead of interpolating. Set to 0 or less to always interpolate (no snapping).")]
    [SerializeField]
    private double snapPositionThreshold = 50.0;
 
    [Tooltip("If the dot product between the receiver's current rotation and a newly received target is less than this, snap straight to it instead of interpolating.")]
    [SerializeField, Range(0f, 1f)]
    private float snapRotationThreshold = 0.5f;

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

    [ConditionalHide("clientAuthoritative")]
    [Tooltip("Log details when a client-authoritative report is rejected. Leave off in normal play - this runs at send rate and allocates strings.")]
    [SerializeField]
    private bool logRejectedUpdates = false;

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
    private uint ticksPerSend = 1;

    /// <summary>The client/server's last accepted/simulated state. This is what corrections are sent from.</summary>
    private StateSnapshot targetState;
    private bool receivedInitState;

    /// <summary>Last state actually sent over the network (used for the dirty check, distinct
    /// from targetState which also tracks locally-simulated state on the sender).</summary>
    private StateSnapshot lastSentState;
    private bool hasSentState;

    private bool IsSender => (clientAuthoritative && IsOwner) || (!clientAuthoritative && IsServerInitialized);

    private void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        scaledRigidbody = GetComponent<ScaledRigidbody>(); // may be null, that's fine
        if (InstanceFinder.IsOffline && keepActiveWhenOffline && NetworkObject.GetIsNetworked())
        {
            Debug.Log(GameLog.ObjectLog(this, $"Setting IsNetworked to false to keep GameObject active for Offline mode."));
            NetworkObject.SetIsNetworked(false);
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        ticksPerSend = (uint)Mathf.Max(1, Mathf.RoundToInt(TimeManager.TickRate / sendRate));
        TimeManager.OnTick += OnTick;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        TimeManager.OnTick -= OnTick;
    }

    public override void OnStartServer()
    {
        targetState = StateSnapshot.Capture(scaledTransform, scaledRigidbody, TimeManager.LocalTick);
    }

    public override void OnSpawnServer(NetworkConnection connection)
    {
        base.OnSpawnServer(connection);
        if (connection.IsHost)
            return;
        // Late joiner: give them the current state immediately instead of waiting for the next tick.
        TargetForceState(connection, targetState);
    }

    private void FixedUpdate()
    {
        if (IsOffline || IsSender)
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

    private void OnTick()
    {
        if (!IsSender)
            return;
        if (TimeManager.LocalTick % ticksPerSend != 0)
            return;

        var snapshot = StateSnapshot.Capture(scaledTransform, scaledRigidbody, TimeManager.LocalTick);
        targetState = snapshot;

        if (!ShouldSend(snapshot))
            return;

        lastSentState = snapshot;
        hasSentState = true;

        if (clientAuthoritative)
        {
            // Owner simulates locally and reports up. Server never sends here on its own
            // initiative - it only responds when it receives a report (see ServerReceiveState).
            if (IsOwner)
            {
                if (!IsServerInitialized)
                    ServerReceiveState(snapshot); // Server will then send to observers
                else
                    ObserversReceiveState(snapshot);
            }
        }
        else if (IsServerInitialized)
        {
            // Server simulates and broadcasts to everyone, including the owner if any.
            ObserversReceiveState(snapshot);
        }
    }

    /// <summary>Dirty check so we don't burn bandwidth resending state that hasn't
    /// meaningfully changed since the last send.</summary>
    private bool ShouldSend(StateSnapshot current)
    {
        if (!hasSentState)
            return true;

        if ((current.RealPosition - lastSentState.RealPosition).ToVector3().sqrMagnitude > positionChangeThreshold * positionChangeThreshold)
            return true;
        if (Mathf.Abs(Quaternion.Dot(current.Rotation, lastSentState.Rotation)) < rotationChangeThreshold)
            return true;
        if ((current.Velocity - lastSentState.Velocity).sqrMagnitude > velocityChangeThreshold * velocityChangeThreshold)
            return true;
        if ((current.AngularVelocity - lastSentState.AngularVelocity).sqrMagnitude > velocityChangeThreshold * velocityChangeThreshold)
            return true;

        return false;
    }

    // --- Client-authoritative: Owner -> Server ---

    [ServerRpc]
    private void ServerReceiveState(StateSnapshot reported, Channel channel = Channel.Unreliable)
    {
        if (reported.Tick <= targetState.Tick)
            return; // stale, duplicate, or out-of-order report arriving late - discard it

        float dt = (reported.Tick - targetState.Tick) * (float)TimeManager.TickDelta;

        if (!receivedInitState || IsPlausible(targetState, reported, dt))
        {
            receivedInitState = true;
            // Accept: adopt the owner's reported state as the server's own, then tell everyone else.
            targetState = reported;
            if (!interpolateTransform)
                reported.ApplyTo(scaledTransform);
            if (!interpolateRigidbody && scaledRigidbody != null)
                reported.ApplyTo(scaledRigidbody);
            ObserversReceiveState(targetState, Channel.Unreliable);
        }
        else
        {
            // Reject: ignore the report entirely, correct the owner back to server truth, and
            // make sure everyone else also has the correct (server) state.
            TargetForceState(Owner, targetState);
            ObserversReceiveState(targetState);
        }
    }

    private bool IsPlausible(StateSnapshot previous, StateSnapshot reported, float timeSinceLastUpdate)
    {
        if (reported.Velocity.sqrMagnitude > ScaledSpacePhysics.speedOfLight * ScaledSpacePhysics.speedOfLight)
            return false;
        if (!IsAccelerationPlausible(previous, reported, timeSinceLastUpdate))
            return false;
        if (!IsPositionDeltaPlausible(previous, reported, timeSinceLastUpdate))
            return false;

        return true;
    }

    private bool IsAccelerationPlausible(StateSnapshot previous, StateSnapshot reported, float timeSinceLastUpdate)
    {
        Vector3 worldVelocityDelta = reported.Velocity - previous.Velocity;
        worldVelocityDelta -= scaledRigidbody.GetGravity().ToVector3() * timeSinceLastUpdate; // Remove velocity change due to gravity to only consider the acceleration the owner tried applying
        Vector3 localVelocityDelta = WorldToLocal(worldVelocityDelta, previous.Rotation);

        bool plausible = IsWithinAxisLimit(localVelocityDelta.x, maxAccelerationPositive.x, maxAccelerationNegative.x, timeSinceLastUpdate)
            && IsWithinAxisLimit(localVelocityDelta.y, maxAccelerationPositive.y, maxAccelerationNegative.y, timeSinceLastUpdate)
            && IsWithinAxisLimit(localVelocityDelta.z, maxAccelerationPositive.z, maxAccelerationNegative.z, timeSinceLastUpdate);
        if (!plausible && logRejectedUpdates)
            Debug.Log(GameLog.ObjectLog(this, $"Velocity delta not plausible: {worldVelocityDelta} | maxs: {maxAccelerationPositive} {maxAccelerationNegative}"));

        return plausible;
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
        if (!plausible && logRejectedUpdates)
            Debug.Log(GameLog.ObjectLog(this, $"Position delta not plausible: {localPositionDelta} | maxs: {maxDeltaX} {maxDeltaY} {maxDeltaZ}"));

        return plausible;
    }

    private float GetMaxPositionDelta(float localVelocity, float maxPositiveAcceleration, float maxNegativeAcceleration, float time)
    {
        float positiveDelta = localVelocity * time + 0.5f * maxPositiveAcceleration * time * time;
        float negativeDelta = localVelocity * time - 0.5f * maxNegativeAcceleration * time * time;

        return Mathf.Max(Mathf.Abs(positiveDelta), Mathf.Abs(negativeDelta)) * leniency;
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
    private void ObserversReceiveState(StateSnapshot state, Channel channel = Channel.Unreliable)
    {
        if (IsSender)
            return;
        if (state.Tick <= targetState.Tick)
            return; // discard out-of-order/duplicate/old data - only the most recent state matters
 
        bool snap = ShouldSnap(state);
        targetState = state;
 
        if (!interpolateTransform || snap)
            targetState.ApplyTo(scaledTransform);
        if (!interpolateRigidbody && scaledRigidbody != null)
            targetState.ApplyTo(scaledRigidbody);

    }

    /// <summary>Whether the gap between our current visual position/rotation and a newly
    /// received target is large enough that we should teleport instead of interpolating.
    /// Only meaningful when interpolateTransform is on - otherwise state is always applied
    /// directly anyway.</summary>
    private bool ShouldSnap(StateSnapshot incoming)
    {
        if (!interpolateTransform)
            return false;
 
        if (snapPositionThreshold > 0.0)
        {
            if ((scaledTransform.realPosition - incoming.RealPosition).sqrMagnitude > snapPositionThreshold * snapPositionThreshold)
                return true;
        }
 
        if (snapRotationThreshold > 0f)
        {
            if (Mathf.Abs(Quaternion.Dot(scaledTransform.transform.localRotation, incoming.Rotation)) < snapRotationThreshold)
                return true;
        }
 
        return false;
    }


    [TargetRpc(ValidateTarget = false)]
    private void TargetForceState(NetworkConnection connection, StateSnapshot state)
    {
        targetState = state;
        targetState.ApplyTo(scaledTransform);
        if (scaledRigidbody != null)
            targetState.ApplyTo(scaledRigidbody);
    }
}