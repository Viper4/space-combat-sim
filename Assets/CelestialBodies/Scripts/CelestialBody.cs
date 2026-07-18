using FishNet.Object;
using SpaceStuff;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(ScaledTransform), typeof(ScaledRigidbody))]
public class CelestialBody : NetworkBehaviour
{
    private const double v = 4.0 / 3.0 * Math.PI;
    private const double G = 6.6743e-11;
    private const double solarRadius = 6.957e8;
    private const double solarMass = 1.989e30;

    [HideInInspector] public ScaledTransform scaledTransform;
    private ScaledRigidbody scaledRigidbody;
    private CelestialBodyGenerator generator;
    private SpaceLight spaceLight;

    [Header("Generation")]
    [SerializeField] private int originalLayer;
    public GenerationSettings generationSettings;
    [HideInInspector] public bool generationSettingsFoldout;

    [Header("Gravity")]
    [ConditionalHide("gravity")] public GravitySettings gravitySettings;
    [HideInInspector] public bool gravitySettingsFoldout;

    [Header("Orbit")]
    [SerializeField] private CelestialBody orbitTarget;
    [SerializeField] private bool tidallyLocked = false;
    [SerializeField] private bool overrideRotationAxis = false;

    private double mass;
    private float temperature;
    private Vector3d scale = Vector3d.zero;

    private bool initialized;
    public bool pauseUpdates = false;

    private bool IsServerOrOffline => IsServerInitialized || IsOffline;

    private void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        TryGetComponent(out generator);
        TryGetComponent(out spaceLight);

        if (IsOffline)
            Init();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        Init();
        if (generator != null)
            InitializeObserversRpc(scale.x, scale.y, scale.z, scaledRigidbody.mass, generator.GetSeeds());
        else
            InitializeObserversRpc(scale.x, scale.y, scale.z, scaledRigidbody.mass, null);
    }

    private void Init()
    {
        if (!IsServerOrOffline)
            return;

        switch (generationSettings.bodyType)
        {
            case GenerationSettings.BodyType.Planet:
                // Generate random scale
                float randomX;
                float randomY;
                float randomZ;
                if (generationSettings.sphere)
                {
                    randomX = randomY = randomZ = Random.Range(generationSettings.scaleRange[0].x, generationSettings.scaleRange[1].x);
                }
                else
                {
                    // Prevent extremely elongated objects (pancakes/needles)
                    float randomXt = Random.Range(0f, 1f);
                    float randomYt = Random.Range(0f, 1f);
                    float randomZt = Random.Range(0f, 1f);
                    int buckets = 3;
                    float bucketSize = 1f / buckets;
                    for(int i = 0; i < buckets; i++)
                    {
                        if (randomXt < (i + 1) * bucketSize)
                        {
                            randomYt = Random.Range(i * bucketSize, (i + 1) * bucketSize);
                            randomZt = Random.Range(i * bucketSize, (i + 1) * bucketSize);
                            break;
                        }
                    }
                    randomX = Mathf.Lerp(generationSettings.scaleRange[0].x, generationSettings.scaleRange[1].x, randomXt);
                    randomY = Mathf.Lerp(generationSettings.scaleRange[0].y, generationSettings.scaleRange[1].y, randomYt);
                    randomZ = Mathf.Lerp(generationSettings.scaleRange[0].z, generationSettings.scaleRange[1].z, randomZt);
                }
                scale = new Vector3d(randomX, randomY, randomZ);

                float density = Random.Range(generationSettings.densityRange.x, generationSettings.densityRange.y);
                scaledRigidbody.mass = density * v * scale.x * scale.y * scale.z;
                break;
            case GenerationSettings.BodyType.Star:
                GenerationSettings.StarTypeRule[] rules = generationSettings.starDistributions;

                float totalWeight = 0f;
                foreach (var rule in rules)
                    totalWeight += Mathf.Max(0f, rule.weight);

                float roll = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                GenerationSettings.StarTypeRule picked = rules[^1];

                foreach (var rule in rules)
                {
                    cumulative += Mathf.Max(0f, rule.weight);
                    if (roll <= cumulative)
                    {
                        picked = rule;
                        break;
                    }
                }

                float t = Random.value;
                double massInSolarMasses = Mathf.Lerp(picked.minMass, picked.maxMass, t);
                mass = massInSolarMasses * solarMass;
                scaledRigidbody.mass = (float)mass;

                temperature = Mathf.Lerp(picked.minTemperature, picked.maxTemperature, t);

                double radiusInMeters;
                if (!picked.setRadius)
                {
                    // Mass-luminosity relation (empirical, main sequence only):
                    //   M < 0.43 M☉  →  L = 0.23 * M^2.3
                    //   0.43–2 M☉    →  L = M^4
                    //   2–55 M☉      →  L = 1.4 * M^3.5
                    //   M > 55 M☉    →  L ≈ 32000 * M (Eddington-limited)
                    double L_solar;
                    if (massInSolarMasses < 0.43)
                        L_solar = 0.23 * Math.Pow(massInSolarMasses, 2.3);
                    else if (massInSolarMasses < 2.0)
                        L_solar = Math.Pow(massInSolarMasses, 4.0);
                    else if (massInSolarMasses < 55.0)
                        L_solar = 1.4 * Math.Pow(massInSolarMasses, 3.5);
                    else
                        L_solar = 32000.0 * massInSolarMasses;

                    // Convert to watts: L☉ = 3.828e26 W
                    const double solarLuminosity = 3.828e26;
                    double luminosity = L_solar * solarLuminosity;

                    // R = sqrt(L / (4π σ T⁴))
                    radiusInMeters = Math.Sqrt(luminosity / (4.0 * Math.PI * SpaceMath.stefanBoltzmann * Math.Pow(temperature, 4.0)));
                }
                else
                {
                    double radiusInSolarRadii = Mathf.Lerp(picked.minRadius, picked.maxRadius, t);
                    radiusInMeters = radiusInSolarRadii * solarRadius;
                }

                scale = new Vector3d(radiusInMeters, radiusInMeters, radiusInMeters);

                if (spaceLight != null)
                {
                    Color tint = Color.white;
                    if (picked.starType == GenerationSettings.StarType.CarbonStar)
                        tint = new Color(1f, 0.1f, 0.1f, 1f);
                    spaceLight.SetTemperature(temperature, tint);

                    if (!IsOffline)
                        SetSpaceLightObserversRpc(temperature, tint);
                }
                break;
            case GenerationSettings.BodyType.BlackHole:

                break;
        }

        scaledTransform.realScale = scale;

        if (gravitySettings != null && gravitySettings.applyGravity)
        {
            ScaledSpacePhysics.Instance.GravityStep += ApplyGravity;
        }

        Vector3 min = generationSettings.initialAngularVelocityRange[0];
        Vector3 max = generationSettings.initialAngularVelocityRange[1];
        scaledRigidbody.angularVelocity = new Vector3d(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z));

        if (generator != null)
            generator.Init();

        initialized = true;

        if(orbitTarget != null)
        {
            StartCoroutine(SetOrbitalVelocity());
        }
    }

    [ObserversRpc(ExcludeServer = true, BufferLast = true)]
    private void InitializeObserversRpc(double scaleX, double scaleY, double scaleZ, double mass, Vector3[] seeds)
    {
        Debug.Log($"[CelestialBody] {name} initialized from server data.");
        scaledTransform.realScale = new Vector3d(scaleX, scaleY, scaleZ);
        scaledRigidbody.mass = mass;
        if (generator != null)
            generator.Init(seeds);
        if (gravitySettings != null && gravitySettings.applyGravity)
        {
            ScaledSpacePhysics.Instance.GravityStep += ApplyGravity;
        }
        initialized = true;
    }

    [ObserversRpc(ExcludeServer = true, BufferLast = true)]
    private void SetSpaceLightObserversRpc(float temperature, Color tint)
    {
        if (spaceLight != null)
            spaceLight.SetTemperature(temperature, tint);
    }

    public bool Initialized()
    {
        return initialized;
    }

    public void SetOrbit(CelestialBody toOrbit)
    {
        orbitTarget = toOrbit;
        StartCoroutine(SetOrbitalVelocity());
    }

    /* shipMass * g = (shipMass * v^2) / distance
     * v = sqrt(distance * g)
     */
    private IEnumerator SetOrbitalVelocity()
    {
        yield return new WaitUntil(orbitTarget.Initialized);

        Vector3d posA = scaledTransform.realPosition;
        Vector3d posB = orbitTarget.scaledTransform.realPosition;
        double distance;
        double g;
        if (orbitTarget.orbitTarget == this)
        {
            // Handle binary systems
            double massA = scaledRigidbody.mass;
            double massB = orbitTarget.scaledRigidbody.mass;
            Vector3d barycenter = (posA * massA + posB * massB) / (massA + massB);

            Vector3d rA = posA - barycenter;
            Vector3d rB = posB - barycenter;
            distance = rA.magnitude + rB.magnitude;

            // orbital plane
            Vector3d axis = Vector3d.Cross(rA, rB);
            if (axis.sqrMagnitude < 1e-10)
                axis = Vector3d.up;
            axis = axis.normalized;

            Vector3d dirA = Vector3d.Cross(axis, rA).normalized;
            Vector3d dirB = Vector3d.Cross(axis, rB).normalized;

            g = orbitTarget.CalculateGravityAcceleration(posA);

            double orbitalSpeed = Math.Sqrt(g * (massA + massB) / distance);

            // velocities (opposite directions, mass-weighted)
            Vector3d vA = dirA * orbitalSpeed * (massB / (massA + massB));
            Vector3d vB = -dirB * orbitalSpeed * (massA / (massA + massB));

            scaledRigidbody.velocity = vA;
            orbitTarget.scaledRigidbody.velocity = vB;
            yield break;
        }

        // Treat A as secondary body, B as primary

        Vector3d toCenter = posB - posA;
        Vector3d perpendicular = Vector3d.Cross(toCenter, orbitTarget.transform.up.ToVector3d()).normalized;

        g = orbitTarget.CalculateGravityAcceleration(scaledTransform.realPosition);
        distance = toCenter.magnitude;

        Vector3d orbitVelocity = Math.Sqrt(distance * g) * perpendicular;

        // Inherit target's velocity to handle nested orbits
        scaledRigidbody.velocity = orbitTarget.scaledRigidbody.velocity + orbitVelocity;

        if (tidallyLocked)
        {
            // Set angular velocity so the body is always facing the orbit target
            double angularSpeed = orbitVelocity.magnitude / distance;
            Vector3d rotationAxis;
            if (overrideRotationAxis)
            {
                rotationAxis = Vector3d.Cross(toCenter, orbitVelocity).normalized;
            }
            else
            {
                rotationAxis = transform.up.ToVector3d();
            }

            scaledRigidbody.angularVelocity = rotationAxis * angularSpeed;
        }
    }

    private void UpdateTidalLock()
    {
        if (!tidallyLocked || orbitTarget == null || overrideRotationAxis)
            return;

        Vector3d up = transform.up.ToVector3d();

        // Desired forward direction toward parent.
        Vector3d desiredForward = (orbitTarget.scaledTransform.realPosition - scaledTransform.realPosition).normalized;

        // Project onto plane perpendicular to spin axis.
        desiredForward -= up * Vector3d.Dot(desiredForward, up);

        if (desiredForward.sqrMagnitude < 1e-10)
            return;

        desiredForward.Normalize();

        // Current forward projected onto same plane.
        Vector3d currentForward = transform.forward.ToVector3d();
        currentForward -= up * Vector3d.Dot(currentForward, up);
        currentForward.Normalize();

        // Signed angle around the spin axis.
        double angle = Math.Atan2(Vector3d.Dot(up, Vector3d.Cross(currentForward, desiredForward)), Vector3d.Dot(currentForward, desiredForward));

        // Orbital angular speed.
        double distance = (orbitTarget.scaledTransform.realPosition - scaledTransform.realPosition).magnitude;

        double g = orbitTarget.CalculateGravityAcceleration(scaledTransform.realPosition);

        double orbitalAngularSpeed = Math.Sqrt(g / distance);

        // Small correction to eliminate drift.
        const double correctionGain = 0.5;

        scaledRigidbody.angularVelocity = up * (orbitalAngularSpeed + angle * correctionGain);
    }

    private void FixedUpdate()
    {
        if (!initialized || generator == null || pauseUpdates)
            return;

        if (IsServerOrOffline && tidallyLocked && !overrideRotationAxis)
        {
            UpdateTidalLock();
        }
        
        if (scaledTransform.visible)
        {
            if (!generator.generated && generationSettings.autoGenerate)
            {
                generator.GenerateCelestialBody();
                scaledTransform.ResetVisualComponents(originalLayer);
                scaledTransform.UpdateVisualComponents();
                scaledTransform.UpdateRealRadius();
            }

            if (!generationSettings.simple && Camera.main != null)
            {
                if (generator.UpdateQuadTrees(Camera.main))
                {
                    scaledTransform.ResetVisualComponents(originalLayer);
                    scaledTransform.UpdateVisualComponents();
                    scaledTransform.UpdateRealRadius();
                }
            }
        }
        else
        {
            if (generator.generated)
            {
                generator.DestroyGeneratedChunks();
            }
        }
    }

    public void ApplyGravity(ScaledRigidbody other)
    {
        if (!gravitySettings.applyGravity || other.id == scaledRigidbody.id || (gravitySettings.affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        Vector3d gravityDirection = (scaledTransform.realPosition - other.scaledTransform.realPosition).normalized;
        double acceleration = CalculateGravityAcceleration(other.scaledTransform.realPosition);

        other.AddGravity(gravityDirection * acceleration);
    }

    public double CalculateGravityAcceleration(Vector3d point)
    {
        // g = (Gm)/r^2
        // m = mass of body
        // r = distance between centers
        return G * mass / (scaledTransform.realPosition - point).sqrMagnitude;
    }

    private void OnDestroy()
    {
        ScaledSpacePhysics.Instance.GravityStep -= ApplyGravity;
    }
}
