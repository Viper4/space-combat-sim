using UnityEngine;
using System.Collections.Generic;
using SpaceStuff;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Linq;

/// <summary>
/// Manages collision detection and response for objects in scaled space.
/// Treats all objects as spheres with radius = Max(realScale.x, realScale.y, realScale.z)
/// </summary>
public class ScaledSpacePhysics : MonoBehaviour
{
    public static ScaledSpacePhysics Instance { get; private set; }
    
    public event Action<DoubleRigidbody> PrePhysicsStep;

    public float restitution = 0.5f;
    
    [SerializeField] private int maxGridLevels;
    [SerializeField] private double baseGridCellSize;
    [SerializeField] private int cellScalingFactor;
    public HGrid hGrid;
    private Dictionary<uint, ScaledCollider> scaledColliders = new Dictionary<uint, ScaledCollider>();
    private HashSet<Pair> previousCollisions = new HashSet<Pair>();

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

    public void RegisterCollider(ScaledCollider collider)
    {
        if (!scaledColliders.ContainsKey(collider.id))
        {
            scaledColliders.Add(collider.id, collider);
            hGrid.UpdatePosition(collider);
        }
    }

    public void UnregisterCollider(ScaledCollider collider)
    {
        scaledColliders.Remove(collider.id);
        hGrid.Delete(collider);
    }

    public void UpdateGridSize(DoubleRigidbody rb)
    {
        foreach(ScaledCollider collider in rb.scaledColliders)
        {
            hGrid.UpdateSize(collider);
        }
    }

    public void UpdateGridPos(DoubleRigidbody rb)
    {
        foreach(ScaledCollider collider in rb.scaledColliders)
        {
            hGrid.UpdatePosition(collider);
        }
    }

    private void FixedUpdate()
    {
        HashSet<Pair> currentCollisions = new HashSet<Pair>();

        bool shiftOrigin = FloatingWorldOrigin.Instance.OverShiftThreshold();
        if (shiftOrigin)
            FloatingWorldOrigin.Instance.ShiftOrigin();
        
        // Insert all rigidbodies into hierarchical grid for collision detection, and run other logic per doubleRigidbody
        long getCandidatesTicks = 0;
        long physicsStepTicks = 0;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        ScaledCollider[] values = scaledColliders.Values.ToArray();
        foreach (ScaledCollider collider in values)
        {
            if (collider.doubleRigidbody.finishedStep)
                continue;
            collider.doubleRigidbody.ClearGravity();
            PrePhysicsStep?.Invoke(collider.doubleRigidbody);
            collider.doubleRigidbody.PhysicsStep(Time.fixedDeltaTime);
            collider.doubleRigidbody.finishedStep = true;
        }
        stopwatch.Stop();
        physicsStepTicks = stopwatch.ElapsedTicks;
        long collisionCheckTicks = 0;
        Stopwatch stopwatch1 = new Stopwatch();
        stopwatch.Reset();
        stopwatch.Start();
        foreach (ScaledCollider collider in values)
        {
            foreach (ScaledCollider candidate in hGrid.GetCandidates(collider))
            {
                // key's 1st 32-bit half is the min id uint, 2nd 32-bit half is the max id uint
                Pair collisionKey = new Pair(Math.Min(collider.id, candidate.id), Math.Max(collider.id, candidate.id));

                if ((!collider.doubleRigidbody.active && !candidate.doubleRigidbody.active) || currentCollisions.Contains(collisionKey))
                    continue;

                stopwatch1.Reset();
                stopwatch1.Start();

                if (collider.isTrigger || candidate.isTrigger)
                {
                    if (CheckTrigger(collider, candidate))
                    {
                        currentCollisions.Add(collisionKey);

                        if (!previousCollisions.Contains(collisionKey))
                        {
                            collider.doubleRigidbody.RaiseTriggerEnter(candidate);
                            candidate.doubleRigidbody.RaiseTriggerEnter(collider);
                        }
                    }
                }
                else
                {
                    if (CheckCollision(collider, candidate, out CollisionInfo collision))
                    {
                        currentCollisions.Add(collisionKey);

                        // Track new collisions
                        if (!previousCollisions.Contains(collisionKey))
                        {
                            CollisionInfo collisionInfoForB = collision;
                            // Swap A and B for the second RB's event
                            collisionInfoForB.colliderA = candidate;
                            collisionInfoForB.colliderB = collider;
                            collisionInfoForB.transformA = candidate.transform;
                            collisionInfoForB.transformB = collider.transform;
                            // Also need to invert normal for the second RB
                            collisionInfoForB.normal = -collision.normal;
                            collider.doubleRigidbody.RaiseCollisionEnter(collision);
                            candidate.doubleRigidbody.RaiseCollisionEnter(collisionInfoForB);
                        }
                        ResolveCollision(collision);
                    }
                }
                
                stopwatch1.Stop();
                collisionCheckTicks += stopwatch1.ElapsedTicks;
            }
            collider.doubleRigidbody.finishedStep = false;
        }
        stopwatch.Stop();
        getCandidatesTicks = stopwatch.ElapsedTicks;
        //Debug.Log($"Physics Step ticks: {physicsStepTicks}, Get candidates loop ticks: {getCandidatesTicks}, Check Collision ticks: {collisionCheckTicks}");

        // Detect collision exits
        foreach (Pair collision in previousCollisions)
        {
            if (!currentCollisions.Contains(collision) 
            && scaledColliders.TryGetValue(collision.idA, out var colliderA) 
            && scaledColliders.TryGetValue(collision.idB, out var colliderB))
            {
                if (colliderA.isTrigger)
                {
                    colliderA.doubleRigidbody.RaiseTriggerExit(colliderB);
                }
                else
                {
                    colliderA.doubleRigidbody.RaiseCollisionExit(colliderB);
                }
                if (colliderB.isTrigger)
                {
                    colliderB.doubleRigidbody.RaiseTriggerExit(colliderA);
                }
                else
                {
                    colliderB.doubleRigidbody.RaiseCollisionExit(colliderA);
                }
            }
        }

        previousCollisions = currentCollisions;

        Physics.SyncTransforms();
    }

    private bool CheckTrigger(ScaledCollider a, ScaledCollider b)
    {
        double radiusA = a.GetRadius();
        double radiusB = b.GetRadius();
        double minDistance = radiusA + radiusB;

        // Positions at END of frame (after PhysicsStep)
        Vector3d posA = a.GetRealCenter();
        Vector3d posB = b.GetRealCenter();
        Vector3d relativePosition = posB - posA;
        double sqrDistance = relativePosition.sqrMagnitude;

        if (sqrDistance < minDistance * minDistance)
        {
            // Overlapping at end of frame — standard intersection
            return true;
        }

        // CCD: sweep from prevPos to realPos (retroactive)
        // displacement = realPos - prevPos = what PhysicsStep just added
        Vector3d startPosA = a.doubleRigidbody.prevPos + a.GetLocalCenter();
        Vector3d startPosB = b.doubleRigidbody.prevPos + b.GetLocalCenter();
        Vector3d dispA = posA - startPosA;
        Vector3d dispB = posB - startPosB;

        // Use start-of-frame positions as the sweep origin
        Vector3d relPos0 = startPosB - startPosA;  // relative pos at frame start
        Vector3d relDisp = dispB - dispA;           // relative displacement over frame
        
        // Quick reject: objects moving apart at frame start
        double bCoeff = 2.0 * Vector3d.Dot(relPos0, relDisp);
        if (bCoeff >= 0)
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
        double cCoeff = Vector3d.Dot(relPos0, relPos0) - minDistance * minDistance;

        double t = SpaceMath.SolveQuadratic(aCoeff, bCoeff, cCoeff);
        if (t < 0.0)
            return false;

        return true;
    }

    private bool CheckCollision(ScaledCollider a, ScaledCollider b, out CollisionInfo collision)
    {
        collision = default;

        double radiusA = a.GetRadius();
        double radiusB = b.GetRadius();
        double minDistance = radiusA + radiusB;

        // Positions at END of frame (after PhysicsStep)
        Vector3d posA = a.GetRealCenter();
        Vector3d posB = b.GetRealCenter();
        Vector3d relativePosition = posB - posA;
        double sqrDistance = relativePosition.sqrMagnitude;
        double distance = minDistance;

        bool collided;

        if (sqrDistance < minDistance * minDistance)
        {
            // Overlapping at end of frame — standard intersection
            collided = true;
            distance = Math.Sqrt(sqrDistance);
            Debug.Log($"Intersect Collide: {a.id} {a.isTrigger} {a.name} and {b.id} {b.isTrigger} {b.name}");
        }
        else
        {
            // CCD: sweep from prevPos to realPos (retroactive)
            // displacement = realPos - prevPos = what PhysicsStep just added
            Vector3d startPosA = a.doubleRigidbody.prevPos + a.GetLocalCenter();
            Vector3d startPosB = b.doubleRigidbody.prevPos + b.GetLocalCenter();
            Vector3d dispA = posA - startPosA;
            Vector3d dispB = posB - startPosB;

            // Use start-of-frame positions as the sweep origin
            Vector3d relPos0 = startPosB - startPosA;  // relative pos at frame start
            Vector3d relDisp = dispB - dispA;           // relative displacement over frame
            
            // Quick reject: objects moving apart at frame start
            double bCoeff = 2.0 * Vector3d.Dot(relPos0, relDisp);
            if (bCoeff >= 0)
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
            Debug.Log($"CCD Collide: {a.id} {a.isTrigger} {a.name} and {b.id} {b.isTrigger} {b.name}");
        }

        if (!collided)
            return false;

        Vector3d normal = distance > 0.0001 ? relativePosition / distance : Vector3d.up;

        double penetration = minDistance - distance;
        Vector3d contactPoint = posA + normal * radiusA;

        collision = new CollisionInfo
        {
            colliderA = a,
            colliderB = b,
            transformA = a.transform,
            transformB = b.transform,
            contactPoint = contactPoint,
            normal = normal,
            penetration = penetration
        };

        return true;
    }

    private void ResolveCollision(CollisionInfo collision)
    {
        if (collision.colliderA.doubleRigidbody.isKinematic && collision.colliderB.doubleRigidbody.isKinematic)
            return;
        Vector3d velocityA = collision.colliderA.doubleRigidbody.velocity;
        Vector3d velocityB = collision.colliderB.doubleRigidbody.velocity;
        Vector3d relativeVelocity = velocityB - velocityA;

        double velocityAlongNormal = Vector3d.Dot(relativeVelocity, collision.normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal > 0)
            return;

        // Calculate impulse magnitude
        double invMassA = collision.colliderA.doubleRigidbody.isKinematic ? 0.0 : 1.0 / collision.colliderA.doubleRigidbody.attachedRigidbody.mass;
        double invMassB = collision.colliderB.doubleRigidbody.isKinematic ? 0.0 : 1.0 / collision.colliderB.doubleRigidbody.attachedRigidbody.mass;

        double impulseMagnitude = -(1.0 + restitution) * velocityAlongNormal / (invMassA + invMassB);

        // Apply impulses
        Vector3d impulse = collision.normal * impulseMagnitude;
        
        collision.colliderA.doubleRigidbody.AddForceAtPosition(-impulse, collision.contactPoint, ForceMode.Impulse);
        collision.colliderB.doubleRigidbody.AddForceAtPosition(impulse, collision.contactPoint, ForceMode.Impulse);

        // Resolve intersection
        const double percent = 0.2;
        const double slop = 0.01;

        double correctionMagnitude = Math.Max(collision.penetration - slop, 0.0) * percent / (invMassA + invMassB);
        Vector3d correction = correctionMagnitude * collision.normal;

        collision.colliderA.doubleRigidbody.scaledTransform.realPosition -= correction * invMassA;
        collision.colliderB.doubleRigidbody.scaledTransform.realPosition += correction * invMassB;
    }

    public List<ScaledCollider> GetOverlapSphere(Vector3d position, double radius, int layerMask, bool ignoreTriggers)
    {
        List<ScaledCollider> overlaps = new List<ScaledCollider>();
        foreach(ScaledCollider collider in hGrid.GetOverlapCandidates(position, radius))
        {
            if (ignoreTriggers && collider.isTrigger)
                continue;
            if (((1 << collider.gameObject.layer) & layerMask) == 0)
                continue;
            double minDistance = collider.GetRadius() + radius;

            Vector3d colliderPos = collider.GetRealCenter();
            Vector3d relativePosition = colliderPos - position;

            if (relativePosition.sqrMagnitude < minDistance * minDistance)
            {
                overlaps.Add(collider);
            }
        }
        return overlaps;
    }

    public struct CollisionInfo
    {
        /// <summary>
        /// The ScaledCollider of the object whose script raised the collision event, null if doesn't have one
        /// </summary>
        public ScaledCollider colliderA;

        /// <summary>
        /// The ScaledCollider of the other object involved in the collision, null if doesn't have one
        /// </summary>
        public ScaledCollider colliderB;

        /// <summary>
        /// This transform whose script raised the collision event
        /// </summary>
        public Transform transformA;

        /// <summary>
        /// The other transform involved in the collision
        /// </summary>
        public Transform transformB;

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
