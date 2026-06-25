using SpaceStuff;
using UnityEngine;
using System.Collections.Generic;
using System;

public class ScaledTransform : MonoBehaviour
{
    private ScaledRigidbody scaledRigidbody;

    [SerializeField] private Vector3d _realPosition; // Actual world position
    public Vector3d realPosition 
    { 
        get
        {
            return _realPosition;
        }
        set
        {
            _realPosition = value;
            if (!inScaledSpace && FloatingWorldOrigin.Instance != null)
            {
                UpdateInWorldSpace(FloatingWorldOrigin.Instance.scaledTransform.realPosition, true);
            }
        }
    }
    [SerializeField] private Vector3d _realScale = Vector3d.one; // Actual world scale
    public Vector3d realScale
    {
        get
        {
            return _realScale;
        }
        set
        {
            if (_realScale != value && scaledRigidbody != null)
                ScaledSpacePhysics.Instance.UpdateGridSize(scaledRigidbody);
            _realScale = value;
        }
    }
    [SerializeField, Tooltip("Tracked colliders/renderers are disabled at screen sizes below this")] private float minScreenPixelSize = 3f;
    
    [HideInInspector] public bool visible = true;
    private Collider[] trackedColliders;
    private Renderer[] trackedRenderers;
    private int[] originalColliderLayers;
    private int[] originalRendererLayers;
    public int scaledSpaceLayer = 3;

    public bool inScaledSpace = false;
    public double scaleFactor = 1000;
    [SerializeField] private bool dynamicScaleFactor;
    
    public float worldSpaceThreshold = 3900;
    public float scaledSpaceThreshold = 4100;

    private void Awake()
    {
        TryGetComponent(out scaledRigidbody);

        ResetVisualComponents();
        
        if (inScaledSpace)
        {
            inScaledSpace = false;
            SwitchToScaledSpace();
        }
        else
        {
            inScaledSpace = true;
            SwitchToWorldSpace();
        }

        SetTrackedComponentsActive(visible);
    }

    private void OnValidate()
    {
        if (trackedColliders == null || trackedRenderers == null)
            GetVisualComponents();
        if (Camera.main != null)
            UpdateTransformEditor();
    }

    private void GetVisualComponents()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        List<Collider> tempColliders = new List<Collider>();
        List<Renderer> tempRenderers = new List<Renderer>();

        for (int i = 0; i < allChildren.Length; i++)
        {
            Collider[] colliders = allChildren[i].GetComponents<Collider>();
            Renderer[] renderers = allChildren[i].GetComponents<Renderer>();
            if (colliders.Length > 0)
            {
                for (int j = 0; j < colliders.Length; j++)
                {
                    if (!colliders[j].isTrigger)
                        tempColliders.Add(colliders[j]);
                }
            }
            if (renderers.Length > 0)
            {
                tempRenderers.AddRange(renderers);
            }
        }
        trackedColliders = tempColliders.ToArray();
        trackedRenderers = tempRenderers.ToArray();
    }

    public void ResetVisualComponents(int originalLayer = -1)
    {
        GetVisualComponents();
        originalColliderLayers = new int[trackedColliders.Length];
        originalRendererLayers = new int[trackedRenderers.Length];

        if (originalLayer < 0)
        {
            for (int i = 0; i < trackedColliders.Length; i++)
            {
                originalColliderLayers[i] = trackedColliders[i].gameObject.layer;
            }
            for (int i = 0; i < trackedRenderers.Length; i++)
            {
                originalRendererLayers[i] = trackedRenderers[i].gameObject.layer;
            }
        }
        else
        {
            for (int i = 0; i < trackedColliders.Length; i++)
            {
                originalColliderLayers[i] = originalLayer;
            }
            for (int i = 0; i < trackedRenderers.Length; i++)
            {
                originalRendererLayers[i] = originalLayer;
            }
        }
    }

    private void SetTrackedComponentsActive(bool value)
    {
        // Disable/enable colliders
        for (int i = 0; i < trackedColliders.Length; i++)
        {
            trackedColliders[i].enabled = value;
        }
        // Disable/enable renderers
        for (int i = 0; i < trackedRenderers.Length; i++)
        {
            trackedRenderers[i].enabled = value;
        }
        visible = value;
    }

    private void LateUpdate()
    {
        // LateUpdate to wait for everything else to finish applying changes to realPosition and camera is done applying transform position changes
        UpdateTransform();
    }

    private void UpdateTransformEditor()
    {
        // Assume main camera is the origin while in editor
        if (Camera.main.transform == transform)
            return;
        Vector3d originPosition = Camera.main.transform.position.ToVector3d();
        // Dont do switching logic in editor since we dont have floating world origin
        if (inScaledSpace)
        {
            UpdateInScaledSpace(originPosition);
        }
        else
        {
            UpdateInWorldSpace(originPosition, true);
        }
    }

    private void UpdateTransform()
    {
        // Floating origin's transform position should always stay static
        if (FloatingWorldOrigin.Instance == null || FloatingWorldOrigin.Instance.scaledTransform == this)
            return;
        Vector3d originPosition = FloatingWorldOrigin.Instance.scaledTransform.realPosition;

        double sqrDistance = (_realPosition - originPosition).sqrMagnitude;
        if (inScaledSpace)
        {
            if (sqrDistance < worldSpaceThreshold * worldSpaceThreshold)
                SwitchToWorldSpace();
            else
                UpdateInScaledSpace(originPosition);
        }
        else
        {
            if (sqrDistance > scaledSpaceThreshold * scaledSpaceThreshold)
                SwitchToScaledSpace();
            else
                UpdateInWorldSpace(originPosition, scaledRigidbody != null && scaledRigidbody.active);
        }
    }

    private void UpdateInScaledSpace(Vector3d originPosition)
    {
        // visualPosition = renderCamPos + scaledOffset
        // scaledOffset = (realPosition - realCamPos) / scale
        Vector3d renderCamPos = Camera.main.transform.position.ToVector3d();
        Vector3d realCamPos = originPosition + renderCamPos;
        Vector3d offset = _realPosition - realCamPos; // Unscaled offset from camera to object

        double radius = Math.Max(Math.Max(_realScale.x, _realScale.y), _realScale.z); // Good enough estimate, CelestialBodies use this for size anyway
        double sqrPixelSize = SpaceMath.CalculateSquarePixelSize(offset.sqrMagnitude, radius);
        if (sqrPixelSize < minScreenPixelSize * minScreenPixelSize)
        {
            if (visible)
                SetTrackedComponentsActive(false);
            transform.position = Vector3.zero; // Reduce risk of floating point errors
            return;
        }
        else
        {
            if (!visible)
                SetTrackedComponentsActive(true);
        }

        transform.position = (renderCamPos + offset / scaleFactor).ToVector3();
        transform.localScale = (_realScale / scaleFactor).ToVector3();
    }

    private void UpdateInWorldSpace(Vector3d originPosition, bool updateRenderPosition)
    {
        transform.localScale = _realScale.ToVector3();
        if (updateRenderPosition)
        {
            // Assume Editor changes realPosition (scaledRigidbody == null) and ScaledRigidbody changes realPosition with velocity
            transform.position = (_realPosition - originPosition).ToVector3();
        }
        else
        {
            // Rigidbody changes transform.position with linearVelocity
            _realPosition = originPosition + transform.position.ToVector3d();
        }
    }

    private void SwitchToScaledSpace()
    {
        // Floating origin should never be in scaled space since we assume the origin has the camera
        if (FloatingWorldOrigin.Instance == null || FloatingWorldOrigin.Instance.scaledTransform == this || !gameObject.activeSelf || inScaledSpace)
            return;
        inScaledSpace = true;
        if (scaledRigidbody != null)
            scaledRigidbody.active = true;
        UpdateVisualComponents();
        UpdateInScaledSpace(FloatingWorldOrigin.Instance.scaledTransform.realPosition);
    }

    private void SwitchToWorldSpace()
    {
        if (!gameObject.activeSelf || !inScaledSpace || FloatingWorldOrigin.Instance == null)
            return;
        if (!visible)
            SetTrackedComponentsActive(true);
        inScaledSpace = false;
        if (scaledRigidbody != null)
            scaledRigidbody.active = false;
        transform.position = (_realPosition - FloatingWorldOrigin.Instance.scaledTransform.realPosition).ToVector3();
        transform.localScale = _realScale.ToVector3();
        UpdateVisualComponents();
    }

    public void UpdateVisualComponents()
    {
        if (inScaledSpace)
        {
            for (int i = 0; i < trackedColliders.Length; i++)
            {
                trackedColliders[i].gameObject.layer = scaledSpaceLayer;
            }
            for (int i = 0; i < trackedRenderers.Length; i++)
            {
                trackedRenderers[i].gameObject.layer = scaledSpaceLayer;
            }
        }
        else
        {
            for (int i = 0; i < trackedColliders.Length; i++)
            {
                trackedColliders[i].gameObject.layer = originalColliderLayers[i];
            }
            for (int i = 0; i < trackedRenderers.Length; i++)
            {
                trackedRenderers[i].gameObject.layer = originalRendererLayers[i];
            }
        }
    }

    public Renderer[] GetTrackedRenderers()
    {
        return (Renderer[])trackedRenderers.Clone();
    }

    public Collider[] GetTrackedColliders()
    {
        return (Collider[])trackedColliders.Clone();
    }
    
    /// <summary>
    /// Transforms position from render space to real space for this ScaledTransform.
    /// </summary>
    /// <param name="renderPoint"></param>
    /// <returns>Vector3d realPoint</returns>
    public Vector3d TransformRenderPoint(Vector3 renderPoint)
    {
        Vector3d offset = (renderPoint - transform.position).ToVector3d();
        if (scaledRigidbody.scaledTransform.inScaledSpace)
            offset *= scaledRigidbody.scaledTransform.scaleFactor;
        return _realPosition + offset;
    }

    /// <summary>
    /// Transforms position from real space to render space for this ScaledTransform.
    /// </summary>
    /// <param name="realPoint"></param>
    /// <returns>Vector3 renderPoint</returns>
    public Vector3 TransformRealPoint(Vector3d realPoint)
    {
        Vector3d offset = realPoint - _realPosition;
        if (scaledRigidbody.scaledTransform.inScaledSpace)
            offset /= scaledRigidbody.scaledTransform.scaleFactor;
        return transform.position + offset.ToVector3();
    }
}
