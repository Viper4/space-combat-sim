using SpaceStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Ship)), RequireComponent(typeof(TargetingSystem))]
public class TurretSystem : MonoBehaviour
{
    [HideInInspector] public Ship ship;
    [SerializeField] private HUDSystem _HUDSystem;
    private TargetingSystem targetingSystem;
    
    [SerializeField] private int maxAmmo = 10000;
    public int currentAmmo {get; private set;}
    [SerializeField] SliderIndicator ammoIndicator;
    [SerializeField] private Transform[] turretPoints;
    [HideInInspector] public Turret[] turrets;
    [SerializeField] private GameObject turretCrosshairPrefab;
    private Transform[] turretCrosshairs;
    private Image[] turretCrosshairImages;

    public bool manualControl = false;

    [SerializeField] private Image manualControlCrosshair;

    [SerializeField] private Color crosshairNormalColor;
    [SerializeField] private Color crosshairHoverColor;
    [SerializeField] private Color crosshairTriggerColor;

    [SerializeField] private Sprite turretNormalSprite;
    [SerializeField] private Sprite turretBlockedSprite;
    [SerializeField] private Color turretNormalColor;
    [SerializeField] private Color turretHoverColor;
    [SerializeField] private Color turretBlockedColor;

    [SerializeField, Tooltip("Tags of RadarTargets to use offensive strategy when targeting")] private string[] offensiveTags;
    private HashSet<string> offensiveTagsSet = new HashSet<string>();
    [SerializeField, Tooltip("tags of RadarTargets to use defensive strategy when targeting")] private string[] defensiveTags;
    private HashSet<string> defensiveTagsSet = new HashSet<string>();
    public float detectRadius = 10000f;
    [SerializeField] private float killRadius = 500f;

    public HashSet<uint> targetsInRange = new HashSet<uint>();

    public Action StartTargetSearch;
    public Action<RadarTarget, bool> CheckTarget;

    private bool triggerHeld;

    private void Start()
    {
        ship = GetComponent<Ship>();
        targetingSystem = GetComponent<TargetingSystem>();
        currentAmmo = maxAmmo;
        ammoIndicator.UpdateUI(currentAmmo, maxAmmo);
        for (int i = 0; i < offensiveTags.Length; i++)
        {
            offensiveTagsSet.Add(offensiveTags[i]);
        }
        for (int i = 0; i < defensiveTags.Length; i++)
        {
            defensiveTagsSet.Add(defensiveTags[i]);
        }
        Collider[] shipColliders = ship.doubleRigidbody.scaledTransform.GetTrackedColliders();

        turrets = new Turret[turretPoints.Length];
        turretCrosshairs = new Transform[turretPoints.Length];
        turretCrosshairImages = new Image[turretPoints.Length];
        for (int i = 0; i < turretPoints.Length; i++)
        {
            turrets[i] = turretPoints[i].GetChild(0).GetComponent<Turret>();
            turrets[i].SetFireTime((float)(i+1) / turretPoints.Length);
            for(int j = 0; j < shipColliders.Length; j++)
            {
                turrets[i].AddIgnoredCollider(shipColliders[j]);
            }
            GameObject newCrosshair = Instantiate(turretCrosshairPrefab, _HUDSystem.combatPanel);
            turretCrosshairs[i] = newCrosshair.transform;
            turretCrosshairImages[i] = newCrosshair.GetComponent<Image>();
        }
        
        targetingSystem.SetCrosshairActive(!manualControl);
        manualControlCrosshair.gameObject.SetActive(manualControl);
        ship.doubleRigidbody.OnScaledTriggerEnter += CheckTargetOnEnter;
        GameManager.Instance.inputActions.Player.Primary.performed += StartTrigger;
        GameManager.Instance.inputActions.Player.Primary.canceled += StopTrigger;
    }

    private void FixedUpdate()
    {
        if (manualControl)
        {
            if (!_HUDSystem.combatHudActive)
            {
                return;
            }

            if (triggerHeld)
            {
                manualControlCrosshair.color = crosshairTriggerColor;
            }
            else if (targetingSystem.CrosshairIsHovering())
            {
                manualControlCrosshair.color = crosshairHoverColor;
            }
            else
            {
                manualControlCrosshair.color = crosshairNormalColor;
            }
            
            for (int i = 0; i < turrets.Length; i++)
            {
                Turret turret = turrets[i];
                turret.currentTarget = targetingSystem.lockedTarget;
                if (targetingSystem.lockedTarget == null)
                    turret.aimDirection = targetingSystem.GetAimPoint() - turret.firePoint.position;
                UpdateTurretOnHUD(i);
            }
        }
        else
        {
            StartTargetSearch?.Invoke();
            foreach (uint id in targetsInRange.ToList())
            {
                if (!RadarRegistry.TryGet(id, out var radarTarget))
                {
                    targetsInRange.Remove(id);
                    continue;
                }

                double sqrDistance = (radarTarget.doubleRigidbody.scaledTransform.realPosition - ship.doubleRigidbody.scaledTransform.realPosition).sqrMagnitude;

                if (sqrDistance > detectRadius * detectRadius)
                    continue;

                if (radarTarget.stealthDistance != -1 && sqrDistance > radarTarget.stealthDistance * radarTarget.stealthDistance)
                    continue;

                bool inKillRadius = sqrDistance < killRadius * killRadius;
                CheckTarget?.Invoke(radarTarget, inKillRadius);
            }

            if (_HUDSystem.combatHudActive)
            {
                for (int i = 0; i < turrets.Length; i++)
                {
                    UpdateTurretOnHUD(i);
                }
            }
            else
            {
                for (int i = 0; i < turrets.Length; i++)
                {
                    turrets[i].GetRaycastHit(out RaycastHit _); // Need to update turret's obstructed bool
                }
            }
        }
    }

    private void StartTrigger(InputAction.CallbackContext context)
    {
        if (!manualControl)
            return;
        triggerHeld = true;
        for (int i = 0; i < turrets.Length; i++)
        {
            turrets[i].shoot = triggerHeld;
        }
    }

    private void StopTrigger(InputAction.CallbackContext context)
    {
        if (!manualControl)
            return;
        triggerHeld = false;
        for (int i = 0; i < turrets.Length; i++)
        {
            turrets[i].shoot = triggerHeld;
        }
    }

    private void UpdateTurretOnHUD(int i)
    {
        Turret turret = turrets[i];
        Vector3 screenHit;
        bool gotHit = turret.GetRaycastHit(out RaycastHit turretHit);
        if (gotHit)
        {
            if (turretHit.transform != ship.transform)
            {
                turretCrosshairImages[i].sprite = turretNormalSprite;
                turretCrosshairImages[i].color = turretHoverColor;
                screenHit = Camera.main.WorldToScreenPoint(turretHit.point);
                
                if (turret.shoot)
                    turretCrosshairImages[i].color = crosshairTriggerColor;
            }
            else
            {
                turretCrosshairImages[i].sprite = turretBlockedSprite;
                turretCrosshairImages[i].color = turretBlockedColor;
                screenHit = Camera.main.WorldToScreenPoint(turret.firePoint.position + turret.firePoint.forward * 50f);
            }
        }
        else
        {
            turretCrosshairImages[i].sprite = turretNormalSprite;
            turretCrosshairImages[i].color = turretNormalColor;
            screenHit = Camera.main.WorldToScreenPoint(turret.firePoint.position + turret.firePoint.forward * 1000f);
            if (screenHit.z <= 0)
            {
                turretCrosshairImages[i].color = Color.clear;
                return;
            }
            if (turret.shoot)
                turretCrosshairImages[i].color = crosshairTriggerColor;
        }
        
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_HUDSystem.combatPanel, screenHit, Camera.main, out Vector3 turretCrosshairPos))
        {
            turretCrosshairs[i].position = turretCrosshairPos;
        }
    }

    private void CheckTargetOnEnter(Component other)
    {
        if (!other.TryGetComponent<RadarTarget>(out var target))
            return;

        if (target.team == ship.team)
            return;

        targetsInRange.Add(target.GetID());
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckTargetOnEnter(other);
    }

    private void OnDestroy()
    {
        ship.doubleRigidbody.OnScaledTriggerEnter -= CheckTargetOnEnter;
        GameManager.Instance.inputActions.Player.Primary.performed -= StartTrigger;
        GameManager.Instance.inputActions.Player.Primary.canceled -= StopTrigger;
    }

    public bool IsOffensive(RadarTarget target)
    {
        return offensiveTagsSet.Contains(target.tag);
    }

    public bool IsDefensive(RadarTarget target)
    {
        return defensiveTagsSet.Contains(target.tag);
    }

    public void ToggleManualControl(int state)
    {
        manualControl = state == 1;
        
        targetingSystem.SetCrosshairActive(!manualControl);
        manualControlCrosshair.gameObject.SetActive(manualControl);
        if (!manualControl)
        {
            for(int i = 0; i < turrets.Length; i++)
            {
                turrets[i].currentTarget = null;
                turrets[i].shoot = false;
            }
        }
        else
        {
            for(int i = 0; i < turrets.Length; i++)
            {
                turrets[i].shoot = false;
            }
        }
    }

    public void UpdateHudForTarget(RadarTarget target)
    {
        if (!_HUDSystem.radarHudActive)
            return;
        if (_HUDSystem.TryGetValue(target.GetID(), out HUDObject hudObject))
        {
            hudObject.SetTargetText(target.turretsTargeting);
        }
    }

    public void OnTurretFire()
    {
        if (currentAmmo <= 0)
            return;
        currentAmmo--;
        ammoIndicator.UpdateUI(currentAmmo, maxAmmo);
    }

    public void RefillAmmo(int amount)
    {
        currentAmmo = Mathf.Min(maxAmmo, amount);
        ammoIndicator.UpdateUI(currentAmmo, maxAmmo);
    }
}
