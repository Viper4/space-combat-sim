using SpaceStuff;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(ScaledTransform), typeof(DoubleRigidbody))]
public class CelestialBody : MonoBehaviour
{
    private const float v = 4 / 3 * Mathf.PI;

    [HideInInspector] public ScaledTransform scaledTransform;
    private DoubleRigidbody doubleRigidbody;
    private CelestialBodyGenerator generator;
    [SerializeField] private int originalLayer;

    [HideInInspector] public bool generationSettingsFoldout;
    public GenerationSettings generationSettings;

    public bool gravity = true;
    [HideInInspector] public bool gravitySettingsFoldout;
    [ConditionalHide("gravity")] public GravitySettings gravitySettings;
    private Vector3d scale = Vector3d.zero;
    private float surfaceGravity;
    [SerializeField] private CelestialBody orbitTarget;

    private bool initialized;
    public bool pauseUpdates = false;

    private void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        doubleRigidbody = GetComponent<DoubleRigidbody>();
        if (TryGetComponent(out generator))
        {
            if (!generationSettings.random)
                scale = Vector3d.one;
        }
        else
        {
            if (!generationSettings.random)
                scale = scaledTransform.realScale;
        }

        if (generationSettings.random)
        {
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
        }

        if (gravity)
        {
            ScaledSpacePhysics.Instance.PrePhysicsStep += ApplyGravity;

            if (generationSettings.scaleRange[0] == Vector3.zero)
            {
                // No scale range specified, so pick random gravity
                surfaceGravity = Random.Range(gravitySettings.surfaceGravityRange.x, gravitySettings.surfaceGravityRange.y);
            }
            else
            {
                // Ensure gravity is proportional to scale
                float tX = Mathf.InverseLerp(generationSettings.scaleRange[0].x, generationSettings.scaleRange[1].x, (float)scale.x);
                float tY = tX;
                float tZ = tX;
                if (!generationSettings.sphere)
                {
                    tY = Mathf.InverseLerp(generationSettings.scaleRange[0].y, generationSettings.scaleRange[1].y, (float)scale.y);
                    tZ = Mathf.InverseLerp(generationSettings.scaleRange[0].z, generationSettings.scaleRange[1].z, (float)scale.z);
                }
                surfaceGravity = Mathf.Lerp(gravitySettings.surfaceGravityRange.x, gravitySettings.surfaceGravityRange.y, (tX + tY + tZ) / 3);
            }
        }

        scaledTransform.realScale = scale;

        if(generationSettings.calculateMass)
            doubleRigidbody.attachedRigidbody.mass = (float)(generationSettings.density * v * scale.x * scale.x * scale.x);

        Vector3 min = generationSettings.initialAngularVelocityRange[0];
        Vector3 max = generationSettings.initialAngularVelocityRange[1];

        doubleRigidbody.angularVelocity = new Vector3d(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z));
        if (TryGetComponent<SpaceLight>(out var spaceLight))
        {
            spaceLight.Init(scale.x);
        }

        initialized = true;

        if(orbitTarget != null)
        {
            StartCoroutine(SetOrbitalVelocity());
        }
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
            double massA = doubleRigidbody.attachedRigidbody.mass;
            double massB = orbitTarget.doubleRigidbody.attachedRigidbody.mass;
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

            doubleRigidbody.velocity = vA;
            orbitTarget.doubleRigidbody.velocity = vB;
            yield break;
        }

        // Treat A as secondary body, B as primary

        Vector3d toCenter = posB - posA;
        Vector3d perpendicular = Vector3d.Cross(toCenter, orbitTarget.transform.up.ToVector3d()).normalized;

        g = orbitTarget.CalculateGravityAcceleration(scaledTransform.realPosition);
        distance = toCenter.magnitude;

        Vector3d orbitVelocity = Math.Sqrt(distance * g) * perpendicular;

        // Inherit target's velocity to handle nested orbits
        doubleRigidbody.velocity = orbitTarget.doubleRigidbody.velocity + orbitVelocity;
    }

    private void FixedUpdate()
    {
        if (generator == null || pauseUpdates)
            return;
        if (scaledTransform.visible)
        {
            if (!generator.generated && generationSettings.autoGenerate)
            {
                if (!generator.initialized && generationSettings.random)
                    generator.GenerateRandomCelestialBody();
                else
                    generator.GenerateCelestialBody();
                scaledTransform.ResetVisualComponents(originalLayer);
                scaledTransform.UpdateVisualComponents();
            }

            if (!generationSettings.simple)
            {
                if (generator.UpdateQuadTrees(Camera.main))
                {
                    scaledTransform.ResetVisualComponents(originalLayer);
                    scaledTransform.UpdateVisualComponents();
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

    public void ApplyGravity(DoubleRigidbody other)
    {
        if (!gravity || other == doubleRigidbody || (gravitySettings.affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        Vector3d gravityDirection = (scaledTransform.realPosition - other.scaledTransform.realPosition).normalized;
        double acceleration = CalculateGravityAcceleration(other.scaledTransform.realPosition);

        other.AddGravity(gravityDirection * acceleration);
    }

    /* surfaceGravity = bodyMass / radius^2
     * bodyMass = surfaceGravity * radius^2
     * shipMass * g = (bodyMass * shipMass) / distance^2
     * g = (surfaceGravity * radius^2) / distance^2
     */
    public double CalculateGravityAcceleration(Vector3d point)
    {
        return surfaceGravity * scale.x * scale.x / (scaledTransform.realPosition - point).sqrMagnitude;
    }

    private void OnDestroy()
    {
        ScaledSpacePhysics.Instance.PrePhysicsStep -= ApplyGravity;
    }
}
