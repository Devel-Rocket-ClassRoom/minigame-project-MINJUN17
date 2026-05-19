using UnityEngine;

public class CookingToolInstance : MonoBehaviour
{
    public CookingToolData data;

    private void Awake() => CookingToolManager.Instance.Register(this);
    private void OnDestroy() => CookingToolManager.Instance?.Unregister(this);
}