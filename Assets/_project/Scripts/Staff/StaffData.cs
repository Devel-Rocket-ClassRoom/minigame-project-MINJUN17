using UnityEngine;

[CreateAssetMenu(fileName = "StaffData", menuName = "Staff/StaffData")]
public class StaffData : ScriptableObject
{
    public StaffRole role;           // Cook / Server / Rider 
    public StaffType grade;          // Junior / Senior / Manager 

    [Header("외형")]
    public Sprite sprite;

    [Header("능력치")]
    public float moveSpeed;
    public float kindness;
    public float speedMultiplier;    // Cook=조리속도, Server=배달속도, Rider=배달속도
    public long hireCost;
    public long salary;
}
