using System.Collections.Generic;
using UnityEngine;

/// 가방 3D 모델을 그 자리에서 찍어 2D 이미지(RenderTexture)로 돌려준다.
/// 매장과 같은 FBX·머티리얼을 그대로 렌더하므로 컬러가 늘어도 3D와 2D가 어긋나지 않는다.
/// 촬영 세트는 y = -1000. 메인 카메라 far clip(1000) 밖이라 관람 화면에는 보이지 않는다.
public static class ProductShot {

    private const int TEXTURE_SIZE = 512;

    private static readonly Vector3 STAGE_ORIGIN = new Vector3(0f, -1000f, 0f);

    private static readonly Vector3 CAMERA_EULER = new Vector3(8f, 180f, 0f);

    /// 가방이 화면을 채우는 비율. 1이면 꽉 차서 답답하다.
    private const float FILL_RATIO = 0.82f;

    private static Transform stage;
    private static Camera shotCamera;

    /// 촬영용 3점 조명. 평소엔 꺼두고 셔터를 누르는 순간에만 켠다.
    private static readonly List<Light> shotLights = new List<Light>();

    /// 제품+컬러 조합별 캐시. 같은 조합을 두 번 찍지 않는다.
    private static readonly Dictionary<string, RenderTexture> cache =
        new Dictionary<string, RenderTexture>();

    /// 이 제품·컬러의 사진. 모델이 없으면 null.
    public static Texture Get(int productIndex, string colorCode) {
        GameObject source = BagLibrary.GetVariantModel(productIndex, colorCode);
        if (source == null) return null;

        string key = $"{productIndex}:{colorCode}";
        if (cache.TryGetValue(key, out RenderTexture cached) && cached != null) return cached;

        EnsureStage();

        RenderTexture texture = Render(source, productIndex, colorCode);
        cache[key] = texture;

        return texture;
    }

    /// 세션 종료·라이브러리 갱신 때 호출. 안 부르면 세션 내내 남는다.
    public static void ClearCache() {
        foreach (RenderTexture texture in cache.Values) {
            if (texture != null) texture.Release();
        }
        cache.Clear();
    }

    // ---------------------------------------------------------------- 촬영 세트

    private static void EnsureStage() {
        if (stage != null && shotCamera != null) return;

        GameObject root = new GameObject("ProductShotStage");
        root.transform.position = STAGE_ORIGIN;
        Object.DontDestroyOnLoad(root);

        stage = root.transform;

        GameObject cameraObject = new GameObject("ShotCamera");
        cameraObject.transform.SetParent(stage, false);

        shotCamera = cameraObject.AddComponent<Camera>();
        shotCamera.orthographic = true;
        shotCamera.nearClipPlane = 0.01f;
        shotCamera.farClipPlane = 5f;

        // 배경 투명 — 어떤 색 패널 위에 올려도 어울린다
        shotCamera.clearFlags = CameraClearFlags.SolidColor;
        shotCamera.backgroundColor = new Color(1f, 1f, 1f, 0f);

        // 카메라는 꺼두고 필요할 때만 수동으로 Render()를 부른다.
        shotCamera.enabled = false;

        CreateLights(cameraObject.transform);
    }

    /// 제품 사진용 3점 조명. 매장 조명이 여기까지 닿지 않는다.
    ///
    /// Directional 라이트는 위치를 완전히 무시하고 씬 전체를 비춘다.
    /// 촬영 세트를 y = -1000 으로 치워도, cullingMask 를 ~0(전체 레이어)으로 둬도 소용없다.
    /// 켜둔 채로 두면 사전 예약 창을 여는 순간 이 셋(합 3.1)이 매장에 더해져서
    /// 벽과 천장이 하얗게 날아간다. 그래서 평소엔 꺼두고 Render() 순간에만 켠다.
    private static void CreateLights(Transform cameraTransform) {
        CreateLight(cameraTransform, "Key",  new Vector3(-25f, 20f, 0f), 1.5f);
        CreateLight(cameraTransform, "Fill", new Vector3(10f, -35f, 0f), 0.7f);
        CreateLight(cameraTransform, "Rim",  new Vector3(-10f, 160f, 0f), 0.9f);
    }

    private static void CreateLight(Transform parent, string name, Vector3 euler, float intensity) {
        GameObject obj = new GameObject($"ShotLight_{name}");
        obj.transform.SetParent(parent, false);
        obj.transform.localRotation = Quaternion.Euler(euler);

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.98f, 0.95f, 1f);
        light.intensity = intensity;
        light.shadows = LightShadows.None;

        // 꺼진 채로 만든다. SetLightsEnabled(true) 는 셔터를 누르는 그 순간에만 부른다.
        light.enabled = false;

        shotLights.Add(light);
    }

    private static void SetLightsEnabled(bool enabled) {
        for (int i = 0; i < shotLights.Count; i++) {
            if (shotLights[i] != null) shotLights[i].enabled = enabled;
        }
    }

    // ---------------------------------------------------------------- 촬영

    private static RenderTexture Render(GameObject source, int productIndex, string colorCode) {
        GameObject model = Object.Instantiate(source, stage);
        model.name = "ShotModel";

        // 전시대와 같은 보정을 거쳐야 실제로 보는 자세와 같아진다
        BagModelEntry entry = BagLibrary.GetModelEntry(productIndex);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = entry != null ? entry.Rotation : Quaternion.identity;

        BagModelUtil.FitToHeight(model, 1f);
        BagLibrary.ApplyVariantMaterial(model.transform, productIndex, colorCode);

        // 콜라이더가 남으면 매장 쪽 레이캐스트에 잡힐 수 있다
        foreach (Collider collider in model.GetComponentsInChildren<Collider>()) {
            Object.DestroyImmediate(collider);
        }

        FrameModel(model);

        RenderTexture texture = new RenderTexture(TEXTURE_SIZE, TEXTURE_SIZE, 24,
                                                  RenderTextureFormat.ARGB32) {
            name = $"ProductShot_{productIndex}_{colorCode}",
            antiAliasing = 4,
        };

        // Render()는 동기 호출이라, 이 사이에만 조명이 켜져 있으면 된다.
        // 예외가 나도 조명이 켜진 채 남지 않도록 finally 로 끈다.
        shotCamera.targetTexture = texture;
        try {
            SetLightsEnabled(true);
            shotCamera.Render();
        } finally {
            SetLightsEnabled(false);
            shotCamera.targetTexture = null;
        }

        Object.DestroyImmediate(model);

        return texture;
    }

    private static void FrameModel(GameObject model) {
        if (!BagModelUtil.TryGetWorldBounds(model, out Bounds bounds)) return;

        // 정사각 이미지라 가로/세로 중 큰 쪽 기준
        float extent = Mathf.Max(bounds.size.x, bounds.size.y) * 0.5f;

        shotCamera.orthographicSize = Mathf.Max(0.05f, extent / FILL_RATIO);

        shotCamera.transform.rotation = Quaternion.Euler(CAMERA_EULER);
        shotCamera.transform.position =
            bounds.center - shotCamera.transform.forward * 2f;
    }
}
