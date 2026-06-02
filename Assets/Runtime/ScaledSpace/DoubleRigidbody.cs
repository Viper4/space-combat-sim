using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(Rigidbody), typeof(ScaledTransform))]
public class DoubleRigidbody : MonoBehaviour
{
    public static float speedLimit = 2.99792458e8f; // Speed of light in m/s

    private static uint nextId;

    public uint id {get; private set;}

    private bool _active;
    public bool active
    {
        get
        {
            return _active;
        }
        set
        {
            if (_active != value)
            {
                if (value)
                {
                    _velocity = attachedRigidbody.linearVelocity.ToVector3d();
                    _angularVelocity = attachedRigidbody.angularVelocity.ToVector3d();
                    attachedRigidbody.isKinematic = true;
                    scaledTransform.SwitchToDoubleRigidbody();
                }
                else
                {
                    attachedRigidbody.isKinematic = false;
                    attachedRigidbody.linearVelocity = _velocity.ToVector3();
                    attachedRigidbody.angularVelocity = _angularVelocity.ToVector3();
                    scaledTransform.SwitchToRigidbody();
                }
            }
            _active = value;
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

    public ScaledTransform scaledTransform {get; private set;}
    public Rigidbody attachedRigidbody {get; private set;}
    [SerializeField, Tooltip("Switch to rigidbody if speed is below this threshold, DoubleRigidbody if above")] private float speedThreshold = -1;

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
            if (!_active)
            {
                attachedRigidbody.linearVelocity = value.ToVector3();
            }
        }
    }

    [SerializeField] private Vector3d _angularVelocity;
    public Vector3d angularVelocity
    {
        get
        {
            return _angularVelocity;
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

    public int hGridLevel = -1;

    /// <summary>
    /// Event fired when this RB enters collision with another RB in scaled space
    /// </summary>
    public event Action<ScaledSpacePhysics.CollisionInfo> OnScaledCollisionEnter;
    
    /// <summary>
    /// Event fired when this RB exits collision with another RB in scaled space
    /// </summary>
    public event Action<DoubleRigidbody> OnScaledCollisionExit;

    /// <summary>
    /// Event fired when other RB enters this RB's trigger
    /// </summary>
    public event Action<DoubleRigidbody> OnScaledTriggerEnter;
    
    /// <summary>
    /// Event fired when other RB exits this RB's trigger
    /// </summary>
    public event Action<DoubleRigidbody> OnScaledTriggerExit;

    public bool trackTrigger = false;
    [Tooltip("Radius of simulated sphere collider in scaled space physics. Use -1 to calculate as Max(scale.x, scale.y, scale.z)"), SerializeField]
    private float scaledColliderRadius = -1f;
    [Tooltip("Radius of simulated sphere trigger in scaled space physics. Use -1 to calculate as Max(scale.x, scale.y, scale.z)"), SerializeField]
    private float scaledTriggerRadius = -1f;

    private Vector3d gravityAcceleration;

    void Awake()
    {
        id = nextId++;
        scaledTransform = GetComponent<ScaledTransform>();
        attachedRigidbody = GetComponent<Rigidbody>();
        if (transform != FloatingWorldOrigin.Instance.transform)
        {
            FloatingWorldOrigin.Instance.OnOriginShift += OnOriginShift;
        }
    }

    private void OnEnable()
    {
        ScaledSpacePhysics.Instance.RegisterDoubleRigidbody(this);
    }

    private void OnDisable()
    {
        ScaledSpacePhysics.Instance.UnregisterDoubleRigidbody(this);
    }
    
    public void ClearGravity()
    {
        gravityAcceleration = Vector3d.zero;
    }

    public void AddGravity(Vector3d gravity)
    {
        gravityAcceleration += gravity;
    }

    public Vector3d GetGravity()
    {
        return gravityAcceleration;
    }

    public void PhysicsStep(float deltaTime)
    {
        if (_isKinematic)
            return;

        AddForce(gravityAcceleration, ForceMode.Acceleration);
        
        if (_active)
        {
            scaledTransform.realPosition += _velocity * deltaTime;

            double sqrAngularSpeed = _angularVelocity.sqrMagnitude;

            if (sqrAngularSpeed > 0.00001)
            {
                double angularSpeed = Math.Sqrt(sqrAngularSpeed);
                double angle = angularSpeed * deltaTime;
                Vector3 axis = (_angularVelocity / angularSpeed).ToVector3();
                Quaternion delta = Quaternion.AngleAxis((float)(angle * Mathf.Rad2Deg), axis);
                transform.rotation = delta * transform.rotation;
            }

            // Can only use rigidbody when speed is below threshold and in world space
            if (speedThreshold != -1 && !scaledTransform.inScaledSpace)
            {
                double sqrSpeed = _velocity.sqrMagnitude;
                if (sqrSpeed < speedThreshold * speedThreshold)
                {
                    active = false;
                }
            }
        }
        else
        {
            Vector3 rigidbodyVelocity = attachedRigidbody.linearVelocity;
            _velocity = rigidbodyVelocity.ToVector3d();
            _angularVelocity = attachedRigidbody.angularVelocity.ToVector3d();
            if (speedThreshold != -1 && !scaledTransform.inScaledSpace)
            {
                float sqrSpeed = rigidbodyVelocity.sqrMagnitude;
                if (sqrSpeed > speedThreshold * speedThreshold)
                {
                    active = true;
                }
            }
        }
    }

    public void AddForce(Vector3d force, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        if (_active)
        {
            Vector3d newVelocity = forceMode switch
            {
                ForceMode.Impulse => _velocity + (force / attachedRigidbody.mass),
                ForceMode.VelocityChange => _velocity + force,
                ForceMode.Acceleration => _velocity + (force * Time.fixedDeltaTime),
                _ => _velocity + (force * (Time.fixedDeltaTime / attachedRigidbody.mass)),
            };
            if (newVelocity.sqrMagnitude < speedLimit * speedLimit)
                _velocity = newVelocity;
        }
        else
        {
            attachedRigidbody.AddForce(force.ToVector3(), forceMode);
        }
    }

    public void AddRelativeForce(Vector3d force, ForceMode forceMode)
    {
        if (_isKinematic)
            return;

        if (_active)
        {
            double globalX = transform.right.x * force.x + transform.up.x * force.y + transform.forward.x * force.z;
            double globalY = transform.right.y * force.x + transform.up.y * force.y + transform.forward.y * force.z;
            double globalZ = transform.right.z * force.x + transform.up.z * force.y + transform.forward.z * force.z;
            AddForce(new Vector3d(globalX, globalY, globalZ), forceMode);
        }
        else
        {
            attachedRigidbody.AddRelativeForce(force.ToVector3(), forceMode);
        }
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

        if (_active)
        {
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
        else
        {
            attachedRigidbody.AddForceAtPosition(force.ToVector3(), position.ToVector3(), forceMode);
        }
    }

    public void AddExplosionForce(float explosionForce, Vector3d explosionPosition, float explosionRadius, float upwardsModifier = 0f, ForceMode forceMode = ForceMode.Force)
    {
        if (_isKinematic)
            return;

        if (_active)
        {
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
        else
        {
            attachedRigidbody.AddExplosionForce(explosionForce, explosionPosition.ToVector3(), explosionRadius, upwardsModifier, forceMode);
        }
    }

    public double GetCollisionRadius()
    {
        if (scaledColliderRadius < 0)
        {
            Vector3d scale = scaledTransform.realScale;
            return Math.Max(Math.Max(scale.x, scale.y), scale.z);
        }
        return scaledColliderRadius;
    }

    public double GetTriggerRadius()
    {
        if (scaledTriggerRadius < 0)
        {
            Vector3d scale = scaledTransform.realScale;
            return Math.Max(Math.Max(scale.x, scale.y), scale.z);
        }
        return scaledTriggerRadius;
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
    internal void RaiseCollisionExit(DoubleRigidbody other)
    {
        OnScaledCollisionExit?.Invoke(other);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision enter event
    /// </summary>
    internal void RaiseTriggerEnter(DoubleRigidbody other)
    {
        OnScaledTriggerEnter?.Invoke(other);
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision exit event
    /// </summary>
    internal void RaiseTriggerExit(DoubleRigidbody other)
    {
        OnScaledTriggerExit?.Invoke(other);
    }

    private void OnOriginShift(Vector3d shift)
    {
        if (!scaledTransform.inScaledSpace && !active)
        {
            // Need to shift since we're using Unity's Rigidbody physics and we're setting realPosition=transform.position
            Vector3d newWorldOriginPosition = FloatingWorldOrigin.Instance.worldOriginPosition + shift;
            double sqrDistance = (scaledTransform.realPosition - newWorldOriginPosition).sqrMagnitude;
            // If object wont switch to scaled space after shift, shift its transform.position, otherwise let ScaledTransform.UpdateTransform() handle switch next frame
            if (sqrDistance < scaledTransform.scaledSpaceThreshold * scaledTransform.scaledSpaceThreshold)
                transform.position -= shift.ToVector3();
        }
    }

    private void OnDestroy()
    {
        // Clear all event listeners
        OnScaledCollisionEnter = null;
        OnScaledCollisionExit = null;
        OnScaledTriggerEnter = null;
        OnScaledTriggerExit = null;

        FloatingWorldOrigin.Instance.OnOriginShift -= OnOriginShift;
    }
}
