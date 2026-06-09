using UnityEngine;
using DG.Tweening;

/// <summary>
/// 별 스프라이트 몇 개가 살짝 흩어졌다 둥실 떠오르며 사라지는 가벼운 연출.
/// 파티클 시스템 없이 스프라이트 + DOTween 으로 구현. 재생 후 스스로 파괴.
/// 해금 연출(ExpansionCinematic.toiletEffectPrefab) 등에 프리팹으로 사용.
/// </summary>
public class FloatingStarsEffect : MonoBehaviour
{
    [Header("별 그림 (여러 개면 랜덤으로 섞임)")]
    [SerializeField] private Sprite[] starSprites;

    [Header("개수 / 퍼짐")]
    [SerializeField] private int count = 6;
    [SerializeField] private float spreadRadius = 0.5f;    // 처음 흩어지는 반경
    [SerializeField] private float floatDistance = 1.5f;   // 떠오르는 높이
    [SerializeField] private float horizontalSway = 0.4f;  // 좌우 흔들림 폭

    [Header("크기 / 시간")]
    [SerializeField] private float starSize = 0.4f;
    [SerializeField] private float duration = 1.2f;        // 떠오르며 사라지는 시간

    [Header("색 / 정렬")]
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private string sortingLayer = "Floor2";
    [SerializeField] private int sortingOrder = 50;

    private void Start()
    {
        if (starSprites == null || starSprites.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < count; i++)
            SpawnStar();

        // 안전망: 모든 별이 끝난 뒤 자기 파괴
        Destroy(gameObject, duration + 0.6f);
    }

    private void SpawnStar()
    {
        var go = new GameObject("Star");
        go.transform.SetParent(transform, false);

        // 시작 위치: 중심에서 살짝 흩어짐
        Vector2 offset = Random.insideUnitCircle * spreadRadius;
        go.transform.localPosition = offset;
        go.transform.localScale = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = starSprites[Random.Range(0, starSprites.Length)];
        sr.sortingOrder = sortingOrder;
        if (!string.IsNullOrEmpty(sortingLayer))
            sr.sortingLayerName = sortingLayer;
        sr.color = new Color(tint.r, tint.g, tint.b, 0f);

        // 목표 위치: 위로 둥실 + 좌우 랜덤
        Vector3 target = go.transform.localPosition
                         + Vector3.up * (floatDistance * Random.Range(0.7f, 1.2f))
                         + Vector3.right * Random.Range(-horizontalSway, horizontalSway);

        float popTime = 0.25f;
        float startDelay = Random.Range(0f, 0.15f);   // 별마다 살짝 시차

        var seq = DOTween.Sequence().SetDelay(startDelay);
        // 팝 등장 (작게→크게 + 서서히 보임)
        seq.Append(go.transform.DOScale(starSize, popTime).SetEase(Ease.OutBack));
        seq.Join(sr.DOFade(1f, popTime));
        // 둥실 떠오르며 사라짐
        seq.Append(go.transform.DOLocalMove(target, duration).SetEase(Ease.OutCubic));
        seq.Join(sr.DOFade(0f, duration).SetEase(Ease.InQuad));
    }
}
