using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 현재 "열려 있는" 씬들에서 Localize 가 안 걸린 TMP_Text / UI.Text 를 찾아
/// String Table 항목 생성 + LocalizeStringEvent 연결을 도와주는 에디터 툴.
/// 메뉴: Tools/Localization/Scene Auto Localizer
///
/// 권장 흐름:
///   1) GameScene / StartScene 을 연다 (둘 다 한 번에 하려면 하나는 Additive 로 열어도 됨).
///   2) [Scan Open Scenes] → 로컬라이즈 안 된 텍스트 목록 표시.
///      (가격/카운트/이름 등 런타임에 코드가 채우는 "동적 텍스트" 추정치는 자동으로 체크 해제됨)
///   3) 체크 박스로 로컬라이즈할 "정적 라벨" 만 선택, Key/영어 칸을 다듬는다.
///   4-A) [Export CSV] → key, ko-KR, en 컬럼 CSV. 영어 번역을 채워 넣고 String Table 에 임포트.
///   4-B) 또는 [Create Entries + Wire Selected] → 항목을 바로 만들고 씬에 LocalizeStringEvent 연결.
///   5) 씬 저장.
/// </summary>
public class SceneLocalizerWindow : EditorWindow
{
    private const string TableName = "StringTable";
    private const string KoCodePrefix = "ko";

    private class Row
    {
        public Component TextComp;     // TMP_Text 또는 UI.Text
        public GameObject Go;
        public string SceneName;
        public string Path;            // 하이어라키 경로
        public string CurrentText;
        public bool Selected;
        public bool LooksDynamic;      // 런타임 동적 텍스트 추정
        public string Key;
        public string English;
    }

    private readonly List<Row> _rows = new List<Row>();
    private Vector2 _scroll;
    private string _csvFolder = "Assets";
    private string _csvPath = "Assets/scene_localization.csv";
    private string _status = "";

    // 동적 텍스트로 추정할 GameObject 이름 키워드
    private static readonly string[] DynamicNameHints =
    {
        "value", "count", "amount", "price", "cost", "num", "number",
        "timer", "time", "name", "score", "money", "coin", "gold",
        "level", "lvl", "qty", "percent", "slider", "input", "placeholder",
        "field", "debug", "label_dynamic", "currency", "won", "cash"
    };

    [MenuItem("Tools/Localization/Scene Auto Localizer")]
    public static void Open()
    {
        GetWindow<SceneLocalizerWindow>("Scene Localizer").minSize = new Vector2(720, 420);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scene Auto Localizer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "현재 열린 씬에서 Localize 가 안 걸린 TMP/Text 를 찾습니다.\n" +
            "• 동적 텍스트(가격/카운트/이름 등) 추정치는 자동으로 체크 해제됩니다 — 직접 확인 후 선택하세요.\n" +
            "• 두 씬을 한 번에 하려면 한쪽을 Additive 로 열고 Scan 하세요.\n" +
            "• 작업 전 git 커밋 권장.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan Open Scenes", GUILayout.Height(26))) Scan();
            using (new EditorGUI.DisabledScope(_rows.Count == 0))
            {
                if (GUILayout.Button("Select Static Only", GUILayout.Height(26)))
                    foreach (var r in _rows) r.Selected = !r.LooksDynamic && HasMeaningfulText(r.CurrentText);
                if (GUILayout.Button("Deselect All", GUILayout.Height(26)))
                    foreach (var r in _rows) r.Selected = false;
            }
        }

        if (_rows.Count > 0)
        {
            int sel = _rows.Count(r => r.Selected);
            EditorGUILayout.LabelField($"발견: {_rows.Count}개 (로컬라이즈 안 됨)   선택: {sel}개");

            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var r in _rows) DrawRow(r);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                _csvFolder = EditorGUILayout.TextField("CSV 저장 폴더", _csvFolder);
                using (new EditorGUI.DisabledScope(sel == 0))
                {
                    if (GUILayout.Button("Export CSV", GUILayout.Width(110), GUILayout.Height(24))) ExportCsv();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _csvPath = EditorGUILayout.TextField("English CSV 경로", _csvPath);
                if (GUILayout.Button("Load English from CSV", GUILayout.Width(170), GUILayout.Height(24)))
                    LoadEnglishFromCsv();
            }

            using (new EditorGUI.DisabledScope(sel == 0))
            {
                if (GUILayout.Button("Create Entries + Wire Selected", GUILayout.Height(30)))
                    CreateEntriesAndWire();
            }
        }

        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.HelpBox(_status, MessageType.None);
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("✓", GUILayout.Width(18));
            GUILayout.Label("Scene / 경로", GUILayout.Width(300));
            GUILayout.Label("현재 텍스트(ko)", GUILayout.Width(150));
            GUILayout.Label("Key", GUILayout.Width(160));
            GUILayout.Label("English");
        }
    }

    private void DrawRow(Row r)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            r.Selected = EditorGUILayout.Toggle(r.Selected, GUILayout.Width(18));

            var pathStyle = new GUIStyle(EditorStyles.label) { wordWrap = false };
            if (r.LooksDynamic) pathStyle.normal.textColor = new Color(0.85f, 0.55f, 0.2f);
            string prefix = r.LooksDynamic ? "⚠ " : "";
            if (GUILayout.Button(prefix + r.SceneName + " / " + r.Path, pathStyle, GUILayout.Width(300)))
                Selection.activeGameObject = r.Go;   // 클릭하면 해당 오브젝트 선택

            EditorGUILayout.LabelField(Truncate(r.CurrentText, 28), GUILayout.Width(150));
            r.Key = EditorGUILayout.TextField(r.Key, GUILayout.Width(160));
            r.English = EditorGUILayout.TextField(r.English);
        }
    }

    // ---------------------------------------------------------------- Scan

    private void Scan()
    {
        _rows.Clear();
        _status = "";
        var usedKeys = new HashSet<string>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                // TMP_Text + UI.Text 둘 다
                CollectComponents<TMP_Text>(root, scene, usedKeys);
                CollectComponents<Text>(root, scene, usedKeys);
            }
        }

        _rows.Sort((a, b) =>
        {
            int s = string.Compare(a.SceneName, b.SceneName, StringComparison.Ordinal);
            return s != 0 ? s : string.Compare(a.Path, b.Path, StringComparison.Ordinal);
        });

        if (_rows.Count == 0)
            _status = "열린 씬에서 로컬라이즈 안 된 텍스트를 찾지 못했습니다. (씬이 열려 있는지 확인)";
    }

    private void CollectComponents<T>(GameObject root, Scene scene, HashSet<string> usedKeys) where T : Component
    {
        foreach (var comp in root.GetComponentsInChildren<T>(true))
        {
            if (comp == null) continue;

            // 이미 LocalizeStringEvent 가 붙어 있으면 건너뜀
            if (comp.GetComponent<LocalizeStringEvent>() != null) continue;

            // 중복 방지: 같은 GO 에 TMP_Text 와 (드물게) Text 둘 다 잡히는 경우
            if (_rows.Any(r => r.TextComp == comp)) continue;
            if (_rows.Any(r => r.Go == comp.gameObject)) continue;

            string text = GetText(comp);
            string path = GetPath(comp.transform);
            string key = MakeUniqueKey(scene.name, comp.gameObject.name, usedKeys);

            bool dynamic = LooksDynamic(comp, text);

            _rows.Add(new Row
            {
                TextComp = comp,
                Go = comp.gameObject,
                SceneName = scene.name,
                Path = path,
                CurrentText = text,
                Selected = !dynamic && HasMeaningfulText(text),
                LooksDynamic = dynamic,
                Key = key,
                English = "",
            });
        }
    }

    private static string GetText(Component c)
    {
        if (c is TMP_Text tmp) return tmp.text;
        if (c is Text t) return t.text;
        return "";
    }

    private static void SetTextValue(Component c, string v)
    {
        if (c is TMP_Text tmp) tmp.text = v;
        else if (c is Text t) t.text = v;
    }

    private static bool HasMeaningfulText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        // 글자(한글/영문)가 하나라도 있어야 의미있는 라벨로 본다
        return Regex.IsMatch(s, @"[\p{L}]");
    }

    private static bool LooksDynamic(Component comp, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        // 숫자/통화/기호로만 이뤄진 경우
        if (Regex.IsMatch(text, @"^[\s\d.,:;%$₩€£\-+/x×*()]*$")) return true;

        // 포맷 자리표시자
        if (text.Contains("{") || text.Contains("}") || text.Contains("%d") ||
            text.Contains("%s") || text.Contains("0/") || text.Contains("/0")) return true;

        // 흔한 더미 텍스트
        string lower = text.Trim().ToLowerInvariant();
        if (lower == "text" || lower == "new text" || lower.StartsWith("sample") ||
            lower.StartsWith("lorem")) return true;

        // InputField/Dropdown 의 동적 텍스트
        if (comp.GetComponentInParent<TMP_InputField>() != null) return true;
        if (comp.GetComponentInParent<InputField>() != null) return true;
        if (comp.GetComponentInParent<TMP_Dropdown>() != null) return true;
        if (comp.GetComponentInParent<Dropdown>() != null) return true;

        // 오브젝트 이름 힌트
        string name = comp.gameObject.name.ToLowerInvariant();
        if (DynamicNameHints.Any(h => name.Contains(h))) return true;

        return false;
    }

    // ---------------------------------------------------------------- Key 생성

    private static string MakeUniqueKey(string sceneName, string goName, HashSet<string> used)
    {
        string scene = Slug(sceneName);
        string n = Slug(goName);
        if (string.IsNullOrEmpty(n)) n = "text";
        string baseKey = $"ui.{scene}.{n}";
        string key = baseKey;
        int i = 2;
        while (used.Contains(key)) key = $"{baseKey}{i++}";
        used.Add(key);
        return key;
    }

    private static string Slug(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(char.ToLowerInvariant(c));
            else if (c == ' ' || c == '_' || c == '-' || c == '/') { /* skip */ }
        }
        return sb.ToString();
    }

    private static string GetPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }

    private static string Truncate(string s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "(빈 텍스트)";
        s = s.Replace("\n", " ");
        return s.Length <= n ? s : s.Substring(0, n) + "…";
    }

    // ---------------------------------------------------------------- CSV

    private void ExportCsv()
    {
        var selected = _rows.Where(r => r.Selected).ToList();
        if (selected.Count == 0) return;

        string folder = string.IsNullOrEmpty(_csvFolder) ? "Assets" : _csvFolder;
        string fullDir = Path.GetFullPath(folder);
        if (!Directory.Exists(fullDir)) Directory.CreateDirectory(fullDir);

        string file = Path.Combine(fullDir, "scene_localization.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Key,Korean (ko-KR),English (en)");
        foreach (var r in selected)
            sb.AppendLine($"{Csv(r.Key)},{Csv(r.CurrentText)},{Csv(r.English)}");

        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true)); // BOM 포함(엑셀 호환)
        AssetDatabase.Refresh();
        _status = $"CSV 저장: {file}\n{selected.Count}개 행. English 칸을 채워 String Table 에 임포트하세요.";
        Debug.Log("[SceneLocalizer] " + _status);
    }

    private static string Csv(string s)
    {
        s ??= "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    /// <summary>CSV 의 English(en) 컬럼을 Key 기준으로 읽어 각 행의 English 칸을 채운다.</summary>
    private void LoadEnglishFromCsv()
    {
        string full = Path.GetFullPath(_csvPath);
        if (!File.Exists(full))
        {
            _status = $"CSV 를 찾지 못했습니다: {full}";
            return;
        }

        var records = ParseCsv(File.ReadAllText(full));
        // 헤더(첫 행)에서 en 컬럼 인덱스 추정. 못 찾으면 3번째 컬럼.
        int keyCol = 0, enCol = 2;
        if (records.Count > 0)
        {
            var header = records[0];
            for (int i = 0; i < header.Count; i++)
            {
                string h = header[i].ToLowerInvariant();
                if (h.Contains("key")) keyCol = i;
                else if (h.Contains("(en)") || h == "english" || h.Contains("english")) enCol = i;
            }
        }

        var map = new Dictionary<string, string>();
        for (int i = 1; i < records.Count; i++)
        {
            var rec = records[i];
            if (rec.Count <= keyCol) continue;
            string key = rec[keyCol];
            string en = rec.Count > enCol ? rec[enCol] : "";
            if (!string.IsNullOrEmpty(key)) map[key] = en;
        }

        int filled = 0;
        foreach (var r in _rows)
            if (map.TryGetValue(r.Key, out var en) && !string.IsNullOrEmpty(en))
            {
                r.English = en;
                filled++;
            }

        _status = $"CSV 에서 English {filled}개 행을 채웠습니다. (총 {map.Count}개 매핑)";
        Debug.Log("[SceneLocalizer] " + _status);
        Repaint();
    }

    /// <summary>따옴표/멀티라인/이스케이프("") 를 처리하는 간단한 CSV 파서.</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* skip */ }
                else if (c == '\n')
                {
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                }
                else field.Append(c);
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    // ---------------------------------------------------------------- 항목 생성 + 연결

    private void CreateEntriesAndWire()
    {
        var selected = _rows.Where(r => r.Selected).ToList();
        if (selected.Count == 0) return;

        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
        {
            _status = $"String Table Collection '{TableName}' 을(를) 찾지 못했습니다.";
            Debug.LogError("[SceneLocalizer] " + _status);
            return;
        }

        var shared = collection.SharedData;
        var dirtyScenes = new HashSet<Scene>();
        int wired = 0;

        foreach (var r in selected)
        {
            if (r.TextComp == null) continue;
            if (string.IsNullOrEmpty(r.Key))
            {
                Debug.LogWarning($"[SceneLocalizer] Key 가 비어 건너뜀: {r.Path}");
                continue;
            }

            // 1) Shared / 각 로케일 테이블에 항목 보장
            var sharedEntry = shared.GetEntry(r.Key) ?? shared.AddKey(r.Key);
            foreach (var table in collection.StringTables)
            {
                bool isKo = table.LocaleIdentifier.Code.StartsWith(KoCodePrefix, StringComparison.OrdinalIgnoreCase);
                string val = isKo ? r.CurrentText : r.English;

                var entry = table.GetEntry(sharedEntry.Id) ?? table.AddEntry(sharedEntry.Id, "");
                // ko 는 항상 현재 텍스트로 채움. en 은 입력했을 때만 덮어씀(빈 값으로 기존 번역 지우지 않도록)
                if (isKo || !string.IsNullOrEmpty(val))
                    entry.Value = val ?? "";
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(shared);

            // 2) LocalizeStringEvent 추가 + 연결
            var evt = Undo.AddComponent<LocalizeStringEvent>(r.Go);
            var reference = new LocalizedString { TableReference = TableName, TableEntryReference = r.Key };
            evt.StringReference = reference;

            WireUpdateString(evt, r.TextComp);

            EditorUtility.SetDirty(evt);
            dirtyScenes.Add(r.Go.scene);
            wired++;
        }

        foreach (var s in dirtyScenes)
            if (s.IsValid()) EditorSceneManager.MarkSceneDirty(s);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        _status = $"완료: {wired}개 항목 생성 + 연결, 씬 저장됨.\n" +
                  "English 가 비어 있으면 String Table 에서 번역을 채우세요.";
        Debug.Log("[SceneLocalizer] " + _status);
        Scan(); // 목록 갱신(이제 연결된 것들은 사라짐)
    }

    /// <summary>OnUpdateString → 텍스트 컴포넌트의 set_text(string) 를 dynamic 으로 연결.</summary>
    private static void WireUpdateString(LocalizeStringEvent evt, Component textComp)
    {
        var setMethod = textComp.GetType().GetProperty("text")?.GetSetMethod();
        if (setMethod == null)
        {
            Debug.LogWarning($"[SceneLocalizer] text 세터를 찾지 못함: {textComp.GetType().Name}");
            return;
        }

        var action = (UnityAction<string>)Delegate.CreateDelegate(
            typeof(UnityAction<string>), textComp, setMethod);

        // 동적(EventDefined) persistent listener 등록 → set_text 가 로컬라이즈된 문자열을 받음
        UnityEventTools.AddPersistentListener(evt.OnUpdateString, action);
    }
}
