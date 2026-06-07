using System;
using SpaceStuff;
using UnityEngine;
using Random = UnityEngine.Random;

public class TorpedoTuner : MonoBehaviour
{
    [SerializeField] private Vector3 minPosition;
    [SerializeField] private Vector3 maxPosition;
    [SerializeField] private GameObject torpedoPrefab;
    [SerializeField] private RadarTarget target;
    [SerializeField] private float targetRandomPosRadius = 500f;
    [SerializeField] private float timeScale = 2f;

    [SerializeField] private int maxGenerations = 100;
    [SerializeField] private int populationSize = 50;
    [SerializeField] private float maxTime = 120f;
    [SerializeField] private float directionWeight = 50f;
    [SerializeField] private float timeWeight = 0.5f;
    [SerializeField] private float hitReward = 100f;
    private TuningTorpedo[] torpedoes;
    private float[] rewards;
    private float[] latDamps;
    private float[] tps;
    private float[] tis;
    private float[] tds;
    private float startTime;
    private int numAlive = 0;
    [SerializeField] private float minOffset = -1.0f;
    [SerializeField] private float maxOffset = 1.0f;
    private int generation;
    [SerializeField] private float bestLatDamp = 4.0f;
    [SerializeField] private float bestP = 1.0f;
    [SerializeField] private float bestI = 0.0f;
    [SerializeField] private float bestD = 0.0f;
    private Vector3d targetAcceleration;

    private void Start()
    {
        Time.timeScale = timeScale;
        torpedoes = new TuningTorpedo[populationSize];
        rewards = new float[populationSize];
        latDamps = new float[populationSize];
        tps = new float[populationSize];
        tis = new float[populationSize];
        tds = new float[populationSize];
        Spawn();
    }

    private void FixedUpdate()
    {
        if (numAlive > 0)
        {
            target.doubleRigidbody.AddForce(targetAcceleration, ForceMode.Acceleration);
            for (int i = 0; i < torpedoes.Length; i++)
            {
                if (torpedoes[i] == null)
                    continue;
                // dot of 1 means parallel with desiredForward, 0 is perpendicular, -1 is opposite
                rewards[i] += Vector3.Dot(torpedoes[i].transform.forward, torpedoes[i].desiredForward) * directionWeight;
            }

            if (Time.time - startTime > maxTime)
            {
                numAlive = 0;
                for (int i = 0; i < torpedoes.Length; i++)
                {
                    if (torpedoes[i] == null)
                        continue;
                    rewards[i] -= (Time.time - startTime) * timeWeight;
                    Destroy(torpedoes[i].gameObject);
                    torpedoes[i] = null;
                }
                GenerationEnd();
            }
        }
    }

    private void Spawn()
    {
        Debug.Log("Spawning torpedoes");
        Vector3 randomPos = new Vector3(
            Random.Range(minPosition.x, maxPosition.x),
            Random.Range(minPosition.y, maxPosition.y),
            Random.Range(minPosition.z, maxPosition.z)
        );
        Quaternion randomRotation = Random.rotation;
        Vector3d randomAngularVelocity = Random.insideUnitSphere.ToVector3d() * 25;
        Vector3d randomVelocity = Random.insideUnitSphere.ToVector3d() * 50;
        targetAcceleration = Random.insideUnitSphere.ToVector3d() * 5;
        target.doubleRigidbody.scaledTransform.realPosition = (Random.insideUnitSphere * targetRandomPosRadius).ToVector3d();
        target.doubleRigidbody.velocity = Random.insideUnitSphere.ToVector3d() * 50;
        // Keep one torpedo with previous best settings
        TuningTorpedo firstTorpedo = Instantiate(torpedoPrefab, randomPos, randomRotation).GetComponent<TuningTorpedo>();
        torpedoes[0] = firstTorpedo;
        firstTorpedo.GetComponent<ScaledTransform>().realPosition = randomPos.ToVector3d();
        firstTorpedo.GetComponent<DoubleRigidbody>().angularVelocity = randomAngularVelocity;
        firstTorpedo.GetComponent<DoubleRigidbody>().velocity = randomVelocity;
        firstTorpedo.lateralDampGain = bestLatDamp;
        firstTorpedo.proportionalGain = bestP;
        firstTorpedo.integralGain = bestI;
        firstTorpedo.derivativeGain = bestD;
        latDamps[0] = firstTorpedo.lateralDampGain;
        tps[0] = firstTorpedo.proportionalGain;
        tis[0] = firstTorpedo.integralGain;
        tds[0] = firstTorpedo.derivativeGain;
        firstTorpedo.index = 0;
        firstTorpedo.OnDetonate += TorpedoDetonate;
        firstTorpedo.Activate(target, 0f);

        for (int i = 1; i < populationSize; i++)
        {
            TuningTorpedo torpedo = Instantiate(torpedoPrefab, randomPos, randomRotation).GetComponent<TuningTorpedo>();
            torpedoes[i] = torpedo;
            torpedo.GetComponent<ScaledTransform>().realPosition = randomPos.ToVector3d();
            torpedo.GetComponent<DoubleRigidbody>().angularVelocity = randomAngularVelocity;
            torpedo.GetComponent<DoubleRigidbody>().velocity = randomVelocity;
            torpedo.lateralDampGain = Mathf.Max(0, bestLatDamp + Random.Range(minOffset, maxOffset));
            torpedo.proportionalGain = Mathf.Max(0, bestP + Random.Range(minOffset, maxOffset));
            torpedo.integralGain = Mathf.Max(0, bestI + Random.Range(minOffset, maxOffset));
            torpedo.derivativeGain = Mathf.Max(0, bestD + Random.Range(minOffset, maxOffset));
            latDamps[i] = torpedo.lateralDampGain;
            tps[i] = torpedo.proportionalGain;
            tis[i] = torpedo.integralGain;
            tds[i] = torpedo.derivativeGain;
            torpedo.index = i;
            torpedo.OnDetonate += TorpedoDetonate;
            torpedo.Activate(target, 0f);
        }
        numAlive = populationSize;
        startTime = Time.time;
    }

    private void GenerationEnd()
    {
        float bestReward = float.MinValue;
        for (int i = 0; i < populationSize; i++)
        {
            if (rewards[i] > bestReward)
            {
                bestReward = rewards[i];
                bestLatDamp = latDamps[i];
                bestP = tps[i];
                bestI = tis[i];
                bestD = tds[i];
            }
            if (torpedoes[i] != null)
                Destroy(torpedoes[i].gameObject);
            rewards[i] = 0f;
        }
        Debug.Log($"Generation ended. Best reward: {bestReward}. latDamp: {bestLatDamp}, PID: {bestP}, {bestI}, {bestD}");
        generation++;
        if (generation < maxGenerations)
        {
            Spawn();
        }
        else
        {
            Debug.Log("Generation end");
        }
    }

    private void TorpedoDetonate(int index)
    {
        rewards[index] += hitReward;
        rewards[index] -= (Time.time - startTime) * timeWeight;
        torpedoes[index] = null;
        numAlive--;
        if (numAlive <= 0)
        {
            GenerationEnd();
        }
    }
}
