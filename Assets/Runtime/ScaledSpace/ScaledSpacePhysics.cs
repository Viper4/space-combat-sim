using UnityEngine;
using System.Collections.Generic;
using SpaceStuff;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Collections;

/// <summary>
/// Manages collision detection and response for objects in scaled space.
/// Treats all objects as spheres
/// </summary>
public class ScaledSpacePhysics : MonoBehaviour
{
    public static ScaledSpacePhysics Instance { get; private set; }
    
    public event Action<ScaledRigidbody> GravityStep;

    public const double percent = 0.2;
    public const double slop = 0.01;
    public const double restitutionThreshold = 1.0;
    
    [SerializeField] private int maxGridLevels;
    [SerializeField] private double baseGridCellSize;
    [SerializeField] private int cellScalingFactor;
    public HGrid hGrid;
    private Dictionary<uint, int> colliderIndexMap = new Dictionary<uint, int>();
    private List<ScaledCollider> scaledColliders = new List<ScaledCollider>();
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
        if (colliderIndexMap.ContainsKey(collider.id))
            return;
        colliderIndexMap.Add(collider.id, scaledColliders.Count);
        scaledColliders.Add(collider);
        hGrid.UpdatePosition(collider);
    }

    private IEnumerator WaitToRemoveCollider(ScaledCollider collider)
    {
        yield return new WaitForEndOfFrame(); // Avoid changing the scaledColliders while iterating it
        int index = colliderIndexMap[collider.id];

        int lastIndex = scaledColliders.Count - 1;

        ScaledCollider last = scaledColliders[lastIndex];

        // Swap collider to remove to end of list for fast removal and maintain indices
        scaledColliders[index] = last;
        colliderIndexMap[last.id] = index;

        scaledColliders.RemoveAt(lastIndex);
        colliderIndexMap.Remove(collider.id);
        hGrid.Delete(collider);
    }

    public void UnregisterCollider(ScaledCollider collider)
    {
        if (this == null || !colliderIndexMap.ContainsKey(collider.id))
            return;

        StartCoroutine(WaitToRemoveCollider(collider));
    }

    public void UpdateGridSize(ScaledRigidbody rb)
    {
        foreach(ScaledCollider collider in rb.scaledColliders)
        {
            hGrid.UpdateSize(collider);
        }
    }

    public void UpdateGridPos(ScaledRigidbody rb)
    {
        foreach(ScaledCollider collider in rb.scaledColliders)
        {
            hGrid.UpdatePosition(collider);
        }
    }

    public void InvokeGravityStep(ScaledRigidbody scaledRigidbody)
    {
        GravityStep?.Invoke(scaledRigidbody);
    }

    private void FixedUpdate()
    {
        HashSet<Pair> currentCollisions = new HashSet<Pair>();

        // Run logic per ScaledRigidbody
        Stopwatch stopwatch = new Stopwatch();
        Stopwatch stopwatch1 = new Stopwatch();
        Stopwatch stopwatch2 = new Stopwatch();
        long fullLoopTicks = 0;
        long getCandidatesTicks = 0;
        long collisionCheckTicks = 0;

        stopwatch.Reset();
        stopwatch.Start();
        foreach (ScaledCollider collider in scaledColliders)
        {
            stopwatch1.Reset();
            stopwatch1.Start();
            foreach (ScaledCollider candidate in hGrid.GetCandidates(collider))
            {
                Pair collisionKey = collider.id < candidate.id ? new Pair(collider.id, candidate.id) : new Pair(candidate.id, collider.id);
                if ((!collider.scaledRigidbody.active && !candidate.scaledRigidbody.active) || currentCollisions.Contains(collisionKey))
                    continue;
                bool usingUnity = !collider.overrideUnity && !candidate.overrideUnity && (!collider.scaledRigidbody.active || !candidate.scaledRigidbody.active);
                bool bothInWorldSpace = !collider.scaledRigidbody.scaledTransform.inScaledSpace && !candidate.scaledRigidbody.scaledTransform.inScaledSpace;
                if (usingUnity && bothInWorldSpace)
                    continue; // If either collider is using Unity rigidbody and both are in world space, should defer to unity physics to handle collisions

                stopwatch2.Reset();
                stopwatch2.Start();

                if (collider.isTrigger || candidate.isTrigger)
                {
                    if (CheckTrigger(collider, candidate))
                    {
                        currentCollisions.Add(collisionKey);

                        if (!previousCollisions.Contains(collisionKey))
                        {
                            collider.scaledRigidbody.RaiseTriggerEnter(candidate);
                            candidate.scaledRigidbody.RaiseTriggerEnter(collider);
                        }
                    }
                }
                else
                {
                    if (CheckCollision(collider, candidate, out CollisionInfo collision))
                    {
                        currentCollisions.Add(collisionKey);
                        ResolveCollision(ref collision);

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
                            collider.scaledRigidbody.RaiseCollisionEnter(collision);
                            candidate.scaledRigidbody.RaiseCollisionEnter(collisionInfoForB);
                        }
                    }
                }
                
                stopwatch2.Stop();
                collisionCheckTicks += stopwatch2.ElapsedTicks;
            }
            stopwatch1.Stop();
            getCandidatesTicks += stopwatch1.ElapsedTicks;
        }
        stopwatch.Stop();
        fullLoopTicks = stopwatch.ElapsedTicks;
        // Debug.Log($"Full loop ticks: {fullLoopTicks}, Get candidates loop ticks: {getCandidatesTicks - collisionCheckTicks}, Check Collision ticks: {collisionCheckTicks}");

        // Detect collision exits
        foreach (Pair collision in previousCollisions)
        {
            if (!currentCollisions.Contains(collision)
                && colliderIndexMap.TryGetValue(collision.idA, out int indexA)
                && colliderIndexMap.TryGetValue(collision.idB, out int indexB))
            {
                ScaledCollider colliderA = scaledColliders[indexA];
                ScaledCollider colliderB = scaledColliders[indexB];
                if (colliderA.isTrigger)
                {
                    colliderA.scaledRigidbody.RaiseTriggerExit(colliderB);
                }
                else
                {
                    colliderA.scaledRigidbody.RaiseCollisionExit(colliderB);
                }
                if (colliderB.isTrigger)
                {
                    colliderB.scaledRigidbody.RaiseTriggerExit(colliderA);
                }
                else
                {
                    colliderB.scaledRigidbody.RaiseCollisionExit(colliderA);
                }
            }
        }

        previousCollisions = currentCollisions;
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
        Vector3d startPosA = a.scaledRigidbody.prevPos + a.GetLocalCenter();
        Vector3d startPosB = b.scaledRigidbody.prevPos + b.GetLocalCenter();
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

        // Positions at END of frame (after ScaledRigidbody.FixedUpdate)
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
            Debug.Log($"[ScaledSpacePhysics] Intersect Collide: {a.id} {a.isTrigger} {a.name} and {b.id} {b.isTrigger} {b.name}.");
        }
        else
        {
            // CCD: sweep from prevPos to realPos (retroactive)
            // displacement = realPos - prevPos = what ScaledRigidbody.FixedUpdate just added
            Vector3d startPosA = a.scaledRigidbody.prevPos + a.GetLocalCenter();
            Vector3d startPosB = b.scaledRigidbody.prevPos + b.GetLocalCenter();
            Vector3d dispA = posA - startPosA;
            Vector3d dispB = posB - startPosB;

            Vector3d relPos0 = startPosB - startPosA;
            Vector3d relDisp = dispB - dispA;

            double aCoeff   = Vector3d.Dot(relDisp, relDisp);
            double halfB    = Vector3d.Dot(relPos0, relDisp);   // bCoeff/2
            double bCoeff   = 2.0 * halfB;
            double r0Sq     = Vector3d.Dot(relPos0, relPos0);
            double minDistSq = minDistance * minDistance;
            double cCoeff   = r0Sq - minDistSq;

            // Reject: separating at frame start
            if (bCoeff >= 0.0)
                return false;

            // Reject: already overlapping — let discrete solver handle it
            if (cCoeff < 0.0)
                return false;

            // Reject: closest approach along sweep exceeds minDistance (no sqrt needed)
            // |relPos0 × relDisp|² = |relPos0|²|relDisp|² - (relPos0·relDisp)²
            double crossSq = aCoeff * r0Sq - halfB * halfB;
            if (crossSq > minDistSq * aCoeff)
                return false;

            double t = SpaceMath.SolveQuadratic(aCoeff, bCoeff, cCoeff);
            if (t < 0.0 || t > 1.0)   // t > 1 = collision outside this frame
                return false;

            collided = true;
            posA = startPosA + dispA * t;
            posB = startPosB + dispB * t;
            relativePosition = posB - posA;

            Debug.Log($"[ScaledSpacePhysics] CCD Collide: {a.id} {a.isTrigger} {a.name} and {b.id} {b.isTrigger} {b.name}.");
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

    private void ResolveCollision(ref CollisionInfo collision)
    {
        if (collision.colliderA.scaledRigidbody.isKinematic && collision.colliderB.scaledRigidbody.isKinematic)
            return;
        Vector3d rA = collision.contactPoint - collision.colliderA.scaledRigidbody.scaledTransform.realPosition;
        Vector3d rB = collision.contactPoint - collision.colliderB.scaledRigidbody.scaledTransform.realPosition;

        Vector3d velocityA = collision.colliderA.scaledRigidbody.velocity + Vector3d.Cross(collision.colliderA.scaledRigidbody.angularVelocity, rA);
        Vector3d velocityB = collision.colliderB.scaledRigidbody.velocity + Vector3d.Cross(collision.colliderB.scaledRigidbody.angularVelocity, rB);
        Vector3d relativeVelocity = velocityB - velocityA;

        double velocityAlongNormal = Vector3d.Dot(relativeVelocity, collision.normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal > 0)
            return;

        // Calculate impulse magnitude
        double invMassA = collision.colliderA.scaledRigidbody.isKinematic ? 0.0 : 1.0 / collision.colliderA.scaledRigidbody.attachedRigidbody.mass;
        double invMassB = collision.colliderB.scaledRigidbody.isKinematic ? 0.0 : 1.0 / collision.colliderB.scaledRigidbody.attachedRigidbody.mass;

        float avgRestitution = Math.Abs(velocityAlongNormal) < restitutionThreshold ? 0f : (collision.colliderA.restitution + collision.colliderB.restitution) * 0.5f;
        double impulseMagnitude = -(1.0 + avgRestitution) * velocityAlongNormal / (invMassA + invMassB);

        // Apply impulses
        collision.impulse = collision.normal * impulseMagnitude;
        
        collision.colliderA.scaledRigidbody.AddForceAtPosition(-collision.impulse, collision.contactPoint, ForceMode.Impulse);
        collision.colliderB.scaledRigidbody.AddForceAtPosition(collision.impulse, collision.contactPoint, ForceMode.Impulse);

        // Resolve intersection
        double correctionMagnitude = Math.Max(collision.penetration - slop, 0.0) * percent / (invMassA + invMassB);
        Vector3d correction = correctionMagnitude * collision.normal;

        collision.colliderA.scaledRigidbody.scaledTransform.realPosition -= correction * invMassA;
        collision.colliderB.scaledRigidbody.scaledTransform.realPosition += correction * invMassB;
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

        /// <summary>
        /// The impulse of the collision relative to A (impulse force applied to B).
        /// </summary>
        public Vector3d impulse;
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
