using SpaceStuff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Ship : RadarTarget
{
    [Header("Ship")] public StatSystem statSystem;
    public Transform pilotPoint;
    [SerializeField] protected bool isPilot;

    public GameObject radarModel;
    [SerializeField] protected ParticleSystem rocketTrail;
    public LayerMask ignoreLayers;

    [SerializeField] private Transform velocityDirectionPivot;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI fuelText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider fuelSlider;
    [SerializeField] private Slider healthSlider;

    [SerializeField] private float engineForce = 50f;
    [SerializeField] private float engineLaunchForce = 50f;
    [SerializeField] private float thrusterForce = 10f;
    private bool launchMode = false;

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

    [SerializeField, Tooltip("Minimum relative collision speed for ship to take damage")] private float minCollideSpeed = 5f;
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

        // Clamp integral between -maxIntegral and maxIntegral to prevent integral windup
        if(Mathf.Approximately(I, 0f))
        {
            xRotatePID = new PIDController(P, I, D, float.MaxValue);
            yRotatePID = new PIDController(P, I, D, float.MaxValue);
            zRotatePID = new PIDController(P, I, D, float.MaxValue);

            xMovePID = new PIDController(P, I, D, float.MaxValue);
            yMovePID = new PIDController(P, I, D, float.MaxValue);
            zMovePID = new PIDController(P, I, D, float.MaxValue);
        }
        else
        {
            xRotatePID = new PIDController(P, I, D, thrusterTorque.x / I);
            yRotatePID = new PIDController(P, I, D, thrusterTorque.y / I);
            zRotatePID = new PIDController(P, I, D, thrusterTorque.z / I);

            xMovePID = new PIDController(P, I, D, thrusterForce / I);
            yMovePID = new PIDController(P, I, D, thrusterForce / I);
            zMovePID = new PIDController(P, I, D, thrusterForce / I);
        }
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

            if (finalForce.sqrMagnitude > 0.0001)
                doubleRigidbody.AddRelativeForce(finalForce, ForceMode.Force);

            if (finalForce.z > 0)
            {
                rocketTrail.gameObject.SetActive(true);
            }
            else
            {
                rocketTrail.gameObject.SetActive(false);
            }

            // Calculate local torque to apply
            Vector3d finalTorque = Vector3d.zero;

            // Rotation inputs for pitch and roll
            Vector2 lookDelta = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked) // Dont rotate ship while player is using mouse to interact with switches/UI
            {
                lookDelta = GameManager.Instance.inputActions.Player.Look.ReadValue<Vector2>();
                Vector2 desiredLook = GameManager.Instance.playerSettings.sensitivity * GameManager.Instance.sensitivityScale * lookDelta;
                desiredLook = Vector2.ClampMagnitude(desiredLook, 1f); // Prevent from rotating faster than max torque allows
                finalTorque = new Vector3d(-desiredLook.y * thrusterTorque.x, 0, 0);
                if (rollMode)
                    finalTorque.z = -desiredLook.x * thrusterTorque.z;
                else
                    finalTorque.y = desiredLook.x * thrusterTorque.y;
            }
            rotating = lookDelta.x != 0 || lookDelta.y != 0;

            if (autoStabilizeRot && !rotating)
            {
                Vector3 localAngularVelocity = transform.InverseTransformDirection(doubleRigidbody.angularVelocity.ToVector3());
                Vector3 error = stableLocalAngularVelocity - localAngularVelocity;

                float tx = xRotatePID.GetOutput(error.x, Time.fixedDeltaTime);
                float ty = yRotatePID.GetOutput(error.y, Time.fixedDeltaTime);
                float tz = zRotatePID.GetOutput(error.z, Time.fixedDeltaTime);
                Vector3 desired = new Vector3(tx, ty, tz);
                desired = Vector3.ClampMagnitude(desired, 1f);

                finalTorque = new Vector3d(
                    desired.x * thrusterTorque.x, 
                    desired.y * thrusterTorque.y, 
                    desired.z * thrusterTorque.z
                );
            }
            if (finalTorque.sqrMagnitude > 0.0001)
                doubleRigidbody.AddRelativeTorque(finalTorque, ForceMode.Force);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Projectile") || collision.transform.CompareTag("Torpedo"))
            return; // Projectiles handle damage themselves
        float sqrCollisionSpeed = collision.relativeVelocity.sqrMagnitude;
        if (collision.rigidbody.mass < 0.5f || sqrCollisionSpeed <= minCollideSpeed * minCollideSpeed)
            return;
        ContactPoint contact = collision.GetContact(0);
        float impulseMagnitude = collision.impulse.magnitude;
        // Optional: attenuate glancing blows (0 = glancing, 1 = head-on)
        float impactDot = Mathf.Abs(Vector3.Dot(collision.impulse.normalized, contact.normal));

        // Front attenuation
        Vector3 relativeCollisionPoint = contact.point - transform.position;
        float attenuation = Mathf.Clamp01((-relativeCollisionPoint.z + maxLocalZ) / (2f * maxLocalZ));

        float damageAmount = impulseMagnitude * impactDot * attenuation * collideDamageScale;

        if (shields != null)
            shields.Damage(damageAmount, contact.point);
        else
            statSystem.Damage(damageAmount);

        Debug.Log($"Collided with {collision.gameObject.name} at speed " +
                $"{collision.relativeVelocity.magnitude:F2}, impulse {impulseMagnitude:F1}, " +
                $"taking {damageAmount:F2} damage");
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
