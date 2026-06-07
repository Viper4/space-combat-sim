using System;
using SpaceStuff;
using TMPro;
using UnityEngine;

public class Ship : RadarTarget
{
    [Header("Ship")] public StatSystem statSystem;
    public Transform pilotPoint;
    [SerializeField] protected bool isPilot;

    public GameObject radarModel;
    [SerializeField] protected ParticleSystem rocketTrail;

    [SerializeField] private Transform velocityDirectionPivot;
    [SerializeField] private TextMeshProUGUI speedText;

    [SerializeField] private float engineForce = 50f;
    [SerializeField] private float engineLaunchForce = 50f;
    [SerializeField] private float thrusterForce = 10f;
    private bool launchMode = false;
    [SerializeField] private AudioSource thrusterAudioSource;
    [SerializeField] private float thrusterVolumeScale = 1.0f;
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private float engineVolumeScale = 0.8f;
    [SerializeField] private AudioClip normalEngineClip;
    [SerializeField] private AudioClip launchEngineClip;

    [SerializeField, Tooltip("Thruster distance from ship's x axis (Pitch)")] private float thrusterRadiusX = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's y axis (Yaw)")] private float thrusterRadiusY = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's z axis (Roll)")] private float thrusterRadiusZ = 1f;
    private Vector3 thrusterTorque;
    private bool rollMode = true;

    private bool autoStabilizeRot = false;
    private bool autoStabilizePos = false;
    private bool useRelativeVelocity = false;
    private Vector3 stableVelocity;
    private Vector3 stableLocalAngularVelocity;
    [SerializeField, Range(0, 1000)] private float P, I, D;
    private PIDController xRotatePID;
    private PIDController yRotatePID;
    private PIDController zRotatePID;

    private PIDController xMovePID;
    private PIDController yMovePID;
    private PIDController zMovePID;

    protected float fuel = 100f;
    [SerializeField] private float maxFuel = 100f;

    [SerializeField, Tooltip("Minimum collision speed for ship to take damage")] private float minCollideSpeed = 5f;
    [SerializeField] private float collideDamageScale = 0.01f;
    [SerializeField, Tooltip("Local Z position at the front tip of the ship")] private float maxLocalZ;
    public Shields shields;

    private bool moving = false;
    private bool rotating = false;

    protected override void Start()
    {
        base.Start();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        thrusterTorque = new Vector3(thrusterForce * thrusterRadiusX, thrusterForce * thrusterRadiusY, thrusterForce * thrusterRadiusZ);

        xRotatePID = new PIDController(P, I, D);
        yRotatePID = new PIDController(P, I, D);
        zRotatePID = new PIDController(P, I, D);

        xMovePID = new PIDController(P, I, D);
        yMovePID = new PIDController(P, I, D);
        zMovePID = new PIDController(P, I, D);
    }

    protected virtual void Update()
    {
        if (isPilot && (IsOwner || GameManager.Instance.offlineMode))
        {
            speedText.text = "SPD " + SpaceMath.SpeedToFormattedString(doubleRigidbody.velocity.magnitude, 2);
            if (doubleRigidbody.velocity != Vector3d.zero)
                velocityDirectionPivot.rotation = Quaternion.LookRotation(doubleRigidbody.velocity.ToVector3(), transform.up);
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (isPilot && (IsOwner || GameManager.Instance.offlineMode))
        {
            // Calculate local force to apply
            Vector3d finalForce;

            // Translation inputs
            Vector3 desiredMove = GameManager.Instance.inputActions.Player.Move.ReadValue<Vector3>();
            desiredMove = Vector3.ClampMagnitude(desiredMove, 1f); // Prevent from moving faster than max force allows
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
                Vector3 stableLocalVelocity = desiredMove * 1000f;
                if (useRelativeVelocity)
                {
                    // stableVelocity assumed to already be in local coords
                    stableLocalVelocity += stableVelocity;
                }
                else
                {
                    // stableVelocity is in world coords, convert to local
                    stableLocalVelocity += transform.InverseTransformDirection(stableVelocity);
                }

                Vector3 localVelocity = transform.InverseTransformDirection(doubleRigidbody.velocity.ToVector3());
                Vector3 error = stableLocalVelocity - localVelocity;

                float fx = xMovePID.GetOutput(error.x, Time.fixedDeltaTime);
                float fy = yMovePID.GetOutput(error.y, Time.fixedDeltaTime);
                float fz = zMovePID.GetOutput(error.z, Time.fixedDeltaTime);
                Vector3 desired = new Vector3(fx, fy, fz);
                desired = Vector3.ClampMagnitude(desired, 1f);

                // Use engine to go forward (+z) and thrusters to go backward (-z)
                finalForce = new Vector3d(
                    desired.x * thrusterForce, 
                    desired.y * thrusterForce, 
                    desired.z * thrusterForce
                );

                if (desired.z > 0)
                {
                    if (launchMode)
                        finalForce.z = desired.z * engineLaunchForce;
                    else
                        finalForce.z = desired.z * engineForce;
                }
            }

            bool hasForce = finalForce.sqrMagnitude > 0.0001;

            if (hasForce)
            {
                bool usingThrusters =
                    Math.Abs(finalForce.x) > 0.001 ||
                    Math.Abs(finalForce.y) > 0.001 ||
                    finalForce.z < 0.0;

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
                    if (!rocketTrail.gameObject.activeSelf)
                    {
                        rocketTrail.gameObject.SetActive(true);
                    }
                    engineAudioSource.clip = launchMode ? launchEngineClip : normalEngineClip;
                    engineAudioSource.volume = (float)finalForce.z / engineLaunchForce * engineVolumeScale;
                    if (!engineAudioSource.isPlaying)
                        engineAudioSource.Play();
                }
                else if (rocketTrail.gameObject.activeSelf)
                {
                    engineAudioSource.volume = 0f;
                    rocketTrail.gameObject.SetActive(false);
                }

                doubleRigidbody.AddRelativeForce(finalForce, ForceMode.Force);
            }
            else
            {
                if (rocketTrail.gameObject.activeSelf)
                {
                    engineAudioSource.volume = 0f;
                    rocketTrail.gameObject.SetActive(false);
                }
            }

            // Calculate local torque to apply
            Vector3d desiredRotate = Vector3d.zero;

            // Rotation inputs for pitch and roll
            Vector2 lookDelta = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked) // Dont rotate ship while player is using mouse to interact with switches/UI
            {
                lookDelta = GameManager.Instance.inputActions.Player.Look.ReadValue<Vector2>();
                Vector2 desiredLook = GameManager.Instance.playerSettings.sensitivity * GameManager.Instance.sensitivityScale * lookDelta;
                desiredLook = Vector2.ClampMagnitude(desiredLook, 1f); // Prevent from rotating faster than max torque allows
                desiredRotate = new Vector3d(-desiredLook.y, 0.0, 0.0);
                if (rollMode)
                    desiredRotate.z = -desiredLook.x;
                else
                    desiredRotate.y = desiredLook.x;
            }
            rotating = lookDelta.x != 0 || lookDelta.y != 0;

            if (autoStabilizeRot && !rotating)
            {
                Vector3 localAngularVelocity = transform.InverseTransformDirection(doubleRigidbody.angularVelocity.ToVector3());
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
            {
                doubleRigidbody.AddRelativeTorque(finalTorque, ForceMode.Force);
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
            else if (!hasForce)
            {
                thrusterAudioSource.volume = 0f;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Projectile") || collision.transform.CompareTag("Torpedo"))
            return;

        Rigidbody otherRb = collision.rigidbody;

        if (otherRb == null || otherRb.mass < 0.5f)
            return;

        if (collision.relativeVelocity.sqrMagnitude < minCollideSpeed * minCollideSpeed)
            return;

        ContactPoint contact = collision.GetContact(0);

        float impulse = collision.impulse.magnitude;

        // Front attenuation
        Vector3 relativeCollisionPoint = contact.point - transform.position;
        float attenuation = Mathf.Clamp01((-relativeCollisionPoint.z + maxLocalZ) / (2f * maxLocalZ));

        float damage = attenuation * impulse * collideDamageScale;

        if (shields != null)
            shields.Damage(damage, contact.point);
        else
            statSystem.Damage(damage);

        Debug.Log(
            $"Impulse: {impulse:F1}, " +
            $"damage: {damage:F1}");
    }

    public void SetAutoRotStabilization(int state)
    {
        autoStabilizeRot = state == 1;
    }

    public void SetAutoPosStabilization(int state)
    {
        autoStabilizePos = state == 1;
    }

    public void ToggleRelativeVelocity(int state)
    {
        useRelativeVelocity = state == 1;
        if (useRelativeVelocity && stableVelocity != Vector3.zero)
        {
            stableVelocity = transform.InverseTransformDirection(stableVelocity);
        }
    }

    public void SetStableConfiguration(int state)
    {
        if (state == 0)
        {
            stableVelocity = Vector3.zero;
            stableLocalAngularVelocity = Vector3.zero;
        }
        else
        {
            if (useRelativeVelocity)
            {
                stableVelocity = transform.InverseTransformDirection(doubleRigidbody.velocity.ToVector3());
            }
            else
            {
                stableVelocity = doubleRigidbody.velocity.ToVector3();
            }
            stableLocalAngularVelocity = transform.InverseTransformDirection(doubleRigidbody.angularVelocity.ToVector3());
        }
    }

    public void ToggleRollYaw(int state)
    {
        rollMode = state == 0;
    }

    public void ToggleLaunchMode(int state)
    {
        launchMode = state == 1;
    }
}
