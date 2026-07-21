using System;
using System.Collections;
using FishNet;
using FishNet.Object;
using SpaceStuff;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(ScaledRigidbody), typeof(RadarTarget))]
public class Ship : NetworkBehaviour
{
    [Header("Ship")]
    public ScaledRigidbody scaledRigidbody;
    public RadarTarget radarTarget;
    public StatSystem statSystem;
    public Shields shields;
    [SerializeField] private AlertSystem alertSystem;
    [SerializeField, Tooltip("Minimum collision impulse for ship to take damage")] private float minImpulse = 5f;
    // Need separate scales since unity's impulse calculation is different from our custom scaled space one
    [SerializeField] private double unityCollideImpulseScale = 0.01;
    [SerializeField] private double scaledCollideImpulseScale = 0.01;
    [SerializeField, Tooltip("Local Z position at the front tip of the ship")] private float maxLocalZ;
    [SerializeField, Tooltip("Collisions at the front of the ship multiply damage by this.")] private float minRamAttenuation = 0.5f;
    [SerializeField, Tooltip("Collisions at the back of the ship multiply damage by this.")] private float maxRamAttenuation = 1.0f;
    private TargetingSystem targetingSystem;
    public bool isShutdown;
    public UnityEvent OnStartup;
    public UnityEvent OnShutdown;

    [Header("HUD stuff")]
    public GameObject hologramPrefab;
    [SerializeField] private Transform velocityDirectionPivot;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Visual/Audio effects")]
    [SerializeField] private Animator effectsAnimator;
    [SerializeField] private AudioSource startAudioSource;
    [SerializeField] private AudioClip startupClip;
    [SerializeField] private AudioClip shutdownClip;
    [SerializeField] private AudioSource[] ambientAudioSources;
    [SerializeField] private float[] minAmbientVolumes;
    [SerializeField] private float[] maxAmbientVolumes;
    [SerializeField] private TextMeshProUGUI[] startupTexts;
    private bool startingUp;
    private bool shuttingDown;

    [SerializeField] private InertialEffects inertialEffects;
    [SerializeField] private AudioSource thrusterAudioSource;
    [SerializeField] private float thrusterVolumeScale = 1.0f;
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private float engineVolumeScale = 0.8f;
    [SerializeField] private GameObject rocketTrail;
    [SerializeField] private float engineTrailScale = 1.0f;
    [SerializeField] private AudioClip normalEngineClip;
    [SerializeField] private AudioClip launchEngineClip;

    [Header("Ship controls")]
    [SerializeField] private float engineCruiseForce = 50f;
    [SerializeField] private float engineCombatForce = 50f;
    [SerializeField] private float thrusterCruiseForce = 10f;
    [SerializeField] private float thrusterCombatForce = 10f;
    [SerializeField, Tooltip("Thruster distance from ship's x axis (Pitch)")] private float thrusterRadiusX = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's y axis (Yaw)")] private float thrusterRadiusY = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's z axis (Roll)")] private float thrusterRadiusZ = 1f;
    private Vector3 thrusterCruiseTorque;
    private Vector3 thrusterCombatTorque;
    private bool rollMode = true;
    private bool combatMode = false;

    private bool autoStabilizeRot = false;
    private bool autoStabilizePos = false;
    private bool useRelativeVelocity = false;
    private Vector3 stableVelocity;
    private Vector3 stableLocalVelocity;
    private Vector3 stableLocalAngularVelocity;

    private bool matchTargetLinearVelocity = false;
    private bool matchTargetAngularVelocity = false;
    private float matchDistance = -1.0f;

    [SerializeField, Range(0, 1000)] private float autoP, autoI, autoD;
    private PIDController xRotatePID;
    private PIDController yRotatePID;
    private PIDController zRotatePID;

    private PIDController xMovePID;
    private PIDController yMovePID;
    private PIDController zMovePID;

    [SerializeField, Range(0, 1000)] private float matchDistanceP, matchDistanceI, matchDistanceD;
    private PIDController matchDistancePID;

    public struct ShipInputData
    {
        public Vector3 move;
        public Vector2 look;
    }

    private ShipInputData currentInput;
    [SerializeField] private float inputSendRate = 20f;
    private float inputSendTimer;
    private bool moving = false;
    private bool rotating = false;

    [Header("Fuel")]
    [SerializeField, Tooltip("Fuel consumption rate while idling in kg/s.")] private float idleFuelConsumption = 0.1f;
    [SerializeField, Tooltip("Fuel consumption rate while at max thrust in kg/s.")] private float maxFuelConsumption = 10f;
    [SerializeField, Tooltip("Exponential factor in fuel consumption formula.")] private float fuelConsumptionFactor = 1.6f;
    private float fuel;
    [SerializeField, Tooltip("Max fuel capacity in kg.")] private float maxFuel = 5000f;
    [SerializeField] private SliderIndicator fuelIndicator;
    private double baseMass;

    private bool IsOwnerOrOffline => IsOwner || IsOffline;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        radarTarget = GetComponent<RadarTarget>();

        thrusterCruiseTorque = new Vector3(thrusterCruiseForce * thrusterRadiusX, thrusterCruiseForce * thrusterRadiusY, thrusterCruiseForce * thrusterRadiusZ);
        thrusterCombatTorque = new Vector3(thrusterCombatForce * thrusterRadiusX, thrusterCombatForce * thrusterRadiusY, thrusterCombatForce * thrusterRadiusZ);

        xRotatePID = new PIDController(autoP, autoI, autoD);
        yRotatePID = new PIDController(autoP, autoI, autoD);
        zRotatePID = new PIDController(autoP, autoI, autoD);

        xMovePID = new PIDController(autoP, autoI, autoD);
        yMovePID = new PIDController(autoP, autoI, autoD);
        zMovePID = new PIDController(autoP, autoI, autoD);

        matchDistancePID = new PIDController(matchDistanceP, matchDistanceI, matchDistanceD);
        if (IsOffline)
        {
            scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
        }
        fuel = maxFuel;
        fuelIndicator.UpdateUI(fuel, maxFuel);

        baseMass = scaledRigidbody.mass;

        if (TryGetComponent(out targetingSystem))
        {
            targetingSystem.OnTargetChange += SetTargetMatchDistance;
        }

        if (PlayerInfoRelay.Instance != null)
            PlayerInfoRelay.Instance.OnPlayerInfoChanged += UpdateShipName;

        if (InstanceFinder.IsOffline)
        {
            Startup();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        UpdateShipName();
        if (IsOwner)
        {
            Startup();
        }
    }

    private void OnDestroy()
    {
        scaledRigidbody.OnScaledCollisionEnter -= OnScaledCollide;
        if (targetingSystem != null)
        {
            targetingSystem.OnTargetChange -= SetTargetMatchDistance;
        }

        if (PlayerInfoRelay.Instance != null)
            PlayerInfoRelay.Instance.OnPlayerInfoChanged -= UpdateShipName;
    }

    private void UpdateShipName()
    {
        if (PlayerRegistry.TryGetPlayer(Owner.ClientId, out PlayerInfo playerInfo))
        {
            name = playerInfo.Username;
        }
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

    private float CalculateFuelBurn(double input, double maxInput)
    {
        float t = (float)Math.Pow(Math.Abs(input) / maxInput, fuelConsumptionFactor);
        return Mathf.Lerp(0.0f, maxFuelConsumption, t);
    }

    private void FixedUpdate()
    {
        if (IsOwnerOrOffline)
        {
            ReadLocalInput();
            if (!IsOffline)
                SendInputToServer();
        }

        if (!IsOwnerOrOffline && !IsServerInitialized)
            return;

        if (isShutdown)
        {
            if (IsOwnerOrOffline)
            {
                UpdateOwnerEffects(Vector3d.zero, Vector3d.zero, Vector3d.zero);
            }
            else
            {
                UpdateServerEffects(Vector3d.zero);
            }
            return;
        }

        // Calculate local force to apply
        Vector3 desiredMove = Vector3.ClampMagnitude(currentInput.move, 1f); // Prevent from moving faster than max force allows
        moving = desiredMove.x != 0 || desiredMove.y != 0 || desiredMove.z != 0;
        
        if (autoStabilizePos)
        {
            Vector3 stableFinalLocalVel = desiredMove * (float)(scaledRigidbody.velocity.sqrMagnitude + 1.0);
            if (!moving && targetingSystem != null && targetingSystem.lockedTarget != null)
            {
                bool shouldUpdateLocalVel = false;
                Vector3 desiredWorldVel = Vector3.zero;
                if (matchTargetLinearVelocity)
                {
                    desiredWorldVel += targetingSystem.lockedTarget.scaledRigidbody.velocity.ToVector3();
                    shouldUpdateLocalVel = true;
                }

                if (matchDistance > 0)
                {
                    Vector3d toTarget = targetingSystem.lockedTarget.scaledRigidbody.scaledTransform.realPosition - scaledRigidbody.scaledTransform.realPosition;
                    double distance = toTarget.magnitude;
                    double distanceError = distance - matchDistance;
                    if (distance > 0.001)
                    {
                        Vector3 targetDirection = (toTarget / distance).ToVector3();
                        float closingSpeed = matchDistancePID.GetOutput((float)distanceError, Time.fixedDeltaTime);
                        desiredWorldVel += targetDirection * closingSpeed;
                        shouldUpdateLocalVel = true;
                    }
                }

                if (shouldUpdateLocalVel)
                    stableFinalLocalVel += transform.InverseTransformDirection(desiredWorldVel);
            }
            else
            {
                // Add local desired move velocity for PID to move to
                if (useRelativeVelocity)
                {
                    stableFinalLocalVel += stableLocalVelocity;
                }
                else
                {
                    // stableVelocity is in world coords, convert to local
                    stableFinalLocalVel += transform.InverseTransformDirection(stableVelocity);
                }
            }

            Vector3 localVelocity = transform.InverseTransformDirection(scaledRigidbody.velocity.ToVector3());
            Vector3 error = stableFinalLocalVel - localVelocity;

            float fx = xMovePID.GetOutput(error.x, Time.fixedDeltaTime);
            float fy = yMovePID.GetOutput(error.y, Time.fixedDeltaTime);
            float fz = zMovePID.GetOutput(error.z, Time.fixedDeltaTime);
            desiredMove = new Vector3(fx, fy, fz);
            desiredMove = Vector3.ClampMagnitude(desiredMove, 1f);
        }

        // Engine can only move ship forward so use engine for +z and thrusters for -z
        Vector3d finalForce;
        if (combatMode)
        {
            finalForce = new Vector3d(
                desiredMove.x * thrusterCombatForce,
                desiredMove.y * thrusterCombatForce,
                desiredMove.z > 0 ? desiredMove.z * engineCombatForce : desiredMove.z * thrusterCombatForce
            );
        }
        else
        {
            finalForce = new Vector3d(
                desiredMove.x * thrusterCruiseForce,
                desiredMove.y * thrusterCruiseForce,
                desiredMove.z > 0 ? desiredMove.z * engineCruiseForce : desiredMove.z * thrusterCruiseForce
            );
        }

        if (finalForce.sqrMagnitude > 0.0001)
            scaledRigidbody.AddRelativeForce(finalForce, ForceMode.Force);

        fuel -= (CalculateFuelBurn(finalForce.x, engineCombatForce) 
                + CalculateFuelBurn(finalForce.y, engineCombatForce) 
                + CalculateFuelBurn(finalForce.z, engineCombatForce)) * Time.fixedDeltaTime;

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
            Vector3 finalStableLocalAngVel = stableLocalAngularVelocity;
            if (matchTargetAngularVelocity && targetingSystem != null && targetingSystem.lockedTarget != null)
            {
                finalStableLocalAngVel = transform.InverseTransformDirection(targetingSystem.lockedTarget.scaledRigidbody.angularVelocity.ToVector3());
            }
            Vector3 error = finalStableLocalAngVel - localAngularVelocity;

            float tx = xRotatePID.GetOutput(error.x, Time.fixedDeltaTime);
            float ty = yRotatePID.GetOutput(error.y, Time.fixedDeltaTime);
            float tz = zRotatePID.GetOutput(error.z, Time.fixedDeltaTime);
            desiredRotate = new Vector3d(tx, ty, tz);
            desiredRotate = Vector3d.ClampMagnitude(desiredRotate, 1f);
        }

        Vector3d finalTorque;
        if (combatMode)
        {
            finalTorque = new Vector3d(
                desiredRotate.x * thrusterCombatTorque.x,
                desiredRotate.y * thrusterCombatTorque.y,
                desiredRotate.z * thrusterCombatTorque.z
            );
        }
        else
        {
            finalTorque = new Vector3d(
                desiredRotate.x * thrusterCruiseTorque.x,
                desiredRotate.y * thrusterCruiseTorque.y,
                desiredRotate.z * thrusterCruiseTorque.z
            );
        }

        if (finalTorque.sqrMagnitude > 0.0001)
        {
            scaledRigidbody.AddRelativeTorque(finalTorque, ForceMode.Force);
        }
        fuel -= (CalculateFuelBurn(finalTorque.x, thrusterCombatTorque.x) 
                + CalculateFuelBurn(finalTorque.y, thrusterCombatTorque.y) 
                + CalculateFuelBurn(finalTorque.z, thrusterCombatTorque.z)) * Time.fixedDeltaTime;
        
        // Idling fuel consumption
        fuel -= idleFuelConsumption * Time.fixedDeltaTime;
        if (fuel <= 0f)
        {
            fuel = 0f;
            Shutdown(true);
        }
        scaledRigidbody.mass = baseMass + fuel;

        if (IsOwnerOrOffline)
        {
            UpdateOwnerEffects(finalForce, desiredRotate, finalTorque);
        }
        else
        {
            UpdateServerEffects(finalForce);
        }
    }
    
    [ObserversRpc(ExcludeServer = true, ExcludeOwner = true, BufferLast = true)]
    private void SetRocketTrailActiveObserversRpc(bool active)
    {
        if (rocketTrail.activeSelf != active)
        {
            rocketTrail.SetActive(active);
        }
    }

    [ObserversRpc(ExcludeServer = true, ExcludeOwner = true)]
    private void SetRocketTrailScaleObserversRpc(float scale)
    {
        rocketTrail.transform.localScale = scale * Vector3.one;
    }

    private void UpdateServerEffects(Vector3d finalForce)
    {
        // Visual effects for force
        bool hasForce = finalForce.sqrMagnitude > 0.0001;

        if (hasForce && finalForce.z > 0.0)
        {
            // Main engine effects
            float t = (float)finalForce.z / engineCombatForce;
            if (!rocketTrail.activeSelf)
            {
                rocketTrail.SetActive(true);
                SetRocketTrailActiveObserversRpc(true);
            }
            float scale = t * engineTrailScale;
            rocketTrail.transform.localScale = scale * Vector3.one;
            SetRocketTrailScaleObserversRpc(scale);
        }
        else if (rocketTrail.activeSelf)
        {
            rocketTrail.SetActive(false);
            SetRocketTrailActiveObserversRpc(false);
        }
    }

    private void UpdateOwnerEffects(Vector3d finalForce, Vector3d desiredRotate, Vector3d finalTorque)
    {
        fuelIndicator.UpdateUI(fuel, maxFuel);
        if (alertSystem != null)
        {
            float fuelPercent = fuel / maxFuel;
            if (fuelPercent < 0.05f)
            {
                alertSystem.SetAlert("Bingo Fuel", true);
            }
            else if (fuelPercent < 0.25f)
            {
                alertSystem.SetAlert("Low Fuel", true);
            }
            else
            {
                alertSystem.SetAlert("Low Fuel", false);
                alertSystem.SetAlert("Bingo Fuel", false);
            }
        }
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
                thrusterAudioSource.volume = (float)magnitude / thrusterCombatForce * thrusterVolumeScale;
            }

            // Main engine effects
            if (usingMainEngine)
            {
                float t = (float)finalForce.z / engineCombatForce;
                if (!rocketTrail.activeSelf)
                {
                    rocketTrail.SetActive(true);
                }
                rocketTrail.transform.localScale = t * engineTrailScale * Vector3.one;
                engineAudioSource.clip = combatMode ? launchEngineClip : normalEngineClip;
                engineAudioSource.volume = t * engineVolumeScale;
                if (!engineAudioSource.isPlaying)
                    engineAudioSource.Play();
            }
            else if (rocketTrail.activeSelf)
            {
                engineAudioSource.volume = 0f;
                rocketTrail.SetActive(false);
            }
        }
        else if (rocketTrail.activeSelf)
        {
            engineAudioSource.volume = 0f;
            rocketTrail.SetActive(false);
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

        // Update inertial effects
        if (inertialEffects != null)
        {
            Rigidbody rb = scaledRigidbody.attachedRigidbody;

            // Local-space linear acceleration: a = F/m (stays local, not TransformVector'd)
            Vector3 localLinAcc = hasForce ? (finalForce / scaledRigidbody.mass).ToVector3() : Vector3.zero;

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
            inertialEffects.UpdateEffects(localLinAcc, localAngAcc, transform.InverseTransformDirection(scaledRigidbody.angularVelocity.ToVector3()));
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

        double sqrImpulse = scaledCollideImpulseScale * collisionInfo.impulse.sqrMagnitude / (scaledRigidbody.mass * scaledRigidbody.mass);
        if (sqrImpulse < minImpulse * minImpulse)
            return;

        // Ram attenuation
        Vector3d relativeCollisionPoint = collisionInfo.contactPoint - scaledRigidbody.scaledTransform.realPosition;
        float t = Mathf.Clamp01((-(float)relativeCollisionPoint.z + maxLocalZ) / (2f * maxLocalZ));
        float attenuation = Mathf.Lerp(minRamAttenuation, maxRamAttenuation, t);
        float damage = (float)(attenuation * sqrImpulse);

        Vector3 renderContactPoint = (collisionInfo.contactPoint - FloatingWorldOrigin.Instance.scaledTransform.realPosition).ToVector3();
        if (!IsOffline)
            ApplyCollideDamageToObservers(damage, renderContactPoint);
        else
            ApplyDamageLocally(damage, renderContactPoint);
        Debug.Log(
            $"Impulse: {collisionInfo.impulse:F1}, " +
            $"Normalized impulse: {sqrImpulse:F1}, " + 
            $"damage: {damage:F1}, " + 
            $"attenuation: {attenuation}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServerInitialized && !IsOffline)
            return;

        // if (collision.transform.CompareTag("Projectile") || collision.transform.CompareTag("Torpedo"))
        //     return;

        Vector3 impulse = collision.impulse;
        double sqrImpulse = unityCollideImpulseScale * impulse.sqrMagnitude / (scaledRigidbody.mass * scaledRigidbody.mass);
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
        float damage = (float)(attenuation * Math.Sqrt(sqrImpulse));
        if (!IsOffline)
            ApplyCollideDamageToObservers(damage, contact.point);
        else
            ApplyDamageLocally(damage, contact.point);

        Debug.Log(
            $"Impulse: {impulse:F1}, " +
            $"Normalized impulse: {sqrImpulse:F1}, " + 
            $"damage: {damage:F1}, " + 
            $"attenuation: {attenuation}, " +
            $"Seperation: {contact.separation}");
    }

    [ServerRpc]
    private void SendAutoRotStabilizationToServer(bool autoStabilizeRot)
    {
        this.autoStabilizeRot = autoStabilizeRot;
    }

    public void SetAutoRotStabilization(int state)
    {
        if (!IsOwnerOrOffline)
            return;
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
        if (!IsOwnerOrOffline)
            return;
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
        if (!IsOwnerOrOffline)
            return;
        useRelativeVelocity = state == 1;
        if (IsOwner)
            SendRelativeVelocityToServer(useRelativeVelocity);
    }

    public void SetStableConfiguration(int state)
    {
        if (!IsOwnerOrOffline)
            return;
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
        if (!IsOwnerOrOffline)
            return;
        rollMode = state == 0;
        if (IsOwner)
            SendRollYawToggleToServer(rollMode);
    }

    [ServerRpc]
    private void SetCombatModeServerRpc(bool combatMode)
    {
        this.combatMode = combatMode;
    }

    public void ToggleCombatMode(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        combatMode = state == 1;
        if (IsOwner)
            SetCombatModeServerRpc(combatMode);
    }

    [ServerRpc]
    private void SetMatchTargetAngularVelocityServerRpc(bool value)
    {
        this.matchTargetLinearVelocity = value;
    }


    public void ToggleMatchTargetAngularVelocity(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        matchTargetAngularVelocity = state == 1;
        if (IsOwner)
            SetMatchTargetAngularVelocityServerRpc(matchTargetAngularVelocity);
    }

    [ServerRpc]
    private void SetMatchTargetLinearVelocityServerRpc(bool value)
    {
        this.matchTargetLinearVelocity = value;
    }

    public void ToggleMatchTargetLinearVelocity(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        matchTargetLinearVelocity = state == 1;
        if (IsOwner)
            SetMatchTargetLinearVelocityServerRpc(matchTargetLinearVelocity);
    }

    [ServerRpc]
    private void SetMatchTargetDistance(float distance)
    {
        this.matchDistance = distance;
    }

    private void SetTargetMatchDistance()
    {
        if (!IsOwnerOrOffline)
            return;
        if (matchDistance < 0)
            return;
        if (targetingSystem != null && targetingSystem.lockedTarget != null)
        {
            matchDistance = (float)(targetingSystem.lockedTarget.scaledRigidbody.scaledTransform.realPosition - scaledRigidbody.scaledTransform.realPosition).magnitude;
            if (IsOwner)
                SetMatchTargetDistance(matchDistance);
        }
    }

    public void ToggleMatchTargetDistance(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        if (state == 0)
        {
            matchDistance = -1;
        }
        else
        {
            matchDistance = 0;
            SetTargetMatchDistance();
        }
    }

    private IEnumerator WaitToStartup()
    {
        startingUp = true;
        yield return new WaitForEndOfFrame();
        AnimatorStateInfo animatorStateInfo = effectsAnimator.GetCurrentAnimatorStateInfo(0);
        float t = animatorStateInfo.normalizedTime;
        while (animatorStateInfo.IsName("Startup") && t < 1f)
        {
            for (int i = 0; i < ambientAudioSources.Length; i++)
            {
                ambientAudioSources[i].volume = Mathf.Lerp(minAmbientVolumes[i], maxAmbientVolumes[i], t);
            }
            yield return new WaitForEndOfFrame();
            animatorStateInfo = effectsAnimator.GetCurrentAnimatorStateInfo(0);
            t = animatorStateInfo.normalizedTime;
        }
        for (int i = 0; i < ambientAudioSources.Length; i++)
        {
            ambientAudioSources[i].volume = maxAmbientVolumes[i];
        }
        Debug.Log($"[Ship] {name} finished startup.");
        isShutdown = false;
        startingUp = false;
    }

    private void Startup()
    {
        if (!isShutdown || startingUp)
            return;
        Debug.Log($"[Ship] {name} starting up.");
        OnStartup?.Invoke();
        startAudioSource.clip = startupClip;
        startAudioSource.Play();
        effectsAnimator.SetTrigger("Startup");
        for(int i = 0; i < startupTexts.Length; i++)
        {
            startupTexts[i].text = "INITIALIZING...";
        }
        StartCoroutine(WaitToStartup());
    }

    private IEnumerator WaitToShutdown()
    {
        shuttingDown = true;
        yield return new WaitForEndOfFrame();
        AnimatorStateInfo animatorStateInfo = effectsAnimator.GetCurrentAnimatorStateInfo(0);
        float t = animatorStateInfo.normalizedTime;
        while (animatorStateInfo.IsName("Shutdown") && t < 1f)
        {
            for (int i = 0; i < ambientAudioSources.Length; i++)
            {
                ambientAudioSources[i].volume = Mathf.Lerp(maxAmbientVolumes[i], minAmbientVolumes[i], t);
            }
            yield return new WaitForEndOfFrame();
            animatorStateInfo = effectsAnimator.GetCurrentAnimatorStateInfo(0);
            t = animatorStateInfo.normalizedTime;
        }
        for (int i = 0; i < ambientAudioSources.Length; i++)
        {
            ambientAudioSources[i].volume = 0;
        }
        Debug.Log($"[Ship] {name} finished shutdown.");
        isShutdown = true;
        shuttingDown = false;
    }

    private void Shutdown(bool instant)
    {
        if (isShutdown || shuttingDown)
            return;
        Debug.Log($"[Ship] {name} shutting down.");
        OnShutdown?.Invoke();
        startAudioSource.clip = shutdownClip;
        startAudioSource.Play();
        if (instant)
        {
            isShutdown = true;
            for (int i = 0; i < ambientAudioSources.Length; i++)
            {
                ambientAudioSources[i].volume = 0;
            }
            return;
        }
        effectsAnimator.SetTrigger("Shutdown");
        for(int i = 0; i < startupTexts.Length; i++)
        {
            startupTexts[i].text = "SHUTTING DOWN...";
        }
        StartCoroutine(WaitToShutdown());
    }
}
