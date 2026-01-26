using UnityEngine;
using UnityEngine.EventSystems;

public class Level_0 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject targetA;
    [SerializeField] private GameObject targetB;
    [SerializeField] private GameObject targetC;

    [Header("Layer")]
    [SerializeField] private LayerMask lineLayer;

    Camera cam;
    bool triggered;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (triggered) return;

        // PC (chuột)
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick(Input.mousePosition);
        }

        // Mobile (touch)
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            HandleClick(Input.GetTouch(0).position);
        }
    }

    void HandleClick(Vector2 screenPos)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, lineLayer);

        if (hit.collider == null) return;

        if (((1 << hit.collider.gameObject.layer) & lineLayer) == 0)
            return;

        TriggerAction();
    }

    void TriggerAction()
    {
        triggered = true;

        if (targetA != null)
            targetA.SetActive(false);

        if (targetB != null)
            targetB.SetActive(false);

        if (targetC != null)
            targetC.SetActive(false);
    }
}
