using System;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(ScaledRigidbody))]
public class ScaledCollider : MonoBehaviour
{
    private static uint nextId;

    public uint id {get; private set;}
    public ScaledRigidbody scaledRigidbody {get; private set;}

    [SerializeField, Tooltip("Local offset from realPosition. RealCenter = realPosition+center*realScale")] private Vector3d center;
    [SerializeField, Tooltip("Radius of simulated sphere collider in scaled space physics. Use -1 to calculate as Max(scale.x, scale.y, scale.z)")] private double radius = -1f;

    public bool isTrigger = false;

    [Range(0, 1), Tooltip("0 => perfectly inelastic (no bounce), 1 => perfectly elastic (full bounce)")] public float restitution = 0f;

    public int hGridLevel = -1;
    public HGrid.GridCell hGridCell;

    [Tooltip("If true, will always use ScaledSpacePhysics for collisions.")] public bool overrideUnity = false;

    private HashSet<uint> ignoredColliders = new HashSet<uint>();

    private void Awake()
    {
        id = nextId++;
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        scaledRigidbody.AddCollider(this);
    }

    private IEnumerator Start()
    {
        if (TryGetComponent<CelestialBody>(out var celestialBody))
        {
            yield return new WaitUntil(celestialBody.Initialized);
        }
        yield return new WaitForFixedUpdate();
        if (radius < 0.0)
        {
            MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers.Length > 0)
            {
                Bounds combinedBounds = meshRenderers[0].bounds;
                for (int i = 1; i < meshRenderers.Length; i++)
                {
                    combinedBounds.Encapsulate(meshRenderers[i].bounds);
                }
                radius = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y, combinedBounds.extents.z);
                if (scaledRigidbody.scaledTransform.inScaledSpace)
                    radius *= scaledRigidbody.scaledTransform.scaleFactor;
            }
            else
            {
                Vector3d scale = scaledRigidbody.scaledTransform.realScale;
                radius = Math.Max(Math.Max(scale.x, scale.y), scale.z);
            }
        }
    }

    private void OnEnable()
    {
        ScaledSpacePhysics.Instance.RegisterCollider(this);
    }

    private void OnDisable()
    {
        ScaledSpacePhysics.Instance.UnregisterCollider(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isTrigger ? Color.blue : Color.red;

        Vector3d rotatedOffset = new Vector3d(center.x * transform.localScale.x, center.y * transform.localScale.y, center.z * transform.localScale.z);
        if (scaledRigidbody == null)
        {
            scaledRigidbody = GetComponent<ScaledRigidbody>();
        }
        rotatedOffset = rotatedOffset.Rotate(scaledRigidbody.transform.rotation);

        Gizmos.DrawWireSphere(transform.position + rotatedOffset.ToVector3(), GetGizmoRadius());
    }

    public Vector3d GetLocalCenter()
    {
        return center;
    }

    public Vector3d GetRealCenter()
    {
        Vector3d scale = scaledRigidbody.scaledTransform.realScale;
        Vector3d rotatedOffset = new Vector3d(center.x * scale.x, center.y * scale.y, center.z * scale.z);
        rotatedOffset = rotatedOffset.Rotate(scaledRigidbody.transform.rotation);
        return scaledRigidbody.scaledTransform.realPosition + rotatedOffset;
    }

    public double GetRadius()
    {
        return radius;
    }

    private float GetGizmoRadius()
    {
        double realRadius = GetRadius();
        ScaledTransform scaledTransform = scaledRigidbody.scaledTransform;
        if (scaledTransform == null)
            scaledTransform = scaledRigidbody.GetComponent<ScaledTransform>();

        if (scaledTransform.inScaledSpace)
        {
            return (float)(realRadius / scaledTransform.scaleFactor);
        }
        return (float)realRadius;
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
}