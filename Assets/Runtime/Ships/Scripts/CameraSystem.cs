using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraSystem : MonoBehaviour
{
    [SerializeField] private TargetingSystem targetingSystem;
    [SerializeField] private Transform[] cameraPoints;
    [SerializeField] private float rotateSpeed = 10f;
    [SerializeField] private Vector2 fovRange = new Vector2(60f, 15f);
    private Camera[] cameras;

    [SerializeField] private Transform buttonParent;
    [SerializeField] private GameObject cameraFeedPanel;

    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private Button setTrackOnButton;
    [SerializeField] private Button setTrackOffButton;

    private int rotationAxisHeld = -1;
    private float zoom;
    private int selectedCamera;
    private bool tracking = false;

    private void AddButtonListener(Button button, int index)
    {
        button.onClick.AddListener(() => SelectCamera(index));
    }

    private void Start()
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
        setTrackOnButton.onClick.AddListener(SetTrackOn);
        setTrackOffButton.onClick.AddListener(SetTrackOff);
    }

    private void Update()
    {
        if (tracking && targetingSystem.lockedTarget != null)
        {
            
        }

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
            cam.localRotation = Quaternion.Euler(Mathf.Clamp(cam.localEulerAngles.x, -89f, 89f), cam.localEulerAngles.y, cam.localEulerAngles.z);
        }
    }

    public void SelectCamera(int i)
    {
        if (selectedCamera == i)
        {
            cameraFeedPanel.SetActive(false);
            DisableCameras();
            return;
        }
        DisableCameras();
        selectedCamera = i;
        cameras[i].gameObject.SetActive(true);
        cameras[i].fieldOfView = Mathf.Lerp(fovRange.x, fovRange.y, zoom);
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

    public void OnZoomChanged(float value)
    {
        zoom = value;
        if (selectedCamera != -1)
            cameras[selectedCamera].fieldOfView = Mathf.Lerp(fovRange.x, fovRange.y, zoom);
    }

    private void SetTrackOn()
    {
        tracking = true;
        setTrackOnButton.gameObject.SetActive(false);
        setTrackOffButton.gameObject.SetActive(true);
    }

    private void SetTrackOff()
    {
        tracking = false;
        setTrackOnButton.gameObject.SetActive(true);
        setTrackOffButton.gameObject.SetActive(false);
    }
}
