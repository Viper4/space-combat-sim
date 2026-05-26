using SpaceStuff;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;

[RequireComponent(typeof(TransformChange)), RequireComponent(typeof(DoubleRigidbody))]
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
            if (!inScaledSpace)
                transform.position = _realPosition.ToVector3(); // Need to update manually
            UpdateTransform();
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
            _realScale = value;
            UpdateTransform();
        }
    }
    [SerializeField, Tooltip("Tracked colliders/renderers are disabled at screen sizes below this")] private float minScreenPixelSize = 3f;
    
    private bool trackedComponentsActive = true;
    private Collider[] trackedColliders;
    private Renderer[] trackedRenderers;
    private int[] originalColliderLayers;
    private int[] originalRendererLayers;
    public int scaledSpaceLayer = 3;

    public bool inScaledSpace = true;
    public double scaleFactor = 1000;
    public float worldSpaceThreshold = 3900;
    public float scaledSpaceThreshold = 4100;

    private void Awake()
    {
        doubleRigidbody = GetComponent<DoubleRigidbody>();
        transformChange = GetComponent<TransformChange>();

        ResetVisualComponents(true);
        SetTrackedComponentsActive(trackedComponentsActive);
    }

    private void OnValidate()
    {
        if (trackedColliders == null || trackedRenderers == null)
            ResetVisualComponents(false, true);
        if (Camera.main != null)
            UpdateTransformEditor();
    }

    public void ResetVisualComponents(bool setOriginalLayers, bool inEditor = false)
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        List<Collider> tempColliders = new List<Collider>();
        List<Renderer> tempRenderers = new List<Renderer>();

        for (int i = 0; i < allChildren.Length; i++)
        {
            Collider collider = allChildren[i].GetComponent<Collider>();
            Renderer renderer = allChildren[i].GetComponent<Renderer>();
            if (collider != null && !collider.isTrigger)
            {
                tempColliders.Add(collider);
            }
            if (renderer != null)
            {
                tempRenderers.Add(renderer);
            }
        }
        trackedColliders = tempColliders.ToArray();
        trackedRenderers = tempRenderers.ToArray();
        originalColliderLayers = new int[trackedColliders.Length];
        originalRendererLayers = new int[trackedRenderers.Length];

        if (setOriginalLayers)
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

        if (!inEditor)
        {
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
        trackedComponentsActive = value;
    }

    private float GetSquareScreenSize(double sqrCameraDistance)
    {
        // screenSize ≈ objectRadius / (distance * tan(FOV/2))
        if (sqrCameraDistance <= 0)
            return 1.0f;
        double radius = Math.Max(Math.Max(_realScale.x, _realScale.y), _realScale.z);
        double tan = Math.Tan(Camera.main.fieldOfView * Mathf.Deg2Rad * 0.5);
        return (float)(radius * radius / (sqrCameraDistance * tan * tan));
    }

    private float GetSquarePixelSize(float sqrScreenSize)
    {
        return sqrScreenSize * Screen.height * Screen.height;
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
            UpdateInWorldSpace(originPosition);
        }
    }

    private void UpdateTransform()
    {
        Vector3d originPosition = FloatingWorldOrigin.Instance.worldOriginPosition;
        double sqrDistance = (_realPosition - originPosition).sqrMagnitude;
        if (inScaledSpace)
        {
            UpdateInScaledSpace(originPosition);
            if (sqrDistance < worldSpaceThreshold * worldSpaceThreshold)
                SwitchToWorldSpace();
        }
        else
        {
            UpdateInWorldSpace(originPosition);
            if (sqrDistance > scaledSpaceThreshold * scaledSpaceThreshold)
                SwitchToScaledSpace();
        }
    }

    private void UpdateInScaledSpace(Vector3d originPosition)
    {
        // visualPosition = renderCamPos + scaledOffset
        // scaledOffset = (realPosition - realCamPos) / scale
        Vector3d renderCamPos = Camera.main.transform.position.ToVector3d();
        Vector3d realCamPos = originPosition + renderCamPos;
        Vector3d offset = _realPosition - realCamPos; // Unscaled offset from camera to object

        float sqrPixelSize = GetSquarePixelSize(GetSquareScreenSize(offset.sqrMagnitude));
        if (sqrPixelSize < minScreenPixelSize * minScreenPixelSize)
        {
            if (trackedComponentsActive)
                SetTrackedComponentsActive(false);
            transform.position = Vector3.zero; // Reduce risk of floating point errors
            return;
        }
        else
        {
            if (!trackedComponentsActive)
                SetTrackedComponentsActive(true);
        }

        transform.position = (renderCamPos + offset / scaleFactor).ToVector3();
        transform.localScale = (_realScale / scaleFactor).ToVector3();
    }

    private void UpdateInWorldSpace(Vector3d originPosition)
    {
        // In world space, we use regular ridigbody and transform, so need to update ScaledTransform's position and scale to match
        _realPosition = originPosition + transform.position.ToVector3d();
        _realScale = transform.localScale.ToVector3d();
    }

    public void SwitchToDoubleRigidbody()
    {
        transformChange.OnPositionChange -= UpdateTransform; // doubleRigidbody takes over movement updates
    }

    public void SwitchToRigidbody()
    {
        transformChange.OnPositionChange -= UpdateTransform; // Prevent double update from multiple consecutive calls
        transformChange.OnPositionChange += UpdateTransform; // doubleRigidbody no longer updates position, so track movement manually
    }

    private void SwitchToScaledSpace()
    {
        // Floating origin should never be in scaled space since we assume the origin has the camera
        if (FloatingWorldOrigin.Instance.transform == transform || !gameObject.activeSelf || inScaledSpace)
            return;
        inScaledSpace = true;
        doubleRigidbody.active = true;
        for (int i = 0; i < trackedColliders.Length; i++)
        {
            trackedColliders[i].gameObject.layer = scaledSpaceLayer;
        }
        for (int i = 0; i < trackedRenderers.Length; i++)
        {
            trackedRenderers[i].gameObject.layer = scaledSpaceLayer;
        }
        FloatingWorldOrigin.Instance.cameraTC.OnPositionChange -= UpdateTransform; // Prevent double update from multiple consecutive calls
        FloatingWorldOrigin.Instance.cameraTC.OnPositionChange += UpdateTransform; // Need to maintain illusion if camera moves
        UpdateInScaledSpace(FloatingWorldOrigin.Instance.worldOriginPosition);
    }

    private void SwitchToWorldSpace()
    {
        if (!gameObject.activeSelf || !inScaledSpace)
            return;
        if (!trackedComponentsActive)
            SetTrackedComponentsActive(true);
        inScaledSpace = false;
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
        FloatingWorldOrigin.Instance.cameraTC.OnPositionChange -= UpdateTransform; // ScaledTransform and Transform match so dont need to maintain illusion
    }
}
