using System;
using UnityEngine;

/// 한 제품의 컬러 변형 하나. (예: p1_black)
/// 색마다 FBX가 따로라 컬러를 바꾸면 모델을 통째로 교체한다.
[Serializable]
public class BagVariantEntry {
    [Tooltip("컬러 코드. BagColorState와 로그가 이 값으로 색을 식별한다")]
    public string code;

    [Tooltip("패널에 보이는 이름")]
    public string displayName;

    [Tooltip("스와치 칩 색. 텍스처를 못 읽을 때만 쓰인다")]
    public string hex;

    [Tooltip("이 컬러의 FBX")]
    public GameObject model;

    [Tooltip("이 컬러의 머티리얼 (Tools > Meshy 에셋 정리가 만든 것)")]
    public Material material;
}

[Serializable]
public class BagModelEntry {

    [Tooltip("에셋 폴더 접두어. p1_black / p1_cognac 이면 여기는 p1")]
    public string productKey;

    [Tooltip("이 제품이 가진 컬러들. 첫 번째가 기본값이다")]
    public BagVariantEntry[] variants = Array.Empty<BagVariantEntry>();

    [Tooltip("기본 모델 = variants[0].model. 자세 잡기 도구와 시착이 이 값을 쓴다")]
    public GameObject prefab;

    /// Blender·Meshy는 Z축이 위인 좌표계로 내보내고, 유니티는 Y축이 위다.
    /// FBX 임포터의 Bake Axis Conversion을 끈 상태라 그 차이를 여기서 되돌린다.
    /// 이 값이 0이면 가방이 뒤로 누운 채 전시된다.
    public static readonly Vector3 DEFAULT_ROTATION = new Vector3(270f, 0f, 0f);

    [Header("전시대 위")]
    [Tooltip("모델이 누워 있거나 뒤를 보고 있을 때 보정할 회전. " +
             "기본값 (270, 0, 0)은 Z-up으로 내보낸 모델을 세우는 보정이다")]
    public Vector3 rotationEuler = new Vector3(270f, 0f, 0f);

    [Tooltip("이 가방만 조금 크게/작게 보이고 싶을 때")]
    public float heightMultiplier = 1f;

    // 시착은 몸 쪽 점(anchorPosition)과 가방 쪽 점(gripPoint)을 겹쳐 놓는 방식이다.
    // 가방 원점은 FBX마다 제각각이라, 원점을 그냥 올려두면 몸을 뚫거나 공중에 뜬다.
    [Header("시착 - 아바타에 거는 위치")]

    [Tooltip("몸 쪽 고정점. 아바타 기준 좌표(키 1.0m 기준)다.\n" +
             "등 상단 / 어깨 위 / 손 등 자유롭게 찍는다. 신장이 바뀌면 이 점도 몸을 따라 움직인다")]
    public Vector3 anchorPosition = Vector3.zero;

    [Tooltip("가방 쪽 접점. 가방 크기 대비 비율(0~1)이라 신장과 무관하다.\n" +
             "(0.5, 1, 0.5) = 가방 윗면 한가운데 = 멜빵/손잡이 꼭대기")]
    public Vector3 gripPoint = new Vector3(0.5f, 1f, 0.5f);

    [Tooltip("걸었을 때의 회전")]
    public Vector3 holdRotation = Vector3.zero;

    public Quaternion Rotation => Quaternion.Euler(rotationEuler);
    public Quaternion HoldRotation => Quaternion.Euler(holdRotation);

    /// 컬러 코드로 변형을 찾는다. 못 찾으면 첫 번째(기본).
    public BagVariantEntry FindVariant(string code) {
        if (variants == null || variants.Length == 0) return null;

        if (!string.IsNullOrEmpty(code)) {
            foreach (BagVariantEntry variant in variants) {
                if (variant != null && variant.code == code) return variant;
            }
        }

        return variants[0];
    }
}

/// 가방 모델(FBX)과 컬러 머티리얼을 참조로 들고 있는 에셋.
/// FBX/머티리얼이 Resources 밖에 있어 Resources.Load로 못 부른다. 이 에셋만 Resources에 두고 참조한다.
/// 생성/갱신: 메뉴 Tools > 가방 라이브러리 갱신
public class BagLibrary : ScriptableObject {

    public const string RESOURCE_PATH = "BagLibrary";

    [Tooltip("Assets/Materials/p1_black 형태의 폴더에서 모은 제품 목록")]
    public BagModelEntry[] bagModels = Array.Empty<BagModelEntry>();

    private static BagLibrary cached;
    private static bool loadAttempted;

    public static BagLibrary Instance {
        get {
            if (cached == null && !loadAttempted) {
                loadAttempted = true;
                cached = Resources.Load<BagLibrary>(RESOURCE_PATH);

                if (cached == null) {
                    Debug.LogWarning("[BagLibrary] Resources/BagLibrary.asset 이 없습니다. " +
                                     "메뉴의 Tools > 가방 라이브러리 갱신 을 한 번 실행해 주세요. " +
                                     "그때까지는 큐브 자리표시자로 동작합니다.");
                }
            }
            return cached;
        }
    }

    public static bool HasModels => Instance != null && Instance.bagModels.Length > 0;

    /// 스툴 인덱스(0~5)에 해당하는 가방. 개수가 모자라면 순환한다.
    public static BagModelEntry GetModelEntry(int index) {
        if (!HasModels) return null;

        BagModelEntry[] models = Instance.bagModels;
        BagModelEntry entry = models[((index % models.Length) + models.Length) % models.Length];

        return entry != null && entry.prefab != null ? entry : null;
    }

    public static GameObject GetModel(int index) {
        BagModelEntry entry = GetModelEntry(index);
        return entry != null ? entry.prefab : null;
    }

    // ------------------------------------------------------------------ 컬러 변형

    /// 이 제품이 가진 컬러 목록 (제품마다 1~3종).
    public static BagVariantEntry[] GetVariants(int index) {
        BagModelEntry entry = GetModelEntry(index);
        return entry != null && entry.variants != null
            ? entry.variants
            : Array.Empty<BagVariantEntry>();
    }

    public static BagVariantEntry GetVariant(int index, string code) {
        BagModelEntry entry = GetModelEntry(index);
        return entry != null ? entry.FindVariant(code) : null;
    }

    /// 이 컬러의 FBX. 못 찾으면 기본 모델로 떨어진다.
    public static GameObject GetVariantModel(int index, string code) {
        BagVariantEntry variant = GetVariant(index, code);

        if (variant != null && variant.model != null) return variant.model;
        return GetModel(index);
    }

    public static Material GetVariantMaterial(int index, string code) {
        BagVariantEntry variant = GetVariant(index, code);
        return variant != null ? variant.material : null;
    }

    /// 스와치 칩에 띄울 텍스처. 없으면 UI가 hex 단색으로 떨어진다.
    public static Texture GetVariantTexture(int index, string code) {
        Material material = GetVariantMaterial(index, code);
        return material != null ? material.mainTexture : null;
    }

    /// 모델에 이 컬러의 머티리얼을 입힌다.
    /// FBX 임포트에서 머티리얼 자동 생성을 꺼놔서, 이걸 안 하면 기본 회색으로 나온다.
    public static bool ApplyVariantMaterial(Transform bagRoot, int index, string code) {
        Material material = GetVariantMaterial(index, code);
        if (bagRoot == null || material == null) return false;

        foreach (Renderer renderer in bagRoot.GetComponentsInChildren<Renderer>(true)) {
            int slots = renderer.sharedMaterials.Length;

            if (slots <= 1) {
                renderer.sharedMaterial = material;
                continue;
            }

            Material[] materials = new Material[slots];
            for (int i = 0; i < slots; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }

        return true;
    }
}
