using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 특정 ExpansionStage가 해금되면:
///  - deactivateOnUnlock : 페이드 아웃 후 비활성화 (해금 전 가림막/망가진 버전)
///  - activateOnUnlock   : 활성화 후 페이드 인   (해금 후 깨끗한 버전)
/// 각 SpriteRenderer의 "원래 알파"를 보존 — 반투명 가림막은 반투명 그대로 유지.
/// </summary>
public class ExpansionStageSwitcher : MonoBehaviour
{
    [SerializeField] private ExpansionStageData stage;

    [Tooltip("해금되면 사라질 오브젝트 (해금 전 가림막/망가진 버전)")]
    [SerializeField] private List<GameObject> deactivateOnUnlock = new();

    [Tooltip("해금되면 나타날 오브젝트 (해금 후 깨끗한 버전)")]
    [SerializeField] private List<GameObject> activateOnUnlock = new();

    [SerializeField] private float fadeDuration = 0.6f;

    public ExpansionStageData Stage => stage;

    private static readonly List<ExpansionStageSwitcher> _all = new();

    // 각 SpriteRenderer의 인스펙터 원본 알파 (반투명 가림막 보존용)
    private readonly Dictionary<SpriteRenderer, float> _baseAlpha = new();

    private void Awake()
    {
        _all.Add(this);
        CacheBaseAlpha(deactivateOnUnlock);
        CacheBaseAlpha(activateOnUnlock);
        if (stage == null) return;
        bool unlocked = ExpansionManager.Instance != null
                        && ExpansionManager.Instance.IsStageCompleted(stage);
        ApplyInstant(unlocked);
    }

    private void OnDestroy() => _all.Remove(this);

    private void CacheBaseAlpha(List<GameObject> list)
    {
        foreach (var go in list)
        {
            if (go == null) continue;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                _baseAlpha[sr] = sr.color.a;
        }
    }

    private float BaseAlphaOf(SpriteRenderer sr)
        => _baseAlpha.TryGetValue(sr, out var a) ? a : sr.color.a;

    private void ApplyInstant(bool unlocked)
    {
        foreach (var go in deactivateOnUnlock)
        {
            if (go == null) continue;
            go.SetActive(!unlocked);
            SetAlpha(go, baseAlpha: true);   // 원래 알파로
        }
        foreach (var go in activateOnUnlock)
        {
            if (go == null) continue;
            go.SetActive(unlocked);
            SetAlpha(go, baseAlpha: true);
        }
    }

    /// <summary>런타임 해금 — 페이드 전환.</summary>
    private void PlayUnlock()
    {
        // 깨끗한 버전: 활성화 후 0 → 원래 알파
        foreach (var go in activateOnUnlock)
        {
            if (go == null) continue;
            go.SetActive(true);
            SetAlpha(go, baseAlpha: false);   // 0으로
            Fade(go, toBase: true, null);
        }
        // 가림막/망가진 버전: 현재 → 0 후 비활성화
        foreach (var go in deactivateOnUnlock)
        {
            if (go == null) continue;
            var target = go;
            Fade(go, toBase: false, () => target.SetActive(false));
        }
    }

    private void SetAlpha(GameObject go, bool baseAlpha)
    {
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var c = sr.color;
            c.a = baseAlpha ? BaseAlphaOf(sr) : 0f;
            sr.color = c;
        }
    }

    private void Fade(GameObject go, bool toBase, Action onComplete)
    {
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        if (srs.Length == 0) { onComplete?.Invoke(); return; }
        int remaining = srs.Length;
        foreach (var sr in srs)
        {
            sr.DOKill();
            float target = toBase ? BaseAlphaOf(sr) : 0f;
            sr.DOFade(target, fadeDuration).OnComplete(() =>
            {
                remaining--;
                if (remaining == 0) onComplete?.Invoke();
            });
        }
    }

    // ─── ExpansionManager 가 호출 ───
    public static void UnlockForStage(ExpansionStageData stage, bool animate)
    {
        if (stage == null) return;
        foreach (var s in _all)
        {
            if (s == null || s.Stage != stage) continue;
            if (animate) s.PlayUnlock();
            else s.ApplyInstant(true);
        }
    }
}
