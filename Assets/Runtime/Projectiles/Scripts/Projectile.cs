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
        doubleRigidbody.OnScaledCollisionEnter += OnCollide;
        doubleRigidbody.OnScaledTriggerEnter += OnTriggerWrapper;       
    }

    private void OnTriggerWrapper(ScaledCollider other)
    {
        OnTrigger(other.transform, other);
    }

    protected virtual void OnCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        Destroy(gameObject);
    }

    protected virtual void OnTrigger(Transform other, ScaledCollider otherDoubleRB)
    {
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & ignoreLayers) != 0)
            return;

        ScaledCollider thisCollider = null;
        foreach(ScaledCollider collider in doubleRigidbody.scaledColliders)
        {
            if (!collider.isTrigger)
            {
                thisCollider = collider;
                break;
            }
        }

        DoubleRigidbody otherDoubleRB = collision.rigidbody == null ? collision.transform.GetComponent<DoubleRigidbody>() : collision.rigidbody.GetComponent<DoubleRigidbody>();
        ScaledCollider otherCollider = null;
        if (otherDoubleRB != null)
        {
            foreach(ScaledCollider collider in otherDoubleRB.scaledColliders)
            {
                if (!collider.isTrigger)
                {
                    otherCollider = collider;
                    break;
                }
            }
        }
        Vector3d realContactPoint = doubleRigidbody.scaledTransform.GetChildRealPosition(collision.GetContact(0).point);
        double realPenetration = collision.GetContact(0).separation;
        if (doubleRigidbody.scaledTransform.inScaledSpace)
            realPenetration *= doubleRigidbody.scaledTransform.scaleFactor;
        ScaledSpacePhysics.CollisionInfo collisionInfo = new()
        {
            colliderA = thisCollider,
            colliderB = otherCollider,
            transformA = transform,
            transformB = collision.transform,
            contactPoint = realContactPoint,
            normal = collision.GetContact(0).normal.ToVector3d(),
            penetration = realPenetration
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
        ScaledCollider otherCollider = null;
        if (otherDoubleRB != null)
        {
            foreach(ScaledCollider collider in otherDoubleRB.scaledColliders)
            {
                if (collider.isTrigger == other.isTrigger)
                {
                    otherCollider = collider;
                    break;
                }
            }
        }
        OnTrigger(other.transform, otherCollider);
    }

    private void OnDestroy()
    {
        doubleRigidbody.OnScaledCollisionEnter -= OnCollide;
        doubleRigidbody.OnScaledTriggerEnter -= OnTriggerWrapper;
    }
}
