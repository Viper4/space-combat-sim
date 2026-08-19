using UnityEngine;
using UnityEngine.Events;

public class StatSystem : MonoBehaviour
{
    public bool immune = false;

    public float health = 100;
    public float maxHealth = 100;

    [SerializeField] private Animation damageAnimation;
    public SliderIndicator healthIndicator;

    [SerializeField] private ParticleSystem damageParticles;

    public UnityEvent<float> OnHealthChanged;
    public UnityEvent<float> OnDeath;

    // Prevent lag from excessive event calls or excessive slider value changes, and allow for smooth animation
    private float lastDamageTime = 0;
    [SerializeField] private float damageCooldown = 0.5f;

    private void Start()
    {
        if (healthIndicator != null)
            healthIndicator.UpdateUI(health, maxHealth);
    }

    public void Damage(float amount)
    {
        if (immune || health <= 0)
            return;
        health -= amount;

        if (health <= 0)
        {
            OnDeath?.Invoke(health);
            health = 0;
            if (healthIndicator != null)
                healthIndicator.UpdateUI(health, maxHealth);
        }

        if (Time.time - lastDamageTime < damageCooldown)
            return;
        damageCooldown = Time.time;

        if (damageAnimation != null)
        {
            damageAnimation.Play();
        }
        if (healthIndicator != null)
            healthIndicator.UpdateUI(health, maxHealth);
        OnHealthChanged?.Invoke(-amount);
    }

    public void Heal(float amount)
    {
        health += amount;
        health = Mathf.Clamp(health, 0, maxHealth);
        healthIndicator.UpdateUI(health, maxHealth);
        OnHealthChanged?.Invoke(amount);
    }

    public void DestroyTarget(GameObject GO)
    {
        Destroy(GO);
    }
}
