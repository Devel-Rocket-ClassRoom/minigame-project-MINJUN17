using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탭 버튼 그룹 — 선택된 탭은 원색, 나머지는 dimColor로 어둡게 표시.
/// 버튼 onClick에서 SelectTab(index)를 호출하거나,
/// SetZone/SetCategory 호출 전에 이 컴포넌트의 SelectTab을 함께 호출하면 된다.
/// </summary>
public class TabButtonGroup : MonoBehaviour
{
    [SerializeField] private Button[] buttons;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color dimColor    = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private int defaultIndex  = 0;

    private int _current = -1;

    private void OnEnable()
    {
        SelectTab(defaultIndex);
    }

    public void SelectTab(int index)
    {
        if (buttons == null) return;
        _current = index;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            var img = buttons[i].GetComponent<Image>();
            if (img != null) img.color = (i == _current) ? activeColor : dimColor;
        }
    }
}
