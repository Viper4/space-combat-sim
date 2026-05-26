using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;

public class StatSystem : MonoBehaviour
{
    public bool immune = false;

    public float health = 100;
    public float maxHealth = 100;

    [SerializeField] private Animation damageAnimation;
    [SerializeField] private List<Slider> healthBars = new List<Slider>();
    [SerializeField] private List<TextMeshProUGUI> healthTexts = new List<TextMeshProUGUI>();

    [SerializeField] private ParticleSystem damageParticles;

    [SerializeField] private UnityEvent onDamage;
    [SerializeField] private UnityEvent onHeal;
    [SerializeField] private UnityEvent<float> onDeath;

    // Prevent lag from excessive event calls or excessive slider value changes, and allow for smooth animation
    private float lastDamageTime = 0;
    [SerializeField] private float damageCooldown = 0.5f;

    private void UpdateUI()
    {
        if (healthBars.Count > 0)
        {
            for (int i = 0; i < healthBars.Count; i++)
            {
                healthBars[i].value = health / maxHealth;
            }
        }
        if (healthTexts.Count > 0)
        {
            for (int i = 0; i < healthTexts.Count; i++)
            {
                healthTexts[i].text = Math.Round(health / maxHealth * 100, 2).ToString();
            }
        }
    }

    public void Damage(float amount)
    {
        if (immune || health <= 0)
            return;
        health -= amount;

        if (health <= 0)
        {
            onDeath?.Invoke(health);
            health = 0;
            UpdateUI();
        }

        if (Time.time - lastDamageTime < damageCooldown)
            return;
        damageCooldown = Time.time;

        if (damageAnimation != null)
        {
            damageAnimation.Play();
        }
        UpdateUI();
        onDamage?.Invoke();
    }

    public void Heal(float amount)
    {
        health += amount;
        health = Mathf.Clamp(health, 0, maxHealth);
        UpdateUI();
        onHeal?.Invoke();
    }

    public void DestroyTarget(GameObject GO)
    {
        Destroy(GO);
    }

    public void AddHealthBar(Slider slider)
    {
        healthBars.Add(slider);
    }

    public void AddHealthText(TextMeshProUGUI text)
    {
        healthTexts.Add(text);
    }
}
