using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Master script for the camera: owns the transform, the follow behavior, GUI-mode
/// cursor toggling, mouse-look while in guiMode, FOV syncing, and all inertial
/// offset state (shake/push/tilt). InertialEffects computes those offsets from ship
/// acceleration and pushes them here via SetPushAndTilt/AddShakeImpulse — CameraControl
/// holds no reference to InertialEffects and is the single place that writes to transform.
/// </summary>
public class CameraControl : MonoBehaviour
{
    [SerializeField] private Transform lockedPoint;
    [SerializeField] private Transform[] freePoints;
    private int currentFreePoint;

    // Tracked in the ship's local space so world velocity cannot cause drift.
    [SerializeField] private Transform shipTransform;

    private Vector3    baseLocalPos;
    private Quaternion baseLocalRot;

    [Header("Follow")]
    [SerializeField] private float lerpSpeed  = 5f;
    [SerializeField] private float slerpSpeed = 10f;

    [Header("Inertial Offsets (driven by InertialEffects)")]
    [SerializeField]
    private float shakeDecaySpeed = 12f;
    private Vector3 shakeOffset;      // world-space additive, decays each FixedUpdate
    private Vector3 localPushOffset;  // ship local-space, set directly by InertialEffects
    private Vector3 tiltEulerOffset;  // ship local-space Euler degrees, set directly by InertialEffects

    [Header("Mouse Look (GUI Mode)")]
    [SerializeField, Tooltip("Max tilt (degrees) on each local axis from mouse look")]
    private Vector2 maxMouseLookTilt = new Vector2(15f, 20f);
    [SerializeField, Tooltip("Max positional offset (m) on each local axis from mouse look")]
    private Vector2 maxMouseLookOffset = new Vector2(0.5f, 0.5f);
    [SerializeField, Tooltip("How quickly mouse-look offset returns to zero after leaving GUI mode")]
    private float mouseLookReturnSpeed = 3f;
    [SerializeField, Tooltip("How quickly mouse-look offset follows the mouse's current screen position")]
    private float mouseLookFollowSpeed = 8f;

    private Vector3 mouseLookTiltOffset;   // local Euler degrees, holds at the mouse's current screen position
    private Vector3 mouseLookPosOffset;    // local-space meters, holds at the mouse's current screen position

    private bool guiMode = false;

    private Camera[] cameras;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentFreePoint = freePoints.Length / 2;

        GameManager.Instance.AddRangedSettingListener("Field of View", OnFOVUpdated);

        if (shipTransform != null)
        {
            baseLocalPos = shipTransform.InverseTransformPoint(lockedPoint.position);
            baseLocalRot = Quaternion.Inverse(shipTransform.rotation) * lockedPoint.rotation;
        }
        else
        {
            // Camera is not a child of the ship — fall back to world space.
            baseLocalPos = lockedPoint.position;
            baseLocalRot = lockedPoint.rotation;
        }

        cameras = GetComponentsInChildren<Camera>();
    }

    private void OnDestroy()
    {
        GameManager.Instance.RemoveRangedSettingListener("Field of View", OnFOVUpdated);
    }

    // ── Public API for InertialEffects ───────────────────────────────────────
    // InertialEffects computes these from ship acceleration each FixedUpdate and
    // pushes the results in here; CameraControl composes them into the final transform.

    /// <summary>Ship-local-space push offset (inertial drift) and tilt (angular inertia), in degrees.</summary>
    public void SetPushAndTilt(Vector3 localPushOffset, Vector3 tiltEulerOffsetDegrees)
    {
        this.localPushOffset = localPushOffset;
        this.tiltEulerOffset = tiltEulerOffsetDegrees;
    }

    /// <summary>
    /// Adds a world-space shake impulse. Overwrites rather than accumulates, matching
    /// the original single-impulse-per-threshold-breach behavior; shake decays on its
    /// own each FixedUpdate.
    /// </summary>
    public void AddShakeImpulse(Vector3 worldShakeOffset)
    {
        shakeOffset = worldShakeOffset;
    }

    /// <summary>Current ship-local camera position, for InertialEffects to use as its lever arm.</summary>
    public Vector3 GetCameraLocalPosition() => baseLocalPos;

    private void Update()
    {
        if (GameManager.Instance.IsPaused)
            return;

        // ── Cursor lock toggle ──────────────────────────────────────────────
        if (GameManager.Instance.inputActions.Player.GUIToggle.WasPressedThisFrame())
        {
            guiMode = !guiMode;
            if (guiMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
        }

        // ── Mouse look while in GUI mode ─────────────────────────────────────
        // Uses absolute mouse position on screen (not per-frame delta), normalized
        // to [-1, 1] from screen center, so the camera HOLDS wherever the mouse is
        // rather than reverting to center the instant the mouse stops moving.
        // Moving the mouse left/right tilts+pans the camera left/right (yaw, +X pan),
        // moving up/down tilts+pans the camera up/down (pitch, +Y pan).
        if (guiMode)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 normalized = new Vector2(
                Mathf.Clamp(mousePos.x / Screen.width, 0f, 1f),
                Mathf.Clamp(mousePos.y / Screen.height, 0f, 1f)
            );

            Vector3 targetTilt = new Vector3(
                Mathf.Lerp(-maxMouseLookTilt.x, maxMouseLookTilt.x, 1f - normalized.y), // mouse toward top of screen -> tilt up
                Mathf.Lerp(-maxMouseLookTilt.y, maxMouseLookTilt.y, normalized.x), // mouse toward right of screen -> tilt right
                0f
            );

            Vector3 targetPos = new Vector3(
                Mathf.Lerp(-maxMouseLookOffset.x, maxMouseLookOffset.x, normalized.x),
                Mathf.Lerp(-maxMouseLookOffset.y, maxMouseLookOffset.y, normalized.y),
                0f
            );

            // Always ease toward the target derived from current mouse position —
            // when the mouse stops moving, normalized stays constant, so the offset
            // settles at that position instead of reverting to center.
            mouseLookTiltOffset = Vector3.Lerp(mouseLookTiltOffset, targetTilt, mouseLookFollowSpeed * Time.deltaTime);
            mouseLookPosOffset  = Vector3.Lerp(mouseLookPosOffset,  targetPos,  mouseLookFollowSpeed * Time.deltaTime);

            if (GameManager.Instance.inputActions.Player.GUILeft.WasPressedThisFrame())
            {
                currentFreePoint = Mathf.Max(currentFreePoint-1, 0);
            }

            if (GameManager.Instance.inputActions.Player.GUIRight.WasPressedThisFrame())
            {
                currentFreePoint = Mathf.Min(currentFreePoint+1, freePoints.Length-1);
            }
        }
        else
        {
            // Not in GUI mode — ease any residual mouse-look offset back to zero.
            mouseLookTiltOffset = Vector3.Lerp(mouseLookTiltOffset, Vector3.zero, mouseLookReturnSpeed * Time.deltaTime);
            mouseLookPosOffset  = Vector3.Lerp(mouseLookPosOffset,  Vector3.zero, mouseLookReturnSpeed * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        // ── Resolve target in ship local space ──────────────────────────────
        Vector3    targetWorldPos;
        Quaternion targetWorldRot;
        if (guiMode)
            freePoints[currentFreePoint].GetPositionAndRotation(out targetWorldPos, out targetWorldRot);
        else
            lockedPoint.GetPositionAndRotation(out targetWorldPos, out targetWorldRot);

        Vector3    targetLocalPos;
        Quaternion targetLocalRot;
        if (shipTransform != null)
        {
            targetLocalPos = shipTransform.InverseTransformPoint(targetWorldPos);
            targetLocalRot = Quaternion.Inverse(shipTransform.rotation) * targetWorldRot;
        }
        else
        {
            targetLocalPos = targetWorldPos;
            targetLocalRot = targetWorldRot;
        }

        // ── Lerp base pose in local space ────────────────────────────────────
        // Ship translation does not change local-space positions, so no matter
        // how fast the ship is moving the camera cannot drift out of the hull.
        baseLocalPos = Vector3.Lerp   (baseLocalPos, targetLocalPos, lerpSpeed  * Time.fixedDeltaTime);
        baseLocalRot = Quaternion.Slerp(baseLocalRot, targetLocalRot, slerpSpeed * Time.fixedDeltaTime);

        // ── Shake decays per fixed step ──────────────────────────────────────
        shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, shakeDecaySpeed * Time.fixedDeltaTime);

        // ── Compose final world transform ────────────────────────────────────
        // localPushOffset/tiltEulerOffset/mouse-look offsets are all in ship or
        // camera local space, so they're summed/right-multiplied before the
        // local→world conversion — they stay aligned to the ship's/camera's own
        // axes regardless of world orientation.
        //
        // shakeOffset is world-space and added last (small jitter; direction irrelevant).
        Vector3 localPos = baseLocalPos + localPushOffset + mouseLookPosOffset;
        Quaternion localRot = baseLocalRot
            * Quaternion.Euler(tiltEulerOffset)
            * Quaternion.Euler(mouseLookTiltOffset);

        if (shipTransform != null)
        {
            transform.SetPositionAndRotation(
                shipTransform.TransformPoint(localPos) + shakeOffset,
                shipTransform.rotation * localRot
            );
        }
        else
        {
            transform.SetPositionAndRotation(
                localPos + shakeOffset,
                localRot
            );
        }
    }

    private void OnFOVUpdated()
    {
        float fov = GameManager.Instance.GetRangedSettingValue("Field of View");
        foreach (Camera camera in cameras)
        {
            camera.fieldOfView = fov;
        }
    }
}