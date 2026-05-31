using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 매출 그래프의 막대 1칸. RevenueGraph가 높이/라벨을 세팅할 수 있게 참조를 노출.
/// 막대 프리팹 루트에 붙이고 두 슬롯을 연결한다.
/// </summary>
public class RevenueBar : MonoBehaviour
{
    [Tooltip("높이를 조절할 막대 Image의 LayoutElement")]
    public LayoutElement bar;
    [Tooltip("막대 아래 월 표시 텍스트")]
    public TextMeshProUGUI monthLabel;
}
