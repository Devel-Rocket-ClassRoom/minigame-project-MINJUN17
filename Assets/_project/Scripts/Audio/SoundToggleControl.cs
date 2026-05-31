using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 소리 음소거 토글 한 채널(BGM 또는 SFX)을 담당.
/// 버튼을 누르면 해당 채널을 음소거 on/off 하고,
/// 아이콘 이미지(음표/스피커)와 스위치 이미지를 각각 on/off 스프라이트로 교체한다.
///
/// 배치: BGM용 1개, SFX용 1개 — 각각 channel만 다르게 두 개 둔다.
/// </summary>
public class SoundToggleControl : MonoBehaviour
{
    public enum Channel { Bgm, Sfx }

    [Header("채널")]
    [SerializeField] private Channel channel;

    [Header("클릭 영역 (보통 스위치에 Button)")]
    [SerializeField] private Button button;

    [Header("아이콘 이미지 (음표/스피커)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite iconOn;    // 소리 켜짐
    [SerializeField] private Sprite iconOff;   // 소리 꺼짐(음소거)

    [Header("스위치 이미지 (토글 손잡이)")]
    [SerializeField] private Image toggleImage;
    [SerializeField] private Sprite toggleOn;
    [SerializeField] private Sprite toggleOff;

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnClick);
        Refresh();   // 패널 열릴 때마다 현재 음소거 상태로 동기화
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        var sm = SoundManager.Get();
        if (sm == null) return;

        if (channel == Channel.Bgm) sm.ToggleBgmMuted();
        else                        sm.ToggleSfxMuted();

        Refresh();
    }

    /// <summary>현재 음소거 상태에 맞춰 아이콘/스위치 스프라이트를 갱신.</summary>
    public void Refresh()
    {
        var sm = SoundManager.Get();
        bool muted = sm != null && (channel == Channel.Bgm ? sm.IsBgmMuted : sm.IsSfxMuted);
        bool on = !muted;   // on = 소리 켜짐

        if (iconImage != null)
        {
            var s = on ? iconOn : iconOff;
            if (s != null) iconImage.sprite = s;
        }
        if (toggleImage != null)
        {
            var s = on ? toggleOn : toggleOff;
            if (s != null) toggleImage.sprite = s;
        }
    }
}
