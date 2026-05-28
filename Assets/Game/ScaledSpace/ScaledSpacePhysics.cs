using UnityEngine;
using System.Collections.Generic;
using SpaceStuff;
using System;

/// <summary>
/// Manages collision detection and response for objects in scaled space.
/// Treats all objects as spheres with radius = Max(realScale.x, realScale.y, realScale.z)
/// </summary>
public class ScaledSpacePhysics : MonoBehaviour
{
    public static ScaledSpacePhysics Instance { get; private set; }
    
    public float restitution = 0.5f;
    [SerializeField] private bool debugDraw = false;
    
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
        Vector3d originShift = FloatingWorldOrigin.Instance.GetOffset();
        bool transformsDirty = shiftOrigin; // If shifting origin, will need to sync transforms after loop, otherwise only if any velocities were nonzero

        // Check all pairs for collisions
        for (int i = 0; i < doubleRigidbodies.Count; i++)
        {
            DoubleRigidbody rbA = doubleRigidbodies[i];

            doubleRigidbodies[i].PhysicsStep(Time.fixedDeltaTime);
            if (!transformsDirty && !rbA.isKinematic && rbA.active && (rbA.velocity != Vector3d.zero || rbA.angularVelocity != Vector3d.zero))
                transformsDirty = true;

            if (shiftOrigin && rbA.transform != FloatingWorldOrigin.Instance.transform && !rbA.scaledTransform.inScaledSpace && !rbA.active)
            {
                // Need to shift any world space objects that are using Unity's Rigidbody physics since they update their realPosition=transform.position
                Vector3d newWorldOriginPosition = FloatingWorldOrigin.Instance.worldOriginPosition + originShift;
                double sqrDistance = (rbA.scaledTransform.realPosition - newWorldOriginPosition).sqrMagnitude;
                // If object wont switch to scaled space after shift, shift its transform.position, otherwise let ScaledTransform.UpdateTransform() handle switch next frame
                if (sqrDistance < rbA.scaledTransform.scaledSpaceThreshold * rbA.scaledTransform.scaledSpaceThreshold)
                    rbA.transform.position -= originShift.ToVector3();
            }

            for (int j = i + 1; j < doubleRigidbodies.Count; j++)
            {
                DoubleRigidbody rbB = doubleRigidbodies[j];

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
        }
        
        // Shift origin after all objects individually shifted
        if (shiftOrigin)
            FloatingWorldOrigin.Instance.ShiftOrigin(originShift);

        if (transformsDirty)
            Physics.SyncTransforms();

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
    }

    private bool CheckCollision(DoubleRigidbody a, DoubleRigidbody b, out CollisionInfo collision)
    {
        collision = default;

        double radiusA = a.GetCollisionRadius();
        double radiusB = b.GetCollisionRadius();
        double minDistance = radiusA + radiusB;

        Vector3d posA = a.scaledTransform.realPosition;
        Vector3d posB = b.scaledTransform.realPosition;
        Vector3d offset = posB - posA;
        double sqrDistance = offset.sqrMagnitude;

        if (sqrDistance >= minDistance * minDistance)
            return false;

        // Calculate collision normal and contact point
        double distance = Math.Sqrt(sqrDistance);
        Vector3d normal = distance > 0.0001 ? offset / distance : Vector3d.up;
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
        Vector3 velocityA = Vector3.zero;
        Vector3 velocityB = Vector3.zero;
        if (rbA != null)
        {
            velocityA = rbA.velocity.ToVector3();
        }
        else if (!thisRBNull)
        {
            velocityA = collision.thisRigidbody.linearVelocity;
        }
        if (rbB != null)
        {
            velocityB = rbB.velocity.ToVector3();
        }
        else if (!otherRBNull)
        {
            velocityB = collision.rigidbody.linearVelocity;
        }
        Vector3 relativeVelocity = velocityB - velocityA;

        float velocityAlongNormal = Vector3.Dot(relativeVelocity, contact.normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal < 0)
            return;

        // Calculate impulse magnitude
        float invMassA = thisRBNull || rbA == null || rbA.isKinematic ? 0.0f : 1.0f / collision.thisRigidbody.mass;
        float invMassB = otherRBNull || rbB == null || rbB.isKinematic ? 0.0f : 1.0f / collision.rigidbody.mass;

        float impulseMagnitude = (1.0f + restitution) * velocityAlongNormal / (invMassA + invMassB);

        // Apply impulses and resolve intersection
        Vector3 impulse = contact.normal * impulseMagnitude;

        const float percent = 0.8f;
        const float slop = 0.001f;

        float correctionMagnitude = Mathf.Max(-contact.separation - slop, 0.0f) * percent / (invMassA + invMassB);
        Vector3 correction = correctionMagnitude * contact.normal;
        
        if (rbA != null && rbA.active)
        {
            rbA.AddForceAtPosition(impulse.ToVector3d(), contact.point.ToVector3d(), ForceMode.Impulse);
            rbA.scaledTransform.realPosition += correction.ToVector3d() * invMassA;
        }
        if (rbB != null && rbB.active)
        {
            rbB.AddForceAtPosition(-impulse.ToVector3d(), contact.point.ToVector3d(), ForceMode.Impulse);
            rbB.scaledTransform.realPosition -= correction.ToVector3d() * invMassB;
        }
    }

    private bool CheckTrigger(DoubleRigidbody a, DoubleRigidbody b)
    {
        double radiusA = a.scaledTriggerRadius;
        double radiusB = b.GetCollisionRadius();
        double minDistance = radiusA + radiusB;

        Vector3d posA = a.scaledTransform.realPosition;
        Vector3d posB = b.scaledTransform.realPosition;
        Vector3d offset = posB - posA;
        double sqrDistance = offset.sqrMagnitude;

        if (sqrDistance >= minDistance * minDistance)
            return false;
        return true;
    }
}
