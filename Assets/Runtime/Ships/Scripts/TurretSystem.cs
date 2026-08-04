using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Ship), typeof(TargetingSystem))]
public class TurretSystem : MonoBehaviour
{
    private Ship ship;
    private TargetingSystem targetingSystem;

    [SerializeField] private Radar radar;
    
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

    public Action StartTargetSearch;
    public Action<RadarTarget, bool> CheckTarget;

    private bool triggerHeld;

    public bool initialized {get; private set;}

    private IEnumerator Start()
    {
        yield return new WaitWhile(() => HUDSystem.Instance == null);
        Init();
    }

    /// <summary>
    /// Should only be called for Owner or in Offline mode.
    /// </summary>
    private void Init()
    {
        if (initialized)
            return;

        ship = GetComponent<Ship>();
        targetingSystem = GetComponent<TargetingSystem>();
        for (int i = 0; i < offensiveTags.Length; i++)
        {
            offensiveTagsSet.Add(offensiveTags[i]);
        }
        for (int i = 0; i < defensiveTags.Length; i++)
        {
            defensiveTagsSet.Add(defensiveTags[i]);
        }
        Collider[] shipColliders = ship.scaledRigidbody.scaledTransform.CloneTrackedColliders();

        turrets = new Turret[turretPoints.Length];
        turretCrosshairs = new Transform[turretPoints.Length];
        turretCrosshairImages = new Image[turretPoints.Length];
        for (int i = 0; i < turretPoints.Length; i++)
        {
            turrets[i] = turretPoints[i].GetChild(0).GetComponent<Turret>();
            turrets[i].SetFireOffset(i, turrets.Length);
            for(int j = 0; j < shipColliders.Length; j++)
            {
                turrets[i].AddIgnoredCollider(shipColliders[j]);
            }
            GameObject newCrosshair = Instantiate(turretCrosshairPrefab, HUDSystem.Instance.combatPanel);
            turretCrosshairs[i] = newCrosshair.transform;
            turretCrosshairImages[i] = newCrosshair.GetComponent<Image>();
        }
        
        targetingSystem.SetCrosshairActive(!manualControl);
        manualControlCrosshair.gameObject.SetActive(manualControl);
        GameManager.Instance.inputActions.Player.Primary.performed += StartTrigger;
        GameManager.Instance.inputActions.Player.Primary.canceled += StopTrigger;

        initialized = true;
    }

    private void OnDestroy()
    {
        if (!initialized)
            return;
        GameManager.Instance.inputActions.Player.Primary.performed -= StartTrigger;
        GameManager.Instance.inputActions.Player.Primary.canceled -= StopTrigger;
    }

    private void FixedUpdate()
    {
        if (!initialized || !ship.isStarted)
            return;
        if (manualControl)
        {
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
        else if (radar != null && radar.IsEnabled)
        {
            StartTargetSearch?.Invoke();
            foreach (RadarTarget radarTarget in radar.GetAllDetectedTargets())
            {
                if (radarTarget.team == ship.attachedRadarTarget.team)
                    continue;
                double sqrDistance = (radarTarget.scaledRigidbody.scaledTransform.realPosition - ship.scaledRigidbody.scaledTransform.realPosition).sqrMagnitude;
                double killRadius = radar.GetKillRadius(radarTarget.tag, 500.0);
                bool inKillRadius = radar.IsKillOn(radarTarget.tag) && sqrDistance < killRadius * killRadius;
                CheckTarget?.Invoke(radarTarget, inKillRadius);
            }

            if (HUDSystem.Instance.combatHudActive)
            {
                for (int i = 0; i < turrets.Length; i++)
                {
                    UpdateTurretOnHUD(i);
                }
            }
        }
        UpdateTotalAmmoIndicator();
    }

    private void StartTrigger(InputAction.CallbackContext context)
    {
        if (GameManager.Instance.IsPaused || !ship.isStarted)
            return;
        triggerHeld = true;
        if (!manualControl)
            return;
        for (int i = 0; i < turrets.Length; i++)
        {
            turrets[i].SetShoot(triggerHeld);
        }
    }

    private void StopTrigger(InputAction.CallbackContext context)
    {
        if (GameManager.Instance.IsPaused)
            return;
        triggerHeld = false;
        if (!manualControl)
            return;
        for (int i = 0; i < turrets.Length; i++)
        {
            turrets[i].SetShoot(triggerHeld);
        }
    }

    private void UpdateTurretOnHUD(int i)
    {
        Turret turret = turrets[i];
        if (!turret.active)
        {
            if (turretCrosshairImages[i].gameObject.activeSelf)
                turretCrosshairImages[i].gameObject.SetActive(false);
            return;
        }
        if (!turretCrosshairImages[i].gameObject.activeSelf)
            turretCrosshairImages[i].gameObject.SetActive(true);
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
        
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(HUDSystem.Instance.combatPanel, screenHit, Camera.main, out Vector3 turretCrosshairPos))
        {
            turretCrosshairs[i].position = turretCrosshairPos;
        }
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
        if (GameManager.Instance.IsPaused)
            return;
        manualControl = state == 1;
        
        targetingSystem.SetCrosshairActive(!manualControl);
        manualControlCrosshair.gameObject.SetActive(manualControl);
        if (!manualControl)
        {
            for(int i = 0; i < turrets.Length; i++)
            {
                turrets[i].currentTarget = null;
                turrets[i].SetShoot(false);
            }
        }
        else
        {
            for(int i = 0; i < turrets.Length; i++)
            {
                turrets[i].SetShoot(triggerHeld);
            }
        }
    }

    private void UpdateTotalAmmoIndicator()
    {
        int totalAmmo = 0;
        int totalMaxAmmo = 0;
        for (int i = 0; i < turrets.Length; i++)
        {
            totalAmmo += turrets[i].GetCurrentAmmo();
            totalMaxAmmo += turrets[i].GetMaxAmmo();
        }

        ammoIndicator.UpdateUI(totalAmmo, totalMaxAmmo);
    }
}
