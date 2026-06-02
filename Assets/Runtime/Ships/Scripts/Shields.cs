using System.Collections;
using UnityEngine;

[RequireComponent(typeof(StatSystem))]
public class Shields : MonoBehaviour
{
    private StatSystem statSystem;
    [SerializeField, Tooltip("The thing this shield is protecting.")] private StatSystem protectedStatSystem;

    private bool active;
    [SerializeField] private GameObject colliderObject;
    private MeshRenderer shieldRenderer;
    private Material shieldMaterial;
    [SerializeField] private float dieTime = 0.5f;
    [SerializeField] private float minAlpha = 0.1f;
    [SerializeField] private float maxAlpha = 1.0f;
    [SerializeField] private float minRadius = 0f;
    [SerializeField] private float maxRadius = 0.5f;
    [SerializeField] private GameObject shieldRippleEffect;
    [SerializeField] private bool damageAfterDeath = false;
    [SerializeField] private float damageDurationScale = 0.01f;
    [SerializeField] private float damageMagnitudeScale = 0.005f;

    // Start is called before the first frame update
    void Start()
    {
        statSystem = GetComponent<StatSystem>();
        shieldRenderer = colliderObject.GetComponent<MeshRenderer>();
        shieldMaterial = shieldRenderer.material; // Clone material
        colliderObject.SetActive(active);
    }

    private void OnDestroy()
    {
        // Cleanup cloned material to avoid memory leaks
        if (shieldMaterial != null)
        {
            Destroy(shieldMaterial);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ToggleShields(int state)
    {
        if (statSystem.health <= 0)
            return;
        
        active = state == 1;
        colliderObject.SetActive(active);
    }

    public void Damage(float amount, Vector3 origin)
    {
        if (active)
        {
            // Shields are up, damage the shield
            ShieldRipple ripple = Instantiate(shieldRippleEffect, colliderObject.transform).GetComponent<ShieldRipple>();
            ripple.Init(origin, amount * damageDurationScale, maxAlpha, minAlpha, maxRadius, minRadius, amount * damageMagnitudeScale);

            statSystem.Damage(amount);
        }
        else
        {
            // Shields are down, damage the protected stat system
            if (protectedStatSystem != null)
                protectedStatSystem.Damage(amount);
        }
    }

    public void OnDeath(float remainingDamage)
    {
        active = false;
        colliderObject.SetActive(false);
        if (damageAfterDeath && protectedStatSystem != null)
            protectedStatSystem.Damage(remainingDamage);
        StartCoroutine(DieAnimation());
    }

    private IEnumerator DieAnimation()
    {
        float timer = 0f;
        while(timer < dieTime)
        {
            timer += Time.deltaTime;
            shieldMaterial.SetFloat("_Alpha", timer / dieTime);
            yield return null;
        }
        shieldRenderer.enabled = false;
    }
}
