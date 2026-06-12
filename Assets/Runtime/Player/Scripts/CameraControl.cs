using SpaceStuff;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [SerializeField] private Transform lockedPoint;
    [SerializeField] private Transform freePoint;

    // Tracked in the ship's local space so world velocity cannot cause drift.
    [SerializeField] private Transform shipTransform;
    private Vector3    baseLocalPos;
    private Quaternion baseLocalRot;

    [Header("Follow")]
    [SerializeField] private float lerpSpeed  = 5f;
    [SerializeField] private float slerpSpeed = 10f;

    [Header("Shake")]
    [SerializeField, Tooltip("Linear acceleration (m/s²) below which no shake is applied")]
    private float shakeThreshold  = 39.2f;   // ~4 G
    private float invSqrShakeThreshold;
    [SerializeField, Tooltip("Shake displacement (m) per m/s² above the threshold")]
    private float shakeFactor     = 0.005f;
    [SerializeField]
    private float shakeDecaySpeed = 12f;

    [Header("Push (Linear)")]
    [SerializeField, Tooltip("Camera displacement (m) per m/s² of linear acceleration")]
    private float pushFactor    = 0.02f;
    [SerializeField]
    private float pushLerpSpeed = 4f;

    [Header("Tilt (Angular)")]
    [SerializeField, Tooltip("Camera tilt (degrees) per rad/s² of angular acceleration")]
    private float tiltFactor    = 5f;
    [SerializeField]
    private float tiltLerpSpeed = 4f;

    private Vector3 shakeOffset;      // world-space additive, decays in Update
    private Vector3 localPushOffset;  // ship local-space, added before the →world transform
    private Vector3 tiltEulerOffset;  // ship local-space Euler degrees

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        invSqrShakeThreshold = 1f / (shakeThreshold * shakeThreshold);
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
    }

    private void Update()
    {
        // ── Cursor lock toggle ──────────────────────────────────────────────────
        if (GameManager.Instance.inputActions.Player.CursorLock.WasPressedThisFrame())
        {
            if (Cursor.lockState == CursorLockMode.Locked)
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

        // ── Resolve target in ship local space ──────────────────────────────────
        Vector3    targetWorldPos;
        Quaternion targetWorldRot;
        if (Cursor.lockState == CursorLockMode.Locked)
            lockedPoint.GetPositionAndRotation(out targetWorldPos, out targetWorldRot);
        else
            freePoint.GetPositionAndRotation(out targetWorldPos, out targetWorldRot);

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

        // ── Lerp base pose in local space ───────────────────────────────────────
        // Ship translation does not change local-space positions, so no matter
        // how fast the ship is moving the camera cannot drift out of the hull.
        baseLocalPos = Vector3.Lerp   (baseLocalPos, targetLocalPos, lerpSpeed  * Time.deltaTime);
        baseLocalRot = Quaternion.Slerp(baseLocalRot, targetLocalRot, slerpSpeed * Time.deltaTime);

        // ── Shake decays per render frame ───────────────────────────────────────
        shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, shakeDecaySpeed * Time.deltaTime);

        // ── Compose final world transform ───────────────────────────────────────
        // localPushOffset is in ship local space, so it is summed with baseLocalPos
        // before the local→world conversion; it will therefore always push the camera
        // along the ship's axes regardless of the ship's world orientation.
        //
        // tiltEulerOffset is right-multiplied onto the world rotation, placing the
        // tilt in the camera's own local frame.
        //
        // shakeOffset is world-space and added last (small jitter; direction irrelevant).
        if (shipTransform != null)
        {
            transform.SetPositionAndRotation(
                shipTransform.TransformPoint(baseLocalPos + localPushOffset) + shakeOffset,
                shipTransform.rotation * baseLocalRot * Quaternion.Euler(tiltEulerOffset)
            );
        }
        else
        {
            transform.SetPositionAndRotation(
                baseLocalPos + localPushOffset + shakeOffset,
                baseLocalRot * Quaternion.Euler(tiltEulerOffset)
            );
        }
    }

    /// <summary>
    /// Called every FixedUpdate from Ship. Both vectors are in the ship's local frame.
    ///   localLinAcc : a = F/m       (m/s²,   local space)
    ///   localAngAcc : α = I⁻¹ · τ  (rad/s², local space)
    /// Passing zero vectors causes all offsets to decay back to neutral.
    /// </summary>
    public void UpdateForceTorqueMovement(Vector3 localLinAcc, Vector3 localAngAcc)
    {
        float dt = Time.fixedDeltaTime;

        // Shake: random impulse noise above the acceleration threshold.
        float linSqrMag = localLinAcc.sqrMagnitude;
        if (linSqrMag > shakeThreshold * shakeThreshold)
            shakeOffset = Random.insideUnitSphere * (linSqrMag * invSqrShakeThreshold * shakeFactor);
        // Decay handled in Update so it runs at render frequency.

        // Push: camera drifts opposite to local linear acceleration.
        localPushOffset = Vector3.Lerp(localPushOffset, -localLinAcc * pushFactor, pushLerpSpeed * dt);

        // Tilt: camera tilts opposite to local angular acceleration.
        // tiltFactor acts as (degrees) / (rad/s²), so 5 means 1 rad/s² → 5° of tilt.
        tiltEulerOffset = Vector3.Lerp(tiltEulerOffset, -localAngAcc * tiltFactor, tiltLerpSpeed * dt);
    }
}