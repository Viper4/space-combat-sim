using System;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(DoubleRigidbody))]
public class Projectile : MonoBehaviour
{
    protected DoubleRigidbody doubleRigidbody;

    [SerializeField, Header("Projectile")] protected LayerMask ignoreLayers;

    [SerializeField] private float destroyDelay = 5;

    private void Awake()
    {
        doubleRigidbody = GetComponent<DoubleRigidbody>();
        if(destroyDelay > 0)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void OnEnable()
    {
        doubleRigidbody.OnScaledCollisionEnter += OnCollide;
        doubleRigidbody.OnScaledTriggerEnter += OnTriggerWrapper;        
    }

    private void OnDisable()
    {
        doubleRigidbody.OnScaledCollisionEnter -= OnCollide;
        doubleRigidbody.OnScaledTriggerEnter -= OnTriggerWrapper;
    }

    private void OnTriggerWrapper(DoubleRigidbody other)
    {
        OnTrigger(other.transform, other);
    }

    protected virtual void OnCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        Destroy(gameObject);
    }

    protected virtual void OnTrigger(Transform other, DoubleRigidbody otherDoubleRB)
    {
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & ignoreLayers) != 0)
            return;

        DoubleRigidbody otherDoubleRB = collision.rigidbody == null ? collision.transform.GetComponent<DoubleRigidbody>() : collision.rigidbody.GetComponent<DoubleRigidbody>();
        ScaledSpacePhysics.CollisionInfo collisionInfo = new()
        {
            transformA = transform,
            transformB = collision.transform,
            rbA = doubleRigidbody,
            rbB = otherDoubleRB,
            contactPoint = collision.GetContact(0).point.ToVector3d(),
            normal = collision.GetContact(0).normal.ToVector3d(),
            penetration = collision.GetContact(0).separation
        };
        OnCollide(collisionInfo);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & ignoreLayers) != 0)
            return;

        if (doubleRigidbody.scaledTransform.inScaledSpace || other.isTrigger)
            return;

        DoubleRigidbody otherDoubleRB = other.attachedRigidbody == null ? other.transform.GetComponent<DoubleRigidbody>() : other.attachedRigidbody.GetComponent<DoubleRigidbody>();
        OnTrigger(other.transform, otherDoubleRB);
    }
}
