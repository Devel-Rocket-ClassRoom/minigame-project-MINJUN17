using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Bottom으로 잘못 일괄 변경된 스프라이트 pivot을 Center(0.5, 0.5)로 되돌리는 복구용.
/// 선택 우선, 없으면 Assets/Imported 전체.
///
/// 메뉴:
///   Tools/Sprites/RESTORE Pivots to Center (Selected or Assets-Imported)
///   Tools/Sprites/RESTORE Pivots to Center (Assets-Imported - All)
/// </summary>
public static class SetSpritePivotsCenter
{
    private const string kDefaultFolder = "Assets/Imported";
    private static readonly Vector2 kCenterPivot = new Vector2(0.5f, 0.5f);

    [MenuItem("Tools/Sprites/RESTORE Pivots to Center (Selected or Assets-Imported)")]
    public static void RunOnSelectionOrDefault()
    {
        Process(GetSearchRoots(useSelectionIfAny: true));
    }

    [MenuItem("Tools/Sprites/RESTORE Pivots to Center (Assets-Imported - All)")]
    public static void RunOnAllImported()
    {
        Process(new[] { kDefaultFolder });
    }

    private static string[] GetSearchRoots(bool useSelectionIfAny)
    {
        if (useSelectionIfAny)
        {
            var sel = Selection.assetGUIDs;
            if (sel != null && sel.Length > 0)
            {
                var list = new List<string>(sel.Length);
                foreach (var g in sel)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    if (!string.IsNullOrEmpty(p)) list.Add(p);
                }
                if (list.Count > 0) return list.ToArray();
            }
        }
        return new[] { kDefaultFolder };
    }

    private static void Process(string[] roots)
    {
        var texturePaths = new List<string>();
        var folderRoots = new List<string>();
        foreach (var r in roots)
        {
            if (AssetDatabase.IsValidFolder(r)) folderRoots.Add(r);
            else if (r.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".psd", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".tga", System.StringComparison.OrdinalIgnoreCase))
            {
                texturePaths.Add(r);
            }
        }
        if (folderRoots.Count > 0)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", folderRoots.ToArray());
            foreach (var g in guids)
                texturePaths.Add(AssetDatabase.GUIDToAssetPath(g));
        }

        if (texturePaths.Count == 0)
        {
            Debug.LogWarning("[SetSpritePivotsCenter] 대상 텍스처가 없음.");
            return;
        }

        int changedFiles = 0;
        int changedSprites = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string path = texturePaths[i];
                EditorUtility.DisplayProgressBar(
                    "RESTORE Sprite Pivots to Center",
                    $"{i + 1}/{texturePaths.Count}  {path}",
                    (float)i / Mathf.Max(1, texturePaths.Count));

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite) continue;

                bool fileChanged = false;

                if (importer.spriteImportMode == SpriteImportMode.Single)
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if (settings.spriteAlignment != (int)SpriteAlignment.Center ||
                        settings.spritePivot != kCenterPivot)
                    {
                        settings.spriteAlignment = (int)SpriteAlignment.Center;
                        settings.spritePivot = kCenterPivot;
                        importer.SetTextureSettings(settings);
                        fileChanged = true;
                        changedSprites++;
                    }
                }
                else if (importer.spriteImportMode == SpriteImportMode.Multiple)
                {
                    var factory = new SpriteDataProviderFactories();
                    factory.Init();
                    var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                    if (provider == null) continue;
                    provider.InitSpriteEditorDataProvider();

                    var rects = provider.GetSpriteRects();
                    bool anyChanged = false;
                    for (int r = 0; r < rects.Length; r++)
                    {
                        if (rects[r].alignment != SpriteAlignment.Center ||
                            rects[r].pivot != kCenterPivot)
                        {
                            rects[r].alignment = SpriteAlignment.Center;
                            rects[r].pivot = kCenterPivot;
                            anyChanged = true;
                            changedSprites++;
                        }
                    }
                    if (anyChanged)
                    {
                        provider.SetSpriteRects(rects);
                        provider.Apply();
                        fileChanged = true;
                    }
                }

                if (fileChanged)
                {
                    importer.SaveAndReimport();
                    changedFiles++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetSpritePivotsCenter] 완료. 변경된 파일: {changedFiles}, 변경된 스프라이트(슬라이스 포함): {changedSprites}");
    }
}
