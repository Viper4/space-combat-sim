using SpaceStuff;
using UnityEngine;

public class InertialEffects : MonoBehaviour
{
    [SerializeField] private Transform lockedPoint;
    [SerializeField] private Transform freePoint;

    // Tracked in the ship's local space so world velocity cannot cause drift.
    [SerializeField] private Transform shipTransform;
    [SerializeField] private AlertSystem alertSystem;
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
    private float maxPushOffset = 2f;
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

    private bool guiMode = false;

    private Camera[] cameras;

    [Header("Fade Effects")]
    [SerializeField, Tooltip("Minimum G force to start updating fade timer for these axes in the positive direction.")]
    private Vector3 minimumPositiveGForces;
    [SerializeField, Tooltip("Minimum G force to start updating fade timer for these axes in the negative direction.")]
    private Vector3 minimumNegativeGForces;
    [SerializeField, Tooltip("Slopes of the functions to calculate fadeTimer increment speed based on gForce for each axis.")] private Vector3 fadeSpeedSlopes;
    [SerializeField, Tooltip("How fast the fade effects (blackout/redout) go back to normal.")] private float recoverSpeed = 0.05f;
    private float fadeTimer;
    private float fadeSpeed;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameManager.Instance.AddRangedSettingListener("Field of View", OnFOVUpdated);
        
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

        cameras = GetComponentsInChildren<Camera>();
    }

    private void OnDestroy()
    {
        GameManager.Instance.RemoveRangedSettingListener("Field of View", OnFOVUpdated);
    }

    private void Update()
    {
        if (GameManager.Instance.IsPaused)
            return;
        // ── Cursor lock toggle ──────────────────────────────────────────────────
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
    }

    private void FixedUpdate()
    {
        // ── Resolve target in ship local space ──────────────────────────────────
        Vector3    targetWorldPos;
        Quaternion targetWorldRot;
        if (guiMode)
            freePoint.GetPositionAndRotation(out targetWorldPos, out targetWorldRot);
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

        // ── Lerp base pose in local space ───────────────────────────────────────
        // Ship translation does not change local-space positions, so no matter
        // how fast the ship is moving the camera cannot drift out of the hull.
        baseLocalPos = Vector3.Lerp   (baseLocalPos, targetLocalPos, lerpSpeed  * Time.fixedDeltaTime);
        baseLocalRot = Quaternion.Slerp(baseLocalRot, targetLocalRot, slerpSpeed * Time.fixedDeltaTime);

        // ── Shake decays per render frame ───────────────────────────────────────
        shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, shakeDecaySpeed * Time.fixedDeltaTime);

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

        // Apply G Force effects
        fadeTimer += fadeSpeed * Time.fixedDeltaTime;
        fadeTimer = Mathf.Clamp(fadeTimer, -2f, 2f);

        if (fadeTimer > 0f)
        {
            // Blackout
            GameManager.Instance.vignette.intensity.value = fadeTimer;
            GameManager.Instance.colorAdjustments.colorFilter.value = Color.Lerp(Color.white, Color.black, fadeTimer - 0.75f);
            GameManager.Instance.colorAdjustments.saturation.value = Mathf.Lerp(0f, -100f, fadeTimer);
            GameManager.Instance.screenBlur.strength.value = fadeTimer;
            GameManager.Instance.volumeMultiplier = Mathf.Max(0f, 1f - fadeTimer);

            if (fadeSpeed <= 0f)
                fadeTimer = Mathf.Max(0, fadeTimer - recoverSpeed * Time.fixedDeltaTime);
        }
        else if (fadeTimer < 0f)
        {
            // Redout
            GameManager.Instance.vignette.intensity.value = 0f;
            GameManager.Instance.colorAdjustments.colorFilter.value = Color.Lerp(Color.white, Color.red, -fadeTimer);
            GameManager.Instance.colorAdjustments.saturation.value = 0f;
            GameManager.Instance.screenBlur.strength.value = -fadeTimer * 1.2f;
            GameManager.Instance.volumeMultiplier = 1f;

            if (fadeSpeed >= 0f)
                fadeTimer = Mathf.Min(0, fadeTimer + recoverSpeed * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Called every FixedUpdate from Ship. Both vectors are in the ship's local frame.
    ///   localLinAcc : a = F/m       (m/s²,   local space)
    ///   localAngAcc : α = I⁻¹ · τ  (rad/s², local space)
    /// Passing zero vectors causes all offsets to decay back to neutral.
    /// </summary>
    public void UpdateEffects(Vector3 localLinAcc, Vector3 localAngAcc, Vector3 angularVelocity)
    {
        float dt = Time.fixedDeltaTime;

        Vector3 tangentialAccel = Vector3.Cross(localAngAcc, baseLocalPos);
        Vector3 centripetalAccel = Vector3.Cross(angularVelocity, Vector3.Cross(angularVelocity, baseLocalPos));

        Vector3 rotationalAccel = tangentialAccel + centripetalAccel;
        Vector3 totalLinearAcceleration = localLinAcc + rotationalAccel;

        // Shake: random impulse noise above the acceleration threshold.
        float linSqrMag = localLinAcc.sqrMagnitude;
        if (linSqrMag > shakeThreshold * shakeThreshold)
            shakeOffset = Random.insideUnitSphere * (linSqrMag * invSqrShakeThreshold * shakeFactor);
        // Decay handled in Update so it runs at render frequency.

        // Push: camera drifts opposite to total linear acceleration.
        localPushOffset = Vector3.Lerp(localPushOffset, -totalLinearAcceleration * pushFactor, pushLerpSpeed * dt);
        localPushOffset.x = Mathf.Clamp(localPushOffset.x, -maxPushOffset, maxPushOffset);
        localPushOffset.y = Mathf.Clamp(localPushOffset.y, -maxPushOffset, maxPushOffset);
        localPushOffset.z = Mathf.Clamp(localPushOffset.z, -maxPushOffset, maxPushOffset);

        // Tilt: camera tilts opposite to local angular acceleration.
        // tiltFactor acts as (degrees) / (rad/s²), so 5 means 1 rad/s² → 5° of tilt.
        tiltEulerOffset = Vector3.Lerp(tiltEulerOffset, -localAngAcc * tiltFactor, tiltLerpSpeed * dt);

        // Calculate fade speed for G force blackout/redout effects
        Vector3 g = totalLinearAcceleration / 9.81f;
        fadeSpeed = 0f;
        // Pushing left and right (both blackout)
        fadeSpeed += Mathf.Max(0f, fadeSpeedSlopes.x * (g.x - minimumPositiveGForces.x));
        fadeSpeed += Mathf.Max(0f, -fadeSpeedSlopes.x * (g.x + minimumPositiveGForces.x));

        fadeSpeed += Mathf.Max(0f, fadeSpeedSlopes.y * (g.y - minimumPositiveGForces.y)); // Pushing blood head->feet (blackout)
        fadeSpeed -= Mathf.Max(0f, -fadeSpeedSlopes.y * (g.y + minimumNegativeGForces.y)); // Pushing blood feet->head (redout)

        // Pushing forward and backward (both blackout)
        fadeSpeed += Mathf.Max(0f, fadeSpeedSlopes.z * (g.z - minimumPositiveGForces.z));
        fadeSpeed += Mathf.Max(0f, -fadeSpeedSlopes.z * (g.z + minimumPositiveGForces.z));
        alertSystem.SetAlert("Over G", (fadeSpeed > 0f && fadeTimer > 0.05f) || (fadeSpeed < 0f && fadeTimer < -0.025f));
    }

    private void OnFOVUpdated()
    {
        float fov = GameManager.Instance.GetRangedSettingValue("Field of View");
        foreach(Camera camera in cameras)
        {
            camera.fieldOfView = fov;
        }
    }
}