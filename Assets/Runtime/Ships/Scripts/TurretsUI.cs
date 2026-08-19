using UnityEngine;
using System.Collections;

public class TurretsUI : MonoBehaviour
{
    [SerializeField] private TurretSystem turretSystem;
    [SerializeField] private Transform shipModel;
    [SerializeField] private GameObject panelUI;
    [SerializeField] private Transform panelParent;
    [SerializeField] private TurretPanel panelPrefab;

    private IEnumerator Start()
    {
        yield return new WaitWhile(() => !turretSystem.initialized);
        
        for (int i = 0; i < turretSystem.turrets.Length; i++)
        {
            Turret turret = turretSystem.turrets[i];

            TurretPanel turretPanel = Instantiate(panelPrefab, panelParent);
            turretPanel.Initialize(turret);
            turretPanel.name = "Turret Panel " + i;

            GameObject panelTurretModel = Instantiate(turretSystem.turrets[i].UIModel, turretPanel.modelParent);
            panelTurretModel.transform.localScale = Vector3.one * 5f;
            Transform panelPlatformModel = panelTurretModel.transform.Find("Platform");

            GameObject shipTurretModel = Instantiate(turretSystem.turrets[i].UIModel, shipModel);
            Vector3 localPos = turretSystem.transform.InverseTransformPoint(turretSystem.turrets[i].transform.position);
            float inverseScaleX = 1f / shipModel.localScale.x;
            float inverseScaleY = 1f / shipModel.localScale.x;
            float inverseScaleZ = 1f / shipModel.localScale.x;
            // offset.x *= inverseScaleX;
            // offset.y *= inverseScaleY;
            // offset.z *= inverseScaleZ;
            
            shipTurretModel.transform.localPosition = localPos;
            shipTurretModel.transform.localScale = new Vector3(inverseScaleX, inverseScaleY, inverseScaleZ);

            Transform shipPlatformModel = shipTurretModel.transform.Find("Platform");

            turretPanel.platformModels = new Transform[] {panelPlatformModel, shipPlatformModel};
            turretPanel.rotatingObjectModels = new Transform[] {panelPlatformModel.Find("Rotating Object"), shipPlatformModel.Find("Rotating Object")};
            turretPanel.GetMaterials(new GameObject[] {panelTurretModel, shipTurretModel});
            
            switch (turret.GetType().Name)
            {
                case "Turret":
                    turretPanel.title.text = "GUN" + (i + 1) + " INFO";
                    break;
                case "LaserTurret":
                    turretPanel.title.text = "LSR" + (i + 1) + " INFO";
                    break;
                case "RailGun":
                    turretPanel.title.text = "RLG" + (i + 1) + " INFO";
                    break;
            }
        }
    }

    public void EnableTurrets()
    {
        for (int i = 0; i < turretSystem.turrets.Length; i++)
        {
            turretSystem.turrets[i].SetActive(true);
        }
    }

    public void DisableTurrets()
    {
        for (int i = 0; i < turretSystem.turrets.Length; i++)
        {
            turretSystem.turrets[i].SetActive(false);
        }
    }

    public void TogglePanels()
    {
        panelUI.SetActive(!panelUI.activeSelf);
    }
}
