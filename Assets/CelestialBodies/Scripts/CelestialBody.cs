using SpaceStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(ScaledTransform))]
public class CelestialBody : MonoBehaviour
{
    private const float v = 4 / 3 * Mathf.PI;

    private CelestialBodyGenerator generator;
    private Rigidbody _rigidbody;
    private DoubleRigidbody doubleRigidbody;

    [HideInInspector] public bool generationSettingsFoldout;
    public GenerationSettings generationSettings;

    public bool gravity = true;
    [HideInInspector] public bool gravitySettingsFoldout;
    [ConditionalHide("gravity")] public GravitySettings gravitySettings;
    private Vector3 scale = Vector3.zero;
    private float surfaceGravity;
    public CelestialBody systemCenter;
    [HideInInspector] public ScaledTransform scaledTransform;

    private List<DoubleRigidbody> thingsInGravity = new List<DoubleRigidbody>();

    private void Start()
    {
        scaledTransform = GetComponent<ScaledTransform>();
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
                scaledTransform.ResetVisualComponents(true);
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

        if (scaledTransform.inScaledSpace)
            scaledTransform.realScale = scale.ToVector3d();
        else
            transform.localScale = scale;

        if (TryGetComponent(out _rigidbody))
        {
            doubleRigidbody = GetComponent<DoubleRigidbody>();
            if(generationSettings.calculateMass)
                _rigidbody.mass = generationSettings.density * v * scale.x * scale.x * scale.x;
            if(systemCenter != null)
            {
                StartCoroutine(SetOrbitalVelocity());
            }
            Vector3 min = generationSettings.initialAngularVelocityRange[0];
            Vector3 max = generationSettings.initialAngularVelocityRange[1];

            _rigidbody.angularVelocity = new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), Random.Range(min.z, max.z));
        }
        if (TryGetComponent<SpaceLight>(out var spaceLight))
        {
            spaceLight.Init(scale.x);
        }
    }

    public bool Initialized()
    {
        return scale != Vector3.zero;
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

        if (doubleRigidbody != null && doubleRigidbody.active)
        {
            doubleRigidbody.velocity = orbitVelocity;
        }
        else
        {
            _rigidbody.linearVelocity = orbitVelocity.ToVector3();
        }
    }

    private void FixedUpdate()
    {
        if (gravity || !generationSettings.simple)
        {
            for (int i = 0; i < thingsInGravity.Count; i++)
            {
                if (thingsInGravity[i] == doubleRigidbody)
                    continue;

                Transform otherTransform = thingsInGravity[i].transform;
                if (!scaledTransform.inScaledSpace && !generationSettings.simple && otherTransform == FloatingWorldOrigin.Instance.transform)
                {
                    generator.UpdateQuadTrees(Camera.main);
                    scaledTransform.ResetVisualComponents(true);
                }
                if (gravity)
                {
                    ScaledTransform otherScaledTransform = thingsInGravity[i].GetComponent<ScaledTransform>();
                    Vector3d gravityDirection = (scaledTransform.realPosition - otherScaledTransform.realPosition).normalized;
                    double acceleration = CalculateGravityAcceleration(otherScaledTransform.realPosition);
                    //if (otherScaledTransform.transform == FloatingWorldOrigin.Instance.transform)
                    //    Debug.Log($"{name} gravity: {acceleration}");

                    thingsInGravity[i].AddForce(gravityDirection * acceleration, ForceMode.Acceleration);
                }
            }
        }
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

    private void OnTriggerEnter(Collider other)
    {
        if (!gravity || (gravitySettings.affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            return;
        Rigidbody otherRigidbody = other.attachedRigidbody;
        if (otherRigidbody != null && otherRigidbody.TryGetComponent(out DoubleRigidbody otherDoubleRigidbody) && !thingsInGravity.Contains(otherDoubleRigidbody))
        {
            thingsInGravity.Add(otherDoubleRigidbody);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!gravity || (gravitySettings.affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            return;
        Rigidbody otherRigidbody = other.attachedRigidbody;
        if (otherRigidbody != null && otherRigidbody.TryGetComponent(out DoubleRigidbody otherDoubleRigidbody))
        {
            thingsInGravity.Remove(otherDoubleRigidbody);
        }
    }
}
