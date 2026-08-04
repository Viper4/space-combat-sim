using UnityEngine;
using SpaceStuff;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(ScaledTransform))]
public class ScaledRigidbody : MonoBehaviour
{
    private static uint nextId;

    public uint id {get; private set;}
    public ScaledTransform scaledTransform {get; private set;}

    [SerializeField] private bool freezePositionX;
    [SerializeField] private bool freezePositionY;
    [SerializeField] private bool freezePositionZ;
    [SerializeField] private bool freezeRotationX;
    [SerializeField] private bool freezeRotationY;
    [SerializeField] private bool freezeRotationZ;

    public RigidbodyConstraints constraints
    {
        get
        {
            RigidbodyConstraints result = RigidbodyConstraints.None;

            if (freezePositionX)
                result |= RigidbodyConstraints.FreezePositionX;
            if (freezePositionY)
                result |= RigidbodyConstraints.FreezePositionY;
            if (freezePositionZ)
                result |= RigidbodyConstraints.FreezePositionZ;

            if (freezeRotationX)
                result |= RigidbodyConstraints.FreezeRotationX;
            if (freezeRotationY)
                result |= RigidbodyConstraints.FreezeRotationY;
            if (freezeRotationZ)
                result |= RigidbodyConstraints.FreezeRotationZ;

            return result;
        }
        set
        {
            freezePositionX = (value & RigidbodyConstraints.FreezePositionX) != 0;
            freezePositionY = (value & RigidbodyConstraints.FreezePositionY) != 0;
            freezePositionZ = (value & RigidbodyConstraints.FreezePositionZ) != 0;

            freezeRotationX = (value & RigidbodyConstraints.FreezeRotationX) != 0;
            freezeRotationY = (value & RigidbodyConstraints.FreezeRotationY) != 0;
            freezeRotationZ = (value & RigidbodyConstraints.FreezeRotationZ) != 0;

            if (attachedRigidbody != null)
                attachedRigidbody.constraints = value;
        }
    }
    [SerializeField, Tooltip("Always use ScaledRigidbody if true.")] private bool overrideUnity;
    private bool collidersEnabled = true;

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

            if (overrideUnity || (FloatingWorldOrigin.Instance != null && FloatingWorldOrigin.Instance.scaledRigidbody == this))
            {
                _active = true;
                attachedRigidbody.constraints = RigidbodyConstraints.FreezePosition | constraints;
                return;
            }

            _active = value;
            UpdateActive();
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
            if (freezePositionX)
            {
                value.x = 0.0;
            }
            if (freezePositionY)
            {
                value.y = 0.0;
            }
            if (freezePositionZ)
            {
                value.z = 0.0;
            }
            if (value.sqrMagnitude > ScaledSpacePhysics.speedOfLight * ScaledSpacePhysics.speedOfLight)
            {
                _velocity = Vector3d.ClampMagnitude(value, ScaledSpacePhysics.speedOfLight);
                if (!_active && FloatingWorldOrigin.Instance != null)
                {
                    Vector3 relativeVelocity = (_velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity).ToVector3();
                    attachedRigidbody.linearVelocity = relativeVelocity;
                }
                return;
            }

            _velocity = value;
            if (!_active && FloatingWorldOrigin.Instance != null)
            {
                Vector3 relativeVelocity = (_velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity).ToVector3();
                attachedRigidbody.linearVelocity = relativeVelocity;
            }
        }
    }

    public Vector3 angularVelocity
    {
        get
        {
            return attachedRigidbody.angularVelocity;
        }
        set
        {
            if (freezeRotationX)
            {
                value.x = 0.0f;
            }
            if (freezeRotationY)
            {
                value.y = 0.0f;
            }
            if (freezeRotationZ)
            {
                value.z = 0.0f;
            }
            
            if (value.sqrMagnitude > ScaledSpacePhysics.speedOfLight * ScaledSpacePhysics.speedOfLight)
                return;

            attachedRigidbody.angularVelocity = value;
        }
    }
    public bool affectedByGravity = true;
    [SerializeField] private Vector3d gravityAcceleration;

    public CollisionDetectionMode collisionDetection = CollisionDetectionMode.Discrete;

    /// <summary>
    /// Event fired when any ScaledCollider of this RB begins colliding with another RB in scaled space
    /// </summary>
    public event Action<ScaledSpacePhysics.CollisionInfo> OnScaledCollisionEnter;
    
    /// <summary>
    /// Event fired when any ScaledCollider of this RB stops colliding with another RB in scaled space
    /// </summary>
    public event Action<ScaledCollider, ScaledCollider> OnScaledCollisionExit;

    /// <summary>
    /// Event fired when another RB enters any trigger ScaledCollider under this RB
    /// </summary>
    public event Action<ScaledCollider, ScaledCollider> OnScaledTriggerEnter;
    
    /// <summary>
    /// Event fired when another RB exits any trigger ScaledCollider under this RB
    /// </summary>
    public event Action<ScaledCollider, ScaledCollider> OnScaledTriggerExit;

    public List<ScaledCollider> scaledColliders {get; private set;}

    private void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        attachedRigidbody = GetComponent<Rigidbody>();
        attachedRigidbody.isKinematic = _isKinematic;
        UpdateActive();
        scaledColliders = new List<ScaledCollider>();
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
        newCollider.enabled = collidersEnabled;
        foreach(ScaledCollider collider in scaledColliders)
        {
            newCollider.IgnoreCollider(collider, true);
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

        // Save collider positions before this frame's update to prepare for CCD checks
        foreach (ScaledCollider scaledCollider in scaledColliders)
        {
            scaledCollider.prevCenterPos = scaledCollider.GetRealCenter();
        }

        gravityAcceleration = Vector3d.zero;
        if (affectedByGravity)
        {
            ScaledSpacePhysics.Instance.InvokeGravityStep(this);
            AddForce(gravityAcceleration, ForceMode.Acceleration);
        }

        float sqrAngularSpeed = attachedRigidbody.angularVelocity.sqrMagnitude;
        if (_active || overrideUnity)
        {
            scaledTransform.realPosition += _velocity * Time.fixedDeltaTime;

            // if (sqrAngularSpeed > 0.00001)
            // {
            //     double angularSpeed = Math.Sqrt(sqrAngularSpeed);
            //     double angle = angularSpeed * Time.fixedDeltaTime;
            //     Vector3 axis = (_angularVelocity / angularSpeed).ToVector3();
            //     Quaternion delta = Quaternion.AngleAxis((float)(angle * Mathf.Rad2Deg), axis);
            //     transform.rotation = delta * transform.rotation;
            // }
        }

        if (_velocity.sqrMagnitude > 0.0001 || sqrAngularSpeed > 0.0001f)
        {
            ScaledSpacePhysics.Instance.UpdateGridPos(this);
        }
    }

    private void UpdateActive()
    {
        if (_active)
        {
            attachedRigidbody.constraints = RigidbodyConstraints.FreezePosition | constraints;
        }
        else
        {
            attachedRigidbody.constraints = constraints;
            if (FloatingWorldOrigin.Instance != null)
            {
                // Floating origin stays static so need to use relative velocity
                Vector3 relativeVelocity = (_velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity).ToVector3();
                attachedRigidbody.linearVelocity = relativeVelocity;
            }
        }
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
        if (newVelocity.sqrMagnitude < ScaledSpacePhysics.speedOfLight * ScaledSpacePhysics.speedOfLight)
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

    public void AddTorque(Vector3 torque, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        attachedRigidbody.AddTorque(torque, forceMode);

        // if (_active)
        // {
        //     Quaternion tensorRot = transform.rotation * attachedRigidbody.inertiaTensorRotation;
        //     Vector3 localTorque = Quaternion.Inverse(tensorRot) * torque.ToVector3();
        //     Vector3 inertia = attachedRigidbody.inertiaTensor;

        //     Vector3 localAngularAccel = forceMode switch
        //     {
        //         ForceMode.Impulse => new Vector3(
        //             localTorque.x / inertia.x,
        //             localTorque.y / inertia.y,
        //             localTorque.z / inertia.z),
        //         ForceMode.VelocityChange => localTorque,
        //         ForceMode.Acceleration => localTorque * Time.fixedDeltaTime,
        //         _ => new Vector3(
        //             localTorque.x * Time.fixedDeltaTime / inertia.x, 
        //             localTorque.y * Time.fixedDeltaTime / inertia.y, 
        //             localTorque.z * Time.fixedDeltaTime / inertia.z)
        //     };
        //     Vector3 worldAngularAccel = tensorRot * localAngularAccel;

        //     Vector3d newVelocity = _angularVelocity + worldAngularAccel.ToVector3d();

        //     if (newVelocity.sqrMagnitude < ScaledSpacePhysics.speedOfLight * ScaledSpacePhysics.speedOfLight)
        //     {
        //         _angularVelocity = newVelocity;
        //     }
        // }
        // else
        // {
        //     attachedRigidbody.AddTorque(torque.ToVector3(), forceMode);
        // }
    }

    public void AddRelativeTorque(Vector3d torque, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        attachedRigidbody.AddRelativeTorque(torque.ToVector3(), forceMode);

        // if (_active)
        // {
        //     double globalX = transform.right.x * torque.x + transform.up.x * torque.y + transform.forward.x * torque.z;
        //     double globalY = transform.right.y * torque.x + transform.up.y * torque.y + transform.forward.y * torque.z;
        //     double globalZ = transform.right.z * torque.x + transform.up.z * torque.y + transform.forward.z * torque.z;
        //     AddTorque(new Vector3d(globalX, globalY, globalZ), forceMode);
        // }
        // else
        // {
        //     attachedRigidbody.AddRelativeTorque(torque.ToVector3(), forceMode);
        // }
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

        AddTorque(torque.ToVector3(), forceMode);
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
    internal void RaiseCollisionExit(ScaledCollider source, ScaledCollider other)
    {
        OnScaledCollisionExit?.Invoke(source, other);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision enter event
    /// </summary>
    internal void RaiseTriggerEnter(ScaledCollider source, ScaledCollider other)
    {
        OnScaledTriggerEnter?.Invoke(source, other);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision exit event
    /// </summary>
    internal void RaiseTriggerExit(ScaledCollider source, ScaledCollider other)
    {
        OnScaledTriggerExit?.Invoke(source, other);
    }

    public void IgnoreScaledRigidbody(ScaledRigidbody other, bool ignore)
    {
        foreach (ScaledCollider collider in scaledColliders)
        {
            foreach (ScaledCollider otherCollider in other.scaledColliders)
            {
                collider.IgnoreCollider(otherCollider, ignore);
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
        collidersEnabled = value;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_isKinematic || !_active)
            return;

        // Need to handle collisions between unity colliders and scaled rigidbodies
        ContactPoint contact = collision.GetContact(0);
        Vector3d realContactPoint = scaledTransform.TransformRenderPoint(contact.point);

        if (contact.separation < 0)
        {
            Vector3d correctionDirection = scaledTransform.realPosition - realContactPoint;
            scaledTransform.realPosition += correctionDirection.normalized * (-contact.separation * 2.0f);
        }

        // AddForce(collision.impulse.ToVector3d() / mass, ForceMode.Impulse);
        AddForceAtPosition(collision.impulse.ToVector3d() / mass, realContactPoint, ForceMode.Impulse);
    }
}
