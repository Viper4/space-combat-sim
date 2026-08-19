using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Events;

public class TurretPanel : MonoBehaviour
{
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.yellow;
    [SerializeField] private Color destroyedColor = Color.red;
    [SerializeField] private Image image;

    public TextMeshProUGUI title;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI targetText;
    public SliderIndicator healthIndicator;
    public SliderIndicator ammoIndicator;
    public Transform modelParent;
    
    [HideInInspector] public Transform[] platformModels;
    [HideInInspector] public Transform[] rotatingObjectModels;

    private List<Material> _modelMaterials = new();
    private Turret _turret;
    private UnityAction<float> _updateStateListener;
    private Action<int> _ammoListener;

    public void Initialize(Turret turret)
    {
        _turret = turret;
        _turret.OnActiveChanged += UpdateState;
        _turret.OnWantsToShootChanged += UpdateState;
        void listener(float x) => UpdateState();
        _turret.statSystem.OnDeath.AddListener(listener);
        _updateStateListener = listener;
        _turret.OnTargetChanged += UpdateTarget;
        _turret.turretSystem.OnManualControlChanged += UpdateTarget;

        _turret.statSystem.healthIndicator = healthIndicator;
        healthIndicator.UpdateUI(turret.statSystem.health, turret.statSystem.maxHealth); // Update indicator immediately

        void ammoListener(int x) => ammoIndicator.UpdateUI(x, turret.GetMaxAmmo());
        _turret.OnAmmoChanged += ammoListener;
        _ammoListener = ammoListener;
        listener(turret.GetCurrentAmmo()); // Update indicator immediately
    }

    public void GetMaterials(GameObject[] gameObjects)
    {
        foreach(GameObject GO in gameObjects)
        {
            Renderer[] renderers = GO.GetComponentsInChildren<Renderer>();
            foreach(Renderer renderer in renderers)
            {
                _modelMaterials.AddRange(renderer.materials);
            }
        }
    }

    private void OnDestroy()
    {
        foreach(Material material in _modelMaterials)
        {
            Destroy(material);
        }
        _turret.OnActiveChanged -= UpdateState;
        _turret.OnWantsToShootChanged -= UpdateState;
        _turret.statSystem.OnDeath.RemoveListener(_updateStateListener);
        _turret.OnTargetChanged -= UpdateTarget;
        _turret.turretSystem.OnManualControlChanged -= UpdateTarget;
        _turret.OnAmmoChanged -= _ammoListener;
    }

    private void FixedUpdate()
    {
        foreach(Transform platformModel in platformModels)
        {
            platformModel.rotation = _turret.platform.rotation;
        }
        foreach(Transform rotatingObjectModel in rotatingObjectModels)
        {
            rotatingObjectModel.rotation = _turret.barrel.rotation;
        }
    }

    private void UpdateState()
    {
        Color color;
        if (_turret.statSystem.health <= 0f)
        {
            color = destroyedColor;
            statusText.text = "<color=red>Destroyed</color>";
        }
        else
        {
            if (_turret.GetActive())
            {
                color = activeColor;
                statusText.text = "<color=green>Active</color>";
            }
            else
            {
                color = inactiveColor;
                statusText.text = "<color=yellow>Inactive</color>";
            }
        }

        image.color = color;
        color.a = 0.9f;
        foreach(Material material in _modelMaterials)
        {
            material.color = color;
            material.SetColor("_EmissionColor", color * 2f);
        }
    }

    private void UpdateTarget()
    {
        string targetColor = _turret.WantsToShoot ? "red" : "yellow";
        if (_turret.currentTarget != null)
        {
            targetText.text = $"<color={targetColor}>" + _turret.currentTarget.name + "</color>";
        }
        else if (_turret.turretSystem.manualControl)
        {
            targetText.text = $"<color={targetColor}>MANUAL</color>";
        }
        else
        {
            targetText.text = "<color=grey>None</color>";
        }
    }

    public void OnClick()
    {
        _turret.SetActiveSilent(!_turret.GetActive());
        UpdateState();
    }
}
