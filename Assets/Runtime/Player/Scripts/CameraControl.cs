using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [SerializeField] private Transform lockedPoint;
    [SerializeField] private Transform freePoint;

    [SerializeField] private float lerpSpeed = 5f;
    [SerializeField] private float slerpSpeed = 10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Toggle cursor lock to allow player to switch between controlling ship and toggling switches/UI in the ship
        if (GameManager.Instance.inputActions.Player.CursorLock.WasPressedThisFrame())
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.position = Vector3.Lerp(transform.position, lockedPoint.position, lerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, lockedPoint.rotation, slerpSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, freePoint.position, lerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, freePoint.rotation, slerpSpeed * Time.deltaTime);
        }
    }
}
