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
    public static float speedOfLight = 2.99792458e8f; // Speed of light in m/s

    public static ScaledSpacePhysics Instance { get; private set; }
    
    public event Action<ScaledRigidbody> GravityStep;

    public const double restitutionThreshold = 1.0;
    [SerializeField] private int maxGridLevels;
    [SerializeField] private double baseGridCellSize;
    [SerializeField] private int cellScalingFactor;
    public HGrid hGrid;
    private Dictionary<uint, int> colliderIndexMap = new Dictionary<uint, int>();
    private List<ScaledCollider> scaledColliders = new List<ScaledCollider>();
    private HashSet<Pair> previousCollisions = new HashSet<Pair>();

    [SerializeField] private bool logCollisions = false;
    [SerializeField] private bool logTimings = false;

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

        // Replace with collider at end of list for fast removal and maintain indices
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

    public ScaledCollider GetScaledCollider(uint id)
    {
        if (!colliderIndexMap.TryGetValue(id, out int index))
            return null;
        return scaledColliders[index];
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
        if (FloatingWorldOrigin.Instance == null || Camera.main == null)
            return;
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
            if (collider == null || !collider.enabled || collider.scaledRigidbody == null)
                continue;
            stopwatch1.Reset();
            stopwatch1.Start();
            foreach (ScaledCollider candidate in hGrid.GetCandidates(collider))
            {
                if (candidate == null || !candidate.enabled || candidate.scaledRigidbody == null)
                    continue;
                Pair collisionKey = collider.id < candidate.id ? new Pair(collider.id, candidate.id) : new Pair(candidate.id, collider.id);
                if (currentCollisions.Contains(collisionKey))
                    continue;
                // Neither are overriding Unity and are both in world space
                bool deferToUnity = !collider.overrideUnity && !candidate.overrideUnity &&
                !collider.scaledRigidbody.scaledTransform.inScaledSpace && !candidate.scaledRigidbody.scaledTransform.inScaledSpace;
                if (deferToUnity)
                {
                    // Check if either can skip over the other in one fixed update
                    Vector3d relativeVelocity = collider.scaledRigidbody.velocity - candidate.scaledRigidbody.velocity;
                    double colliderRadius = collider.GetRadius();
                    double candidateRadius = candidate.GetRadius();
                    double combinedRadius = colliderRadius + candidateRadius;
                    // double combinedRadius = collider.GetRadius() + candidate.GetRadius();
                    double sqrTravelDistance = relativeVelocity.sqrMagnitude * Time.fixedDeltaTime * Time.fixedDeltaTime;
                    deferToUnity &= sqrTravelDistance <= 4.0 * combinedRadius * combinedRadius;
                }
                
                if (deferToUnity)
                    continue; // Defer to Unity physics to handle collisions

                stopwatch2.Reset();
                stopwatch2.Start();

                if (collider.isTrigger || candidate.isTrigger)
                {
                    if (CheckTrigger(collider, candidate))
                    {
                        currentCollisions.Add(collisionKey);

                        if (!previousCollisions.Contains(collisionKey))
                        {
                            collider.scaledRigidbody.RaiseTriggerEnter(collider, candidate);
                            candidate.scaledRigidbody.RaiseTriggerEnter(candidate, collider);
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
        if (logTimings)
            Debug.Log(GameLog.ObjectLog(this, $"Full loop ticks: {fullLoopTicks}, Get candidates loop ticks: {getCandidatesTicks - collisionCheckTicks}, Check Collision ticks: {collisionCheckTicks}."));
        
        // Detect collision exits
        foreach (Pair collision in previousCollisions)
        {
            if (!currentCollisions.Contains(collision)
                && colliderIndexMap.TryGetValue(collision.idA, out int indexA)
                && colliderIndexMap.TryGetValue(collision.idB, out int indexB))
            {
                ScaledCollider colliderA = scaledColliders[indexA];
                ScaledCollider colliderB = scaledColliders[indexB];
                if (colliderA == null || colliderB == null)
                    continue;
                if (colliderA.isTrigger)
                {
                    colliderA.scaledRigidbody.RaiseTriggerExit(colliderA, colliderB);
                }
                else
                {
                    colliderA.scaledRigidbody.RaiseCollisionExit(colliderB, colliderA);
                }
                if (colliderB.isTrigger)
                {
                    colliderB.scaledRigidbody.RaiseTriggerExit(colliderB, colliderA);
                }
                else
                {
                    colliderB.scaledRigidbody.RaiseCollisionExit(colliderB, colliderA);
                }
            }
        }

        previousCollisions = currentCollisions;
    }

    private double SweepCCD(Vector3d dispA, Vector3d dispB, Vector3d relPos0, double minDistance)
    {
        // CCD: sweep from start to end
        // displacement = endPos - startPos
        // relPos0 = startPosB - startPosA
        Vector3d relDisp = dispB - dispA;

        double aCoeff   = relDisp.sqrMagnitude;

        // Reject: a and b didn't move closer to each other
        if (aCoeff <= double.Epsilon)
            return -1.0;
        
        double r0Sq     = relPos0.sqrMagnitude;
        double minDistSq = minDistance * minDistance;
        double cCoeff   = r0Sq - minDistSq;

        // Reject: already overlapping — let discrete solver handle it
        if (cCoeff < 0.0)
            return -1.0;

        double halfB    = Vector3d.Dot(relPos0, relDisp);   // bCoeff/2
        double bCoeff   = 2.0 * halfB;
        // Reject: closest approach along sweep exceeds minDistance
        // |relPos0 × relDisp|² = |relPos0|²|relDisp|² - (relPos0·relDisp)²
        double crossSq = aCoeff * r0Sq - halfB * halfB;
        if (crossSq > minDistSq * aCoeff)
            return -1.0;

        double t = SpaceMath.SolveQuadratic(aCoeff, bCoeff, cCoeff);
        if (t > 1.0)   // t > 1 = collision outside this frame
            return -1.0;
        return t;
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

        if (a.scaledRigidbody.collisionDetection == CollisionDetectionMode.Discrete && b.scaledRigidbody.collisionDetection == CollisionDetectionMode.Discrete)
            return false;

        // Retroactive sweep CCD
        double t = SweepCCD(posA - a.prevCenterPos, posB - b.prevCenterPos, b.prevCenterPos - a.prevCenterPos, minDistance);
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
        double distance;

        if (sqrDistance < minDistance * minDistance)
        {
            // Overlapping at end of frame — standard intersection
            distance = Math.Sqrt(sqrDistance);
            if (logCollisions)
                Debug.Log(GameLog.ObjectLog(this, $"Intersect Collide: {a.id} {a.isTrigger} {a.name} and {b.id} {b.isTrigger} {b.name}."));
        }
        else
        {
            if (a.scaledRigidbody.collisionDetection == CollisionDetectionMode.Discrete && b.scaledRigidbody.collisionDetection == CollisionDetectionMode.Discrete)
                return false;

            Vector3d dispA = posA - a.prevCenterPos;
            Vector3d dispB = posB - b.prevCenterPos;
            // Retroactive sweep CCD
            double t = SweepCCD(dispA, dispB, b.prevCenterPos - a.prevCenterPos, minDistance);
            if (t < 0.0)
                return false;
            posA = a.prevCenterPos + dispA * t;
            posB = b.prevCenterPos + dispB * t;
            relativePosition = posB - posA;
            distance = minDistance; // Should be safe to assume the collision is once the spheres begin touching
            if (logCollisions)
                Debug.Log(GameLog.ObjectLog(this, $"CCD Collide: {a.id} {a.isTrigger} {a.name} and {b.id} {b.isTrigger} {b.name}."));
        }

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

        Vector3d velocityA = collision.colliderA.scaledRigidbody.velocity + Vector3d.Cross(collision.colliderA.scaledRigidbody.angularVelocity.ToVector3d(), rA);
        Vector3d velocityB = collision.colliderB.scaledRigidbody.velocity + Vector3d.Cross(collision.colliderB.scaledRigidbody.angularVelocity.ToVector3d(), rB);
        Vector3d relativeVelocity = velocityB - velocityA;

        double velocityAlongNormal = Vector3d.Dot(relativeVelocity, collision.normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal > 0)
            return;

        // Calculate impulse magnitude
        double invMassA = collision.colliderA.scaledRigidbody.isKinematic ? 0.0 : 1.0 / collision.colliderA.scaledRigidbody.mass;
        double invMassB = collision.colliderB.scaledRigidbody.isKinematic ? 0.0 : 1.0 / collision.colliderB.scaledRigidbody.mass;

        float avgRestitution = Math.Abs(velocityAlongNormal) < restitutionThreshold ? 0f : (collision.colliderA.restitution + collision.colliderB.restitution) * 0.5f;
        double impulseMagnitude = -(1.0 + avgRestitution) * velocityAlongNormal / (invMassA + invMassB);

        // Apply impulses
        collision.impulse = collision.normal * impulseMagnitude;
        
        collision.colliderA.scaledRigidbody.AddForceAtPosition(-collision.impulse, collision.contactPoint, ForceMode.Impulse);
        collision.colliderB.scaledRigidbody.AddForceAtPosition(collision.impulse, collision.contactPoint, ForceMode.Impulse);

        // Pushing the less massive object to avoid things like bullets pushing giant objects around
        if (collision.colliderA.scaledRigidbody.mass < collision.colliderB.scaledRigidbody.mass)
        {
            collision.colliderA.scaledRigidbody.scaledTransform.realPosition -= collision.normal * collision.penetration;
        }
        else
        {
            collision.colliderB.scaledRigidbody.scaledTransform.realPosition += collision.normal * collision.penetration;
        }
    }

    public bool CheckSphere(Vector3d position, double radius, int layerMask, bool ignoreTriggers)
    {
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
                return true;
            }
        }
        return false;
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
