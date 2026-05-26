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

    [SerializeField] protected HUDSystem _HUDSystem;
    [SerializeField] private GameObject pilotUI;

    public Animator UIAnimator;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI fuelText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider fuelSlider;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Transform velocityDirectionPivot;

    [SerializeField] private float enginePower = 50f;
    [SerializeField] private float thrusterPower = 10f;

    [SerializeField, Tooltip("Thruster distance from ship's x axis (Pitch)")] private float thrusterRadiusX = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's y axis (Yaw)")] private float thrusterRadiusY = 5f;
    [SerializeField, Tooltip("Thruster distance from ship's z axis (Roll)")] private float thrusterRadiusZ = 1f;
    private Vector3 thrusterTorque;
    private bool rollMode = true;

    private bool autoStabilizeRot = false;
    private bool autoStabilizePos = false;
    private Vector3 stableLocalVelocity;
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

        thrusterTorque = new Vector3(thrusterPower * thrusterRadiusX, thrusterPower * thrusterRadiusY, thrusterPower * thrusterRadiusZ);

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

            xMovePID = new PIDController(P, I, D, thrusterPower / I);
            yMovePID = new PIDController(P, I, D, thrusterPower / I);
            zMovePID = new PIDController(P, I, D, thrusterPower / I);
        }
    }

    protected virtual void Update()
    {
        if (isPilot && (IsOwner || GameManager.Instance.offlineMode))
        {
            speedText.text = "Speed: " + CustomMethods.SpeedToFormattedString(doubleRigidbody.velocity.magnitude, 2);
            if (doubleRigidbody.velocity != Vector3d.zero)
                velocityDirectionPivot.rotation = Quaternion.LookRotation(doubleRigidbody.velocity.ToVector3(), transform.up);
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (isPilot && (IsOwner || GameManager.Instance.offlineMode))
        {
            // Translation inputs
            Vector3 newMove = GameManager.Instance.inputActions.Player.Move.ReadValue<Vector3>();
            moving = newMove.x != 0 || newMove.y != 0 || newMove.z != 0;

            double forceX = newMove.x * thrusterPower;
            double forceY = newMove.y * thrusterPower;
            double forceZ = Mathf.Max(0, newMove.z) * enginePower + Mathf.Min(0, newMove.z) * thrusterPower; // Engine can only move ship forward
            doubleRigidbody.AddRelativeForce(new Vector3d(forceX, forceY, forceZ), ForceMode.Force);

            if (newMove.z > 0)
            {
                rocketTrail.gameObject.SetActive(true);
                // if (!rocketTrail.isPlaying)
                // {
                //     rocketTrail.Play();
                // }
            }
            else
            {
                rocketTrail.gameObject.SetActive(false);
                // if (rocketTrail.isPlaying)
                // {
                //     rocketTrail.Stop();
                // }
            }

            // Rotation inputs for pitch and roll
            Vector2 lookDelta = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked) // Dont rotate ship while player is using mouse to interact with switches/UI
            {
                lookDelta = GameManager.Instance.inputActions.Player.Look.ReadValue<Vector2>();
                Vector2 desiredLook = GameManager.Instance.playerSettings.sensitivity * GameManager.Instance.sensitivityScale * Time.fixedDeltaTime * lookDelta;

                Vector3d torque = new Vector3d(-desiredLook.y * thrusterTorque.x, 0, 0);
                if (rollMode)
                    torque.z = -desiredLook.x * thrusterTorque.z;
                else
                    torque.y = desiredLook.x * thrusterTorque.y;
                doubleRigidbody.AddRelativeTorque(torque, ForceMode.Force);
            }
            rotating = lookDelta.x != 0 || lookDelta.y != 0;

            if (autoStabilizeRot && !rotating)
            {
                Vector3 localAngularVelocity = transform.InverseTransformDirection(doubleRigidbody.angularVelocity.ToVector3());
                Vector3 error = localAngularVelocity - stableLocalAngularVelocity;

                float tx = -xRotatePID.GetOutput(error.x, Time.fixedDeltaTime);
                float ty = -yRotatePID.GetOutput(error.y, Time.fixedDeltaTime);
                float tz = -zRotatePID.GetOutput(error.z, Time.fixedDeltaTime);
                Vector3 desired = new Vector3(tx, ty, tz);
                desired = Vector3.ClampMagnitude(desired, 1f);

                Vector3d localTorque = new Vector3d(desired.x * thrusterTorque.x, desired.y * thrusterTorque.y, desired.z * thrusterTorque.z);

                doubleRigidbody.AddRelativeTorque(localTorque, ForceMode.Force);
            }
            if (autoStabilizePos && !moving)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(velocity);
                Vector3 error = localVelocity - stableLocalVelocity;

                float fx = -xMovePID.GetOutput(error.x, Time.fixedDeltaTime);
                float fy = -yMovePID.GetOutput(error.y, Time.fixedDeltaTime);
                float fz = -zMovePID.GetOutput(error.z, Time.fixedDeltaTime);
                Vector3 desired = new Vector3(fx, fy, fz);
                desired = Vector3.ClampMagnitude(desired, 1f);

                // Use engine to go forward (+z) and thrusters to go backward (-z)
                Vector3d localForce = new Vector3d(desired.x * thrusterPower, desired.y * thrusterPower, desired.z > 0 ? desired.z * enginePower : desired.z * thrusterPower);

                doubleRigidbody.AddRelativeForce(localForce, ForceMode.Force);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Projectile"))
            return; // Projectiles handle damage themselves
        float sqrCollisionSpeed = collision.relativeVelocity.sqrMagnitude;
        if (sqrCollisionSpeed <= minCollideSpeed * minCollideSpeed)
            return;
        Vector3 collisionPoint = collision.GetContact(0).point;
        Vector3 relativeCollisionPoint = collisionPoint - transform.position;
        // Damage at the front of the ship is less severe
        float attenuation = Mathf.Clamp01((-relativeCollisionPoint.z + maxLocalZ) / (2 * maxLocalZ)); // Remap [back, front] from [-maxLocalZ, maxLocalZ] to [1, 0]
        float damageAmount = (sqrCollisionSpeed - minCollideSpeed * minCollideSpeed) * attenuation * collideDamageScale;
        if (shields != null)
            shields.Damage(damageAmount, collisionPoint);
        else
            statSystem.Damage(damageAmount);
        Debug.Log($"{transform.name} collided with {collision.transform.name}");
    }

    public void SetStableConfiguration(int state)
    {
        if (state == 0)
        {
            stableLocalVelocity = Vector3.zero;
            stableLocalAngularVelocity = Vector3.zero;
        }
        else
        {
            stableLocalVelocity = transform.InverseTransformDirection(doubleRigidbody.velocity.ToVector3());
            stableLocalAngularVelocity = transform.InverseTransformDirection(doubleRigidbody.angularVelocity.ToVector3());
        }
    }

    public void SetAutoRotStabilization(int state)
    {
        autoStabilizeRot = state == 1;
    }

    public void SetAutoPosStabilization(int state)
    {
        autoStabilizePos = state == 1;
    }

    public void ToggleRollYaw(int state)
    {
        rollMode = state == 0;
    }
}
