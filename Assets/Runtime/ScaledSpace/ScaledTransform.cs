using SpaceStuff;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

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
            if (!inScaledSpace)
            {
                if (FloatingWorldOrigin.Instance == null)
                {
                    StartCoroutine(WaitToUpdateRenderPos());
                }
                else
                {
                    UpdateInWorldSpace(FloatingWorldOrigin.Instance.scaledTransform.realPosition, true);
                }
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
    [SerializeField] private bool useRealScaleForRadius = false;
    [SerializeField] private Renderer[] worldSpaceOnlyRenderers;
    [SerializeField, Tooltip("Tracked colliders/renderers are disabled at screen sizes below this")] private float minScreenPixelSize = 3f;
    [SerializeField] private double scaleFactor = 1.0;

    [HideInInspector] public int index = -1;
    [HideInInspector] public bool visible = true;
    [HideInInspector] public Collider[] trackedColliders;
    [HideInInspector] public Renderer[] trackedRenderers;
    private int[] originalColliderLayers;
    private int[] originalRendererLayers;

    public int scaledSpaceLayer = 3;
    public bool inScaledSpace = false;
    public float worldSpaceThreshold = 3900;
    public float scaledSpaceThreshold = 4100;
    public double realRadius = -1.0;

    public Action<double> OnChangeScaleFactor;

    private void Awake()
    {
        TryGetComponent(out scaledRigidbody);

        ResetVisualComponents();
        
        // Make sure everything is setup properly for whichever space
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
        UpdateRealRadius();
    }

    private void OnEnable()
    {
        if (ScaledSpaceVisuals.Instance == null)
            return;
        ScaledSpaceVisuals.Instance.RegisterScaledTransform(this);
    }

    private void OnDisable()
    {
        ScaledSpaceVisuals.Instance.UnregisterScaledTransform(this);
    }

    private void OnValidate()
    {
        if (trackedColliders == null || trackedRenderers == null)
            GetVisualComponents();
        if (Camera.main != null)
            UpdateTransformEditor();
    }

    private IEnumerator WaitToUpdateRenderPos()
    {
        yield return new WaitUntil(() => FloatingWorldOrigin.Instance != null);
        if (!inScaledSpace)
            UpdateInWorldSpace(FloatingWorldOrigin.Instance.scaledTransform.realPosition, true);
    }

    private void GetVisualComponents()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
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
            if (trackedColliders[i] == null)
                continue;
            trackedColliders[i].enabled = value;
        }

        // Disable/enable renderers
        for (int i = 0; i < trackedRenderers.Length; i++)
        {
            if (trackedRenderers[i] == null)
                continue;
            trackedRenderers[i].enabled = value;
        }
        
        visible = value;
    }

    private void FixedUpdate()
    {
        UpdateTransform();
    }

    private void LateUpdate()
    {
        if (inScaledSpace && FloatingWorldOrigin.Instance != null && FloatingWorldOrigin.Instance.scaledTransform != this)
            UpdateInScaledSpace(FloatingWorldOrigin.Instance.scaledTransform.realPosition);
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
        UpdateRealRadius();
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
            // Unity retardation causes object render positions to appear to "lag" behind 
            // when player rotates/moves their camera around with unity rigidbody
            // so need to do this in LateUpdate instead
            // else
            //     UpdateInScaledSpace(originPosition);
        }
        else
        {
            if (sqrDistance > scaledSpaceThreshold * scaledSpaceThreshold)
                SwitchToScaledSpace();
            else
                UpdateInWorldSpace(originPosition, scaledRigidbody != null && scaledRigidbody.active);
        }

        if (useRealScaleForRadius)
        {
            realRadius = Math.Max(Math.Max(_realScale.x, _realScale.y), _realScale.z) * 0.5; // Good enough estimate
        }
    }

    private void CheckVisibility(double sqrDistance)
    {
        double sqrPixelSize = SpaceMath.CalculateSquarePixelSize(sqrDistance, realRadius);
        if (sqrPixelSize < minScreenPixelSize * minScreenPixelSize)
        {
            if (visible)
                SetTrackedComponentsActive(false);
            transform.position = Vector3.zero; // Reduce risk of floating point errors
            return;
        }

        if (!visible)
        {
            SetTrackedComponentsActive(true);
            if (ScaledSpaceVisuals.Instance != null)
                ScaledSpaceVisuals.Instance.UpdateScaleFactors();
        }
    }

    private void UpdateInScaledSpace(Vector3d originPosition)
    {
        if (Camera.main == null)
            return;
        // visualPosition = renderCamPos + scaledOffset
        // scaledOffset = (realPosition - realCamPos) / scale
        Vector3d renderCamPos = Camera.main.transform.position.ToVector3d();
        Vector3d realCamPos = originPosition + renderCamPos;
        Vector3d offset = _realPosition - realCamPos; // Unscaled offset from camera to object

        CheckVisibility(offset.sqrMagnitude);
        if (visible)
        {
            transform.position = (renderCamPos + offset / scaleFactor).ToVector3();
            transform.localScale = (_realScale / scaleFactor).ToVector3();
        }
    }

    private void UpdateInWorldSpace(Vector3d originPosition, bool updateRenderPosition)
    {
        transform.localScale = _realScale.ToVector3();
        if (updateRenderPosition)
        {
            // Should run if Editor changes realPosition (scaledRigidbody == null) or ScaledRigidbody changes realPosition with velocity
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
        if (FloatingWorldOrigin.Instance == null || Camera.main == null || FloatingWorldOrigin.Instance.scaledTransform == this || !gameObject.activeSelf || inScaledSpace)
            return;
        inScaledSpace = true;
        if (scaledRigidbody != null)
            scaledRigidbody.active = true;

        Vector3d renderCamPos = Camera.main.transform.position.ToVector3d();
        Vector3d realCamPos = FloatingWorldOrigin.Instance.scaledTransform.realPosition + renderCamPos;
        Vector3d offset = _realPosition - realCamPos; // Unscaled offset from camera to object
        CheckVisibility(offset.sqrMagnitude);
        if (visible)
            ScaledSpaceVisuals.Instance.UpdateScaleFactors();
        UpdateVisualComponents();
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
                if (trackedColliders[i] == null)
                    continue;
                trackedColliders[i].gameObject.layer = scaledSpaceLayer;
            }
            for (int i = 0; i < trackedRenderers.Length; i++)
            {
                if (trackedRenderers[i] == null)
                    continue;
                trackedRenderers[i].gameObject.layer = scaledSpaceLayer;
            }
            foreach(Renderer renderer in worldSpaceOnlyRenderers)
            {
                renderer.enabled = false;
            }
        }
        else
        {
            for (int i = 0; i < trackedColliders.Length; i++)
            {
                if (trackedColliders[i] == null)
                    continue;
                trackedColliders[i].gameObject.layer = originalColliderLayers[i];
            }
            for (int i = 0; i < trackedRenderers.Length; i++)
            {
                if (trackedRenderers[i] == null)
                    continue;
                trackedRenderers[i].gameObject.layer = originalRendererLayers[i];
            }
            foreach (Renderer renderer in worldSpaceOnlyRenderers)
            {
                renderer.enabled = true;
            }
        }
    }

    public void UpdateRealRadius()
    {
        // Update realRadius
        if (trackedRenderers.Length > 0 && !useRealScaleForRadius)
        {
            bool boundsSet = false;
            Bounds combinedBounds = new Bounds();
            for(int i = 0; i < trackedRenderers.Length; i++)
            {
                if (trackedRenderers[i] == null || !trackedRenderers[i].enabled)
                    continue;
                
                // Update realRadius
                if (!boundsSet)
                {
                    combinedBounds = trackedRenderers[i].bounds;
                    boundsSet = true;
                }
                else
                {
                    combinedBounds.Encapsulate(trackedRenderers[i].bounds);
                }
            }
            if (boundsSet)
            {
                realRadius = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y, combinedBounds.extents.z);
                if (inScaledSpace && scaleFactor > 0.0)
                    realRadius *= scaleFactor;
            }
        }
        else
        {
            realRadius = Math.Max(Math.Max(_realScale.x, _realScale.y), _realScale.z) * 0.5; // Good enough estimate
        }
    }

    public Renderer[] CloneTrackedRenderers()
    {
        return (Renderer[])trackedRenderers.Clone();
    }

    public Collider[] CloneTrackedColliders()
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
        if (inScaledSpace)
            offset *= scaleFactor;
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
        if (inScaledSpace)
            offset /= scaleFactor;
        return transform.position + offset.ToVector3();
    }

    public double GetScaleFactor()
    {
        return scaleFactor;
    }

    public void SetScaleFactor(double scaleFactor)
    {
        this.scaleFactor = scaleFactor;
        OnChangeScaleFactor?.Invoke(scaleFactor);
    }
}
