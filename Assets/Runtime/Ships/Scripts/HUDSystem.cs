using System.Collections;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;
using UnityEngine.UI;

public class HUDSystem : MonoBehaviour
{
    public bool radarHudActive = false;
    public bool combatHudActive = false;
    [SerializeField] private GameObject radarHudObjectPrefab;
    [SerializeField] private GameObject radarHudParent;
    [SerializeField] private float radarHUDDistance = 1.5f;
    public RectTransform combatPanel;
    [SerializeField] private float combatHUDDistance = 1.0f;
    public Image targetCrosshair;
    [SerializeField, Tooltip("Distance away from camera center HUD objects will show details text.")] private float detailsDistance = 0.05f;

    private Dictionary<uint, HUDObject> radarIDHUDPair = new Dictionary<uint, HUDObject>();

    private void Start()
    {
        radarHudParent.SetActive(radarHudActive);
        combatPanel.gameObject.SetActive(combatHudActive);
        targetCrosshair.gameObject.SetActive(false);
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

    public void TargetObject(RadarTarget target)
    {
        
    }

    public HUDObject CreateObject(RadarTarget target, string details)
    {
        Vector3 position = CalculateHUDPosition(target.doubleRigidbody.scaledTransform.realPosition, target.tag);
        float sqrDistanceToCenter = (Camera.main.transform.position + Camera.main.transform.forward * radarHUDDistance - position).sqrMagnitude;
        bool detailsActive = sqrDistanceToCenter < detailsDistance * detailsDistance;
        HUDObject newHUDObject = Instantiate(radarHudObjectPrefab, radarHudParent.transform).GetComponent<HUDObject>();
        newHUDObject.Init(this, position, target.GetID(), details, detailsActive);
        return newHUDObject;
    }

    public bool UpdateObject(RadarTarget target, string details)
    {
        if (!radarIDHUDPair.TryGetValue(target.GetID(), out HUDObject HUDObject))
            return false;

        Vector3 position = CalculateHUDPosition(target.doubleRigidbody.scaledTransform.realPosition, target.tag);
        float sqrDistanceToCenter = (Camera.main.transform.position + Camera.main.transform.forward * radarHUDDistance - position).sqrMagnitude;
        bool detailsActive = sqrDistanceToCenter < detailsDistance * detailsDistance;
        if (!target.targeted)
        {
            HUDObject.UpdateObject(position, details, detailsActive);
            return true;
        }

        if (!target.doubleRigidbody.scaledTransform.visible)
        {
            // Need to pass predicted future position to set position of predicted center marker
            HUDObject.UpdateObject(position, details, detailsActive);
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

        HUDObject.UpdateObject(position, quad, details, detailsActive);
        return true;
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
}
