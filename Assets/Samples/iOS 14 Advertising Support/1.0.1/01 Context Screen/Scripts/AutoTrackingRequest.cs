using System.Collections;
using UnityEngine;
using Unity.Advertisement.IosSupport.Components;

public class AutoTrackingRequest : MonoBehaviour
{
    [SerializeField] private ContextScreenView contextScreenView;

    private void Start()
    {
        StartCoroutine(DelayRequestTracking());
    }

    private IEnumerator DelayRequestTracking()
    {
        yield return new WaitForSeconds(1.5f); // Chờ 2 giây

        if (contextScreenView != null)
        {
            contextScreenView.RequestAuthorizationTracking();
        }
        else
        {
            Debug.LogError("AutoTrackingRequest: ContextScreenView is not assigned.");
        }
    }
}
