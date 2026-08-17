using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orbits a camera around a target transform.
/// - Optional automatic orbit around a configurable world-space axis.
/// - Click-and-drag orbiting via the new Input System.
/// - Scroll wheel zoom with distance clamping.
/// </summary>
[AddComponentMenu("Camera-Control/Orbit Camera")]
public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform the camera orbits around.")]
    [SerializeField] private Transform target;
    [Tooltip("Local offset from the target's position used as the orbit pivot.")]
    [SerializeField] private Vector3 targetOffset = Vector3.zero;

    [Header("Auto Orbit")]
    [SerializeField] private bool autoOrbit = false;
    [Tooltip("Degrees per second the camera auto-orbits when enabled.")]
    [SerializeField] private float autoOrbitSpeed = 10f;
    [Tooltip("World-space axis the auto-orbit rotates around.")]
    [SerializeField] private Vector3 autoOrbitAxis = Vector3.up;

    [Header("Drag Rotation")]
    [SerializeField] private bool allowDragRotation = true;
    [Tooltip("Degrees per pixel of pointer movement.")]
    [SerializeField] private float dragSensitivity = 0.25f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;
    [Tooltip("Clamp the vertical orbit angle to avoid flipping over the poles.")]
    [SerializeField] private bool clampPitch = true;
    [SerializeField] private float minPitch = -89f;
    [SerializeField] private float maxPitch = 89f;

    [Header("Zoom")]
    [SerializeField] private bool allowZoom = true;
    [SerializeField] private float zoomSensitivity = 1f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private float zoomSmoothTime = 0.1f;

    [Header("Initial State")]
    [SerializeField] private float startDistance = 10f;
    [SerializeField] private float startYaw = 0f;
    [SerializeField] private float startPitch = 20f;

    private float yaw;
    private float pitch;
    private float distance;
    private float targetDistance;
    private float zoomVelocity;

    private bool isDragging;
    private Vector2 lastPointerPosition;

    private InputAction pointAction;
    private InputAction dragAction;
    private InputAction scrollAction;

    private void Awake()
    {
        yaw = startYaw;
        pitch = startPitch;
        distance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        targetDistance = distance;

        // Built with the new Input System's generic pointer actions so this
        // works across mouse, pen, and touch without extra setup.
        pointAction = new InputAction("OrbitCameraPoint", binding: "<Pointer>/position");
        dragAction = new InputAction("OrbitCameraDrag", binding: "<Mouse>/leftButton");
        scrollAction = new InputAction("OrbitCameraScroll", binding: "<Mouse>/scroll/y");
    }

    private void OnEnable()
    {
        pointAction.Enable();
        dragAction.Enable();
        scrollAction.Enable();

        dragAction.started += OnDragStarted;
        dragAction.canceled += OnDragEnded;
    }

    private void OnDisable()
    {
        dragAction.started -= OnDragStarted;
        dragAction.canceled -= OnDragEnded;

        pointAction.Disable();
        dragAction.Disable();
        scrollAction.Disable();

        isDragging = false;
    }

    private void OnDragStarted(InputAction.CallbackContext ctx)
    {
        if (!allowDragRotation) return;

        isDragging = true;
        lastPointerPosition = pointAction.ReadValue<Vector2>();
    }

    private void OnDragEnded(InputAction.CallbackContext ctx)
    {
        isDragging = false;
    }

    private void Update()
    {
        if (target == null) return;

        HandleAutoOrbit();
        HandleDragRotation();
        HandleZoom();
        ApplyTransform();
    }

    private void HandleAutoOrbit()
    {
        if (!autoOrbit) return;

        // Auto-orbit rotates the yaw/pitch representation by rotating a
        // direction vector around the given world axis, then re-deriving
        // yaw/pitch so it composes cleanly with drag input and arbitrary axes.
        Quaternion currentRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 currentDir = currentRot * Vector3.back; // direction from target to camera

        Quaternion autoRot = Quaternion.AngleAxis(autoOrbitSpeed * Time.deltaTime, autoOrbitAxis.normalized);
        Vector3 newDir = autoRot * currentDir;

        DirectionToYawPitch(newDir, out yaw, out pitch);
    }

    private void HandleDragRotation()
    {
        if (!allowDragRotation || !isDragging) return;

        Vector2 currentPointerPosition = pointAction.ReadValue<Vector2>();
        Vector2 delta = currentPointerPosition - lastPointerPosition;
        lastPointerPosition = currentPointerPosition;

        float xSign = invertX ? -1f : 1f;
        float ySign = invertY ? 1f : -1f;

        yaw += delta.x * dragSensitivity * xSign;
        pitch += delta.y * dragSensitivity * ySign;

        if (clampPitch)
        {
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private void HandleZoom()
    {
        if (!allowZoom) return;

        float scroll = scrollAction.ReadValue<float>();
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
        {
            targetDistance -= scroll * zoomSensitivity;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        distance = zoomSmoothTime > 0f
            ? Mathf.SmoothDamp(distance, targetDistance, ref zoomVelocity, zoomSmoothTime)
            : targetDistance;
    }

    private void ApplyTransform()
    {
        Vector3 pivot = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = pivot + offset;
        transform.rotation = rotation;
    }

    /// <summary>
    /// Converts a direction (from target to camera) into yaw/pitch angles
    /// consistent with Quaternion.Euler(pitch, yaw, 0) * Vector3.back.
    /// </summary>
    private static void DirectionToYawPitch(Vector3 direction, out float yawOut, out float pitchOut)
    {
        direction = direction.normalized;
        pitchOut = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        yawOut = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + 180f;
    }

    /// <summary>Sets the orbit target at runtime.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>Enables or disables auto-orbit at runtime.</summary>
    public void SetAutoOrbit(bool enabled, float speed = -1f, Vector3? axis = null)
    {
        autoOrbit = enabled;
        if (speed >= 0f) autoOrbitSpeed = speed;
        if (axis.HasValue) autoOrbitAxis = axis.Value;
    }
}