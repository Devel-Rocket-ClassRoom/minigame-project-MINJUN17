using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 시상식 무대 조명. 평소엔 환하다가(불 켜짐), 발표 순간 암전 + 구석 조명 빔이
/// 켜지며 사회자에게 핀조명이 떨어지는 연출.
///
/// - LightsOn()   : 암전 해제(밝음) + 빔 꺼짐  → 사회자 걸어올 때
/// - SpotlightOn(): 암전 + 빔이 쭉 내려옴(핀조명) → 발표 순간
///
/// 빔 RectTransform은 Pivot을 "조명 기구 쪽 끝"(보통 위)에 두면
/// DOScaleY 0→1 로 기구에서 사회자 쪽으로 뻗어 내려온다.
/// 오른쪽 빔은 같은 스프라이트에 Scale X = -1 로 미러해서 쓰면 됨.
/// CeremonyDirector가 LightsOn/SpotlightOn 을 호출. 단독 테스트도 가능.
/// </summary>
public class CeremonyLighting : MonoBehaviour
{
    [Header("암전 오버레이 (검은 풀스크린 Image의 CanvasGroup)")]
    [SerializeField] private CanvasGroup dim;
    [Range(0f, 1f)]
    [Tooltip("발표 순간 암전 진하기 (1=완전 검정)")]
    [SerializeField] private float dimAlpha = 0.8f;

    [Header("구석 조명 빔 (Pivot = 조명 기구 쪽 끝)")]
    [SerializeField] private List<RectTransform> beams = new();
    [Tooltip("빔들을 묶은 CanvasGroup (페이드용). 없으면 스케일만으로 처리")]
    [SerializeField] private CanvasGroup beamGroup;

    [Header("핀조명 원 (캐릭터 발밑/주위 둥근 빛)")]
    [Tooltip("사회자 위치에 둘 둥근 빛 Image의 CanvasGroup. 핀조명 켜질 때 같이 페이드 인")]
    [SerializeField] private CanvasGroup spotlightCircle;

    [Tooltip("켜면 빔이 DOScaleY로 '쭉 내려옴'(Pivot Y=1 권장). 끄면 페이드 인만(Pivot 무관)")]
    [SerializeField] private bool growBeams = true;

    [Header("타이밍")]
    [SerializeField] private float fadeDuration = 0.5f;
    [Tooltip("빔이 쭉 내려오는 시간(초)")]
    [SerializeField] private float beamExtendDuration = 0.45f;
    [Tooltip("빔별 시작 시차(초) — 0이면 동시에")]
    [SerializeField] private float beamStagger = 0.08f;
    [SerializeField] private Ease beamEase = Ease.OutCubic;

    [Header("켜질 때 깜빡임")]
    [SerializeField] private bool flicker = true;

    private Sequence _seq;
    private readonly List<float> _baseScaleY = new();   // 에디터에서 설정한 "완전히 켜진" Y 스케일

    private void Awake()
    {
        // 빔의 원래 Y 스케일을 "켜짐" 기준값으로 캐시 (0으로 만들기 전에 먼저)
        _baseScaleY.Clear();
        foreach (var b in beams)
            _baseScaleY.Add(b != null ? b.localScale.y : 1f);

        ApplyLightsOnInstant();
    }

    private void OnDisable() => _seq?.Kill();

    // ─── 외부 호출 ───

    /// <summary>불 켜짐: 암전 해제 + 빔 꺼짐 (사회자 걸어올 때).</summary>
    [ContextMenu("▶ Lights On (불 켜짐)")]
    public void LightsOnContext() => LightsOn();

    public void LightsOn(bool instant = false)
    {
        _seq?.Kill();
        if (instant) { ApplyLightsOnInstant(); return; }

        if (dim != null)
        {
            dim.DOFade(0f, fadeDuration).SetUpdate(true).SetLink(gameObject);
            dim.blocksRaycasts = false;
        }
        if (beamGroup != null)
            beamGroup.DOFade(0f, fadeDuration).SetUpdate(true).SetLink(gameObject);

        if (spotlightCircle != null)
            spotlightCircle.DOFade(0f, fadeDuration).SetUpdate(true).SetLink(gameObject);

        if (growBeams)
            foreach (var b in beams)
                if (b != null)
                    b.DOScaleY(0f, fadeDuration).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>핀조명: 암전 + 빔이 쭉 내려옴 (발표 순간).</summary>
    [ContextMenu("▶ Spotlight On (핀조명)")]
    public void SpotlightOn()
    {
        _seq?.Kill();

        // 빔 시작 상태(꺼짐)로 리셋 — X 부호(미러)는 보존, Y만 0 (grow 모드일 때만)
        if (beamGroup != null) beamGroup.alpha = 0f;
        if (growBeams)
            foreach (var b in beams)
                if (b != null) { var s = b.localScale; b.localScale = new Vector3(s.x, 0f, s.z); }

        _seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1) 암전
        if (dim != null)
        {
            dim.blocksRaycasts = true;
            _seq.Join(dim.DOFade(dimAlpha, fadeDuration));
        }

        // 2) 빔 켜짐 + 쭉 내려옴 (스태거)
        if (beamGroup != null)
            _seq.Join(beamGroup.DOFade(1f, fadeDuration * 0.6f));

        // 2.5) 핀조명 원 페이드 인 (캐릭터 주위 둥근 빛)
        if (spotlightCircle != null)
            _seq.Join(spotlightCircle.DOFade(1f, fadeDuration));

        if (growBeams)
            for (int i = 0; i < beams.Count; i++)
            {
                var b = beams[i];
                if (b == null) continue;
                float targetY = i < _baseScaleY.Count ? _baseScaleY[i] : 1f;
                _seq.Insert(i * beamStagger,
                    b.DOScaleY(targetY, beamExtendDuration).SetEase(beamEase));
            }

        // 3) 깜빡임 (켜진 직후 살짝)
        if (flicker && beamGroup != null)
        {
            _seq.Append(beamGroup.DOFade(0.55f, 0.06f));
            _seq.Append(beamGroup.DOFade(1f, 0.06f));
            _seq.Append(beamGroup.DOFade(0.7f, 0.05f));
            _seq.Append(beamGroup.DOFade(1f, 0.05f));
        }
    }

    /// <summary>암전 끄고 핀조명 닫기 (확인 버튼 등에서).</summary>
    public void SpotlightOff() => LightsOn();

    // ─── 내부 ───

    private void ApplyLightsOnInstant()
    {
        if (dim != null)
        {
            dim.alpha = 0f;
            dim.blocksRaycasts = false;
        }
        if (beamGroup != null) beamGroup.alpha = 0f;
        if (spotlightCircle != null) spotlightCircle.alpha = 0f;
        if (growBeams)
            foreach (var b in beams)
                if (b != null) { var s = b.localScale; b.localScale = new Vector3(s.x, 0f, s.z); }
    }
}
