#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

/// 흩어진 가방 에셋 폴더(p1_black, p2 ...)를 Assets/Bags 아래로 모은다.
/// (메뉴 → Tools → 가방 에셋 폴더 정리)
///
/// 탐색기로 옮기면 .meta 가 어긋나 GUID가 새로 생기고 BagLibrary 참조가 전부 Missing 이 된다.
/// AssetDatabase.MoveAsset 은 .meta 를 함께 옮겨 GUID를 유지하므로 참조가 살아남는다.
public static class BagAssetOrganizer {

    private const string DESTINATION = "Assets/Bags";

    /// p1 / p1_black / p4_cognac — BagLibraryBuilder와 같은 규칙
    private static readonly Regex FOLDER_PATTERN = new Regex(@"^p\d+(_[A-Za-z]+)?$");

    [MenuItem("Tools/가방 에셋 폴더 정리")]
    public static void Organize() {
        List<string> folders = FindVariantFolders();

        if (folders.Count == 0) {
            Debug.LogWarning("[BagAssetOrganizer] p1 / p1_black 형태의 폴더를 찾지 못했습니다.");
            return;
        }

        EnsureFolder(DESTINATION);

        var moved = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();

        foreach (string source in folders) {
            string name = Path.GetFileName(source);
            string target = $"{DESTINATION}/{name}";

            if (source == target) {
                skipped.Add($"  {name,-12} 이미 제자리");
                continue;
            }

            if (AssetDatabase.IsValidFolder(target)) {
                failed.Add($"  {name,-12} {target} 이 이미 있습니다 — 손으로 확인해 주세요");
                continue;
            }

            // 빈 문자열이면 옮겨도 된다는 뜻
            string problem = AssetDatabase.ValidateMoveAsset(source, target);
            if (!string.IsNullOrEmpty(problem)) {
                failed.Add($"  {name,-12} {problem}");
                continue;
            }

            string error = AssetDatabase.MoveAsset(source, target);

            if (string.IsNullOrEmpty(error)) moved.Add($"  {name,-12} {source}  →  {target}");
            else failed.Add($"  {name,-12} {error}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var report = new List<string>();
        if (moved.Count > 0) { report.Add($"옮김 {moved.Count}개"); report.AddRange(moved); }
        if (skipped.Count > 0) { report.Add($"건너뜀 {skipped.Count}개"); report.AddRange(skipped); }

        Debug.Log($"[BagAssetOrganizer] 정리 완료\n\n{string.Join("\n", report)}\n\n" +
                  "GUID가 유지되므로 BagLibrary의 참조는 그대로입니다. " +
                  "확인 삼아 Tools > 가방 라이브러리 갱신 을 한 번 돌려 주세요.");

        if (failed.Count > 0) {
            Debug.LogError($"[BagAssetOrganizer] 옮기지 못한 폴더 {failed.Count}개\n\n" +
                           string.Join("\n", failed));
        }

        Object destination = AssetDatabase.LoadAssetAtPath<Object>(DESTINATION);
        if (destination != null) Selection.activeObject = destination;
    }

    /// 목적지(Assets/Bags) 안의 폴더도 걸리지만 위에서 '이미 제자리'로 걸러진다.
    private static List<string> FindVariantFolders() {
        var folders = new List<string>();

        foreach (string directory in Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories)) {
            string path = directory.Replace('\\', '/');
            if (FOLDER_PATTERN.IsMatch(Path.GetFileName(path))) folders.Add(path);
        }

        folders.Sort(string.CompareOrdinal);
        return folders;
    }

    private static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);

        // Assets 위로는 유니티가 관리하지 않으므로 멈춘다
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
