#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

/// Meshy AI 가방 에셋을 URP용으로 정리한다. (메뉴 → Tools → Meshy 에셋 정리)
/// 그대로 임포트하면 노멀·거칠기·메탈릭이 sRGB 컬러로 들어와 조명이 틀리고,
/// FBX 슬롯마다 잘못된 .mat이 자동 생성된다.
/// 임포트 설정 교정 → 폴더당 URP Lit 머티리얼 1개 생성 → 자동 생성 .mat 폴더 삭제.
/// 몇 번을 돌려도 결과가 같다.
public static class MeshyMaterialSetup {

    /// 경로를 박아두면 에셋을 옮길 때마다 깨지므로 프로젝트 전체를 훑는다.
    private const string SEARCH_ROOT = "Assets";

    /// p1 / p1_black / p4_cognac 형태의 가방 폴더
    private static readonly Regex FOLDER_PATTERN = new Regex(@"^p\d+(_[A-Za-z]+)?$");

    /// 가방이 아닌 Meshy 에셋은 폴더 이름이 제각각이라, FBX 유무로 판단한다.
    private const string MESHY_FBX_PATTERN = "Meshy_*.fbx";

    /// URP Lit은 거칠기 슬롯이 없다. 메탈릭=RGB, 스무스니스(=1-거칠기)=알파로 구운
    /// 한 장을 _MetallicGlossMap에 꽂아야 한다. false면 굽지 않고 아래 고정값만 쓴다.
    private const bool PACK_METALLIC_SMOOTHNESS = true;

    /// 맵을 굽지 않을 때 쓸 고정값. 매트한 가죽 기준.
    private const float FALLBACK_METALLIC = 0f;
    private const float FALLBACK_SMOOTHNESS = 0.3f;

    /// 원본이 4K~8K라 그대로 두면 VRAM이 금방 찬다.
    private const int MAX_TEXTURE_SIZE = 2048;

    private const string GENERATED_MATERIAL_FOLDER = "Materials";

    [MenuItem("Tools/Meshy 에셋 정리")]
    public static void Run() {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) {
            Debug.LogError("[MeshyMaterialSetup] URP Lit 셰이더를 찾지 못했습니다. " +
                           "이 프로젝트가 URP로 설정되어 있는지 확인해 주세요.");
            return;
        }

        List<string> folders = FindVariantFolders();
        if (folders.Count == 0) {
            Debug.LogWarning("[MeshyMaterialSetup] 정리할 폴더를 찾지 못했습니다. " +
                             "p1 / p1_black 형태이거나, Meshy_*.fbx 가 들어 있는 폴더가 대상입니다. " +
                             "(위치는 Assets 아래 어디든 상관없습니다)");
            return;
        }

        var report = new List<string>();

        // AssetDatabase.StartAssetEditing()으로 묶으면 안 된다. 임포트가 미뤄지면
        // Read/Write를 켠 텍스처의 픽셀을 즉시 읽지 못해 예외가 난다.
        try {
            for (int i = 0; i < folders.Count; i++) {
                string folder = folders[i];

                EditorUtility.DisplayProgressBar(
                    "Meshy 에셋 정리",
                    $"{Path.GetFileName(folder)}  ({i + 1}/{folders.Count})",
                    (float)i / folders.Count);

                report.Add(ProcessFolder(folder, shader));
            }
        } finally {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MeshyMaterialSetup] {folders.Count}개 폴더 정리 완료\n\n" +
                  string.Join("\n", report));
    }

    private static List<string> FindVariantFolders() {
        var folders = new List<string>();

        foreach (string directory in Directory.GetDirectories(SEARCH_ROOT, "*", SearchOption.AllDirectories)) {
            string path = directory.Replace('\\', '/');
            if (IsMeshyFolder(path)) folders.Add(path);
        }

        folders.Sort((a, b) => {
            int byName = string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b));
            return byName != 0 ? byName : string.CompareOrdinal(a, b);
        });

        return folders;
    }

    /// 가방은 폴더 이름 규칙으로, 그 외 Meshy 에셋은 내용물(FBX 유무)로 판단한다.
    /// 이름 규칙을 늘리지 않아도 새 에셋이 그냥 걸리게 하려는 것.
    private static bool IsMeshyFolder(string folder) {
        if (FOLDER_PATTERN.IsMatch(Path.GetFileName(folder))) return true;

        return Directory.GetFiles(folder, MESHY_FBX_PATTERN, SearchOption.TopDirectoryOnly).Length > 0;
    }

    /// 가방 폴더는 이름을 그대로 쓴다 — BagLibrary가 p1_black.mat 을 찾는다.
    /// 그 외에는 Meshy가 붙인 꼬리(_texture_fbx)를 뗀다.
    private static string VariantName(string folder) {
        string name = Path.GetFileName(folder);
        if (FOLDER_PATTERN.IsMatch(name)) return name;

        if (name.EndsWith("_fbx")) name = name.Substring(0, name.Length - "_fbx".Length);
        if (name.EndsWith("_texture")) name = name.Substring(0, name.Length - "_texture".Length);

        return name;
    }

    private static string ProcessFolder(string folder, Shader shader) {
        string variantName = VariantName(folder);
        MeshySet set = Collect(folder);

        if (set.baseColorPath == null) {
            return $"  {variantName,-12} 건너뜀 — 베이스 컬러 텍스처를 찾지 못했습니다";
        }

        // sRGB가 켜진 채로 픽셀을 읽으면 값이 틀어지므로 설정을 먼저 고친다.
        ConfigureTexture(set.baseColorPath, TextureImporterType.Default, sRGB: true);
        ConfigureTexture(set.normalPath, TextureImporterType.NormalMap, sRGB: false);
        ConfigureTexture(set.roughnessPath, TextureImporterType.Default, sRGB: false);
        ConfigureTexture(set.metallicPath, TextureImporterType.Default, sRGB: false);

        Material material = LoadOrCreateMaterial($"{folder}/{variantName}.mat", shader);

        material.SetTexture("_BaseMap", Load(set.baseColorPath));
        material.SetTexture("_MainTex", Load(set.baseColorPath));
        material.SetColor("_BaseColor", Color.white);

        ApplyNormalMap(material, set.normalPath);
        string surfaceNote = ApplySurfaceMaps(material, folder, variantName, set);

        EditorUtility.SetDirty(material);

        int slots = ConfigureModel(set.fbxPath);
        RemoveGeneratedMaterials(folder);

        return $"  {variantName,-12} 머티리얼 1개 · 메시 슬롯 {slots}개 · {surfaceNote}";
    }

    /// Meshy가 붙인 접미사(_normal / _roughness / _metallic)로 역할을 구분한다.
    private struct MeshySet {
        public string fbxPath;
        public string baseColorPath;
        public string normalPath;
        public string roughnessPath;
        public string metallicPath;
    }

    private static MeshySet Collect(string folder) {
        var set = new MeshySet();

        // 자동 생성된 Materials 하위 폴더가 딸려 오지 않도록 직속만 본다.
        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)) {
            string path = file.Replace('\\', '/');

            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path).ToLowerInvariant();

            if (extension == ".fbx") {
                set.fbxPath = path;
                continue;
            }
            if (extension != ".png" && extension != ".jpg" && extension != ".tga") continue;

            // 우리가 구운 결과물을 다시 입력으로 삼으면 안 된다
            if (name.EndsWith("_MetallicSmoothness")) continue;

            if (name.EndsWith("_normal")) set.normalPath = path;
            else if (name.EndsWith("_roughness")) set.roughnessPath = path;
            else if (name.EndsWith("_metallic")) set.metallicPath = path;
            else set.baseColorPath = path;
        }

        return set;
    }

    /// 매번 새로 만들면 씬·프리팹이 물고 있던 참조가 끊기므로 있으면 재사용한다.
    private static Material LoadOrCreateMaterial(string path, Shader shader) {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null) {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        if (material.shader != shader) material.shader = shader;
        return material;
    }

    private static void ApplyNormalMap(Material material, string normalPath) {
        Texture2D normal = Load(normalPath);

        material.SetTexture("_BumpMap", normal);

        // _NORMALMAP 키워드를 안 켜면 셰이더가 노멀 계산을 통째로 건너뛴다.
        // 인스펙터로 넣을 땐 유니티가 켜주지만 코드로 넣을 땐 직접 켜야 한다.
        if (normal != null) {
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        } else {
            material.DisableKeyword("_NORMALMAP");
        }
    }

    /// 합친 맵을 물리고, 실패하면 고정값으로 떨어진다.
    private static string ApplySurfaceMaps(Material material, string folder,
                                           string variantName, MeshySet set) {
        Texture2D packed = null;

        if (PACK_METALLIC_SMOOTHNESS && (set.metallicPath != null || set.roughnessPath != null)) {
            packed = BuildMetallicSmoothness(folder, variantName, set);
        }

        if (packed != null) {
            material.SetTexture("_MetallicGlossMap", packed);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

            // 맵이 있으면 이 둘은 맵 값에 곱해지는 배율이 된다. 1이 곧 "맵 그대로".
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);

            // 스무스니스를 알베도 알파가 아니라 이 맵의 알파에서 읽으라는 뜻 (0 = 메탈릭 알파)
            material.SetFloat("_SmoothnessTextureChannel", 0f);

            return "메탈릭·스무스니스 맵 적용";
        }

        material.SetTexture("_MetallicGlossMap", null);
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Metallic", FALLBACK_METALLIC);
        material.SetFloat("_Smoothness", FALLBACK_SMOOTHNESS);

        return $"스무스니스 {FALLBACK_SMOOTHNESS} 고정";
    }

    /// URP Lit이 거칠기 맵을 따로 못 받으므로, 메탈릭=RGB / 스무스니스=알파로 구워 저장한다.
    /// 거칠기와 스무스니스는 정확히 반대 개념이라 1-r 로 반전해 넣는다.
    private static Texture2D BuildMetallicSmoothness(string folder, string variantName, MeshySet set) {
        Texture2D metallic = LoadReadable(set.metallicPath);
        Texture2D roughness = LoadReadable(set.roughnessPath);

        if (metallic == null && roughness == null) return null;

        // 큰 쪽에 맞춘다. UV 비율로 샘플링하므로 두 맵 크기가 달라도 된다.
        int width = Mathf.Max(metallic != null ? metallic.width : 0,
                              roughness != null ? roughness.width : 0);
        int height = Mathf.Max(metallic != null ? metallic.height : 0,
                               roughness != null ? roughness.height : 0);

        if (width <= 0 || height <= 0) return null;

        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++) {
            float v = (y + 0.5f) / height;

            for (int x = 0; x < width; x++) {
                float u = (x + 0.5f) / width;

                float m = metallic != null ? metallic.GetPixelBilinear(u, v).r : 0f;
                float r = roughness != null ? roughness.GetPixelBilinear(u, v).r : 1f - FALLBACK_SMOOTHNESS;

                pixels[y * width + x] = new Color(m, m, m, 1f - r);
            }
        }

        // linear: true — 색이 아니라 숫자 데이터라 감마 보정을 타면 안 된다
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.SetPixels(pixels);
        texture.Apply();

        string path = $"{folder}/{variantName}_MetallicSmoothness.png";
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        // 켜뒀던 Read/Write를 되돌린다. 켜둔 채면 CPU 메모리에도 남아 용량이 두 배가 된다.
        RestoreReadable(set.metallicPath);
        RestoreReadable(set.roughnessPath);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureTexture(path, TextureImporterType.Default, sRGB: false, alphaIsTransparency: false);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void ConfigureTexture(string path, TextureImporterType type, bool sRGB,
                                         bool alphaIsTransparency = true) {
        if (path == null) return;
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;

        bool changed = false;

        if (importer.textureType != type) {
            importer.textureType = type;
            changed = true;
        }

        // 노멀맵은 유니티가 sRGB를 알아서 끈다
        if (type != TextureImporterType.NormalMap && importer.sRGBTexture != sRGB) {
            importer.sRGBTexture = sRGB;
            changed = true;
        }

        // 노멀맵에는 의미가 없는 설정이다
        if (type == TextureImporterType.Default && importer.alphaIsTransparency != alphaIsTransparency) {
            importer.alphaIsTransparency = alphaIsTransparency;
            changed = true;
        }

        if (importer.maxTextureSize > MAX_TEXTURE_SIZE) {
            importer.maxTextureSize = MAX_TEXTURE_SIZE;
            changed = true;
        }

        if (changed) importer.SaveAndReimport();
    }

    /// 픽셀을 읽으려면 Read/Write가 켜져 있어야 한다.
    private static Texture2D LoadReadable(string path) {
        if (path == null) return null;

        if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.isReadable) {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void RestoreReadable(string path) {
        if (path == null) return;

        if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.isReadable) {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }

    /// FBX 쪽 머티리얼 자동 생성을 끈다 (런타임에 직접 입히므로 쓰레기 .mat만 늘어난다).
    /// 반환값은 메시의 머티리얼 슬롯 개수.
    private static int ConfigureModel(string fbxPath) {
        if (fbxPath == null) return 0;
        if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter importer)) return 0;

        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null) return 0;

        int slots = 0;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) {
            slots += renderer.sharedMaterials.Length;
        }

        return slots;
    }

    /// 임포터가 만든 잘못된 머티리얼 폴더를 통째로 지운다.
    private static void RemoveGeneratedMaterials(string folder) {
        string path = $"{folder}/{GENERATED_MATERIAL_FOLDER}";
        if (AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
    }

    private static Texture2D Load(string path) {
        return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
#endif
