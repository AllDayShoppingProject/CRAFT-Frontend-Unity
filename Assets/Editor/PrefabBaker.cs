#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// 코드로 만들던 오브젝트를 프리팹 에셋으로 굽는다.
/// 프리팹이 있으면 GalleryController가 CreatePrimitive 대신 이걸 Instantiate 한다.
/// (없으면 코드로 큐브를 만들므로 언제든 되돌릴 수 있다)
public static class PrefabBaker {

    private const string PrefabFolder = "Assets/Resources/Prefabs";
    private const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/프리팹 굽기/스툴 · 가방")]
    public static void BakeProductPrefabs() {
        EnsureFolder("Assets/Resources");
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        GameObject stool = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stool.name = "Stool";
        stool.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
        stool.tag = "Stool";
        AssignMaterial(stool, "StoolMat", new Color(0.90f, 0.89f, 0.87f, 1f), 0.35f);
        SavePrefab(stool, PrefabFolder + "/Stool.prefab");

        GameObject bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bag.name = "Bag";
        bag.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
        bag.tag = "Bag";
        AssignMaterial(bag, "BagMat", new Color(0.60f, 0.10f, 0.10f, 1f), 0.45f);
        bag.AddComponent<BagRotator>();
        SavePrefab(bag, PrefabFolder + "/Bag.prefab");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PrefabBaker] Stool.prefab / Bag.prefab 생성 완료. " +
                  "이제 프리팹을 열어 메시나 머티리얼을 바꾸면 코드 수정 없이 반영됩니다.");
    }

    [MenuItem("Tools/프리팹 굽기/가방 상세 UI")]
    public static void BakeBagViewUIPrefab() {
        EnsureFolder("Assets/Resources");
        EnsureFolder(PrefabFolder);

        GameObject go = new GameObject("BagViewUI");
        BagViewUI ui = go.AddComponent<BagViewUI>();

        // 에디트 모드에서는 Awake가 돌지 않으므로 직접 계층을 만든다.
        ui.BuildHierarchy();

        SavePrefab(go, PrefabFolder + "/BagViewUI.prefab");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PrefabBaker] BagViewUI.prefab 생성 완료. " +
                  "프리팹을 열어 색/여백/폰트 크기를 자유롭게 고치면 됩니다. " +
                  "버튼 동작은 실행 시 코드에서 다시 연결되므로 건드리지 않아도 됩니다.");
    }

    [MenuItem("Tools/프리팹 굽기/전부")]
    public static void BakeAll() {
        BakeProductPrefabs();
        BakeBagViewUIPrefab();
    }

    private static void AssignMaterial(GameObject target, string materialName, Color color, float smoothness) {
        string path = MaterialFolder + "/" + materialName + ".mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    private static void SavePrefab(GameObject source, string path) {
        AssetDatabase.DeleteAsset(path);
        PrefabUtility.SaveAsPrefabAsset(source, path);
        Object.DestroyImmediate(source);
    }

    private static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
