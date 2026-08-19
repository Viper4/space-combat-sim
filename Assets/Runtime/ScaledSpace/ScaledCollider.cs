using System;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;

public class ScaledCollider : MonoBehaviour
{
    private static uint nextId;

    public uint id {get; private set;}
    public ScaledRigidbody scaledRigidbody {get; private set;}

    [SerializeField, Tooltip("Local offset from realPosition. RealCenter = realPosition+center*realScale")]
    private Vector3d center;
    [SerializeField, Tooltip("Radius of simulated sphere collider in scaled space physics. Use -1 to calculate as Max(scale.x, scale.y, scale.z)")]
    private double radius = -1f;

    public bool isTrigger = false;

    [Range(0, 1), Tooltip("0 => perfectly inelastic (no bounce), 1 => perfectly elastic (full bounce)")] public float restitution = 0f;

    public HGrid.GridCell hGridCell;
    [HideInInspector] public int listIndex = -1;

    [Tooltip("If true, will always use ScaledSpacePhysics for collisions.")] public bool overrideUnity = false;

    private HashSet<uint> ignoredColliders = new HashSet<uint>();

    [HideInInspector] public Vector3d prevCenterPos;

    private void Awake()
    {
        id = nextId++;
        scaledRigidbody = GetComponentInParent<ScaledRigidbody>();
        if (scaledRigidbody == null)
        {
            Debug.LogError($"[ScaledCollider] {name} requires an attached ScaledRigidbody on this object or in the parent hierarchy.");
            return;
        }
        scaledRigidbody.AddCollider(this);
        prevCenterPos = GetRealCenter();
    }

    private void Start()
    {
        ScaledSpacePhysics.Instance.RegisterCollider(this);
    }

    private void OnDestroy()
    {
        ScaledSpacePhysics.Instance.UnregisterCollider(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isTrigger ? Color.blue : Color.red;

        Vector3d rotatedOffset = new Vector3d(center.x * transform.localScale.x, center.y * transform.localScale.y, center.z * transform.localScale.z);
        if (scaledRigidbody == null)
        {
            scaledRigidbody = GetComponentInParent<ScaledRigidbody>();
        }
        rotatedOffset = rotatedOffset.Rotate(scaledRigidbody.transform.rotation);

        Gizmos.DrawWireSphere(transform.position + rotatedOffset.ToVector3(), GetGizmoRadius());
    }

    public Vector3d GetRealCenter()
    {
        Vector3d scale = scaledRigidbody.scaledTransform.realScale;
        Vector3d rotatedOffset = new Vector3d(center.x * scale.x, center.y * scale.y, center.z * scale.z);
        rotatedOffset = rotatedOffset.Rotate(scaledRigidbody.transform.rotation);
        return scaledRigidbody.scaledTransform.realPosition + rotatedOffset;
    }

    public void SetRadius(double radius)
    {
        if (radius < 0.0)
            this.radius = -1.0;
        this.radius = radius;
        ScaledSpacePhysics.Instance.UpdateGridSize(scaledRigidbody);
    }

    public double GetRadius()
    {
        if (radius < 0.0)
        {
            ScaledTransform scaledTransform = scaledRigidbody.scaledTransform;
            if (scaledTransform == null)
                scaledTransform = scaledRigidbody.GetComponent<ScaledTransform>();
            return scaledTransform.realRadius;
        }
        return radius;
    }

    private float GetGizmoRadius()
    {
        double realRadius = GetRadius();
        ScaledTransform scaledTransform = scaledRigidbody.scaledTransform;
        if (scaledTransform == null)
            scaledTransform = scaledRigidbody.GetComponent<ScaledTransform>();

        double scaleFactor = scaledTransform.GetScaleFactor();
        if (scaledTransform.inScaledSpace && scaleFactor > 0.0)
        {
            return (float)(realRadius / scaleFactor);
        }
        return (float)realRadius;
    }

    public void IgnoreCollider(ScaledCollider other, bool ignore)
    {
        if (ignore)
        {
            ignoredColliders.Add(other.id);
        }
        else
        {
            ignoredColliders.Remove(other.id);
        }
    }

    public bool IsIgnoring(ScaledCollider other)
    {
        return ignoredColliders.Contains(other.id);
    }
}