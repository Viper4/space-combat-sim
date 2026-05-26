using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(ScaledTransform))]
public class DoubleRigidbody : MonoBehaviour
{
    public static float speedLimit = 1000;

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
                    velocity = attachedRigidbody.linearVelocity.ToVector3d();
                    angularVelocity = attachedRigidbody.angularVelocity.ToVector3d();
                    attachedRigidbody.isKinematic = true;
                    scaledTransform.SwitchToDoubleRigidbody();
                }
                else
                {
                    attachedRigidbody.isKinematic = false;
                    attachedRigidbody.linearVelocity = velocity.ToVector3();
                    attachedRigidbody.angularVelocity = angularVelocity.ToVector3();
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

    public Vector3d velocity;
    public Vector3d angularVelocity;

    /// <summary>
    /// Event fired when this RB enters collision with another RB in scaled space
    /// </summary>
    public event Action<DoubleRigidbody, ScaledSpacePhysics.CollisionInfo> OnScaledCollisionEnter;
    
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
    public float scaledTriggerRadius = 1.0f; // Radius to use for triggers in scaled space

    void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        attachedRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (ScaledSpacePhysics.Instance != null)
            ScaledSpacePhysics.Instance.RegisterDoubleRigidbody(this);
    }

    private void OnDisable()
    {
        if (ScaledSpacePhysics.Instance != null)
            ScaledSpacePhysics.Instance.UnregisterDoubleRigidbody(this);
    }

    void FixedUpdate()
    {
        if (_isKinematic)
            return;

        if (_active)
        {
            scaledTransform.realPosition += velocity * Time.fixedDeltaTime;
            Quaternion deltaRotation = Quaternion.Euler(Mathf.Rad2Deg * Time.fixedDeltaTime * angularVelocity.ToVector3());
            transform.rotation *= deltaRotation;
            Debug.Log($"DoubleRigidbody: {angularVelocity}, delta: {deltaRotation}");

            // Can only use rigidbody when speed is below threshold and in world space
            if (speedThreshold != -1 && !scaledTransform.inScaledSpace)
            {
                double sqrSpeed = velocity.sqrMagnitude;
                if (sqrSpeed < speedThreshold * speedThreshold)
                {
                    active = false;
                }
            }
        }
        else
        {
            Vector3 rigidbodyVelocity = attachedRigidbody.linearVelocity;
            velocity = rigidbodyVelocity.ToVector3d();
            angularVelocity = attachedRigidbody.angularVelocity.ToVector3d();
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
                ForceMode.Impulse => velocity + (force / attachedRigidbody.mass),
                ForceMode.VelocityChange => velocity + force,
                ForceMode.Acceleration => velocity + (force * Time.fixedDeltaTime),
                _ => velocity + (force * (Time.fixedDeltaTime / attachedRigidbody.mass)),
            };
            if (newVelocity.sqrMagnitude < speedLimit * speedLimit)
                velocity = newVelocity;
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
            Vector3d newVelocity = forceMode switch
            {
                ForceMode.Impulse => angularVelocity + (torque / attachedRigidbody.mass),
                ForceMode.VelocityChange => angularVelocity + torque,
                ForceMode.Acceleration => angularVelocity + (torque * Time.fixedDeltaTime),
                _ => angularVelocity + (torque * (Time.fixedDeltaTime / attachedRigidbody.mass)),
            };
            if (newVelocity.sqrMagnitude < speedLimit * speedLimit)
                angularVelocity = newVelocity;
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

    private void OnCollisionEnter(Collision collision)
    {
        if (_active) // This only happens when other collision's rigidbody isKinematic=false
        {
            // Get contact information
            ContactPoint contact = collision.GetContact(0);
            Vector3 contactNormal = contact.normal;
            Vector3 contactPoint = contact.point;
            
            // Get relativeVelocity with other rigidbody
            Rigidbody otherRigidbody = collision.rigidbody;
            Vector3 relativeVelocity = velocity.ToVector3();
            if (otherRigidbody != null)
            {
                if (otherRigidbody.TryGetComponent<DoubleRigidbody>(out var otherDoubleRigidbody))
                {
                    relativeVelocity -= otherDoubleRigidbody.velocity.ToVector3();
                }
                else
                {
                    relativeVelocity -= otherRigidbody.linearVelocity;
                }  
            }
            
            // Project onto contact normal
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, contactNormal);
            
            // Don't resolve if velocities are separating
            if (velocityAlongNormal > 0)
                return;
            
            // Calculate impulse magnitude (simplified elastic collision with equal mass assumption)
            float impulseMagnitude = -(1 + ScaledSpacePhysics.Instance.restitution) * velocityAlongNormal / 2f;
            
            // Apply impulse force and torque in one call
            Vector3d impulse = (contactNormal * impulseMagnitude).ToVector3d();
            AddForceAtPosition(impulse, contactPoint.ToVector3d(), ForceMode.Impulse);
        }
    }

    public double GetCollisionRadius()
    {
        Vector3d scale = scaledTransform.realScale;
        return Math.Max(Math.Max(scale.x, scale.y), scale.z) * 0.5;
    }

    /// <summary>
    /// Internal method called by ScaledSpacePhysics to raise collision enter event
    /// </summary>
    internal void RaiseCollisionEnter(DoubleRigidbody other, ScaledSpacePhysics.CollisionInfo collision)
    {
        OnScaledCollisionEnter?.Invoke(other, collision);
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
}
