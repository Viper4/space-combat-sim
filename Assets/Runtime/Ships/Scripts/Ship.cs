using System;
using System.Collections;
using FishNet.Object;
using SpaceStuff;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

[RequireComponent(typeof(ScaledRigidbody), typeof(RadarTarget))]
public class Ship : NetworkBehaviour
{
    [Header("Ship")]
    public ScaledRigidbody scaledRigidbody;
    public RadarTarget radarTarget;
    public StatSystem statSystem;
    public Shields shields;
    [SerializeField] private NetworkScaledObject networkScaledObject;
    [SerializeField] private AlertSystem alertSystem;
    [SerializeField, Tooltip("Minimum collision impulse for ship to take damage")] private float minImpulse = 5f;
    // Need separate scales since unity's impulse calculation is different from our custom scaled space one
    [SerializeField] private double unityCollideImpulseScale = 0.01;
    [SerializeField] private double scaledCollideImpulseScale = 0.01;
    [SerializeField, Tooltip("Local Z position at the front tip of the ship")] private float maxLocalZ;
    [SerializeField, Tooltip("Collisions at the front of the ship multiply damage by this.")] private float minRamAttenuation = 0.5f;
    [SerializeField, Tooltip("Collisions at the back of the ship multiply damage by this.")] private float maxRamAttenuation = 1.0f;
    private TargetingSystem targetingSystem;

    [Header("Startup")]
    [SerializeField] private AudioClip batteryStartClip;
    [SerializeField] private GameObject batteryOnIndicator;
    [SerializeField] private GameObject batteryOffIndicator;
    [SerializeField] private AudioClip beepClip;
    [SerializeField] private AudioClip APUStartClip;
    [SerializeField] private AudioClip shutdownClip;
    [SerializeField] private AudioClip engineOnClip;
    [SerializeField] private AudioClip engineStartClip;
    [SerializeField] private float engineAmbientVolume = 0.7f;
    [SerializeField] private Vector2 APUOnTimeRange;
    [SerializeField, Tooltip("Radius to add to this RadarTarget's passive emission while battery is on.")] private float batteryOnEmission = 50f;
    [SerializeField, Tooltip("Radius to add to this RadarTarget's passive emission while APU is started.")] private float APUStartedEmission = 500f;
    [SerializeField, Tooltip("Radius to add to this RadarTarget's passive emission while engine is started and in cruise.")] private float engineCruiseEmission = 750f;
    [SerializeField, Tooltip("Radius to add to this RadarTarget's passive emission while engine is started and in high-g.")] private float engineHighGEmission = 1500f;
    [SerializeField, Tooltip("Radius to add to this RadarTarget's passive emission while shields are active.")] private float shieldsEmission = 2000f;

    [HideInInspector] public bool isStarted = false;
    private bool isBatteryOn;
    private bool isAPUOn;
    private Coroutine APUOnRoutine;
    private Coroutine APUStartupRoutine;
    private Coroutine APUShutdownRoutine;
    private bool isEngineOn;
    private bool engineStarted;
    private Coroutine engineOnRoutine;
    private Coroutine engineStartupRoutine;
    public UnityEvent OnAPUOn;
    public UnityEvent OnAPUOff;
    public UnityEvent OnStartupStart;
    public UnityEvent OnStartupEnd;
    public UnityEvent OnShutdownStart;
    public UnityEvent OnShutdownEnd;
    public UnityEvent OnEngineStart;
    public UnityEvent OnEngineStop;

    [Header("HUD/UI")]
    public GameObject hologramPrefab;
    [SerializeField] private Transform velocityDirectionPivot;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Visual/Audio effects")]
    [SerializeField] private Animator effectsAnimator;
    [SerializeField] private AudioSource startAudioSource;
    [SerializeField] private AudioSource[] ambientAudioSources;
    [SerializeField] private float[] minAmbientVolumes;
    [SerializeField] private float[] maxAmbientVolumes;
    [SerializeField] private TextMeshProUGUI[] startupTexts;

    [SerializeField] private InertialEffects inertialEffects;
    [SerializeField] private AudioSource thrusterAudioSource;
    [SerializeField] private float thrusterVolumeScale = 1.0f;
    [SerializeField] private AudioSource engineAmbientAudioSource;
    [SerializeField] private float engineAmbientShutdownTime = 2.5f;
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private float engineVolumeScale = 0.8f;
    [SerializeField] private GameObject rocketTrail;
    [SerializeField] private float engineTrailScale = 1.0f;
    [SerializeField] private AudioClip normalEngineClip;
    [SerializeField] private AudioClip launchEngineClip;

    [Header("Ship controls")]
    [SerializeField] private float engineCruiseAcc = 4f;
    [SerializeField] private float engineHighGAcc = 14f;
    [SerializeField] private float thrusterCruiseAcc = 1f;
    [SerializeField] private float thrusterHighGAcc = 4f;
    [SerializeField, Tooltip("Thruster distance from ship's x axis (Pitch)")] private float thrusterRadiusX = 6f;
    [SerializeField, Tooltip("Thruster distance from ship's y axis (Yaw)")] private float thrusterRadiusY = 6f;
    [SerializeField, Tooltip("Thruster distance from ship's z axis (Roll)")] private float thrusterRadiusZ = 1.5f;
    [SerializeField] private Vector3 thrusterCruiseAngAcc;
    [SerializeField] private Vector3 thrusterHighGAngAcc;
    private bool rollMode = true;
    private bool highGMode = false;

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

    private Vector3 moveInput;
    private Vector3 lookInput;

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

        float mass = (float)scaledRigidbody.mass;
        Vector3 inertiaTensor = scaledRigidbody.attachedRigidbody.inertiaTensor;
        thrusterCruiseAngAcc = new Vector3(
            thrusterCruiseAcc * thrusterRadiusX * mass / inertiaTensor.x,
            thrusterCruiseAcc * thrusterRadiusY * mass / inertiaTensor.y,
            thrusterCruiseAcc * thrusterRadiusZ * mass / inertiaTensor.z
        );

        thrusterHighGAngAcc = new Vector3(
            thrusterHighGAcc * thrusterRadiusX * mass / inertiaTensor.x,
            thrusterHighGAcc * thrusterRadiusY * mass / inertiaTensor.y,
            thrusterHighGAcc * thrusterRadiusZ * mass / inertiaTensor.z
        );
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

        if (IsOffline)
        {
            OnShutdownEnd?.Invoke(); // Update UI elements active state
        }
        UpdateNetworkMaxAcceleration();
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
            OnShutdownEnd?.Invoke(); // Update UI elements active state
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
        moveInput = Vector3.zero;
        lookInput = Vector2.zero;
        if (GameManager.Instance.IsPaused)
            return;

        moveInput = GameManager.Instance.inputActions.Player.Move.ReadValue<Vector3>();
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            lookInput = GameManager.Instance.inputActions.Player.Look.ReadValue<Vector2>();
            lookInput *= GameManager.Instance.GetRangedSettingValue("Sensitivity") * GameManager.Instance.sensitivityScale;
        }
    }

    private float CalculateFuelBurn(double input, double maxInput)
    {
        float t = (float)Math.Pow(Math.Abs(input) / maxInput, fuelConsumptionFactor);
        return Mathf.Lerp(0.0f, maxFuelConsumption, t);
    }

    private void ApplyForceAndTorque()
    {
        // Calculate local force to apply
        Vector3 desiredMove = Vector3.ClampMagnitude(moveInput, 1f); // Prevent from moving faster than max force allows
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
        Vector3d finalAcc;
        if (highGMode)
        {
            finalAcc = new Vector3d(
                desiredMove.x * thrusterHighGAcc,
                desiredMove.y * thrusterHighGAcc,
                desiredMove.z > 0 ? desiredMove.z * engineHighGAcc : desiredMove.z * thrusterHighGAcc
            );
        }
        else
        {
            finalAcc = new Vector3d(
                desiredMove.x * thrusterCruiseAcc,
                desiredMove.y * thrusterCruiseAcc,
                desiredMove.z > 0 ? desiredMove.z * engineCruiseAcc : desiredMove.z * thrusterCruiseAcc
            );
        }

        if (finalAcc.sqrMagnitude > 0.0001)
            scaledRigidbody.AddRelativeForce(finalAcc, ForceMode.Acceleration);

        fuel -= (CalculateFuelBurn(finalAcc.x, engineHighGAcc)
                + CalculateFuelBurn(finalAcc.y, engineHighGAcc)
                + CalculateFuelBurn(finalAcc.z, engineHighGAcc)) * Time.fixedDeltaTime;

        // Calculate local torque to apply
        Vector3d desiredRotate;

        // Rotation inputs for pitch and roll
        Vector2 desiredLook = Vector2.ClampMagnitude(lookInput, 1f); // Prevent from rotating faster than max torque allows
        desiredRotate = new Vector3d(-desiredLook.y, 0.0, 0.0);
        if (rollMode)
            desiredRotate.z = -desiredLook.x;
        else
            desiredRotate.y = desiredLook.x;
        rotating = lookInput.x != 0 || lookInput.y != 0;

        if (autoStabilizeRot && !rotating)
        {
            Vector3 localAngularVelocity = transform.InverseTransformDirection(scaledRigidbody.angularVelocity);
            Vector3 finalStableLocalAngVel = stableLocalAngularVelocity;
            if (matchTargetAngularVelocity && targetingSystem != null && targetingSystem.lockedTarget != null)
            {
                finalStableLocalAngVel = transform.InverseTransformDirection(targetingSystem.lockedTarget.scaledRigidbody.angularVelocity);
            }
            Vector3 error = finalStableLocalAngVel - localAngularVelocity;

            float tx = xRotatePID.GetOutput(error.x, Time.fixedDeltaTime);
            float ty = yRotatePID.GetOutput(error.y, Time.fixedDeltaTime);
            float tz = zRotatePID.GetOutput(error.z, Time.fixedDeltaTime);
            desiredRotate = new Vector3d(tx, ty, tz);
            desiredRotate = Vector3d.ClampMagnitude(desiredRotate, 1f);
        }

        Vector3d finalAngAcc;
        if (highGMode)
        {
            finalAngAcc = new Vector3d(
                desiredRotate.x * thrusterHighGAngAcc.x,
                desiredRotate.y * thrusterHighGAngAcc.y,
                desiredRotate.z * thrusterHighGAngAcc.z
            );
        }
        else
        {
            finalAngAcc = new Vector3d(
                desiredRotate.x * thrusterCruiseAngAcc.x,
                desiredRotate.y * thrusterCruiseAngAcc.y,
                desiredRotate.z * thrusterCruiseAngAcc.z
            );
        }

        if (finalAngAcc.sqrMagnitude > 0.0001)
        {
            scaledRigidbody.AddRelativeTorque(finalAngAcc, ForceMode.Acceleration);
        }
        fuel -= (CalculateFuelBurn(finalAngAcc.x, thrusterHighGAngAcc.x) 
                + CalculateFuelBurn(finalAngAcc.y, thrusterHighGAngAcc.y) 
                + CalculateFuelBurn(finalAngAcc.z, thrusterHighGAngAcc.z)) * Time.fixedDeltaTime;

        if (IsOwnerOrOffline)
        {
            UpdateOwnerEffects(finalAcc, desiredRotate, finalAngAcc);
        }
        else
        {
            UpdateServerEffects(finalAcc);
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwnerOrOffline && !IsServerInitialized)
            return;
        
        if (IsOwnerOrOffline)
            ReadLocalInput();

        if (!isStarted)
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

        if (engineStarted)
        {
            if (IsOwnerOrOffline)
                ApplyForceAndTorque(); // NetworkScaledObject should handle syncing stuff to server
            fuel -= idleFuelConsumption * Time.fixedDeltaTime * 2f;
        }
        else
        {
            fuel -= idleFuelConsumption * Time.fixedDeltaTime;
            if (IsOwnerOrOffline)
            {
                UpdateOwnerEffects(Vector3d.zero, Vector3d.zero, Vector3d.zero);
            }
            else
            {
                UpdateServerEffects(Vector3d.zero);
            }
        }
        
        if (fuel <= 0f)
        {
            fuel = 0f;
            Shutdown(true);
            SetBatteryOn(false);
        }
        scaledRigidbody.mass = baseMass + fuel;
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

    private void UpdateServerEffects(Vector3d finalAcc)
    {
        // Visual effects for force
        bool hasForce = finalAcc.sqrMagnitude > 0.0001;

        if (hasForce && finalAcc.z > 0.0)
        {
            // Main engine effects
            float t = (float)finalAcc.z / engineHighGAcc;
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

    private void UpdateOwnerEffects(Vector3d finalAcc, Vector3d desiredRotate, Vector3d finalAngAcc)
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
        bool hasForce = finalAcc.sqrMagnitude > 0.0001;
        bool usingThrusters =
                Math.Abs(finalAcc.x) > 0.001 ||
                Math.Abs(finalAcc.y) > 0.001 ||
                finalAcc.z < 0.0;
        if (hasForce)
        {
            bool usingMainEngine = finalAcc.z > 0.0;

            // Thruster audio
            if (usingThrusters)
            {
                double magnitude = finalAcc.magnitude;

                thrusterAudioSource.transform.localPosition = new Vector3(
                    -(float)(finalAcc.x / magnitude) * thrusterRadiusX,
                    -(float)(finalAcc.y / magnitude) * thrusterRadiusY,
                    -(float)(finalAcc.z / magnitude) * thrusterRadiusZ
                );
                thrusterAudioSource.volume = (float)magnitude / thrusterHighGAcc * thrusterVolumeScale;
            }

            // Main engine effects
            if (usingMainEngine)
            {
                float t = (float)finalAcc.z / engineHighGAcc;
                if (!rocketTrail.activeSelf)
                {
                    rocketTrail.SetActive(true);
                }
                rocketTrail.transform.localScale = t * engineTrailScale * Vector3.one;
                engineAudioSource.clip = highGMode ? launchEngineClip : normalEngineClip;
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
            inertialEffects.UpdateEffects(finalAcc.ToVector3(), finalAngAcc.ToVector3(), transform.InverseTransformDirection(scaledRigidbody.angularVelocity));
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
            stableLocalAngularVelocity = transform.InverseTransformDirection(scaledRigidbody.angularVelocity);
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

    private void UpdateNetworkMaxAcceleration()
    {
        Vector3 negativeMax;
        Vector3 positiveMax;
        if (highGMode)
        {
            negativeMax = Vector3.one * thrusterHighGAcc;
            positiveMax = negativeMax;
            positiveMax.z = engineHighGAcc;
        }
        else
        {
            negativeMax = Vector3.one * thrusterCruiseAcc;
            positiveMax = negativeMax;
            positiveMax.z = engineCruiseAcc;
        }
        networkScaledObject.SetMaxAcceleration(positiveMax, negativeMax);
    }

    [ServerRpc]
    private void SetHighGModeServerRpc(bool highGMode)
    {
        this.highGMode = highGMode;
        UpdateNetworkMaxAcceleration();
        UpdatePassiveEmission();
    }

    public void ToggleHighGMode(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        highGMode = state == 1;
        if (IsOwner)
            SetHighGModeServerRpc(highGMode);
        UpdatePassiveEmission();
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

    public void ToggleBattery(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        if (state == 0)
        {
            if (isStarted)
            {
                Shutdown(true);
            }
            SetBatteryOn(false);
        }
        else
        {
            if (fuel <= 0f)
                return;
            startAudioSource.PlayOneShot(batteryStartClip);
            SetBatteryOn(true);
        }
    }

    public void ToggleAPU(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        switch (state)
        {
            case 0:
                if (isStarted)
                {
                    Shutdown(false);
                }
                else
                {
                    if (APUOnRoutine != null)
                    {
                        StopCoroutine(APUOnRoutine);
                        APUOnRoutine = null;
                    }
                    if (APUStartupRoutine != null)
                    {
                        StopCoroutine(APUStartupRoutine);
                        APUStartupRoutine = null;
                    }
                    isAPUOn = false;
                    OnAPUOff?.Invoke();
                }
                break;
            case 1:
                if (!isBatteryOn || isStarted || APUStartupRoutine != null)
                    return;
                APUOnRoutine ??= StartCoroutine(TurnAPUOn());
                break;
            case 2:
                StartAPU();
                break;
        }
    }

    public void ToggleEngine(int state)
    {
        if (!IsOwnerOrOffline)
            return;
        switch (state)
        {
            case 0:
                if (engineStarted)
                {
                    ShutdownEngine();
                }
                else
                {
                    if (engineOnRoutine != null)
                    {
                        StopCoroutine(engineOnRoutine);
                        engineOnRoutine = null;
                    }
                    if (engineStartupRoutine != null)
                    {
                        StopCoroutine(engineStartupRoutine);
                        engineStartupRoutine = null;
                    }
                    isEngineOn = false;
                }
                break;
            case 1:
                if (engineStarted || !isStarted)
                    return;
                engineOnRoutine ??= StartCoroutine(EngineOnRoutine());
                break;
            case 2:
                StartEngine();
                break;
        }
    }

    public void UpdatePassiveEmission()
    {
        float emissionRadius = 0f;
        if (isBatteryOn)
            emissionRadius += batteryOnEmission;
        if (isStarted)
            emissionRadius += APUStartedEmission;
        if (engineStarted)
            emissionRadius += highGMode ? engineHighGEmission : engineCruiseEmission;
        if (shields.IsActive)
            emissionRadius += shieldsEmission;
        if (emissionRadius <= 0)
        {
            radarTarget.SetEmissionActive(false);
            Debug.Log(GameLog.ObjectLog(this, $"Disabled passive emission trigger."));
        }
        else
        {
            radarTarget.SetEmissionActive(true);
            radarTarget.SetEmissionTriggerRadius(emissionRadius);
            Debug.Log(GameLog.ObjectLog(this, $"Updated passive emission radius to {emissionRadius}."));
        }
    }

    [ServerRpc]
    private void UpdateBatteryServerRpc(bool value)
    {
        isBatteryOn = value;
        UpdatePassiveEmission();
    }

    [ServerRpc]
    private void UpdateAPUServerRpc(bool value)
    {
        isStarted = value;
        if (!value)
            isAPUOn = false;
        UpdatePassiveEmission();
    }

    [ServerRpc]
    private void UpdateEngineServerRpc(bool value)
    {
        engineStarted = value;
        if (!value)
            isEngineOn = false;
        UpdatePassiveEmission();
    }

    private void SetBatteryOn(bool value)
    {
        if (isBatteryOn == value)
            return;
        isBatteryOn = value;
        batteryOnIndicator.SetActive(value);
        batteryOffIndicator.SetActive(!value);
        UpdatePassiveEmission();
        if (IsOwner && !IsServerInitialized)
            UpdateBatteryServerRpc(value);
    }

    private void SetAPUStarted(bool value)
    {
        isStarted = value;
        if (!value)
            isAPUOn = false;
        UpdatePassiveEmission();
        if (IsOwner && !IsServerInitialized)
            UpdateAPUServerRpc(value);
    }

    private void SetEngineStarted(bool value)
    {
        engineStarted = value;
        if (!value)
            isEngineOn = false;
        UpdatePassiveEmission();
        if (IsOwner && !IsServerInitialized)
            UpdateEngineServerRpc(value);
    }

    private IEnumerator TurnAPUOn()
    {
        if (fuel <= 0f)
            yield break;
        startAudioSource.clip = beepClip;
        startAudioSource.Play();
        yield return new WaitForSeconds(Random.Range(APUOnTimeRange.x, APUOnTimeRange.y));
        startAudioSource.PlayOneShot(beepClip);
        isAPUOn = true;
        APUOnRoutine = null;
        OnAPUOn?.Invoke();
    }

    private IEnumerator APUStartup()
    {
        if (fuel <= 0f)
            yield break;
        yield return new WaitUntil(() => isAPUOn);
        OnStartupStart?.Invoke();
        startAudioSource.clip = APUStartClip;
        startAudioSource.Play();
        effectsAnimator.SetTrigger("Startup");
        for(int i = 0; i < startupTexts.Length; i++)
        {
            startupTexts[i].text = "INITIALIZING...";
        }
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
        SetAPUStarted(true);
        APUStartupRoutine = null;
        OnStartupEnd?.Invoke();
    }

    private IEnumerator ShutdownAPURoutine(bool instant)
    {
        OnShutdownStart?.Invoke();
        startAudioSource.clip = shutdownClip;
        startAudioSource.Play();
        ShutdownEngine();
        if (instant)
        {
            for (int i = 0; i < ambientAudioSources.Length; i++)
            {
                ambientAudioSources[i].volume = 0;
            }
            SetAPUStarted(false);
            APUShutdownRoutine = null;
            OnShutdownEnd?.Invoke();
            OnAPUOff?.Invoke();
            yield break;
        }
        for(int i = 0; i < startupTexts.Length; i++)
        {
            startupTexts[i].text = "SHUTTING DOWN...";
        }
        effectsAnimator.SetTrigger("Shutdown");
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
        SetAPUStarted(false);
        APUShutdownRoutine = null;
        OnShutdownEnd?.Invoke();
        OnAPUOff?.Invoke();
    }

    public void StartAPU()
    {
        if (!IsOwnerOrOffline || !isBatteryOn || isStarted || APUStartupRoutine != null || fuel <= 0f)
            return;
        APUStartupRoutine = StartCoroutine(APUStartup());
    }

    private void ResetAllCoroutines()
    {
        StopAllCoroutines();
        APUOnRoutine = null;
        APUStartupRoutine = null;
        engineOnRoutine = null;
        engineStartupRoutine = null;
    }

    public void Shutdown(bool instant)
    {
        if (!IsOwnerOrOffline || !isStarted || APUShutdownRoutine != null)
            return;
        ResetAllCoroutines();
        APUShutdownRoutine = StartCoroutine(ShutdownAPURoutine(instant));
    }

    private IEnumerator EngineOnRoutine()
    {
        if (fuel <= 0f)
            yield break;
        startAudioSource.PlayOneShot(beepClip);
        startAudioSource.clip = engineOnClip;
        startAudioSource.Play();
        yield return new WaitWhile(() => startAudioSource.isPlaying);
        startAudioSource.PlayOneShot(beepClip);
        isEngineOn = true;
        engineOnRoutine = null;
    }

    private IEnumerator EngineStartRoutine()
    {
        if (fuel <= 0f)
            yield break;
        yield return new WaitUntil(() => isEngineOn);
        engineAmbientAudioSource.PlayOneShot(engineStartClip, 0.9f);
        float timer = 0f;
        engineAmbientAudioSource.volume = 0f;
        engineAmbientAudioSource.pitch = 1f;
        engineAmbientAudioSource.Play();
        while(timer < engineStartClip.length)
        {
            timer += Time.deltaTime;
            engineAmbientAudioSource.volume = engineAmbientVolume * timer / engineStartClip.length;
            yield return null;
        }
        SetEngineStarted(true);
        engineStartupRoutine = null;
        OnEngineStart?.Invoke();
    }

    private void StartEngine()
    {
        if (!IsOwnerOrOffline || !isStarted || engineStarted || engineStartupRoutine != null || fuel <= 0f)
            return;
        StartCoroutine(EngineStartRoutine());
    }

    private IEnumerator SlowDownEngineAudio()
    {
        float timer = 0f;
        while(timer < engineAmbientShutdownTime)
        {
            timer += Time.deltaTime;
            float t = 1f - timer / engineAmbientShutdownTime;
            engineAmbientAudioSource.volume = engineAmbientVolume * t;
            engineAmbientAudioSource.pitch = t;
            yield return null;
        }
        engineAmbientAudioSource.Stop();
    }

    private void ShutdownEngine()
    {
        if (!IsOwnerOrOffline || !engineStarted)
            return;
        if (engineStartupRoutine != null)
        {
            StopCoroutine(engineStartupRoutine);
            engineStartupRoutine = null;
        }
        StartCoroutine(SlowDownEngineAudio());
        SetEngineStarted(false);
        OnEngineStop?.Invoke();
    }
}
