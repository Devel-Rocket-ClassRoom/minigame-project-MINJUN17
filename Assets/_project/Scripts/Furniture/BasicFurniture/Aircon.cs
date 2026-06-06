using UnityEngine;

public class Aircon : MonoBehaviour
{
    private void OnEnable()
    {
        AirconManager.Instance?.Register();
    }

    private void OnDisable()
    {
        AirconManager.Instance?.Unregister();
    }
}
