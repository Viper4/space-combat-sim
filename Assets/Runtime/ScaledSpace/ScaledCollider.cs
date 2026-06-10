using System;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(DoubleRigidbody))]
public class ScaledCollider : MonoBehaviour
{
    private static uint nextId;

    public uint id {get; private set;}
    public DoubleRigidbody doubleRigidbody {get; private set;}

    [SerializeField, Tooltip("Local offset from realPosition. RealCenter = realPosition+center*realScale")] private Vector3d center;
    [SerializeField, Tooltip("Radius of simulated sphere collider in scaled space physics. Use -1 to calculate as Max(scale.x, scale.y, scale.z)")] private double radius = -1f;

    public bool isTrigger = false;

    public int hGridLevel = -1;
    public HGrid.GridCell hGridCell;

    private HashSet<uint> ignoredColliders = new HashSet<uint>();

    private void Awake()
    {
        doubleRigidbody = GetComponent<DoubleRigidbody>();
        id = nextId++;
        doubleRigidbody.AddCollider(this);
        ScaledSpacePhysics.Instance.RegisterCollider(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isTrigger ? Color.blue : Color.red;

        if (doubleRigidbody == null)
        {
            doubleRigidbody = GetComponent<DoubleRigidbody>();
        }

        Vector3d rotatedOffset = new Vector3d(center.x * transform.localScale.x, center.y * transform.localScale.y, center.z * transform.localScale.z);
        rotatedOffset = rotatedOffset.Rotate(doubleRigidbody.transform.rotation);

        Gizmos.DrawWireSphere(transform.position + rotatedOffset.ToVector3(), (float)radius);
    }

    public Vector3d GetLocalCenter()
    {
        return center;
    }

    public Vector3d GetRealCenter()
    {
        Vector3d scale = doubleRigidbody.scaledTransform.realScale;
        Vector3d rotatedOffset = new Vector3d(center.x * scale.x, center.y * scale.y, center.z * scale.z);
        rotatedOffset = rotatedOffset.Rotate(doubleRigidbody.transform.rotation);
        return doubleRigidbody.scaledTransform.realPosition + rotatedOffset;
    }

    public double GetRadius()
    {
        if (radius < 0)
        {
            Vector3d scale = doubleRigidbody.scaledTransform.realScale;
            return Math.Max(Math.Max(scale.x, scale.y), scale.z);
        }
        return radius;
    }

    public void IgnoreCollider(uint otherId, bool ignore)
    {
        if (ignore)
        {
            ignoredColliders.Add(otherId);
        }
        else
        {
            ignoredColliders.Remove(otherId);
        }
    }

    public bool IsIgnoring(uint otherId)
    {
        return ignoredColliders.Contains(otherId);
    }

    private void OnDestroy()
    {
        ScaledSpacePhysics.Instance.UnregisterCollider(this);
    }
}