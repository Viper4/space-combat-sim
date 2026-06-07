using SpaceStuff;
using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(TransformChange))]
public class ScaledTransform : MonoBehaviour
{
    private DoubleRigidbody doubleRigidbody;
    private TransformChange transformChange;

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
            UpdateTransform(true);
        }
    }
    [SerializeField] private Vector3d _realScale; // Actual world scale
    public Vector3d realScale
    {
        get
        {
            return _realScale;
        }
        set
        {
            if (_realScale != value && doubleRigidbody != null)
                ScaledSpacePhysics.Instance.UpdateGridSize(doubleRigidbody);
            _realScale = value;
            UpdateTransform(true);
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
    public float worldSpaceThreshold = 3900;
    public float scaledSpaceThreshold = 4100;

    private void Awake()
    {
        TryGetComponent(out doubleRigidbody);
        transformChange = GetComponent<TransformChange>();

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
        if (doubleRigidbody != null)
        {
            if (doubleRigidbody.active)
            {
                SwitchToDoubleRigidbody();
            }
            else
            {
                SwitchToRigidbody();
            }
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

    /// <summary>
    /// Returns the square of the size of the object assumed to be a sphere with radius=max(realScale.x, realScale.y, realScale.z) on screen in pixels based on its given square distance from the camera's realPosition.
    /// </summary>
    /// <param name="sqrCameraDistance">Square distance from the camera in the actual world (using realPosition)</param>
    /// <returns>Pixel size squared</returns>
    public float GetSquarePixelSize(double sqrCameraDistance)
    {
        // screenSize ≈ objectRadius / (distance * tan(FOV/2))
        if (sqrCameraDistance <= 0)
            return 1.0f;
        double radius = Math.Max(Math.Max(_realScale.x, _realScale.y), _realScale.z);
        double tan = Math.Tan(Camera.main.fieldOfView * Mathf.Deg2Rad * 0.5);
        double sqrScreenSize = radius * radius / (sqrCameraDistance * tan * tan);
        return (float)(sqrScreenSize * Screen.height * Screen.height);
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

    private void UpdateTransform(bool forceWorldTransformUpdate)
    {
        Vector3d originPosition = FloatingWorldOrigin.Instance.worldOriginPosition;
        if (FloatingWorldOrigin.Instance.transform == transform)
        {
            if (FloatingWorldOrigin.Instance.OverShiftThreshold())
                return; // Let ScaledSpacePhysics handle origin shift, don't update origin's transform.position to outside of shiftThreshold
            UpdateInWorldSpace(originPosition, forceWorldTransformUpdate);
            return;
        }

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
                UpdateInWorldSpace(originPosition, forceWorldTransformUpdate);
        }
    }

    private void UpdateTransformByEvent()
    {
        UpdateTransform(false);
    }

    private void UpdateInScaledSpace(Vector3d originPosition)
    {
        // visualPosition = renderCamPos + scaledOffset
        // scaledOffset = (realPosition - realCamPos) / scale
        Vector3d renderCamPos = Camera.main.transform.position.ToVector3d();
        Vector3d realCamPos = originPosition + renderCamPos;
        Vector3d offset = _realPosition - realCamPos; // Unscaled offset from camera to object

        float sqrPixelSize = GetSquarePixelSize(offset.sqrMagnitude);
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

    private void UpdateInWorldSpace(Vector3d originPosition, bool forceUpdateTransform)
    {
        if (forceUpdateTransform || doubleRigidbody == null || doubleRigidbody.active)
        {
            // Assume Editor changes realPosition (doubleRigidbody == null) and DoubleRigidbody changes realPosition with velocity
            transform.position = (_realPosition - originPosition).ToVector3();
            transform.localScale = _realScale.ToVector3();
        }
        else
        {
            // Rigidbody changes transform.position with linearVelocity
            _realPosition = originPosition + transform.position.ToVector3d();
            _realScale = transform.localScale.ToVector3d();
        }
    }

    public void SwitchToDoubleRigidbody()
    {
        transformChange.OnPositionChange -= UpdateTransformByEvent; // doubleRigidbody takes over movement updates
    }

    public void SwitchToRigidbody()
    {
        transformChange.OnPositionChange -= UpdateTransformByEvent; // Prevent double update from multiple consecutive calls
        transformChange.OnPositionChange += UpdateTransformByEvent; // doubleRigidbody no longer updates position, so track movement manually
    }

    private void SwitchToScaledSpace()
    {
        // Floating origin should never be in scaled space since we assume the origin has the camera
        if (FloatingWorldOrigin.Instance.transform == transform || !gameObject.activeSelf || inScaledSpace)
            return;
        inScaledSpace = true;
        if (doubleRigidbody != null)
            doubleRigidbody.active = true;
        for (int i = 0; i < trackedColliders.Length; i++)
        {
            trackedColliders[i].gameObject.layer = scaledSpaceLayer;
        }
        for (int i = 0; i < trackedRenderers.Length; i++)
        {
            trackedRenderers[i].gameObject.layer = scaledSpaceLayer;
        }
        FloatingWorldOrigin.Instance.cameraTC.OnPositionChange -= UpdateTransformByEvent; // Prevent double update from multiple consecutive calls
        FloatingWorldOrigin.Instance.cameraTC.OnPositionChange += UpdateTransformByEvent; // Need to maintain illusion if camera moves
        UpdateInScaledSpace(FloatingWorldOrigin.Instance.worldOriginPosition);
    }

    private void SwitchToWorldSpace()
    {
        if (!gameObject.activeSelf || !inScaledSpace)
            return;
        if (!visible)
            SetTrackedComponentsActive(true);
        inScaledSpace = false;
        if (doubleRigidbody != null)
            doubleRigidbody.active = false;
        transform.position = (_realPosition - FloatingWorldOrigin.Instance.worldOriginPosition).ToVector3();
        transform.localScale = _realScale.ToVector3();
        for (int i = 0; i < trackedColliders.Length; i++)
        {
            trackedColliders[i].gameObject.layer = originalColliderLayers[i];
        }
        for (int i = 0; i < trackedRenderers.Length; i++)
        {
            trackedRenderers[i].gameObject.layer = originalRendererLayers[i];
        }
        FloatingWorldOrigin.Instance.cameraTC.OnPositionChange -= UpdateTransformByEvent; // ScaledTransform and Transform match so dont need to maintain illusion
    }

    public Renderer[] GetTrackedRenderers()
    {
        return (Renderer[])trackedRenderers.Clone();
    }

    public Collider[] GetTrackedColliders()
    {
        return (Collider[])trackedColliders.Clone();
    }

    private void OnDestroy()
    {
        FloatingWorldOrigin.Instance.cameraTC.OnPositionChange -= UpdateTransformByEvent;
    }
    
    public Vector3d GetChildRealPosition(Vector3 childPosition)
    {
        Vector3d offset = (transform.position - childPosition).ToVector3d();
        if (doubleRigidbody.scaledTransform.inScaledSpace)
            offset *= doubleRigidbody.scaledTransform.scaleFactor;
        return _realPosition - offset;
    }
}
