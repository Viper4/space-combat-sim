using UnityEngine;
using SpaceStuff;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(ScaledTransform))]
public class ScaledRigidbody : MonoBehaviour
{
    public static float speedLimit = 2.99792458e8f; // Speed of light in m/s
    private static uint nextId;

    public uint id {get; private set;}
    public ScaledTransform scaledTransform {get; private set;}

    private bool _active;
    public bool active
    {
        get
        {
            return _active;
        }
        set
        {
            if (attachedRigidbody == null)
                attachedRigidbody = GetComponent<Rigidbody>();

            if (FloatingWorldOrigin.Instance != null && FloatingWorldOrigin.Instance.scaledRigidbody == this)
            {
                _active = true;
                attachedRigidbody.isKinematic = true;
                return;
            }

            if (value)
            {
                attachedRigidbody.isKinematic = true;
            }
            else
            {
                attachedRigidbody.isKinematic = false;
                if (FloatingWorldOrigin.Instance != null)
                {
                    // Floating origin stays static so need to use relative velocity
                    Vector3 relativeVelocity = (_velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity).ToVector3();
                    attachedRigidbody.linearVelocity = relativeVelocity;
                }
                attachedRigidbody.angularVelocity = _angularVelocity.ToVector3();
            }
            _active = value;
        }
    }

    [SerializeField] private double _mass;
    public double mass
    {
        get
        {
            return _mass;
        }
        set
        {
            if (value < 0.0000001)
                return;
            _mass = value;
            if (attachedRigidbody != null)
                attachedRigidbody.mass = (float)value;
        }
    }

    [SerializeField] private bool _isKinematic;
    public bool isKinematic
    {
        get
        {
            return _isKinematic;
        }
        set
        {
            _isKinematic = value;
            attachedRigidbody.isKinematic = value;
        }
    }

    public Rigidbody attachedRigidbody {get; private set;}

    [SerializeField] private Vector3d _velocity;
    public Vector3d velocity
    {
        get
        {
            return _velocity;
        }
        set
        {
            if (value.sqrMagnitude > speedLimit * speedLimit)
                return;

            _velocity = value;
            if (!_active && FloatingWorldOrigin.Instance != null)
            {
                Vector3 relativeVelocity = (_velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity).ToVector3();
                attachedRigidbody.linearVelocity = relativeVelocity;
            }
        }
    }

    [SerializeField] private Vector3d _angularVelocity;
    public Vector3d angularVelocity
    {
        get
        {
            if (_active)
            {
                return _angularVelocity;
            }
            else
            {
                return attachedRigidbody.angularVelocity.ToVector3d();
            }
        }
        set
        {
            if (value.sqrMagnitude > speedLimit * speedLimit)
                return;

            _angularVelocity = value;
            if (!_active)
            {
                attachedRigidbody.angularVelocity = value.ToVector3();
            }
        }
    }
    public bool affectedByGravity = true;
    [SerializeField] private Vector3d gravityAcceleration;

    /// <summary>
    /// Event fired when this RB enters collision with another RB in scaled space
    /// </summary>
    public event Action<ScaledSpacePhysics.CollisionInfo> OnScaledCollisionEnter;
    
    /// <summary>
    /// Event fired when this RB exits collision with another RB in scaled space
    /// </summary>
    public event Action<ScaledCollider> OnScaledCollisionExit;

    /// <summary>
    /// Event fired when other RB enters this RB's trigger
    /// </summary>
    public event Action<ScaledCollider> OnScaledTriggerEnter;
    
    /// <summary>
    /// Event fired when other RB exits this RB's trigger
    /// </summary>
    public event Action<ScaledCollider> OnScaledTriggerExit;

    public List<ScaledCollider> scaledColliders {get; private set;}
    public Vector3d prevPos;

    private void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        attachedRigidbody = GetComponent<Rigidbody>();
        scaledColliders = new List<ScaledCollider>();
        prevPos = scaledTransform.realPosition;
        id = nextId++;

        if (_mass < 0.0000001)
            mass = 0.0000001;
    }

    private void OnValidate()
    {
        if (_mass < 0.0000001)
            mass = 0.0000001;
    }

    private void OnDestroy()
    {
        // Clear all event listeners
        OnScaledCollisionEnter = null;
        OnScaledCollisionExit = null;
        OnScaledTriggerEnter = null;
        OnScaledTriggerExit = null;
    }

    public void AddCollider(ScaledCollider newCollider)
    {
        foreach(ScaledCollider collider in scaledColliders)
        {
            collider.IgnoreCollider(newCollider.id, true);
        }
        scaledColliders.Add(newCollider);
    }

    public void AddGravity(Vector3d gravity)
    {
        gravityAcceleration += gravity;
    }

    public Vector3d GetGravity()
    {
        return gravityAcceleration;
    }

    private void FixedUpdate()
    {
        if (_isKinematic || ScaledSpacePhysics.Instance == null)
            return;

        gravityAcceleration = Vector3d.zero;
        if (affectedByGravity)
        {
            ScaledSpacePhysics.Instance.InvokeGravityStep(this);
            AddForce(gravityAcceleration, ForceMode.Acceleration);
        }

        double sqrAngularSpeed = _angularVelocity.sqrMagnitude;
        if (_active)
        {
            scaledTransform.realPosition += _velocity * Time.fixedDeltaTime;

            if (sqrAngularSpeed > 0.00001)
            {
                double angularSpeed = Math.Sqrt(sqrAngularSpeed);
                double angle = angularSpeed * Time.fixedDeltaTime;
                Vector3 axis = (_angularVelocity / angularSpeed).ToVector3();
                Quaternion delta = Quaternion.AngleAxis((float)(angle * Mathf.Rad2Deg), axis);
                transform.rotation = delta * transform.rotation;
            }
        }

        if (_velocity.sqrMagnitude > 0.0001 || sqrAngularSpeed > 0.0001)
        {
            ScaledSpacePhysics.Instance.UpdateGridPos(this);
        }
        prevPos = scaledTransform.realPosition;
    }

    public void AddForce(Vector3d force, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        Vector3d newVelocity = forceMode switch
        {
            ForceMode.Impulse => _velocity + (force / _mass),
            ForceMode.VelocityChange => _velocity + force,
            ForceMode.Acceleration => _velocity + (force * Time.fixedDeltaTime),
            _ => _velocity + (force * (Time.fixedDeltaTime / _mass)),
        };
        if (newVelocity.sqrMagnitude < speedLimit * speedLimit)
        {
            _velocity = newVelocity;
            if (!_active && FloatingWorldOrigin.Instance != null)
            {
                Vector3 relativeVelocity = (_velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity).ToVector3();
                attachedRigidbody.linearVelocity = relativeVelocity;
            }
        }
    }

    public void AddRelativeForce(Vector3d force, ForceMode forceMode)
    {
        if (_isKinematic)
            return;
        double globalX = transform.right.x * force.x + transform.up.x * force.y + transform.forward.x * force.z;
        double globalY = transform.right.y * force.x + transform.up.y * force.y + transform.forward.y * force.z;
        double globalZ = transform.right.z * force.x + transform.up.z * force.y + transform.forward.z * force.z;
        AddForce(new Vector3d(globalX, globalY, globalZ), forceMode);
    }

    public void AddTorque(Vector3d torque, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        if (_active)
        {
            Quaternion tensorRot = transform.rotation * attachedRigidbody.inertiaTensorRotation;
            Vector3 localTorque = Quaternion.Inverse(tensorRot) * torque.ToVector3();
            Vector3 inertia = attachedRigidbody.inertiaTensor;

            Vector3 localAngularAccel = forceMode switch
            {
                ForceMode.Impulse => new Vector3(
                    localTorque.x / inertia.x,
                    localTorque.y / inertia.y,
                    localTorque.z / inertia.z),
                ForceMode.VelocityChange => localTorque,
                ForceMode.Acceleration => localTorque * Time.fixedDeltaTime,
                _ => new Vector3(
                    localTorque.x * Time.fixedDeltaTime / inertia.x, 
                    localTorque.y * Time.fixedDeltaTime / inertia.y, 
                    localTorque.z * Time.fixedDeltaTime / inertia.z)
            };
            Vector3 worldAngularAccel = tensorRot * localAngularAccel;

            Vector3d newVelocity = _angularVelocity + worldAngularAccel.ToVector3d();

            if (newVelocity.sqrMagnitude < speedLimit * speedLimit)
            {
                _angularVelocity = newVelocity;
            }
        }
        else
        {
            attachedRigidbody.AddTorque(torque.ToVector3(), forceMode);
        }
    }

    public void AddRelativeTorque(Vector3d torque, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        if (_active)
        {
            double globalX = transform.right.x * torque.x + transform.up.x * torque.y + transform.forward.x * torque.z;
            double globalY = transform.right.y * torque.x + transform.up.y * torque.y + transform.forward.y * torque.z;
            double globalZ = transform.right.z * torque.x + transform.up.z * torque.y + transform.forward.z * torque.z;
            AddTorque(new Vector3d(globalX, globalY, globalZ), forceMode);
        }
        else
        {
            attachedRigidbody.AddRelativeTorque(torque.ToVector3(), forceMode);
        }
    }

    public void AddForceAtPosition(Vector3d force, Vector3d position, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        Vector3d center = scaledTransform.realPosition;

        AddForce(force, forceMode);

        // Torque = r x F
        Vector3d leverArm = position - center;

        Vector3d torque = new Vector3d(
            leverArm.y * force.z - leverArm.z * force.y,
            leverArm.z * force.x - leverArm.x * force.z,
            leverArm.x * force.y - leverArm.y * force.x
        );

        AddTorque(torque, forceMode);
    }

    public void AddExplosionForce(float explosionForce, Vector3d explosionPosition, float explosionRadius, float upwardsModifier = 0f, ForceMode forceMode = ForceMode.Force)
    {
        if (_isKinematic)
            return;

        Vector3d center = scaledTransform.realPosition;

        Vector3d adjustedExplosionPos = explosionPosition - Vector3d.up * upwardsModifier;

        Vector3d offset = center - adjustedExplosionPos;

        double distance = offset.magnitude;

        if (distance < 0.0001)
        {
            offset = transform.up.ToVector3d();
            distance = 0;
        }
        else
        {
            offset /= distance;
        }

        double attenuation = explosionRadius <= 0 ? 1.0 : Math.Max(0.0, 1.0 - distance / explosionRadius);

        if (attenuation <= 0)
            return;

        Vector3d force = offset * (explosionForce * attenuation);

        // Apply at explosion center to generate torque
        AddForceAtPosition(force, adjustedExplosionPos, forceMode);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision enter event
    /// </summary>
    internal void RaiseCollisionEnter(ScaledSpacePhysics.CollisionInfo collision)
    {
        OnScaledCollisionEnter?.Invoke(collision);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision exit event
    /// </summary>
    internal void RaiseCollisionExit(ScaledCollider other)
    {
        OnScaledCollisionExit?.Invoke(other);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision enter event
    /// </summary>
    internal void RaiseTriggerEnter(ScaledCollider other)
    {
        OnScaledTriggerEnter?.Invoke(other);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision exit event
    /// </summary>
    internal void RaiseTriggerExit(ScaledCollider other)
    {
        OnScaledTriggerExit?.Invoke(other);
    }

    public void IgnoreScaledRigidbody(ScaledRigidbody other, bool ignore)
    {
        foreach (ScaledCollider collider in scaledColliders)
        {
            foreach (ScaledCollider otherCollider in other.scaledColliders)
            {
                collider.IgnoreCollider(otherCollider.id, ignore);
            }
        }
    }

    public void DestroyScaledColliders()
    {
        for(int i = scaledColliders.Count - 1; i >= 0; i--)
        {
            Destroy(scaledColliders[i].gameObject);
        }
    }

    public void EnableScaledColliders(bool value)
    {
        for(int i = 0; i < scaledColliders.Count; i++)
        {
            scaledColliders[i].enabled = value;
        }
    }

    private void ResolveCollision(Vector3d contactPoint, Vector3d relativeVelocity, Vector3d normal, double invMassB, float restitutionB)
    {
        Vector3 contactRenderPoint = FloatingWorldOrigin.Instance.scaledTransform.TransformRealPoint(contactPoint);

        Debug.DrawRay(contactRenderPoint, normal.ToVector3() * 10, Color.purple);
        Debug.DrawRay(contactRenderPoint, relativeVelocity.ToVector3(), Color.orange);

        double velocityAlongNormal = Vector3d.Dot(relativeVelocity, normal);

        // Don't resolve if velocities are separating
        if (velocityAlongNormal >= 0.0)
            return;

        // Calculate impulse magnitude
        double invMassA = isKinematic ? 0.0 : 1.0 / _mass;

        float avgRestitution = Math.Abs(velocityAlongNormal) < ScaledSpacePhysics.restitutionThreshold ? 0f : scaledColliders.Count > 0 ? (scaledColliders[0].restitution + restitutionB) * 0.5f : restitutionB;
        double impulseMagnitude = -(1.0 + avgRestitution) * velocityAlongNormal / (invMassA + invMassB);

        // Apply impulses
        Vector3d impulse = normal * impulseMagnitude;
        Debug.Log($"Applying unity collision impulse of: {impulse} magnitude {impulseMagnitude}");
        AddForceAtPosition(impulse, contactPoint, ForceMode.Impulse);
        Debug.DrawRay(contactRenderPoint, impulse.ToVector3(), Color.cyan);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isKinematic || !_active)
            return;

        // Need to handle collisions between unity colliders and scaled rigidbodies
        ContactPoint contact = collision.GetContact(0);
        Vector3d realContactPoint = scaledTransform.TransformRenderPoint(contact.point);

        // Rigidbody otherRB = collision.rigidbody;

        // Vector3d relativeVelocity;
        // double massB;
        // float restitutionB;
        // if (otherRB == null)
        // {
        //     relativeVelocity = -_velocity;
        //     massB = 1E6;
        //     restitutionB = 0.5f;
        // }
        // else
        // {
        //     massB = otherRB.mass;
        //     if (collision.transform.TryGetComponent<ScaledCollider>(out var otherScaledCollider))
        //     {
        //         relativeVelocity = otherScaledCollider.scaledRigidbody.velocity - _velocity;
        //         restitutionB = otherScaledCollider.restitution;
        //         massB = otherScaledCollider.scaledRigidbody.mass;
        //     }
        //     else if (FloatingWorldOrigin.Instance.scaledRigidbody.id == id)
        //     {
        //         relativeVelocity = otherRB.linearVelocity.ToVector3d();
        //         restitutionB = 0.5f;
        //     }
        //     else
        //     {
        //         relativeVelocity = otherRB.linearVelocity.ToVector3d() + FloatingWorldOrigin.Instance.scaledRigidbody._velocity - _velocity;
        //         restitutionB = 0.5f;
        //     }
        // }
        if (contact.separation < 0)
        {
            Vector3d correctionDirection = scaledTransform.realPosition - realContactPoint;
            scaledTransform.realPosition += correctionDirection.normalized * (-contact.separation * 2.0f);
        }

        // AddForce(collision.impulse.ToVector3d() / mass, ForceMode.Impulse);
        AddForceAtPosition(collision.impulse.ToVector3d() / mass, realContactPoint, ForceMode.Impulse);
    }
}
