using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 여러 줄 대사를 순서대로 표시하는 박스. (튜토리얼/이벤트 공용)
/// - Play(lines, onComplete) 호출 → 한 줄씩 표시, 다음 버튼/탭으로 진행.
/// - 타자 효과 중 누르면 즉시 완성, 다 나온 상태면 다음 줄. 마지막 줄 후 onComplete.
/// - 초상화 애니메이션은 portrait에 UIImageAnimator를 따로 붙이면 됨(이 박스는 텍스트/진행만).
/// </summary>
public class DialogueBox : MonoBehaviour
{
    [SerializeField] private GameObject root;          // 박스 루트 (없으면 자기 자신)
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Button nextButton;        // 다음/탭 (박스 전체에 깔아도 됨)

    [Header("타자 효과")]
    [SerializeField] private bool typewriter = true;
    [SerializeField] private float charsPerSecond = 40f;

    private List<string> _lines;
    private int _index;
    private Action _onComplete;
    private Coroutine _typing;
    private bool _isTyping;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (nextButton != null) nextButton.onClick.AddListener(OnNext);
    }

    private void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(OnNext);
    }

    /// <summary>대사 줄 목록을 표시 시작. 마지막 줄까지 넘기면 onComplete 호출.</summary>
    /// <summary>여러 줄 대사 (탭으로 진행, 마지막에 onComplete). 인사/마무리용.</summary>
    public void Play(List<string> lines, Action onComplete)
    {
        _lines = lines;
        _index = 0;
        _onComplete = onComplete;
        if (root != null) root.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);   // 탭 진행 ON
        ShowLine();
    }

    /// <summary>설명만 표시 (탭으로 안 넘어감 → 탭이 타겟으로 통과). 행동 단계 안내용.</summary>
    public void ShowStatic(string line)
    {
        _onComplete = null;
        _lines = new List<string> { line };
        _index = 0;
        if (root != null) root.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(false);  // 탭 진행 OFF (타겟 클릭 허용)
        ShowLine();
    }

    public void Hide()
    {
        if (_typing != null) { StopCoroutine(_typing); _typing = null; }
        _isTyping = false;
        if (root != null) root.SetActive(false);
    }

    private void ShowLine()
    {
        if (_lines == null || _index >= _lines.Count) { Finish(); return; }
        string line = _lines[_index];

        if (typewriter)
        {
            if (_typing != null) StopCoroutine(_typing);
            _typing = StartCoroutine(TypeRoutine(line));
        }
        else
        {
            if (text != null) text.text = line;
            _isTyping = false;
        }
    }

    private IEnumerator TypeRoutine(string line)
    {
        _isTyping = true;
        if (text != null) text.text = "";
        float cps = Mathf.Max(1f, charsPerSecond);
        float t = 0f;
        int shown = 0;
        while (shown < line.Length)
        {
            t += Time.unscaledDeltaTime * cps;
            shown = Mathf.Min(line.Length, Mathf.FloorToInt(t));
            if (text != null) text.text = line.Substring(0, shown);
            yield return null;
        }
        if (text != null) text.text = line;
        _isTyping = false;
        _typing = null;
    }

    private void OnNext()
    {
        // 타이핑 중이면 즉시 완성
        if (_isTyping)
        {
            if (_typing != null) { StopCoroutine(_typing); _typing = null; }
            if (text != null && _lines != null && _index < _lines.Count) text.text = _lines[_index];
            _isTyping = false;
            return;
        }

        _index++;
        if (_lines == null || _index >= _lines.Count) Finish();
        else ShowLine();
    }

    private void Finish()
    {
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }
}
