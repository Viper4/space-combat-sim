using UnityEngine;
using System.Collections.Generic;
using SpaceStuff;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// Manages collision detection and response for objects in scaled space.
/// Treats all objects as spheres with radius = Max(realScale.x, realScale.y, realScale.z)
/// </summary>
public class ScaledSpacePhysics : MonoBehaviour
{
    public static ScaledSpacePhysics Instance { get; private set; }
    
    public event Action<DoubleRigidbody> PrePhysicsStep;

    public float restitution = 0.5f;
    [SerializeField] private bool debugDraw = false;
    
    [SerializeField] private int maxGridLevels;
    [SerializeField] private double baseGridCellSize;
    [SerializeField] private int cellScalingFactor;
    public HGrid hGrid;
    private Dictionary<uint, DoubleRigidbody> doubleRigidbodies = new Dictionary<uint, DoubleRigidbody>();
    // private List<DoubleRigidbody> doubleRigidbodies = new List<DoubleRigidbody>();
    private HashSet<Pair> previousCollisions = new HashSet<Pair>();
    private HashSet<Pair> previousTriggers = new HashSet<Pair>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        hGrid = new HGrid(maxGridLevels, baseGridCellSize, cellScalingFactor);
    }

    public void RegisterDoubleRigidbody(DoubleRigidbody rb)
    {
        if (!doubleRigidbodies.ContainsKey(rb.id))
        {
            doubleRigidbodies.Add(rb.id, rb);
            hGrid.UpdatePosition(rb);
        }
    }

    public void UnregisterDoubleRigidbody(DoubleRigidbody rb)
    {
        doubleRigidbodies.Remove(rb.id);
        hGrid.Delete(rb);
    }

    public void UpdateGridSize(DoubleRigidbody rb)
    {
        hGrid.UpdateSize(rb);
    }

    public void UpdateGridPos(DoubleRigidbody rb)
    {
        hGrid.UpdatePosition(rb);
    }

    private void FixedUpdate()
    {
        HashSet<Pair> currentCollisions = new HashSet<Pair>();
        HashSet<Pair> currentTriggers = new HashSet<Pair>();

        bool shiftOrigin = FloatingWorldOrigin.Instance.OverShiftThreshold();
        if (shiftOrigin)
            FloatingWorldOrigin.Instance.ShiftOrigin();
        
        bool transformsDirty = shiftOrigin; // If shifting origin, will need to sync transforms after loop, otherwise only if any velocities were nonzero
        // Insert all rigidbodies into hierarchical grid for collision detection, and run other logic per doubleRigidbody
        long getCandidatesTicks = 0;
        long physicsStepTicks = 0;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        foreach (DoubleRigidbody rb in doubleRigidbodies.Values)
        {
            rb.ClearGravity();
            PrePhysicsStep?.Invoke(rb);
            rb.PhysicsStep(Time.fixedDeltaTime);

            if (!transformsDirty && rb.active && !rb.isKinematic && rb.velocity.sqrMagnitude > 0.0001)
                transformsDirty = true;
        }
        stopwatch.Stop();
        physicsStepTicks = stopwatch.ElapsedTicks;
        long collisionCheckTicks = 0;
        Stopwatch stopwatch1 = new Stopwatch();
        stopwatch.Reset();
        stopwatch.Start();
        foreach (DoubleRigidbody rb in doubleRigidbodies.Values)
        {
            /*foreach(DoubleRigidbody candidate in hGrid.GetCandidates(rb))
            {
                ulong triggerKey = rb.id << 32 | candidate.id;
                if (rb.trackTrigger && !currentTriggers.Contains(triggerKey) && CheckTrigger(rb, candidate))
                {
                    currentTriggers.Add(triggerKey);
                    if (!previousTriggers.Contains(triggerKey))
                        rb.RaiseTriggerEnter(candidate);
                }
            }*/

            foreach (DoubleRigidbody candidate in hGrid.GetCandidates(rb))
            {
                // key's 1st 32-bit half is the min id uint, 2nd 32-bit half is the max id uint
                Pair collisionKey = new Pair(Math.Min(rb.id, candidate.id), Math.Max(rb.id, candidate.id));

                if ((!rb.active && !candidate.active) || currentCollisions.Contains(collisionKey))
                    continue;

                stopwatch1.Reset();
                stopwatch1.Start();
                if (CheckCollision(rb, candidate, out CollisionInfo collision))
                {
                    currentCollisions.Add(collisionKey);
                    ResolveCollision(collision);

                    // Track new collisions
                    if (!previousCollisions.Contains(collisionKey))
                    {
                        CollisionInfo collisionInfoForB = collision;
                        // Swap A and B for the second RB's event
                        collisionInfoForB.rbA = candidate;
                        collisionInfoForB.rbB = rb;
                        collisionInfoForB.transformA = candidate.transform;
                        collisionInfoForB.transformB = rb.transform;
                        // Also need to invert normal for the second RB
                        collisionInfoForB.normal = -collision.normal;
                        rb.RaiseCollisionEnter(collision);
                        candidate.RaiseCollisionEnter(collisionInfoForB);
                    }
                }
                stopwatch1.Stop();
                collisionCheckTicks += stopwatch1.ElapsedTicks;
            }
        }
        stopwatch.Stop();
        getCandidatesTicks = stopwatch.ElapsedTicks;
        Debug.Log($"Physics Step ticks: {physicsStepTicks}, Get candidates loop ticks: {getCandidatesTicks}, Check Collision ticks: {collisionCheckTicks}");

        // Detect collision exits
        foreach (Pair collision in previousCollisions)
        {
            if (!currentCollisions.Contains(collision) 
            && doubleRigidbodies.TryGetValue(collision.idA, out var rbA) 
            && doubleRigidbodies.TryGetValue(collision.idB, out var rbB))
            {
                rbA.RaiseCollisionExit(rbB);
                rbB.RaiseCollisionExit(rbA);
            }
        }

        // Detect trigger exits
        foreach (Pair trigger in previousTriggers)
        {
            if (!currentTriggers.Contains(trigger)
            && doubleRigidbodies.TryGetValue(trigger.idA, out var rbA) 
            && doubleRigidbodies.TryGetValue(trigger.idB, out var rbB))
            {
                rbA.RaiseTriggerExit(rbB);
            }
        }

        previousCollisions = currentCollisions;
        previousTriggers = currentTriggers;

        if (transformsDirty)
            Physics.SyncTransforms();
    }

    private bool CheckCollision(DoubleRigidbody a, DoubleRigidbody b, out CollisionInfo collision)
    {
        collision = default;

        double radiusA = a.GetCollisionRadius();
        double radiusB = b.GetCollisionRadius();
        double minDistance = radiusA + radiusB;

        // Positions at END of frame (after PhysicsStep)
        Vector3d posA = a.scaledTransform.realPosition;
        Vector3d posB = b.scaledTransform.realPosition;
        Vector3d relativePosition = posB - posA;
        double sqrDistance = relativePosition.sqrMagnitude;
        double distance = minDistance;

        bool collided;

        if (sqrDistance < minDistance * minDistance)
        {
            // Overlapping at end of frame — standard intersection
            collided = true;
            distance = Math.Sqrt(sqrDistance);
            Debug.Log($"Intersect Collide: {a.id} {a.name} and {b.id} {b.name}");
        }
        else
        {
            // CCD: sweep from prevPos to realPos (retroactive)
            // displacement = realPos - prevPos = what PhysicsStep just added
            Vector3d dispA = a.scaledTransform.realPosition - a.prevPos;
            Vector3d dispB = b.scaledTransform.realPosition - b.prevPos;

            // Use start-of-frame positions as the sweep origin
            Vector3d startPosA = a.prevPos;
            Vector3d startPosB = b.prevPos;
            Vector3d relPos0 = startPosB - startPosA;  // relative pos at frame start
            Vector3d relDisp = dispB - dispA;           // relative displacement over frame
            
            // Quick reject: objects moving apart at frame start
            if (Vector3d.Dot(relPos0, relDisp) >= 0)
                return false;

            // Only need CCD if relative displacement exceeds the gap
            double gap = Math.Sqrt(relPos0.sqrMagnitude) - minDistance;
            if (gap < 0)
                gap = 0; // prevPos already overlapping — handled next frame
            double sqrRelDisp = relDisp.sqrMagnitude;
            if (sqrRelDisp <= gap * gap)
                return false;

            // Quadratic: |relPos0 + relDisp*t|² = minDistance²,  t ∈ [0,1]
            double aCoeff = Vector3d.Dot(relDisp, relDisp);
            double bCoeff = 2.0 * Vector3d.Dot(relPos0, relDisp);
            double cCoeff = Vector3d.Dot(relPos0, relPos0) - minDistance * minDistance;

            double t = SpaceMath.SolveQuadratic(aCoeff, bCoeff, cCoeff);
            if (t < 0.0)
                return false;

            collided = true;

            // Interpolate positions to moment of contact for normal/contact point
            posA = startPosA + dispA * t;
            posB = startPosB + dispB * t;
            relativePosition = posB - posA;
            // |relativePosition| == minDistance by construction, so distance = minDistance
            // penetration will be 0 — velocity impulse handles separation; position
            // correction is handled because ResolveCollision moves realPosition directly
            Debug.Log($"CCD Collide: {a.id} {a.name} and {b.id} {b.name}");
        }

        if (!collided)
            return false;

        Vector3d normal = distance > 0.0001 ? relativePosition / distance : Vector3d.up;

        double penetration = minDistance - distance;
        Vector3d contactPoint = posA + normal * radiusA;

        collision = new CollisionInfo
        {
            transformA = a.transform,
            transformB = b.transform,
            rbA = a,
            rbB = b,
            contactPoint = contactPoint,
            normal = normal,
            penetration = penetration
        };

        return true;
    }

    private void ResolveCollision(CollisionInfo collision)
    {
        if (collision.rbA.isKinematic && collision.rbB.isKinematic)
            return;
        Vector3d velocityA = collision.rbA.velocity;
        Vector3d velocityB = collision.rbB.velocity;
        Vector3d relativeVelocity = velocityB - velocityA;

        double velocityAlongNormal = Vector3d.Dot(relativeVelocity, collision.normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal > 0)
            return;

        // Calculate impulse magnitude
        double invMassA = collision.rbA.isKinematic ? 0.0 : 1.0 / collision.rbA.attachedRigidbody.mass;
        double invMassB = collision.rbB.isKinematic ? 0.0 : 1.0 / collision.rbB.attachedRigidbody.mass;

        double impulseMagnitude = -(1.0 + restitution) * velocityAlongNormal / (invMassA + invMassB);

        // Apply impulses
        Vector3d impulse = collision.normal * impulseMagnitude;
        
        collision.rbA.AddForceAtPosition(-impulse, collision.contactPoint, ForceMode.Impulse);
        collision.rbB.AddForceAtPosition(impulse, collision.contactPoint, ForceMode.Impulse);

        // Resolve intersection
        const double percent = 0.8;
        const double slop = 0.001;

        double correctionMagnitude = Math.Max(collision.penetration - slop, 0.0) * percent / (invMassA + invMassB);
        Vector3d correction = correctionMagnitude * collision.normal;

        collision.rbA.scaledTransform.realPosition -= correction * invMassA;
        collision.rbB.scaledTransform.realPosition += correction * invMassB;
    }

    public struct CollisionInfo
    {
        /// <summary>
        /// This transform whose script raised the collision event
        /// </summary>
        public Transform transformA;
        /// <summary>
        /// The other transform involved in the collision
        /// </summary>
        public Transform transformB;
        /// <summary>
        /// The DoubleRigidbody of the object whose script raised the collision event, if it has one
        /// </summary>
        public DoubleRigidbody rbA;
        /// <summary>
        /// The DoubleRigidbody of the other object involved in the collision, if it has one
        /// </summary>
        public DoubleRigidbody rbB;
        /// <summary>
        /// The point of contact in world space, calculated as the point on the surface of the first object along the collision normal
        /// </summary>
        public Vector3d contactPoint;
        /// <summary>
        /// The normalized collision normal pointing from the center of A to the center of B
        /// </summary>
        public Vector3d normal;
        /// <summary>
        /// Distance needed to move the objects apart along the collision normal so they are just touching
        /// </summary>
        public double penetration;
    }

    public void ResolveCollision(DoubleRigidbody rbA, DoubleRigidbody rbB, Collision collision)
    {
        // Only manually apply impulse and position correction to active DoubleRigidbodies, regular Rigidbody handles those itself
        if ((rbA == null || !rbA.active) && (rbB == null || !rbB.active))
            return;
        bool thisRBNull = collision.thisRigidbody == null;
        bool otherRBNull = collision.rigidbody == null;
        if ((thisRBNull || collision.thisRigidbody.isKinematic) && (otherRBNull || collision.rigidbody.isKinematic))
            return;

        ContactPoint contact = collision.GetContact(0);
        Vector3d normal = contact.normal.ToVector3d();
        Vector3d contactPoint = contact.point.ToVector3d();
        Vector3d velocityA = Vector3d.zero;
        Vector3d velocityB = Vector3d.zero;
        if (rbA != null)
        {
            velocityA = rbA.velocity;
        }
        else if (!thisRBNull)
        {
            velocityA = collision.thisRigidbody.linearVelocity.ToVector3d();
        }
        if (rbB != null)
        {
            velocityB = rbB.velocity;
        }
        else if (!otherRBNull)
        {
            velocityB = collision.rigidbody.linearVelocity.ToVector3d();
        }
        Vector3d relativeVelocity = velocityB - velocityA;

        double velocityAlongNormal = Vector3d.Dot(relativeVelocity, normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal < 0)
            return;

        // Calculate impulse magnitude
        double invMassA = thisRBNull || rbA == null || rbA.isKinematic ? 0.0 : 1.0 / collision.thisRigidbody.mass;
        double invMassB = otherRBNull || rbB == null || rbB.isKinematic ? 0.0 : 1.0 / collision.rigidbody.mass;

        double impulseMagnitude = (1.0 + restitution) * velocityAlongNormal / (invMassA + invMassB);

        // Apply impulses and resolve intersection
        Vector3d impulse = normal * impulseMagnitude;

        const double percent = 0.8;
        const double slop = 0.001;

        double correctionMagnitude = Math.Max(-contact.separation - slop, 0.0) * percent / (invMassA + invMassB);
        Vector3d correction = correctionMagnitude * normal;
        
        if (rbA != null && rbA.active)
        {
            rbA.AddForceAtPosition(impulse, contactPoint, ForceMode.Impulse);
            rbA.scaledTransform.realPosition += correction * invMassA;
        }
        if (rbB != null && rbB.active)
        {
            rbB.AddForceAtPosition(-impulse, contactPoint, ForceMode.Impulse);
            rbB.scaledTransform.realPosition -= correction * invMassB;
        }
    }

    private bool CheckTrigger(DoubleRigidbody a, DoubleRigidbody b)
    {
        double radiusA = a.GetTriggerRadius();
        double radiusB = b.GetCollisionRadius();
        double minDistance = radiusA + radiusB;

        Vector3d posA = a.scaledTransform.realPosition;
        Vector3d posB = b.scaledTransform.realPosition;
        Vector3d relativePosition = posB - posA;
        double sqrDistance = relativePosition.sqrMagnitude;

        if (sqrDistance < minDistance * minDistance)
            return true;

        // Check if we need to do CCD for fast moving objects
        Vector3d velA = a.velocity;
        Vector3d velB = b.velocity;
        Vector3d relativeVelocity = velB - velA;
        double sqrDistancePerFrame = relativeVelocity.sqrMagnitude * Time.fixedDeltaTime * Time.fixedDeltaTime;
        if (sqrDistance < sqrDistancePerFrame && sqrDistancePerFrame > minDistance * minDistance)
        {
            // Continuous Collision Detection (CCD) for spheres:
            //  ((posB​−posA​)+(velB​−velA​)t).magnitude^2 = (radiusA​+radiusB​)^2
            //  (relativePos + relativeVel*t).magnitude^2 = (combinedRadius)^2
            //  (P+Vt)⋅(P+Vt) = R^2
            //  P⋅P + 2(P⋅V)t + (V⋅V)t^2 = R^2
            //  (V⋅V)t^2 + 2(P⋅V)t + (P⋅P−R^2) = 0
            // aCoef = V⋅V
            // bCoef = 2(P⋅V)
            // cCoef = P⋅P−R^2

            double aCoeff = Vector3d.Dot(relativeVelocity, relativeVelocity);
            double bCoeff = 2.0 * Vector3d.Dot(relativePosition, relativeVelocity);
            double cCoeff = Vector3d.Dot(relativePosition, relativePosition) - minDistance * minDistance;

            // Use quadratic formula to solve for t
            double discriminant = bCoeff * bCoeff - 4.0 * aCoeff * cCoeff;

            if (discriminant >= 0.0)
            {
                double sqrtD = Math.Sqrt(discriminant);

                double t0 = (-bCoeff - sqrtD) / (2.0 * aCoeff);
                double t1 = (-bCoeff + sqrtD) / (2.0 * aCoeff);

                if (t0 >= 0.0 && t0 <= Time.fixedDeltaTime)
                    return true;
                if (t1 >= 0.0 && t1 <= Time.fixedDeltaTime)
                    return true;
            }
        }
        return false;
    }

    public struct Pair
    {
        public uint idA;
        public uint idB;

        public Pair(uint idA, uint idB)
        {
            this.idA = idA;
            this.idB = idB;
        }
    }
}
