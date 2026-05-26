using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUDSystem : MonoBehaviour
{
    public Transform HUDPivot;
    [SerializeField] private GameObject HUDObjectPrefab;
    [SerializeField] private GameObject HUDObjectParent;
    [SerializeField] private float HUDDistance = 1.5f;
    public RectTransform combatPanel;
    public Image mainCrosshair;

    private Dictionary<uint, HUDObject> radarIDHUDPair = new Dictionary<uint, HUDObject>();

    public HUDObject CreateObject(uint ID, Vector3 position, Bounds bounds, string name, string details)
    {
        HUDObject newHUDObject = Instantiate(HUDObjectPrefab, HUDObjectParent.transform).GetComponent<HUDObject>();
        newHUDObject.Init(this, position, bounds, ID, name, details);
        return newHUDObject;
    }

    public HUDObject CreateObject(uint ID, Transform target, string name, string details)
    {
        Bounds bounds = target.TryGetComponent<MeshRenderer>(out var colliderRenderer) ? colliderRenderer.bounds : new Bounds() { center = target.position, size = Vector3.zero };
        foreach (Transform child in target)
        {
            if (child.TryGetComponent<MeshRenderer>(out var childRenderer))
            {
                bounds.Encapsulate(childRenderer.bounds);
            }
        }
        Vector3 position = HUDPivot.position + (target.position - HUDPivot.position).normalized * HUDDistance;
        return CreateObject(ID, position, bounds, name, details);
    }

    public bool UpdateObject(uint ID, Vector3 position, Bounds bounds, string name, string details)
    {
        if (radarIDHUDPair.TryGetValue(ID, out HUDObject HUDObject))
        {
            HUDObject.UpdateObject(position, bounds, name, details);
            return true;
        }
        return false;
    }

    public bool UpdateObject(uint ID, Transform target, string name, string details)
    {
        if (radarIDHUDPair.TryGetValue(ID, out HUDObject HUDObject))
        {
            Bounds bounds = target.TryGetComponent<MeshRenderer>(out var colliderRenderer) ? colliderRenderer.bounds : new Bounds() { center = target.position, size = Vector3.zero };
            foreach (Transform child in target)
            {
                if (child.TryGetComponent<MeshRenderer>(out var childRenderer))
                {
                    bounds.Encapsulate(childRenderer.bounds);
                }
            }
            Vector3 position = HUDPivot.position + (target.position - HUDPivot.position).normalized * HUDDistance;
            HUDObject.UpdateObject(position, bounds, name, details);
            return true;
        }

        return false;
    }

    public bool TryGetValue(uint ID, out HUDObject HUDObject)
    {
        return radarIDHUDPair.TryGetValue(ID, out HUDObject);
    }

    public void Add(uint ID, HUDObject HUDObject)
    {
        radarIDHUDPair.Add(ID, HUDObject);
    }

    public void Remove(uint ID)
    {
        radarIDHUDPair.Remove(ID);
    }

    public void ToggleHUD(int state)
    {
        HUDPivot.gameObject.SetActive(state == 1);
    }
}
