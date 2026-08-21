#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

/// `p1_black` 형태의 폴더를 훑어 Resources/BagLibrary.asset 을 만들거나 갱신한다.
/// 폴더 이름이 곧 데이터다 (p1_black / p1_cognac → 제품 p1의 컬러 2종).
///
/// FBX·머티리얼은 Resources 밖이라 런타임에 직접 못 불러오지만,
/// 이 에셋만 Resources에 두면 참조를 타고 접근할 수 있다.
/// 손으로 맞춰둔 회전·시착 자세는 제품 키(p1)로 찾아 그대로 살린다.
public static class BagLibraryBuilder {

    private const string LibraryPath = "Assets/Resources/BagLibrary.asset";

    // 경로를 박아두면 에셋을 옮길 때마다 깨지므로 프로젝트 전체를 훑는다.
    private const string SearchRoot = "Assets";

    /// p1 / p1_black / p4_cognac
    private static readonly Regex FolderPattern = new Regex(@"^p(\d+)(?:_([A-Za-z]+))?$");

    /// 폴더 이름의 컬러 접미사 → (코드, 표시 이름, 칩 색)
    /// 코드는 서버 colors[].color 및 이벤트 로그의 color 필드와 같아야 해서,
    /// 백엔드가 쓰는 영문 소문자 폴더 접미사를 그대로 코드로 삼는다.
    private static readonly Dictionary<string, (string code, string name, string hex)> ColorTable =
        new Dictionary<string, (string, string, string)> {
            { "black",     ("black",    "블랙",     "#1C1C1E") },
            { "cognac",    ("cognac",   "코냑",     "#8C5A2B") },
            { "green",     ("green",    "그린",     "#2F4A38") },
            { "white",     ("white",    "화이트",   "#F2F0EA") },
            { "ivory",     ("ivory",    "아이보리", "#EDE6D8") },
            { "lotus",     ("lotus",    "로터스",   "#D9C7C0") },
            { "navy",      ("navy",     "네이비",   "#22304A") },
            { "burgundy",  ("burgundy", "버건디",   "#5E1F2B") },
            { "camel",     ("camel",    "카멜",     "#B08654") },
            { "olive",     ("olive",    "올리브",   "#5A5C3A") },
            { "grey",      ("grey",     "그레이",   "#8A8A8E") },
            { "gray",      ("grey",     "그레이",   "#8A8A8E") },
            { "taupe",     ("taupe",    "토프",     "#8B7F72") },
            { "cherry",    ("cherry",   "체리",     "#8E1F2F") },
            { "kaki",      ("kaki",     "카키",     "#6B6440") },
            { "khaki",     ("kaki",     "카키",     "#6B6440") },
        };

    /// 컬러 접미사가 없는 폴더(p2, p3 ...)에 붙일 값
    private static readonly (string code, string name, string hex) SingleColor =
        ("original", "오리지널", "#8C5A2B");

    [MenuItem("Tools/가방 라이브러리 갱신")]
    public static void Build() {
        EnsureFolder("Assets/Resources");

        BagLibrary library = AssetDatabase.LoadAssetAtPath<BagLibrary>(LibraryPath);
        bool isNew = library == null;

        if (isNew) library = ScriptableObject.CreateInstance<BagLibrary>();

        library.bagModels = BuildEntries(library.bagModels);

        if (isNew) AssetDatabase.CreateAsset(library, LibraryPath);

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = library;

        var lines = new List<string>();
        foreach (BagModelEntry entry in library.bagModels) {
            var names = new List<string>();
            foreach (BagVariantEntry variant in entry.variants) names.Add(variant.displayName);

            lines.Add($"  {entry.productKey,-4} 컬러 {entry.variants.Length}종 — {string.Join(", ", names)}");
        }

        Debug.Log($"[BagLibraryBuilder] 제품 {library.bagModels.Length}개 등록 완료 → {LibraryPath}\n\n" +
                  string.Join("\n", lines));

        if (library.bagModels.Length == 0) {
            Debug.LogWarning("[BagLibraryBuilder] p1 / p1_black 형태의 폴더를 찾지 못했습니다. " +
                             "폴더 이름 규칙을 확인해 주세요. (위치는 Assets 아래 어디든 상관없습니다)");
        }
    }

    private static BagModelEntry[] BuildEntries(BagModelEntry[] existing) {
        var grouped = new SortedDictionary<int, List<BagVariantEntry>>();
        var keyNames = new SortedDictionary<int, string>();

        foreach (string folder in FindVariantFolders()) {
            string folderName = Path.GetFileName(folder);

            Match match = FolderPattern.Match(folderName);
            if (!match.Success) continue;

            if (!int.TryParse(match.Groups[1].Value, out int number)) continue;

            BagVariantEntry variant = BuildVariant(folder, match.Groups[2].Value);
            if (variant == null) continue;

            if (!grouped.TryGetValue(number, out List<BagVariantEntry> list)) {
                list = new List<BagVariantEntry>();
                grouped[number] = list;
                keyNames[number] = $"p{number}";
            }

            list.Add(variant);
        }

        var entries = new List<BagModelEntry>();

        foreach (KeyValuePair<int, List<BagVariantEntry>> pair in grouped) {
            string productKey = keyNames[pair.Key];

            // 손으로 맞춰둔 자세를 잃지 않도록 기존 항목을 이어받는다
            BagModelEntry entry = FindExisting(existing, productKey) ?? new BagModelEntry();

            entry.productKey = productKey;
            entry.variants = pair.Value.ToArray();
            entry.prefab = entry.variants[0].model;

            entries.Add(entry);
        }

        return entries.ToArray();
    }

    /// 폴더 이름순 정렬이라 에셋을 어디로 옮기든 컬러 순서가 흔들리지 않는다.
    private static List<string> FindVariantFolders() {
        var folders = new List<string>();

        foreach (string directory in Directory.GetDirectories(SearchRoot, "*", SearchOption.AllDirectories)) {
            string path = directory.Replace('\\', '/');
            if (FolderPattern.IsMatch(Path.GetFileName(path))) folders.Add(path);
        }

        folders.Sort((a, b) => {
            int byName = string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b));
            return byName != 0 ? byName : string.CompareOrdinal(a, b);
        });

        return folders;
    }

    /// 폴더 하나 = 컬러 하나.
    private static BagVariantEntry BuildVariant(string folder, string colorSuffix) {
        GameObject model = FindFirst<GameObject>(folder, "*.fbx");
        if (model == null) return null;

        (string code, string name, string hex) color = ResolveColor(colorSuffix);

        return new BagVariantEntry {
            code = color.code,
            displayName = color.name,
            hex = color.hex,
            model = model,
            material = FindFirst<Material>(folder, "*.mat"),
        };
    }

    private static (string code, string name, string hex) ResolveColor(string suffix) {
        if (string.IsNullOrEmpty(suffix)) return SingleColor;

        string key = suffix.ToLowerInvariant();
        if (ColorTable.TryGetValue(key, out (string, string, string) known)) return known;

        // 표에 없는 색은 폴더 접미사를 그대로 쓴다
        return (key, suffix, "#8A8A8E");
    }

    /// 임포터가 만든 Materials 폴더가 딸려 들어오지 않도록 폴더 직속만 본다.
    private static T FindFirst<T>(string folder, string pattern) where T : Object {
        string[] files = Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly);
        System.Array.Sort(files, string.CompareOrdinal);

        foreach (string file in files) {
            T asset = AssetDatabase.LoadAssetAtPath<T>(file.Replace('\\', '/'));
            if (asset != null) return asset;
        }

        return null;
    }

    private static BagModelEntry FindExisting(BagModelEntry[] existing, string productKey) {
        if (existing == null) return null;

        foreach (BagModelEntry entry in existing) {
            if (entry != null && entry.productKey == productKey) return entry;
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
