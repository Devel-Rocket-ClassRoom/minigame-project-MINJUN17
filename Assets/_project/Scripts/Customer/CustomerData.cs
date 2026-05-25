using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "CustomerData", menuName = "Customer/CustomerData")]
public class CustomerData : ScriptableObject
{
    public GameObject customerPrefab;
    public LocalizedString customerName;   // 다국어 표시명
    public Sprite icon;

    public int minOrderCount = 1;      // 최소 주문 개수
    public int maxOrderCount = 1;      // 최대 주문 개수

    public int baseSatisfaction;

    public float moveSpeed = 2f;
    public float eatSpeed = 5f;
    public float patience = 20f;

    public float spawnWeight = 1f; // 스폰 매니저에서 스폰 빈도
}
