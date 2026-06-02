using SpaceStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(ScaledTransform)), RequireComponent(typeof(DoubleRigidbody))]
public class CelestialBody : MonoBehaviour
{
    private const float v = 4 / 3 * Mathf.PI;

    private CelestialBodyGenerator generator;
    private DoubleRigidbody doubleRigidbody;
    [SerializeField] private int originalLayer;

    [HideInInspector] public bool generationSettingsFoldout;
    public GenerationSettings generationSettings;

    public bool gravity = true;
    [HideInInspector] public bool gravitySettingsFoldout;
    [ConditionalHide("gravity")] public GravitySettings gravitySettings;
    private Vector3 scale = Vector3.zero;
    private float surfaceGravity;
    public CelestialBody systemCenter;
    [HideInInspector] public ScaledTransform scaledTransform;

    private bool initialized;

    private void Start()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        doubleRigidbody = GetComponent<DoubleRigidbody>();
        if (TryGetComponent(out generator))
        {
            if(!generationSettings.random)
                scale = Vector3.one;

            if (generationSettings.autoGenerate)
            {
                if (generationSettings.random)
                    generator.GenerateRandomCelestialBody();
                else
                    generator.GenerateCelestialBody();
                scaledTransform.ResetVisualComponents(originalLayer);
            }
        }
        else
        {
            if (!generationSettings.random)
                scale = transform.localScale;
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
            scale = new Vector3(randomX, randomY, randomZ);
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
                float tX = Mathf.InverseLerp(generationSettings.scaleRange[0].x, generationSettings.scaleRange[1].x, scale.x);
                float tY = tX;
                float tZ = tX;
                if (!generationSettings.sphere)
                {
                    tY = Mathf.InverseLerp(generationSettings.scaleRange[0].y, generationSettings.scaleRange[1].y, scale.y);
                    tZ = Mathf.InverseLerp(generationSettings.scaleRange[0].z, generationSettings.scaleRange[1].z, scale.z);
                }
                surfaceGravity = Mathf.Lerp(gravitySettings.surfaceGravityRange.x, gravitySettings.surfaceGravityRange.y, (tX + tY + tZ) / 3);
            }
        }

        scaledTransform.realScale = scale.ToVector3d();

        if(generationSettings.calculateMass)
            doubleRigidbody.attachedRigidbody.mass = generationSettings.density * v * scale.x * scale.x * scale.x;
        if(systemCenter != null)
        {
            StartCoroutine(SetOrbitalVelocity());
        }
        Vector3 min = generationSettings.initialAngularVelocityRange[0];
        Vector3 max = generationSettings.initialAngularVelocityRange[1];

        doubleRigidbody.angularVelocity = new Vector3d(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z));
        if (TryGetComponent<SpaceLight>(out var spaceLight))
        {
            spaceLight.Init(scale.x);
        }

        initialized = true;
    }

    public bool Initialized()
    {
        return initialized;
    }

    /* shipMass * g = (shipMass * v^2) / distance
     * v = sqrt(distance * g)
     */
    IEnumerator SetOrbitalVelocity()
    {
        yield return new WaitUntil(() => systemCenter.Initialized());

        Vector3d toCenter = systemCenter.scaledTransform.realPosition - scaledTransform.realPosition;
        Vector3d perpendicular = Vector3d.Cross(toCenter, systemCenter.transform.up.ToVector3d()).normalized;
        double g = systemCenter.CalculateGravityAcceleration(scaledTransform.realPosition);
        double distance = Vector3d.Distance(scaledTransform.realPosition, systemCenter.scaledTransform.realPosition);
        Vector3d orbitVelocity = Math.Sqrt(distance * g) * perpendicular;
        doubleRigidbody.velocity = orbitVelocity;
    }

    private void FixedUpdate()
    {
        if (!generationSettings.simple)
        {
            if (generator.UpdateQuadTrees(Camera.main))
            {
                scaledTransform.ResetVisualComponents(originalLayer);
            }
        }
    }

    public void ApplyGravity(DoubleRigidbody other)
    {
        if (other == doubleRigidbody || !gravity)
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
