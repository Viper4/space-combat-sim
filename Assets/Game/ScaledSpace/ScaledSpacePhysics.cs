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
        if (doubleRigidbodies.Count < 2)
            return;

        HashSet<(DoubleRigidbody, DoubleRigidbody)> currentCollisions = new HashSet<(DoubleRigidbody, DoubleRigidbody)>();
        HashSet<(DoubleRigidbody, DoubleRigidbody)> currentTriggers = new HashSet<(DoubleRigidbody, DoubleRigidbody)>();

        // Check all pairs for collisions
        for (int i = 0; i < doubleRigidbodies.Count; i++)
        {
            for (int j = i + 1; j < doubleRigidbodies.Count; j++)
            {
                DoubleRigidbody rbA = doubleRigidbodies[i];
                DoubleRigidbody rbB = doubleRigidbodies[j];

                // Only check collisions/triggers between objects in scaled space
                if (!rbA.scaledTransform.inScaledSpace || !rbB.scaledTransform.inScaledSpace)
                    continue;

                if (CheckCollision(rbA, rbB, out CollisionInfo collision))
                {
                    currentCollisions.Add((rbA, rbB));
                    ResolveCollision(collision);

                    // Track new collisions
                    if (!previousCollisions.Contains((rbA, rbB)))
                    {
                        rbA.RaiseCollisionEnter(rbB, collision);
                        rbB.RaiseCollisionEnter(rbA, collision);
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
        Vector3d velocityA = collision.rbA.velocity;
        Vector3d velocityB = collision.rbB.velocity;
        Vector3d relativeVelocity = velocityA - velocityB;

        double velocityAlongNormal = Vector3d.Dot(relativeVelocity, collision.normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal > 0)
            return;

        // Calculate impulse magnitude
        float impulseMagnitude = -(1 + restitution) * (float)velocityAlongNormal / 2f;

        // Apply impulses
        Vector3d impulse = collision.normal * impulseMagnitude;
        
        collision.rbA.AddForceAtPosition(impulse, collision.contactPoint, ForceMode.Impulse);
        
        collision.rbB.AddForceAtPosition(-impulse, collision.contactPoint, ForceMode.Impulse);
    }

    public struct CollisionInfo
    {
        public DoubleRigidbody rbA;
        public DoubleRigidbody rbB;
        public Vector3d contactPoint;
        public Vector3d normal;
        public double penetration;
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
