using System;
using GameKit.Dependencies.Utilities;
using SpaceStuff;
using UnityEngine;
using Random = UnityEngine.Random;

public class TuningTorpedoTuner : MonoBehaviour
{
    [Header("Scene References")]

    [SerializeField]
    private GameObject torpedoPrefab;

    [SerializeField]
    private RadarTarget target;

    [SerializeField]
    private int spectateIndex = 0;

    [SerializeField]
    private Transform spectateDesiredMarker;

    [SerializeField]
    Transform spectateForwardMarker;

    [Header("TuningTorpedo Spawn Area")]

    [SerializeField]
    private float minDistance;

    [SerializeField]
    private float maxDistance;

    [Header("Target Scenario")]

    [SerializeField]
    private float targetMaxSpeed = 50f;

    [SerializeField]
    private float targetMaxAcceleration = 5f;

    [Header("TuningTorpedo Initial State")]

    [SerializeField]
    private float torpedoMaxSpeed = 50f;

    [SerializeField]
    private float torpedoMaxAngularSpeed = 25f;

    [SerializeField]
    private bool useInitialGenome = false;

    [SerializeField, ConditionalHide("useInitialGenome")]
    private float initialP = -1f;

    [SerializeField, ConditionalHide("useInitialGenome")]
    private float initialI = -1f;

    [SerializeField, ConditionalHide("useInitialGenome")]
    private float initialD = -1f;

    [SerializeField, ConditionalHide("useInitialGenome")]
    private float initialNC = -1f;

    [Header("Genetic Algorithm")]

    [SerializeField]
    private int populationSize = 50;

    [SerializeField]
    private int maxGenerations = 100;

    [SerializeField]
    private int eliteCount = 2;

    [SerializeField]
    private int tournamentSize = 3;

    [SerializeField, Range(0f, 1f)]
    private float mutationChance = 0.25f;

    [SerializeField]
    private float pMutationStrength = 1f;
    [SerializeField]
    private float iMutationStrength = 1f;
    [SerializeField]
    private float dMutationStrength = 1f;
    [SerializeField]
    private float nCMutationStrength = 1f;

    [Header("Simulation")]

    [SerializeField]
    private float maxTime = 120f;

    [SerializeField]
    private float timeScale = 2f;

    [Header("PID Search Bounds")]

    [SerializeField]
    private Vector2 proportionalRange = new(0f, 100f);

    [SerializeField]
    private Vector2 integralRange = new(0f, 1f);

    [SerializeField]
    private Vector2 derivativeRange = new(0f, 50f);

    [SerializeField]
    private Vector2 navigationConstantRange = new(0.5f, 10f);

    [Header("Best Result")]

    [SerializeField]
    private float bestP;

    [SerializeField]
    private float bestI;

    [SerializeField]
    private float bestD;

    [SerializeField]
    private float bestNavConstant;

    [SerializeField]
    private float bestFitness = float.MinValue;

    private TuningTorpedo[] torpedoes;

    private int generation;
    [SerializeField] private int numAlive;
    private float generationStartTime;

    private Vector3d targetAcceleration;

    // Shared scenario state for every torpedo in a generation.
    private Vector3 sharedSpawnPosition;
    private Quaternion sharedSpawnRotation;
    private Vector3d sharedInitialVelocity;
    private Vector3 sharedInitialAngularVelocity;

    private struct Genome
    {
        public float p;
        public float i;
        public float d;
        public float nC;

        public Genome(float p, float i, float d, float nC)
        {
            this.p = p;
            this.i = i;
            this.d = d;
            this.nC = nC;
        }
    }

    private void Start()
    {
        Time.timeScale = timeScale;

        torpedoes = new TuningTorpedo[populationSize];

        SpawnGeneration();
    }

    private void Update()
    {
        if (GameManager.Instance.inputActions.Player.Primary.WasPressedThisFrame())
        {
            spectateIndex = (spectateIndex + 1) % populationSize;
            bool foundTorpedo = false;
            for (int i = spectateIndex; i < populationSize; i++)
            {
                if (torpedoes[i].gameObject.activeSelf)
                {
                    spectateIndex = i;
                    foundTorpedo = true;
                    break;
                }
            }
            if (!foundTorpedo)
            {
                for (int i = 0; i < spectateIndex; i++)
                {
                    if (torpedoes[i].gameObject.activeSelf)
                    {
                        spectateIndex = i;
                        foundTorpedo = true;
                        break;
                    }
                }
            }
            SetSpectateCamera(foundTorpedo);
        }
    }

    private void FixedUpdate()
    {
        if (numAlive <= 0)
            return;

        UpdateTarget();
        UpdateSpectateMarkers();

        if (Time.time - generationStartTime >= maxTime)
        {
            EndGeneration();
        }
    }

    private void SetSpectateCamera(bool haveTorpedo)
    {
        if (!haveTorpedo)
        {
            spectateIndex = -1;
            Camera.main.transform.position = new Vector3(0, 0, -50f);
            return;
        }
    }

    private void UpdateSpectateMarkers()
    {
        if (spectateIndex < 0)
            return;

        Camera.main.transform.position = torpedoes[spectateIndex].transform.position + new Vector3(0f, 0f, -25f);
        spectateDesiredMarker.position = torpedoes[spectateIndex].transform.position;
        spectateForwardMarker.position = torpedoes[spectateIndex].transform.position + torpedoes[spectateIndex].transform.forward;
        
        spectateDesiredMarker.rotation = Quaternion.LookRotation(torpedoes[spectateIndex].desiredForward);
        spectateForwardMarker.rotation = Quaternion.LookRotation(torpedoes[spectateIndex].transform.forward);
    }

    private void SpawnGeneration()
    {
        ResetScenario();

        /*
         * Generate the torpedo scenario ONCE.
         *
         * Every torpedo in this generation receives these exact same
         * position, rotation, velocity, and angular velocity values.
         */
        float randomDistance = Random.Range(minDistance, maxDistance);
        sharedSpawnPosition = Random.onUnitSphere * randomDistance;
        sharedSpawnRotation = Random.rotation;

        sharedInitialVelocity = Random.insideUnitSphere.ToVector3d() * torpedoMaxSpeed;
        sharedInitialAngularVelocity = Random.insideUnitSphere * torpedoMaxAngularSpeed;

        Genome initialGenome = new Genome(initialP, initialI, initialD, initialNC);
        Debug.Log($"Spawning first generation {generation + 1} {randomDistance}m away from target.");

        for (int i = 0; i < populationSize; i++)
        {
            TuningTorpedo torpedo = Instantiate(torpedoPrefab, sharedSpawnPosition, sharedSpawnRotation).GetComponent<TuningTorpedo>();
            torpedo.name = $"Torpedo {i}";
            torpedoes[i] = torpedo;

            ScaledTransform scaledTransform = torpedo.GetComponent<ScaledTransform>();

            /*
             * All torpedoes start at exactly the same position.
             */
            scaledTransform.realPosition = sharedSpawnPosition.ToVector3d();

            /*
             * All torpedoes start with exactly the same velocity.
             */
            torpedo.scaledRigidbody.velocity = sharedInitialVelocity;

            /*
             * All torpedoes start with exactly the same angular velocity.
             */
            torpedo.scaledRigidbody.angularVelocity = sharedInitialAngularVelocity;

            /*
             * Ignore Unity physics collisions with every torpedo
             * that was spawned before this one.
             */
            IgnoreCollisionsWithPrev(i);
            if (useInitialGenome)
            {
                Genome mutatedGenome = Mutate(initialGenome, true);
                torpedo.SetPIDGains(
                    mutatedGenome.p,
                    mutatedGenome.i,
                    mutatedGenome.d
                );
                torpedo.navigationConstant = mutatedGenome.nC;
            }
            else
            {
                torpedo.SetPIDGains(
                    Random.Range(proportionalRange.x, proportionalRange.y),
                    Random.Range(integralRange.x, integralRange.y),
                    Random.Range(derivativeRange.x, derivativeRange.y)
                );
                torpedo.navigationConstant = Random.Range(navigationConstantRange.x, navigationConstantRange.y);
            }

            torpedo.OnDetonated += OnTuningTorpedoHit;

            torpedo.Activate(target, 0.75f);
        }

        numAlive = populationSize;
        generationStartTime = Time.time;
    }

    private void ResetGeneration()
    {
        ResetScenario();

        /*
         * Generate the torpedo scenario ONCE.
         *
         * Every torpedo in this generation receives these exact same
         * position, rotation, velocity, and angular velocity values.
         */
        float randomDistance = Random.Range(minDistance, maxDistance);
        sharedSpawnPosition = Random.onUnitSphere * randomDistance;
        sharedSpawnRotation = Random.rotation;

        sharedInitialVelocity = Random.insideUnitSphere.ToVector3d() * torpedoMaxSpeed;
        sharedInitialAngularVelocity = Random.insideUnitSphere * torpedoMaxAngularSpeed;

        Debug.Log($"Starting generation {generation + 1} {randomDistance}m away from target.");

        for (int i = 0; i < populationSize; i++)
        {
            TuningTorpedo torpedo = torpedoes[i];
            torpedo.fitness = 0f;

            ScaledTransform scaledTransform = torpedo.GetComponent<ScaledTransform>();

            /*
             * All torpedoes start at exactly the same position.
             */
            scaledTransform.realPosition = sharedSpawnPosition.ToVector3d();

            /*
             * All torpedoes start with exactly the same velocity.
             */
            torpedo.scaledRigidbody.velocity = sharedInitialVelocity;

            /*
             * All torpedoes start with exactly the same angular velocity.
             */
            torpedo.scaledRigidbody.angularVelocity = sharedInitialAngularVelocity;

            torpedo.gameObject.SetActive(true);
            torpedo.Activate(target, 0.75f);
        }

        numAlive = populationSize;
        generationStartTime = Time.time;
    }

    private void IgnoreCollisionsWithPrev(int index)
    {
        TuningTorpedo newTuningTorpedo = torpedoes[index];
        Collider[] newColliders = newTuningTorpedo.GetComponentsInChildren<Collider>();
        ScaledCollider[] newScaledColliders = newTuningTorpedo.GetComponentsInChildren<ScaledCollider>();

        for (int i = 0; i < index; i++)
        {
            TuningTorpedo previousTuningTorpedo = torpedoes[i];
            if (previousTuningTorpedo == null)
                continue;

            ScaledCollider[] previousScaledColliders = previousTuningTorpedo.GetComponentsInChildren<ScaledCollider>();
            foreach(ScaledCollider scaledCollider in newScaledColliders)
            {
                if (scaledCollider == null)
                    continue;
                foreach (ScaledCollider previousCollider in previousScaledColliders)
                {
                    scaledCollider.IgnoreCollider(previousCollider, true);
                }
            }

            Collider[] previousColliders = previousTuningTorpedo.GetComponentsInChildren<Collider>();

            foreach (Collider newCollider in newColliders)
            {
                if (newCollider == null)
                    continue;

                foreach (Collider previousCollider in previousColliders)
                {
                    if (previousCollider == null)
                        continue;

                    Physics.IgnoreCollision(newCollider, previousCollider, true);
                }
            }
        }
    }

    private void ResetScenario()
    {
        Time.timeScale = timeScale;

        target.scaledRigidbody
            .scaledTransform
            .realPosition = Vector3d.zero;

        target.scaledRigidbody.velocity =
            Random.insideUnitSphere.ToVector3d() *
            targetMaxSpeed;

        target.scaledRigidbody.angularVelocity =
            Vector3.zero;

        targetAcceleration =
            Random.insideUnitSphere.ToVector3d() *
            targetMaxAcceleration;
    }

    private void UpdateTarget()
    {
        target.scaledRigidbody.AddForce(
            targetAcceleration,
            ForceMode.Acceleration
        );
    }

    private void EndGeneration()
    {
        EvaluateBestGenome();

        Debug.Log(
            $"Generation {generation + 1} ended.\n" +
            $"Best Fitness: {bestFitness:F2}\n" +
            $"P: {bestP:F4}\n" +
            $"I: {bestI:F4}\n" +
            $"D: {bestD:F4}\n" +
            $"nC: {bestNavConstant:F4}"
        );

        generation++;

        if (generation >= maxGenerations)
        {
            Debug.Log("TuningTorpedo tuning complete.");
            return;
        }

        CreateNextGeneration();
        ResetGeneration();
    }

    private void EvaluateBestGenome()
    {
        int bestIndex = 0;

        for (int i = 1; i < populationSize; i++)
        {
            if (torpedoes[i].fitness > torpedoes[bestIndex].fitness)
            {
                bestIndex = i;
            }
        }

        bestFitness = torpedoes[bestIndex].fitness;

        bestP = torpedoes[bestIndex].proportionalGain;
        bestI = torpedoes[bestIndex].integralGain;
        bestD = torpedoes[bestIndex].derivativeGain;
        bestNavConstant = torpedoes[bestIndex].navigationConstant;
    }

    private void CreateNextGeneration()
    {
        SortPopulationByFitness();

        int actualEliteCount = Mathf.Min(eliteCount, populationSize);

        for (int i = actualEliteCount; i < populationSize; i++)
        {
            int parentA = TournamentSelect();
            int parentB = TournamentSelect();

            Genome child = Mutate(Crossover(parentA, parentB));

            torpedoes[i].SetPIDGains(child.p, child.i, child.d);
            torpedoes[i].navigationConstant = child.nC;
        }
    }

    private void SortPopulationByFitness()
    {
        for (int i = 0; i < populationSize - 1; i++)
        {
            int bestIndex = i;

            for (int j = i + 1; j < populationSize; j++)
            {
                if (torpedoes[j].fitness > torpedoes[bestIndex].fitness)
                {
                    bestIndex = j;
                }
            }

            if (bestIndex == i)
                continue;

            (torpedoes[i], torpedoes[bestIndex]) = (torpedoes[bestIndex], torpedoes[i]);
        }
    }

    private int TournamentSelect()
    {
        int bestIndex = Random.Range(0, populationSize / 2);

        for (int i = 1; i < tournamentSize; i++)
        {
            int candidateIndex = Random.Range(0, populationSize / 2);

            if (torpedoes[candidateIndex].fitness > torpedoes[bestIndex].fitness)
            {
                bestIndex = candidateIndex;
            }
        }

        return bestIndex;
    }

    private Genome Crossover(int parentAindex, int parentBIndex)
    {
        TuningTorpedo parentA = torpedoes[parentAindex];
        TuningTorpedo parentB = torpedoes[parentBIndex];
        return new Genome(
            Random.value < 0.5f ? parentA.proportionalGain : parentB.proportionalGain,
            Random.value < 0.5f ? parentA.integralGain : parentB.integralGain,
            Random.value < 0.5f ? parentA.derivativeGain : parentB.derivativeGain,
            Random.value < 0.5f ? parentA.navigationConstant : parentB.navigationConstant
        );
    }

    private Genome Mutate(Genome genome, bool init = false)
    {
        float chance = init ? 0.9f : mutationChance;
        float multiplier = init ? 1f : 3f;
        if (Random.value < chance)
        {
            genome.p += RandomGaussian() * pMutationStrength * multiplier;
        }

        if (Random.value < chance)
        {
            genome.i += RandomGaussian() * iMutationStrength * multiplier;
        }

        if (Random.value < chance)
        {
            genome.d += RandomGaussian() * dMutationStrength * multiplier;
        }

        if (Random.value < chance)
        {
            genome.nC += RandomGaussian() * nCMutationStrength * multiplier;
        }

        genome.p = Mathf.Max(0f, genome.p);
        genome.i = Mathf.Max(0f, genome.i);
        genome.d = Mathf.Max(0f, genome.d);
        genome.nC = Mathf.Clamp(genome.nC, navigationConstantRange.x, navigationConstantRange.y);

        return genome;
    }

    private float RandomGaussian()
    {
        float u1 = 1f - Random.value;
        float u2 = 1f - Random.value;

        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
    }

    public void OnTuningTorpedoHit(TuningTorpedo torpedo)
    {
        if (torpedo.killOnDetonate)
            numAlive--;
        if (numAlive <= 0)
        {
            EndGeneration();
        }
    }
}