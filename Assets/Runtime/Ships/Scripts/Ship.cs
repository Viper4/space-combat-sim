using System;
using FishNet.Object;
using SpaceStuff;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(ScaledRigidbody), typeof(RadarTarget))]
public class Ship : NetworkBehaviour
{
    [Header("Ship")]
    public ScaledRigidbody scaledRigidbody;
    public RadarTarget radarTarget;
    public StatSystem statSystem;
    public Shields shields;
    [SerializeField, Tooltip("Minimum collision impulse for ship to take damage")] private float minImpulse = 5f;
    // Need separate scales since unity's impulse calculation is different from our custom scaled space one
    [SerializeField] private double unityCollideImpulseScale = 0.01;
    [SerializeField] private double scaledCollideImpulseScale = 0.01;
    [SerializeField, Tooltip("Local Z position at the front tip of the ship")] private float maxLocalZ;
    [SerializeField, Tooltip("Collisions at the front of the ship multiply damage by this.")] private float minRamAttenuation = 0.5f;
    [SerializeField, Tooltip("Collisions at the back of the ship multiply damage by this.")] private float maxRamAttenuation = 1.0f;

    [Header("HUD stuff")]
    public GameObject hologramPrefab;
    [SerializeField] private Transform velocityDirectionPivot;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Visual/Audio effects")]
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private AudioSource thrusterAudioSource;
    [SerializeField] private float thrusterVolumeScale = 1.0f;
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private float engineVolumeScale = 0.8f;
    [SerializeField] private ParticleSystem rocketTrail;
    [SerializeField] private float engineTrailScale = 1.0f;
    [SerializeField] private AudioClip normalEngineClip;
    [SerializeField] private AudioClip launchEngineClip;

    [Header("Ship controls")]
    [SerializeField] private float engineForce = 50f;
    [SerializeField] private float engineLaunchForce = 50f;
    [SerializeField] private float thrusterForce = 10f;
    [SerializeField, Tooltip("Thruster distance from ship's x axis (Pitch)")] private float thrusterRadiusX = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's y axis (Yaw)")] private float thrusterRadiusY = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's z axis (Roll)")] private float thrusterRadiusZ = 1f;
    private Vector3 thrusterTorque;
    private bool rollMode = true;
    private bool launchMode = false;
    private float fuel = 100f;
    [SerializeField] private float maxFuel = 100f;

    private bool autoStabilizeRot = false;
    private bool autoStabilizePos = false;
    private bool useRelativeVelocity = false;
    private Vector3 stableVelocity;
    private Vector3 stableLocalVelocity;
    private Vector3 stableLocalAngularVelocity;
    [SerializeField, Range(0, 1000)] private float P, I, D;
    private PIDController xRotatePID;
    private PIDController yRotatePID;
    private PIDController zRotatePID;

    private PIDController xMovePID;
    private PIDController yMovePID;
    private PIDController zMovePID;

    private bool moving = false;
    private bool rotating = false;

    public struct ShipInputData
    {
        public Vector3 move;
        public Vector2 look;
    }

    private ShipInputData currentInput;
    [SerializeField] private float inputSendRate = 20f;
    private float inputSendTimer;

    private bool ShouldSimulateShip => IsOffline || IsServerInitialized || IsOwner;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        radarTarget = GetComponent<RadarTarget>();

        thrusterTorque = new Vector3(thrusterForce * thrusterRadiusX, thrusterForce * thrusterRadiusY, thrusterForce * thrusterRadiusZ);

        xRotatePID = new PIDController(P, I, D);
        yRotatePID = new PIDController(P, I, D);
        zRotatePID = new PIDController(P, I, D);

        xMovePID = new PIDController(P, I, D);
        yMovePID = new PIDController(P, I, D);
        zMovePID = new PIDController(P, I, D);
        if (IsOffline)
        {
            scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
    }

    private void OnDestroy()
    {
        scaledRigidbody.OnScaledCollisionEnter -= OnScaledCollide;
    }

    private void ReadLocalInput()
    {
        currentInput.move = Vector3.zero;
        currentInput.look = Vector2.zero;
        if (GameManager.Instance.IsPaused)
            return;

        currentInput.move = GameManager.Instance.inputActions.Player.Move.ReadValue<Vector3>();
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            currentInput.look = GameManager.Instance.inputActions.Player.Look.ReadValue<Vector2>();
            currentInput.look *= GameManager.Instance.GetRangedSettingValue("Sensitivity") * GameManager.Instance.sensitivityScale;
        }
    }

    private void SendInputToServer()
    {
        inputSendTimer += Time.fixedDeltaTime;
        if (inputSendTimer >= 1f / Mathf.Max(1f, inputSendRate))
        {
            inputSendTimer -= 1f / Mathf.Max(1f, inputSendRate);
            SetInputServerRpc(currentInput);
        }
    }

    [ServerRpc]
    private void SetInputServerRpc(ShipInputData input)
    {
        currentInput = input;
    }

    private void FixedUpdate()
    {
        if (!IsServerInitialized && !IsOwner && !IsOffline)
            return;

        if (IsOwner || IsOffline)
        {
            ReadLocalInput();
            if (!IsOffline)
                SendInputToServer();
        }

        if (!ShouldSimulateShip)
            return;

        // Calculate local force to apply
        Vector3d finalForce;

        Vector3 desiredMove = Vector3.ClampMagnitude(currentInput.move, 1f); // Prevent from moving faster than max force allows
        moving = desiredMove.x != 0 || desiredMove.y != 0 || desiredMove.z != 0;

        double forceX = desiredMove.x * thrusterForce;
        double forceY = desiredMove.y * thrusterForce;
        double forceZ = desiredMove.z * thrusterForce;
        // Engine can only move ship forward so use engine for +z and thrusters for -z
        if (desiredMove.z > 0)
        {
            if (launchMode)
                forceZ = desiredMove.z * engineLaunchForce;
            else
                forceZ = desiredMove.z * engineForce;
        }
        finalForce = new Vector3d(forceX, forceY, forceZ);

        if (autoStabilizePos)
        {
            // Add local desired move velocity for PID to move to
            Vector3 stableFinalLocalVel = desiredMove * (float)(scaledRigidbody.velocity.sqrMagnitude + 1.0);
            if (useRelativeVelocity)
            {
                stableFinalLocalVel += stableLocalVelocity;
            }
            else
            {
                // stableVelocity is in world coords, convert to local
                stableFinalLocalVel += transform.InverseTransformDirection(stableVelocity);
            }

            Vector3 localVelocity = transform.InverseTransformDirection(scaledRigidbody.velocity.ToVector3());
            Vector3 error = stableFinalLocalVel - localVelocity;

            float fx = xMovePID.GetOutput(error.x, Time.fixedDeltaTime);
            float fy = yMovePID.GetOutput(error.y, Time.fixedDeltaTime);
            float fz = zMovePID.GetOutput(error.z, Time.fixedDeltaTime);
            desiredMove = new Vector3(fx, fy, fz);
            desiredMove = Vector3.ClampMagnitude(desiredMove, 1f);

            // Use engine to go forward (+z) and thrusters to go backward (-z)
            finalForce = new Vector3d(
                desiredMove.x * thrusterForce,
                desiredMove.y * thrusterForce,
                desiredMove.z * thrusterForce
            );

            if (desiredMove.z > 0)
            {
                if (launchMode)
                    finalForce.z = desiredMove.z * engineLaunchForce;
                else
                    finalForce.z = desiredMove.z * engineForce;
            }
        }

        if (finalForce.sqrMagnitude > 0.0001)
            scaledRigidbody.AddRelativeForce(finalForce, ForceMode.Force);

        // Calculate local torque to apply
        Vector3d desiredRotate;

        // Rotation inputs for pitch and roll
        Vector2 lookInput = Vector2.ClampMagnitude(currentInput.look, 1f); // Prevent from rotating faster than max torque allows
        desiredRotate = new Vector3d(-lookInput.y, 0.0, 0.0);
        if (rollMode)
            desiredRotate.z = -lookInput.x;
        else
            desiredRotate.y = lookInput.x;
        rotating = currentInput.look.x != 0 || currentInput.look.y != 0;

        if (autoStabilizeRot && !rotating)
        {
            Vector3 localAngularVelocity = transform.InverseTransformDirection(scaledRigidbody.angularVelocity.ToVector3());
            Vector3 error = stableLocalAngularVelocity - localAngularVelocity;

            float tx = xRotatePID.GetOutput(error.x, Time.fixedDeltaTime);
            float ty = yRotatePID.GetOutput(error.y, Time.fixedDeltaTime);
            float tz = zRotatePID.GetOutput(error.z, Time.fixedDeltaTime);
            desiredRotate = new Vector3d(tx, ty, tz);
            desiredRotate = Vector3d.ClampMagnitude(desiredRotate, 1f);
        }

        Vector3d finalTorque = new Vector3d(
            desiredRotate.x * thrusterTorque.x,
            desiredRotate.y * thrusterTorque.y,
            desiredRotate.z * thrusterTorque.z
        );

        if (finalTorque.sqrMagnitude > 0.0001)
            scaledRigidbody.AddRelativeTorque(finalTorque, ForceMode.Force);

        if (IsOwner || IsOffline)
        {
            UpdateOwnerEffects(finalForce, desiredRotate, finalTorque);
        }
        else
        {
            UpdateServerEffects(finalForce);
        }
    }

    private void UpdateServerEffects(Vector3d finalForce)
    {
        // Visual effects for force
        bool hasForce = finalForce.sqrMagnitude > 0.0001;

        if (hasForce && finalForce.z > 0.0)
        {
            // Main engine effects
            float t = (float)finalForce.z / engineLaunchForce;
            if (!rocketTrail.gameObject.activeSelf)
            {
                rocketTrail.gameObject.SetActive(true);
            }
            rocketTrail.transform.localScale = t * engineTrailScale * Vector3.one;
        }
        else if (rocketTrail.gameObject.activeSelf)
        {
            rocketTrail.gameObject.SetActive(false);
        }
    }

    private void UpdateOwnerEffects(Vector3d finalForce, Vector3d desiredRotate, Vector3d finalTorque)
    {
        // Visual effects for force
        bool hasForce = finalForce.sqrMagnitude > 0.0001;
        bool usingThrusters =
                Math.Abs(finalForce.x) > 0.001 ||
                Math.Abs(finalForce.y) > 0.001 ||
                finalForce.z < 0.0;
        if (hasForce)
        {
            bool usingMainEngine = finalForce.z > 0.0;

            // Thruster audio
            if (usingThrusters)
            {
                double magnitude = finalForce.magnitude;

                thrusterAudioSource.transform.localPosition = new Vector3(
                    -(float)(finalForce.x / magnitude) * thrusterRadiusX,
                    -(float)(finalForce.y / magnitude) * thrusterRadiusY,
                    -(float)(finalForce.z / magnitude) * thrusterRadiusZ
                );
                thrusterAudioSource.volume = (float)magnitude / thrusterForce * thrusterVolumeScale;
            }

            // Main engine effects
            if (usingMainEngine)
            {
                float t = (float)finalForce.z / engineLaunchForce;
                if (!rocketTrail.gameObject.activeSelf)
                {
                    rocketTrail.gameObject.SetActive(true);
                }
                rocketTrail.transform.localScale = t * engineTrailScale * Vector3.one;
                engineAudioSource.clip = launchMode ? launchEngineClip : normalEngineClip;
                engineAudioSource.volume = t * engineVolumeScale;
                if (!engineAudioSource.isPlaying)
                    engineAudioSource.Play();
            }
            else if (rocketTrail.gameObject.activeSelf)
            {
                engineAudioSource.volume = 0f;
                rocketTrail.gameObject.SetActive(false);
            }
        }
        else if (rocketTrail.gameObject.activeSelf)
        {
            engineAudioSource.volume = 0f;
            rocketTrail.gameObject.SetActive(false);
        }
        
        // Audio effects for torque
        bool hasTorque = desiredRotate.sqrMagnitude > 0.0001;
        if (hasTorque)
        {
            if (!hasForce)
            {
                thrusterAudioSource.transform.position = transform.position;
                thrusterAudioSource.volume = (float)desiredRotate.magnitude * thrusterVolumeScale;
            }
            else
            {
                thrusterAudioSource.volume = Mathf.Max(thrusterAudioSource.volume, (float)desiredRotate.magnitude * thrusterVolumeScale);
            }
        }
        else if (!usingThrusters)
        {
            thrusterAudioSource.volume = 0f;
        }

        // Update speed HUD UI
        speedText.text = "SPD " + SpaceMath.SpeedToFormattedString(scaledRigidbody.velocity.magnitude, "F2");
        if (scaledRigidbody.velocity != Vector3d.zero)
            velocityDirectionPivot.rotation = Quaternion.LookRotation(scaledRigidbody.velocity.ToVector3(), transform.up);

        // Update camera movement
        if (cameraControl != null)
        {
            Rigidbody rb = scaledRigidbody.attachedRigidbody;

            // Local-space linear acceleration: a = F/m (stays local, not TransformVector'd)
            Vector3 localLinAcc = hasForce ? finalForce.ToVector3() / rb.mass : Vector3.zero;

            // Local-space angular acceleration: α = I⁻¹τ
            // First rotate torque into the principal-axis frame, divide, then rotate back to local.
            Vector3 localAngAcc = Vector3.zero;
            if (hasTorque)
            {
                Vector3 principalTorque = Quaternion.Inverse(rb.inertiaTensorRotation) * finalTorque.ToVector3();
                Vector3 principalAlpha = new Vector3(
                    principalTorque.x / rb.inertiaTensor.x,
                    principalTorque.y / rb.inertiaTensor.y,
                    principalTorque.z / rb.inertiaTensor.z
                );
                localAngAcc = rb.inertiaTensorRotation * principalAlpha;
            }

            cameraControl.UpdateForceTorqueMovement(localLinAcc, localAngAcc);
        }
    }

    private void ApplyDamageLocally(float damage, Vector3 contactPoint)
    {
        if (shields != null)
            shields.Damage(damage, contactPoint);
        else
            statSystem.Damage(damage);
    }

    [ObserversRpc]
    private void ApplyCollideDamageToObservers(float damage, Vector3 contactPoint)
    {
        ApplyDamageLocally(damage, contactPoint);
    }

    private void OnScaledCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        if (!IsServerInitialized && !IsOffline)
            return;

        // if (collisionInfo.transformB.CompareTag("Projectile") || collisionInfo.transformB.CompareTag("Torpedo"))
        //     return;

        double sqrImpulse = scaledCollideImpulseScale * collisionInfo.impulse.sqrMagnitude / (scaledRigidbody.attachedRigidbody.mass * scaledRigidbody.attachedRigidbody.mass);
        if (sqrImpulse < minImpulse * minImpulse)
            return;

        // Ram attenuation
        Vector3d relativeCollisionPoint = collisionInfo.contactPoint - scaledRigidbody.scaledTransform.realPosition;
        float t = Mathf.Clamp01((-(float)relativeCollisionPoint.z + maxLocalZ) / (2f * maxLocalZ));
        float attenuation = Mathf.Lerp(minRamAttenuation, maxRamAttenuation, t);
        Debug.Log($"Attenuation: {attenuation}");
        float damage = (float)(attenuation * sqrImpulse);

        Vector3 renderContactPoint = (collisionInfo.contactPoint - FloatingWorldOrigin.Instance.scaledTransform.realPosition).ToVector3();
        if (!IsOffline)
            ApplyCollideDamageToObservers(damage, renderContactPoint);
        else
            ApplyDamageLocally(damage, renderContactPoint);
        Debug.Log(
            $"Impulse: {collisionInfo.impulse:F1}, " +
            $"Normalized impulse: {sqrImpulse:F1}, " + 
            $"damage: {damage:F1}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServerInitialized && !IsOffline)
            return;

        // if (collision.transform.CompareTag("Projectile") || collision.transform.CompareTag("Torpedo"))
        //     return;

        Vector3 impulse = collision.impulse;
        double sqrImpulse = unityCollideImpulseScale * impulse.sqrMagnitude / (scaledRigidbody.attachedRigidbody.mass * scaledRigidbody.attachedRigidbody.mass);
        if (sqrImpulse < minImpulse * minImpulse)
            return;

        ContactPoint contact = collision.GetContact(0);
        if (contact.separation < 0f)
        {
            Vector3 direction = collision.transform.position - transform.position;
            scaledRigidbody.scaledTransform.realPosition += (direction.normalized * contact.separation).ToVector3d();
        }

        // Ram attenuation
        Vector3 relativeCollisionPoint = contact.point - transform.position;
        float t = Mathf.Clamp01((-relativeCollisionPoint.z + maxLocalZ) / (2f * maxLocalZ));
        float attenuation = Mathf.Lerp(minRamAttenuation, maxRamAttenuation, t);
        Debug.Log($"Attenuation: {attenuation}");
        float damage = (float)(attenuation * sqrImpulse);
        if (!IsOffline)
            ApplyCollideDamageToObservers(damage, contact.point);
        else
            ApplyDamageLocally(damage, contact.point);

        Debug.Log(
            $"Impulse: {impulse:F1}, " +
            $"Normalized impulse: {sqrImpulse:F1}, " + 
            $"damage: {damage:F1}, " +
            $"Seperation: {contact.separation}");
    }

    [ServerRpc]
    private void SendAutoRotStabilizationToServer(bool autoStabilizeRot)
    {
        this.autoStabilizeRot = autoStabilizeRot;
    }

    public void SetAutoRotStabilization(int state)
    {
        autoStabilizeRot = state == 1;
        if (IsOwner)
            SendAutoRotStabilizationToServer(autoStabilizeRot);
    }

    [ServerRpc]
    private void SendAutoPosStabilizationToServer(bool autoStabilizePos)
    {
        this.autoStabilizePos = autoStabilizePos;
    }

    public void SetAutoPosStabilization(int state)
    {
        autoStabilizePos = state == 1;
        if (IsOwner)
            SendAutoPosStabilizationToServer(autoStabilizePos);
    }

    [ServerRpc]
    private void SendRelativeVelocityToServer(bool useRelativeVelocity)
    {
        this.useRelativeVelocity = useRelativeVelocity;
    }

    public void ToggleRelativeVelocity(int state)
    {
        useRelativeVelocity = state == 1;
        if (IsOwner)
            SendRelativeVelocityToServer(useRelativeVelocity);
    }

    public void SetStableConfiguration(int state)
    {
        if (state == 0)
        {
            stableVelocity = Vector3.zero;
            stableLocalVelocity = Vector3.zero;
            stableLocalAngularVelocity = Vector3.zero;
        }
        else
        {
            if (useRelativeVelocity)
            {
                stableLocalVelocity = transform.InverseTransformDirection(scaledRigidbody.velocity.ToVector3());
            }
            else
            {
                stableVelocity = scaledRigidbody.velocity.ToVector3();
            }
            stableLocalAngularVelocity = transform.InverseTransformDirection(scaledRigidbody.angularVelocity.ToVector3());
        }
    }

    [ServerRpc]
    private void SendRollYawToggleToServer(bool rollMode)
    {
        this.rollMode = rollMode;
    }

    public void ToggleRollYaw(int state)
    {
        rollMode = state == 0;
        if (IsOwner)
            SendRollYawToggleToServer(rollMode);
    }

    [ServerRpc]
    private void SendLaunchModeToServer(bool launchMode)
    {
        this.launchMode = launchMode;
    }

    public void ToggleLaunchMode(int state)
    {
        launchMode = state == 1;
        if (IsOwner)
            SendLaunchModeToServer(launchMode);
    }
}
