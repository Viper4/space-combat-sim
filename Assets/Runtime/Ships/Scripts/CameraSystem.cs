using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceStuff;

public class CameraSystem : MonoBehaviour
{
    [SerializeField] private Transform[] cameraPoints;
    [SerializeField] private float rotateSpeed = 10f;
    private Camera[] cameras;

    [SerializeField] private Transform buttonParent;
    [SerializeField] private GameObject cameraFeedPanel;

    [SerializeField] private GameObject buttonPrefab;

    [SerializeField] private Button[] camRotationButtons;
    private int rotationAxisHeld = -1;
    [SerializeField] private Button zoomInButton;
    [SerializeField] private Button zoomOutButton;

    private int selectedCamera;

    private void AddButtonListener(Button button, int index)
    {
        button.onClick.AddListener(() => SelectCamera(index));
    }

    void Start()
    {
        cameras = new Camera[cameraPoints.Length];
        for (int i = 0; i < cameraPoints.Length; i++)
        {
            cameras[i] = cameraPoints[i].GetChild(0).GetComponent<Camera>();
            GameObject cameraButton = Instantiate(buttonPrefab, buttonParent);
            cameraButton.name = "Camera Button " + i;
            AddButtonListener(cameraButton.GetComponent<Button>(), i); // Do this so the event doesn't just reference int i and instead creates a new integer
            cameraButton.transform.Find("Button Front").Find("Text").GetComponent<TextMeshProUGUI>().text = "CAM" + (i + 1);
            cameras[i].gameObject.SetActive(false);
        }
    }

    void Update()
    {
        float pitchInput = 0;
        float yawInput = 0;

        switch (rotationAxisHeld)
        {
            case 0:
                pitchInput = -1;
                break;

            case 1:
                pitchInput = 1;
                break;

            case 2:
                yawInput = -1;
                break;

            case 3:
                yawInput = 1;
                break;
        }

        if (selectedCamera != -1)
        {
            Transform cam = cameraPoints[selectedCamera];

            cam.Rotate(Vector3.right, pitchInput * rotateSpeed * Time.deltaTime, Space.Self);
            cam.Rotate(Vector3.up, yawInput * rotateSpeed * Time.deltaTime, Space.Self);
        }
    }

    public void SelectCamera(int i)
    {
        DisableCameras();
        selectedCamera = i;
        buttonParent.parent.gameObject.SetActive(false);
        cameras[i].gameObject.SetActive(true);
        cameraFeedPanel.SetActive(true);
    }

    public void DisableCameras()
    {
        selectedCamera = -1;
        for (int j = 0; j < cameras.Length; j++)
        {
            cameras[j].gameObject.SetActive(false);
        }
    }

    public void OnRotateButtonDown(int axis)
    {
        rotationAxisHeld = axis;
    }

    public void OnRotateButtonUp(int axis)
    {
        rotationAxisHeld = -1;
    }
}
