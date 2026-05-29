using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FloorToggleButton : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CameraController cameraController;

    [Header("비주얼")]
    [SerializeField] private Image bgImage;
    [SerializeField] private RectTransform innerRect;   // 위아래로 움직일 내부 이미지
    [SerializeField] private TextMeshProUGUI label;

    [Header("1층 설정")]
    [SerializeField] private Sprite bgFloor1;
    [SerializeField] private string labelFloor1 = "1층";

    [Header("2층 설정")]
    [SerializeField] private Sprite bgFloor2;
    [SerializeField] private string labelFloor2 = "2층";

    [Header("토글 위치")]
    [SerializeField] private float posFloor1Y = -20f;   // 1층일 때 inner Y
    [SerializeField] private float posFloor2Y =  20f;   // 2층일 때 inner Y
    [SerializeField] private float slideDuration = 0.15f;

    private Coroutine _slideCoroutine;

    public void Toggle()
    {
        cameraController.ToggleFloor();
        Refresh(animated: true);
    }

    private void OnEnable() => Refresh(animated: false);

    private void Refresh(bool animated)
    {
        bool isFloor1 = cameraController == null
                     || cameraController.CurrentFloor == FloorIndex.Floor1;

        if (bgImage != null) bgImage.sprite = isFloor1 ? bgFloor1 : bgFloor2;
        if (label   != null) label.text     = isFloor1 ? labelFloor1 : labelFloor2;

        float targetY = isFloor1 ? posFloor1Y : posFloor2Y;

        if (innerRect == null) return;

        if (animated)
        {
            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideTo(targetY));
        }
        else
        {
            var pos = innerRect.anchoredPosition;
            pos.y = targetY;
            innerRect.anchoredPosition = pos;
        }
    }

    private IEnumerator SlideTo(float targetY)
    {
        float startY = innerRect.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            var pos = innerRect.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, t);
            innerRect.anchoredPosition = pos;
            yield return null;
        }

        var finalPos = innerRect.anchoredPosition;
        finalPos.y = targetY;
        innerRect.anchoredPosition = finalPos;
    }
}
