using UnityEngine;

/// <summary>
/// Computes camera-relative offsets caused by the ship's linear/angular acceleration:
/// shake (impulse jitter), push (inertial drift), tilt (angular inertia), and
/// G-force blackout/redout fade values. CameraControl is the master script — it owns
/// the transform and all offset state; InertialEffects pushes its computed results
/// into CameraControl via SetPushAndTilt/AddShakeImpulse each FixedUpdate.
/// </summary>
public class InertialEffects : MonoBehaviour
{
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private AlertSystem alertSystem;

    [Header("Shake")]
    [SerializeField, Tooltip("Linear acceleration (m/s²) below which no shake is applied")]
    private float shakeThreshold  = 39.2f;   // ~4 G
    private float invSqrShakeThreshold;
    [SerializeField, Tooltip("Shake displacement (m) per m/s² above the threshold")]
    private float shakeFactor     = 0.005f;

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

    // Local running state used to lerp toward each new target each FixedUpdate.
    // (CameraControl owns the offsets actually applied to the transform; these are
    // InertialEffects' own working copies so it can smoothly interpolate them.)
    private Vector3 localPushOffset;
    private Vector3 tiltEulerOffset;

    [Header("Fade Effects")]
    [SerializeField, Tooltip("Minimum G force to start updating fade timer for these axes in the positive direction.")]
    private Vector3 minimumPositiveGForces;
    [SerializeField, Tooltip("Minimum G force to start updating fade timer for these axes in the negative direction.")]
    private Vector3 minimumNegativeGForces;
    [SerializeField, Tooltip("Slopes of the functions to calculate fadeTimer increment speed based on gForce for each axis.")]
    private Vector3 fadeSpeedSlopes;
    [SerializeField, Tooltip("How fast the fade effects (blackout/redout) go back to normal.")]
    private float recoverSpeed = 0.05f;
    private float fadeTimer;
    private float fadeSpeed;

    public float FadeTimer => fadeTimer;

    private void Start()
    {
        invSqrShakeThreshold = 1f / (shakeThreshold * shakeThreshold);
    }

    private void FixedUpdate()
    {
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
    /// Passing zero vectors causes push/tilt to decay back to neutral (shake never
    /// re-fires when linAcc is zero, since it only triggers above shakeThreshold).
    /// Pulls the camera's current local position from CameraControl to use as the
    /// lever arm for tangential/centripetal acceleration, then pushes the computed
    /// push/tilt/shake offsets back into CameraControl.
    /// </summary>
    public void UpdateEffects(Vector3 localLinAcc, Vector3 localAngAcc, Vector3 angularVelocity)
    {
        float dt = Time.fixedDeltaTime;
        Vector3 cameraLocalPos = cameraControl.GetCameraLocalPosition();

        Vector3 tangentialAccel = Vector3.Cross(localAngAcc, cameraLocalPos);
        Vector3 centripetalAccel = Vector3.Cross(angularVelocity, Vector3.Cross(angularVelocity, cameraLocalPos));

        Vector3 rotationalAccel = tangentialAccel + centripetalAccel;
        Vector3 totalLinearAcceleration = localLinAcc + rotationalAccel;

        // Shake: random impulse noise above the acceleration threshold.
        float linSqrMag = localLinAcc.sqrMagnitude;
        if (linSqrMag > shakeThreshold * shakeThreshold)
        {
            Vector3 shake = Random.insideUnitSphere * (linSqrMag * invSqrShakeThreshold * shakeFactor);
            cameraControl.AddShakeImpulse(shake);
        }
        // Decay of the existing shake offset is handled inside CameraControl.

        // Push: camera drifts opposite to total linear acceleration.
        localPushOffset = Vector3.Lerp(localPushOffset, -totalLinearAcceleration * pushFactor, pushLerpSpeed * dt);
        localPushOffset.x = Mathf.Clamp(localPushOffset.x, -maxPushOffset, maxPushOffset);
        localPushOffset.y = Mathf.Clamp(localPushOffset.y, -maxPushOffset, maxPushOffset);
        localPushOffset.z = Mathf.Clamp(localPushOffset.z, -maxPushOffset, maxPushOffset);

        // Tilt: camera tilts opposite to local angular acceleration.
        // tiltFactor acts as (degrees) / (rad/s²), so 5 means 1 rad/s² → 5° of tilt.
        tiltEulerOffset = Vector3.Lerp(tiltEulerOffset, -localAngAcc * tiltFactor, tiltLerpSpeed * dt);

        cameraControl.SetPushAndTilt(localPushOffset, tiltEulerOffset);

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
}