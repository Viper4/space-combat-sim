using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurretsUI : MonoBehaviour
{
    private struct TurretPanel
    {
        public GameObject gameObject;
        public TextMeshProUGUI title;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI targetText;
        public TextMeshProUGUI ammoText;
        public TextMeshProUGUI healthText;
        public Transform modelPlatform;
        public Transform modelRotatingObject;
    }

    [SerializeField] private TurretSystem turretSystem;

    [SerializeField] private Transform buttonParent;
    [SerializeField] private Transform panelParent;

    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameObject panelPrefab;

    [SerializeField] private GameObject enableTurretButton;
    [SerializeField] private GameObject disableTurretButton;

    private TurretPanel[] turretPanels;
    private int selectedTurret = -1;

    private void AddButtonListener(Button button, int index)
    {
        button.onClick.AddListener(() => SelectTurretPanel(index));
    }

    private void Start()
    {
        turretPanels = new TurretPanel[turretSystem.turrets.Length];
        for (int i = 0; i < turretSystem.turrets.Length; i++)
        {
            Turret turret = turretSystem.turrets[i];
            GameObject turretButton = Instantiate(buttonPrefab, buttonParent);
            turretButton.name = "Turret Button " + i;
            GameObject turretPanel = Instantiate(panelPrefab, panelParent);
            turretPanel.name = "Turret Panel " + i;
            Transform modelPlatform = Instantiate(turretSystem.turrets[i].UIModel, turretPanel.transform.Find("Model Parent")).transform.Find("Platform");
            turretPanels[i] = new TurretPanel()
            {
                gameObject = turretPanel,
                title = turretPanel.transform.Find("Title").GetComponent<TextMeshProUGUI>(),
                statusText = turretPanel.transform.Find("Status Text").GetComponent<TextMeshProUGUI>(),
                targetText = turretPanel.transform.Find("Target Text").GetComponent<TextMeshProUGUI>(),
                ammoText = turretPanel.transform.Find("Ammo Text").GetComponent<TextMeshProUGUI>(),
                healthText = turretPanel.transform.Find("Health Text").GetComponent<TextMeshProUGUI>(),
                modelPlatform = modelPlatform,
                modelRotatingObject = modelPlatform.Find("Rotating Object")
            };
            AddButtonListener(turretButton.GetComponent<Button>(), i); // Do this so the event doesn't just reference int i and instead creates a new integer

            TextMeshProUGUI turretButtonText = turretButton.transform.Find("Button Front").Find("Text").GetComponent<TextMeshProUGUI>();

            switch (turret.GetType().Name)
            {
                case "Turret":
                    turretButtonText.text = "TRT" + (i + 1);

                    turretPanels[i].title.text = "TRT" + (i + 1) + " INFO";
                    break;
                case "LaserTurret":
                    turretButtonText.text = "LSR" + (i + 1);

                    turretPanels[i].title.text = "LSR" + (i + 1) + " INFO";
                    break;
                case "RailGun":
                    turretButtonText.text = "RIL" + (i + 1);

                    turretPanels[i].title.text = "RIL" + (i + 1) + " INFO";
                    break;
            }
        }
    }

    void Update()
    {
        if(selectedTurret >= 0)
        {
            TurretPanel panel = turretPanels[selectedTurret];
            Turret turret = turretSystem.turrets[selectedTurret];
            if (turret.destroyed)
            {
                panel.statusText.text = "<color=red>Destroyed</color>";
            }
            else
            {
                if (turret.active)
                {
                    panel.statusText.text = "Active";
                }
                else
                {
                    panel.statusText.text = "<color=yellow>Inactive</color>";
                }
            }
            
            if (turretSystem.manualControl)
            {
                panel.targetText.text = "<color=yellow>MANUAL CONTROL</color>";
            }
            else
            {
                panel.targetText.text = turret.currentTarget == null ? "<color=grey>None</color>" : turret.currentTarget.name;
            }
            //panel.ammoText.text = "AMMO: " + turret.ammo;
            float healthPercent = turret.statSystem.health / turret.statSystem.maxHealth;
            if (healthPercent < 0.25f)
            {
                panel.healthText.color = Color.red;
            }
            else if (healthPercent < 0.5f)
            {
                panel.healthText.color = Color.yellow;
            }
            else
            {
                panel.healthText.color = Color.green;
            }
            panel.healthText.text = Math.Round(healthPercent * 100, 2) + "%";
            panel.modelPlatform.rotation = turret.platform.rotation;
            panel.modelRotatingObject.rotation = turret.barrel.rotation;
        }
    }

    public void SelectTurretPanel(int i)
    {
        buttonParent.parent.gameObject.SetActive(false);
        panelParent.parent.gameObject.SetActive(true);
        for(int j = 0; j < turretPanels.Length; j++)
        {
            turretPanels[j].gameObject.SetActive(false);
        }
        turretPanels[i].gameObject.SetActive(true);
        selectedTurret = i;
        enableTurretButton.SetActive(!turretSystem.turrets[i].active);
        disableTurretButton.SetActive(turretSystem.turrets[i].active);
    }

    public void EnableTurret()
    {
        if (selectedTurret >= 0)
        {
            turretSystem.turrets[selectedTurret].active = true;
            enableTurretButton.SetActive(false);
            disableTurretButton.SetActive(true);
        }
    }

    public void DisableTurret()
    {
        if (selectedTurret >= 0)
        {
            turretSystem.turrets[selectedTurret].active = false;
            enableTurretButton.SetActive(true);
            disableTurretButton.SetActive(false);
        }
    }
}
