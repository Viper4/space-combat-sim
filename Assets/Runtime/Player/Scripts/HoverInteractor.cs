using UnityEngine;
using UnityEngine.InputSystem;

public class HoverInteractor : MonoBehaviour
{
    [SerializeField] private LayerMask hoverLayerMask;
    [SerializeField] private float maxDistance = 10f;

    private HoverInteractable currentHovered;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hoverLayerMask, QueryTriggerInteraction.Ignore) 
                && hit.collider.CompareTag("PointerInteractable") 
                && hit.collider.TryGetComponent(out HoverInteractable interactable))
            {
                if (currentHovered != interactable)
                {
                    interactable.OnHoverEnter();
                    if (currentHovered != null)
                    {
                        currentHovered.OnHoverExit();
                    }
                }
                currentHovered = interactable;
            }
            else
            {
                if (currentHovered != null)
                {
                    currentHovered.OnHoverExit();
                    currentHovered = null;
                }
            }
        }
    }
}
