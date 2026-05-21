using UnityEngine;

[CreateAssetMenu(fileName = "MenuData", menuName = "Menu/MenuData")]
public class MenuData : ScriptableObject
{
    public string menuName;
    public Sprite foodSprite;
    public int price;
    public int cost;
    public CookingToolData tool;          // 1:1
    public int satisfactionUnlock;        // 만족도 해금 비용 (선택)
    public float spawnWeight;
}