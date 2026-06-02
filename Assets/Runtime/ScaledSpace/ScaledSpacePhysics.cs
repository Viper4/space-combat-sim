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
    private List<DoubleRigidbody> doubleRigidbodies = new List<DoubleRigidbody>();
    private HashSet<(DoubleRigidbody, DoubleRigidbody)> previousCollisions = new HashSet<(DoubleRigidbody, DoubleRigidbody)>();
    private HashSet<(DoubleRigidbody, DoubleRigidbody)> previousTriggers = new HashSet<(DoubleRigidbody, DoubleRigidbody)>();

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

    public void RegisterDoubleRigidbody(DoubleRigidbody doubleRB)
    {
        if (!doubleRigidbodies.Contains(doubleRB))
        {
            doubleRigidbodies.Add(doubleRB);
        }
    }

    public void UnregisterDoubleRigidbody(DoubleRigidbody doubleRB)
    {
        doubleRigidbodies.Remove(doubleRB);
    }

    private void FixedUpdate()
    {
        HashSet<(DoubleRigidbody, DoubleRigidbody)> currentCollisions = new HashSet<(DoubleRigidbody, DoubleRigidbody)>();
        HashSet<(DoubleRigidbody, DoubleRigidbody)> currentTriggers = new HashSet<(DoubleRigidbody, DoubleRigidbody)>();

        bool shiftOrigin = FloatingWorldOrigin.Instance.OverShiftThreshold();
        if (shiftOrigin)
            FloatingWorldOrigin.Instance.ShiftOrigin();
        
        bool transformsDirty = shiftOrigin; // If shifting origin, will need to sync transforms after loop, otherwise only if any velocities were nonzero
        hGrid.Clear();
        // Insert all rigidbodies into hierarchical grid for collision detection, and run other logic per doubleRigidbody
        for (int i = 0; i < doubleRigidbodies.Count; i++)
        {
            DoubleRigidbody rb = doubleRigidbodies[i];
            rb.ClearGravity();
            PrePhysicsStep?.Invoke(rb);
            rb.PhysicsStep(Time.fixedDeltaTime);

            hGrid.Insert(rb);

            if (!transformsDirty && rb.active && !rb.isKinematic && rb.velocity.sqrMagnitude > 0.0001)
                transformsDirty = true;
        }

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        IEnumerable<(DoubleRigidbody, DoubleRigidbody)> candidates = hGrid.GetCandidatePairs();
        stopwatch.Stop();
        long getCandidatesTicks = stopwatch.ElapsedTicks;
        int num = 0;
        stopwatch.Reset();
        stopwatch.Start();
        foreach ((DoubleRigidbody rbA, DoubleRigidbody rbB) in candidates)
        {
            num++;
            // Only check collisions/triggers between objects not handled by Unity physics
            if (!rbA.active && !rbB.active)
                continue;

            if (CheckCollision(rbA, rbB, out CollisionInfo collision))
            {
                currentCollisions.Add((rbA, rbB));
                ResolveCollision(collision);

                // Track new collisions
                if (!previousCollisions.Contains((rbA, rbB)))
                {
                    CollisionInfo collisionInfoForB = collision;
                    // Swap A and B for the second RB's event
                    collisionInfoForB.rbA = rbB;
                    collisionInfoForB.rbB = rbA;
                    collisionInfoForB.transformA = rbB.transform;
                    collisionInfoForB.transformB = rbA.transform;
                    // Also need to invert normal for the second RB
                    collisionInfoForB.normal = -collision.normal;
                    rbA.RaiseCollisionEnter(collision);
                    rbB.RaiseCollisionEnter(collisionInfoForB);
                }
            }

            if (rbA.trackTrigger && CheckTrigger(rbA, rbB))
            {
                currentTriggers.Add((rbA, rbB));
                if (!previousTriggers.Contains((rbA, rbB)))
                    rbA.RaiseTriggerEnter(rbB);
            }
            if (rbB.trackTrigger && CheckTrigger(rbB, rbA))
            {
                currentTriggers.Add((rbB, rbA));
                if (!previousTriggers.Contains((rbB, rbA)))
                    rbB.RaiseTriggerEnter(rbA);
            }
        }
        stopwatch.Stop();
        long loopTicks = stopwatch.ElapsedTicks;
        Debug.Log($"pairs: {num}. GetCandidates ticks: {getCandidatesTicks}. Loop ticks: {loopTicks}");
        // Detect collision exits
        foreach (var previousCollision in previousCollisions)
        {
            if (!currentCollisions.Contains(previousCollision))
            {
                previousCollision.Item1.RaiseCollisionExit(previousCollision.Item2);
                previousCollision.Item2.RaiseCollisionExit(previousCollision.Item1);
            }
        }

        // Detect trigger exits
        foreach (var previousTrigger in previousTriggers)
        {
            if (!currentTriggers.Contains(previousTrigger))
            {
                previousTrigger.Item1.RaiseTriggerExit(previousTrigger.Item2);
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

        Vector3d posA = a.scaledTransform.realPosition;
        Vector3d posB = b.scaledTransform.realPosition;
        Vector3d relativePosition = posB - posA;
        double sqrDistance = relativePosition.sqrMagnitude;
        double distance = minDistance;

        bool collided = false;
        if (sqrDistance < minDistance * minDistance)
        {
            collided = true;
            distance = Math.Sqrt(sqrDistance);
            Debug.Log($"Intersect collide: {a.name} with {b.name}");
        }
        else
        {
            // Check if the velocities of A and B can cause them to skip over each other in one frame
            Vector3d velA = a.velocity;
            Vector3d velB = b.velocity;
            Vector3d relativeVelocity = velB - velA;
            double sqrDistancePerFrame = relativeVelocity.sqrMagnitude * Time.fixedDeltaTime * Time.fixedDeltaTime;
            if (sqrDistancePerFrame > minDistance * minDistance)
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

                    double t = double.PositiveInfinity;
                    if (t0 >= 0.0 && t0 <= Time.fixedDeltaTime)
                        t = t0;
                    if (t1 >= 0.0 && t1 <= Time.fixedDeltaTime)
                        t = Math.Min(t, t1);

                    if (!double.IsInfinity(t))
                    {
                        collided = true;

                        posA += velA * t;
                        posB += velB * t;

                        relativePosition = posB - posA;
                        // distance is already = minDistance
                        Debug.Log($"CCD collide: {a.name} with {b.name}");
                    }
                }
            }
        }

        if (!collided)
            return false;

        // Calculate collision normal and contact point
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

        if (debugDraw)
        {
            Debug.DrawLine(posA.ToVector3(), posB.ToVector3(), Color.red);
            Debug.DrawLine(contactPoint.ToVector3(), (contactPoint + normal * 10).ToVector3(), Color.green);
        }

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
}
