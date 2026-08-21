#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// 한글 TMP 폰트 에셋을 만든다. (메뉴 → Tools → 한글 TMP 폰트 생성)
/// 시스템 폰트를 Assets/Fonts/ 로 복사해 Resources/Fonts/KoreanSDF.asset 을 굽는다.
/// Dynamic 아틀라스라 11,172자를 미리 굽지 않고 필요한 글자만 그때그때 올린다.
/// 먼저 Window > TextMeshPro > Import TMP Essential Resources 를 한 번 실행해야 한다.
public static class KoreanTmpFontSetup {

    private const string FontsFolder = "Assets/Fonts";
    private const string ResourcesFolder = "Assets/Resources/Fonts";
    private const string FontAssetPath = ResourcesFolder + "/KoreanSDF.asset";

    // 앞에서부터 찾아 먼저 있는 것을 쓴다 (윈도우 → macOS 순)
    private static readonly string[] CandidateFonts = {
        @"C:\Windows\Fonts\malgun.ttf",
        @"C:\Windows\Fonts\NanumGothic.ttf",
        @"C:\Windows\Fonts\gulim.ttc",
        "/System/Library/Fonts/AppleSDGothicNeo.ttc",
    };

    [MenuItem("Tools/한글 TMP 폰트 생성")]
    public static void CreateKoreanFontAsset() {
        string sourcePath = FindSystemFont();
        if (sourcePath == null) {
            EditorUtility.DisplayDialog("한글 폰트를 찾지 못했습니다",
                "시스템에서 한글 폰트를 찾지 못했어요.\n" +
                "원하는 .ttf 파일을 Assets/Fonts/ 에 직접 넣고 다시 실행해 주세요.", "확인");
            return;
        }

        EnsureFolder(FontsFolder);
        EnsureFolder("Assets/Resources");
        EnsureFolder(ResourcesFolder);

        string fileName = Path.GetFileName(sourcePath);
        string projectFontPath = FontsFolder + "/" + fileName;

        if (!File.Exists(projectFontPath)) {
            File.Copy(sourcePath, projectFontPath);
            AssetDatabase.ImportAsset(projectFontPath, ImportAssetOptions.ForceUpdate);
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(projectFontPath);
        if (sourceFont == null) {
            Debug.LogError($"[KoreanTmpFontSetup] 폰트를 불러오지 못했습니다: {projectFontPath}");
            return;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,                              // 샘플링 크기
            9,                               // 아틀라스 패딩
            GlyphRenderMode.SDFAA,
            1024, 1024,
            AtlasPopulationMode.Dynamic,
            true);                           // 아틀라스 여러 장 허용

        if (fontAsset == null) {
            Debug.LogError("[KoreanTmpFontSetup] TMP 폰트 에셋 생성에 실패했습니다.");
            return;
        }

        fontAsset.name = "KoreanSDF";

        AssetDatabase.DeleteAsset(FontAssetPath);
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0) {
            fontAsset.atlasTextures[0].name = "KoreanSDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        }
        if (fontAsset.material != null) {
            fontAsset.material.name = "KoreanSDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = fontAsset;
        Debug.Log($"[KoreanTmpFontSetup] 생성 완료: {FontAssetPath} (원본: {fileName})");
    }

    private static string FindSystemFont() {
        foreach (string path in CandidateFonts) {
            if (File.Exists(path)) return path;
        }

        if (Directory.Exists(FontsFolder)) {
            string[] ttf = Directory.GetFiles(FontsFolder, "*.ttf");
            if (ttf.Length > 0) return Path.GetFullPath(ttf[0]);
        }

        return null;
    }

    private static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
