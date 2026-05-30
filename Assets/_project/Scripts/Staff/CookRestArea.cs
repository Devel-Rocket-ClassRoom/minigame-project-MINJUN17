using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 요리사가 일감 없을 때 쉬는 자리들(우선순위 순서대로).
/// 배치: 주방 어딘가에 빈 GameObject로 두고, 자식 Transform들을 restPositions에 순서대로 등록.
///       배열 맨 위가 1순위 → 요리사 1명이면 항상 1순위 자리에서 쉼.
/// 비워두면 CookStaff는 기존처럼 주방 빈 칸 중 가까운 곳에서 쉼(폴백).
/// 씬에 하나만 둔다.
/// </summary>
public class CookRestArea : MonoBehaviour
{
    public static CookRestArea Instance { get; private set; }

    [Tooltip("요리사 휴식 자리들 (보통 3개). 위에서부터 1순위.")]
    [SerializeField] private Transform[] restPositions;

    public IReadOnlyList<Transform> RestPositions => restPositions;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
