using System.Collections;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;
using UnityEngine.UI;

public class HUDSystem : MonoBehaviour
{
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

    private void Start()
    {
        radarHudParent.SetActive(radarHudActive);
        combatPanel.gameObject.SetActive(combatHudActive);
    }

    private Vector3 CalculateHUDPosition(Vector3d realPosition, string tag)
    {
        Vector3d camRealPos = FloatingWorldOrigin.Instance.worldOriginPosition + Camera.main.transform.position.ToVector3d();
        Vector3 direction = (realPosition - camRealPos).normalized.ToVector3();
        var distanceOffset = tag switch
        {
            "Projectile" => -0.05f, // Projectiles highest priority
            "Ship" => 0.0f,
            _ => 0.05f,
        };
        Vector3 position = Camera.main.transform.position + direction * (radarHUDDistance + distanceOffset);
        return position;
    }

    public HUDObject CreateObject(RadarTarget target, string details, Vector3d predictedPosition)
    {
        Vector3 position = CalculateHUDPosition(target.doubleRigidbody.scaledTransform.realPosition, target.tag);
        Vector3 prediction = CalculateHUDPosition(predictedPosition, target.tag);
        float sqrDistanceToCenter = (Camera.main.transform.position + Camera.main.transform.forward * radarHUDDistance - position).sqrMagnitude;
        bool detailsActive = sqrDistanceToCenter < detailsDistance * detailsDistance;
        HUDObject newHUDObject = Instantiate(radarHudObjectPrefab, radarHudParent.transform).GetComponent<HUDObject>();
        newHUDObject.Init(this, position, target.GetID(), details, detailsActive, prediction);
        newHUDObject.sqrDistanceToCenter = sqrDistanceToCenter;
        return newHUDObject;
    }

    public bool UpdateObject(RadarTarget target, string details, Vector3d predictedPosition)
    {
        if (!radarIDHUDPair.TryGetValue(target.GetID(), out HUDObject HUDObject))
            return false;

        Vector3 position = CalculateHUDPosition(target.doubleRigidbody.scaledTransform.realPosition, target.tag);
        Vector3 predicted = CalculateHUDPosition(predictedPosition, target.tag);
        float sqrDistanceToCenter = (Camera.main.transform.position + Camera.main.transform.forward * radarHUDDistance - position).sqrMagnitude;
        HUDObject.sqrDistanceToCenter = sqrDistanceToCenter;

        if (targetingSystem.lockedTarget != target || !target.doubleRigidbody.scaledTransform.visible)
        {
            // No quad bounds
            HUDObject.UpdateObject(position, details, sqrDistanceToCenter < detailsDistance * detailsDistance, predicted);
            return true;
        }

        Quadrilateral quad;
        if (target.useScaleForBounds)
        {
            // Use ellipse based on lossy scale of target's transform and its rotation
            quad = SpaceGeometry.GetEllipsoidBoundingBox(target.transform.position, target.transform.lossyScale, target.transform.rotation, Camera.main);
        }
        else
        {
            // Calculate bounding box based on renderers
            quad = SpaceGeometry.GetMinimumBoundingBox(target.boundsRenderers, Camera.main);
        }

        HUDObject.UpdateObject(position, quad, details, true, predicted);
        return true;
    }

    public void SetTargetDirectionMarkerActive(bool active)
    {
        if (targetDirMarker.gameObject.activeSelf != active)
            targetDirMarker.gameObject.SetActive(active);
    }

    public void UpdateTargetDirectionMarker(Vector3d targetPosition)
    {
        Camera cam = Camera.main;

        Vector3d camRealPos = FloatingWorldOrigin.Instance.worldOriginPosition + cam.transform.position.ToVector3d();

        Vector3 worldDirection = (targetPosition - camRealPos).normalized.ToVector3();
        Vector3 localDirection = cam.transform.InverseTransformDirection(worldDirection);
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
}
