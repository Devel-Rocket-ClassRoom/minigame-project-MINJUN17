using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬에 있는 모든 Button에 클릭 효과음을 자동으로 건다.
/// 배치: "항상 켜져 있는" 오브젝트(예: UI 캔버스 루트)에 하나만 붙이면 됨.
/// 런타임에 동적으로 생성한 버튼은 ButtonClickSfx.Register(button) 로 직접 등록.
/// </summary>
public class ButtonClickSfx : MonoBehaviour
{
    [SerializeField] private SfxId clickSfx = SfxId.ButtonClick;
    [Tooltip("비활성 상태인 버튼까지 포함해서 등록")]
    [SerializeField] private bool includeInactive = true;

    private static SfxId _sfx = SfxId.ButtonClick;

    private void Start()
    {
        _sfx = clickSfx;

        var buttons = FindObjectsByType<Button>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var b in buttons) Register(b);
    }

    /// <summary>버튼 하나에 클릭음을 건다(중복 등록 방지). 런타임 생성 버튼에 사용.</summary>
    public static void Register(Button button)
    {
        if (button == null) return;
        button.onClick.RemoveListener(Play);   // 중복 방지
        button.onClick.AddListener(Play);
    }

    private static void Play() => SoundManager.Get()?.PlaySfx(_sfx);
}
