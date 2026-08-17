using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using SpaceStuff;
using UnityEngine;
using System.Collections;
using FishNet;

public class HUDSystem : NetworkBehaviour
{
    public static HUDSystem Instance {get; private set;}

    public bool radarHudActive {get; private set;}
    public bool combatHudActive {get; private set;}
    [SerializeField] private TargetingSystem targetingSystem;
    [SerializeField] private GameObject radarHudObjectPrefab;
    [SerializeField] private GameObject radarHudParent;
    [SerializeField] private float radarHUDDistance = 1.5f;
    public RectTransform combatPanel;
    [SerializeField] private float combatHUDDistance = 1.0f;
    [SerializeField, Tooltip("Distance away from camera center HUD objects will show details text.")] private float detailsDistance = 0.05f;
    [SerializeField] private RectTransform targetDirMarker;
    [SerializeField, Tooltip("Radius away from center of screen to show marker.")] private float targetDirMarkerRadius = 1.0f;
    [SerializeField] private float targetDirMarkerDistance = 1.0f;

    private Dictionary<uint, HUDObject> radarIDHUDPair = new Dictionary<uint, HUDObject>();
    private Camera _mainCamera;

    private void Awake()
    {
        if (!InstanceFinder.IsOffline)
            return;

        if (Instance != null)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;
    }

    private Camera GetMainCamera()
    {
        if (_mainCamera != null)
            return _mainCamera;
        _mainCamera = Camera.main;
        return _mainCamera;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner)
        {
            if (Instance != null)
            {
                Destroy(Instance.gameObject);
            }
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private Vector3 CalculateHUDPosition(Vector3d realPosition, string tag)
    {
        Vector3d camRealPos = FloatingWorldOrigin.Instance.GetRealCameraPosition();
        Vector3 direction = (realPosition - camRealPos).normalized.ToVector3();
        float distanceOffset = tag switch
        {
            "Projectile" => -0.05f, // Projectiles highest priority
            "Ship" => 0.0f,
            _ => 0.05f,
        };
        Vector3 position = GetMainCamera().transform.position + direction * (radarHUDDistance + distanceOffset);
        return position;
    }

    private Vector3 CalculateHUDPosition(Vector3 renderPosition, string tag)
    {
        Vector3 direction = (renderPosition - GetMainCamera().transform.position).normalized;
        float distanceOffset = tag switch
        {
            "Projectile" => -0.05f, // Projectiles highest priority
            "Ship" => 0.0f,
            _ => 0.05f,
        };
        Vector3 position = GetMainCamera().transform.position + direction * (radarHUDDistance + distanceOffset);
        return position;
    }

    public HUDObject CreateObject(RadarTarget target, string details, Vector3d predictedPosition)
    {
        Vector3 position = CalculateHUDPosition(target.scaledRigidbody.scaledTransform.realPosition, target.tag);
        Vector3 prediction = CalculateHUDPosition(predictedPosition, target.tag);
        float sqrDistanceToCenter = (GetMainCamera().transform.position + GetMainCamera().transform.forward * radarHUDDistance - position).sqrMagnitude;
        
        bool detailsActive = (targetingSystem.lockedTarget != null && targetingSystem.lockedTarget.GetID() == target.GetID())
                             || sqrDistanceToCenter < detailsDistance * detailsDistance;
        HUDObject newHUDObject = Instantiate(radarHudObjectPrefab, radarHudParent.transform).GetComponent<HUDObject>();
        newHUDObject.Init(this, position, target.GetID(), details, detailsActive, prediction);
        newHUDObject.sqrDistanceToCenter = sqrDistanceToCenter;
        return newHUDObject;
    }

    public bool UpdateObject(RadarTarget target, string details, Vector3d predictedPosition)
    {
        if (!radarIDHUDPair.TryGetValue(target.GetID(), out HUDObject HUDObject))
            return false;

        Vector3 position = CalculateHUDPosition(target.scaledRigidbody.scaledTransform.realPosition, target.tag);
        Vector3 predicted = CalculateHUDPosition(predictedPosition, target.tag);
        float sqrDistanceToCenter = (GetMainCamera().transform.position + GetMainCamera().transform.forward * radarHUDDistance - position).sqrMagnitude;
        HUDObject.sqrDistanceToCenter = sqrDistanceToCenter;
        bool isLockedTarget = targetingSystem.lockedTarget != null && targetingSystem.lockedTarget.GetID() == target.GetID();
        bool detailsActive = isLockedTarget || sqrDistanceToCenter < detailsDistance * detailsDistance;
        if (!isLockedTarget || !target.scaledRigidbody.scaledTransform.visible)
        {
            // No quad bounds
            HUDObject.UpdateObject(position, details, detailsActive, predicted);
            return true;
        }

        Quadrilateral quad;
        if (target.useScaleForBounds)
        {
            // Use ellipse based on lossy scale of target's transform and its rotation

            quad = SpaceGeometry.GetEllipsoidBoundingBox(target.transform.position, target.transform.lossyScale, target.transform.rotation, GetMainCamera());
        }
        else
        {
            // Calculate bounding box based on renderers
            quad = SpaceGeometry.GetMinimumBoundingBox(target.boundsRenderers, GetMainCamera());
        }

        HUDObject.UpdateObject(position, quad, details, detailsActive, predicted);
        return true;
    }

    public void SetTargetDirectionMarkerActive(bool active)
    {
        if (targetDirMarker.gameObject.activeSelf != active)
            targetDirMarker.gameObject.SetActive(active);
    }

    public void UpdateTargetDirectionMarker(Vector3d targetPosition)
    {
        Vector3d camRealPos = FloatingWorldOrigin.Instance.GetRealCameraPosition();

        Vector3 worldDirection = (targetPosition - camRealPos).normalized.ToVector3();
        Vector3 localDirection = GetMainCamera().transform.InverseTransformDirection(worldDirection);
        Vector2 screenDirection = new Vector2(localDirection.x, localDirection.y);

        if (localDirection.z >= 0f && screenDirection.sqrMagnitude < targetDirMarkerDistance * targetDirMarkerDistance)
        {
            SetTargetDirectionMarkerActive(false);
            return;
        }
        SetTargetDirectionMarkerActive(true);

        screenDirection.Normalize();

        targetDirMarker.anchoredPosition = screenDirection * targetDirMarkerRadius;
        float angle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg - 90f;
        targetDirMarker.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public bool TryGetValue(uint id, out HUDObject HUDObject)
    {
        return radarIDHUDPair.TryGetValue(id, out HUDObject);
    }

    public void Add(uint id, HUDObject HUDObject)
    {
        radarIDHUDPair.Add(id, HUDObject);
    }

    public void Remove(uint id)
    {
        radarIDHUDPair.Remove(id);
    }

    public void ToggleRadarHUD(int state)
    {
        radarHudActive = state == 1;
        radarHudParent.SetActive(radarHudActive);
    }

    public void ToggleCombatHUD(int state)
    {
        combatHudActive = state == 1;
        combatPanel.gameObject.SetActive(combatHudActive);
    }

    public RadarTarget GetClosestTarget()
    {
        float closestDistance = float.MaxValue;
        RadarTarget best = null;
        foreach((uint id, HUDObject HUDObject) in radarIDHUDPair)
        {
            if (HUDObject.sqrDistanceToCenter < closestDistance)
            {
                if (RadarRegistry.TryGet(id, out best))
                    closestDistance = HUDObject.sqrDistanceToCenter;
            }
        }
        return best;
    }

    public void SetTurretsTargeting(uint targetId, int num)
    {
        if (!radarHudActive)
            return;
        if (TryGetValue(targetId, out HUDObject hudObject))
        {
            hudObject.SetTargetText(num);
        }
    }
}
