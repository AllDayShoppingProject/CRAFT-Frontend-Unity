using UnityEngine;

/// 입구 광장 오른쪽 벽에 붙는 포토 부스. 시착 모드에서 아바타가 이 배경 앞에 선다.
/// 배경은 Resources/Textures/PhotoBackdrop 텍스처를 쓰고, 없으면 코드로 만들어 쓴다.
public static class PhotoBooth {

    private const string BACKDROP_RESOURCE = "Textures/PhotoBackdrop";

    private static readonly Color FRAME_COLOR    = new Color(0.12f, 0.12f, 0.13f, 1f);
    private static readonly Color PLATFORM_COLOR = new Color(0.20f, 0.19f, 0.19f, 1f);
    private static readonly Color KEY_LIGHT      = new Color(1f, 0.95f, 0.88f, 1f);

    public static void Build(Transform parent) {
        GameObject boothRoot = new GameObject("PhotoBooth");
        boothRoot.transform.SetParent(parent, false);

        BuildBackdrop(boothRoot.transform);
        BuildFrame(boothRoot.transform);
        BuildPlatform(boothRoot.transform);
        BuildLights(boothRoot.transform);
    }

    private static void BuildBackdrop(Transform parent) {
        Vector3 center = StoreLayout.BoothBackdropCenter;

        GameObject backdrop = CreateCube("Backdrop", parent, center,
            new Vector3(0.08f, StoreLayout.BOOTH_HEIGHT, StoreLayout.BOOTH_WIDTH));

        Material material = backdrop.GetComponent<Renderer>().material;
        material.color = Color.white;           // 틴트 제거 (텍스처 색 그대로)
        material.mainTexture = ResolveBackdropTexture();
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
    }

    private static void BuildFrame(Transform parent) {
        Vector3 center = StoreLayout.BoothBackdropCenter;
        float w = StoreLayout.BOOTH_WIDTH;
        float h = StoreLayout.BOOTH_HEIGHT;
        float bar = 0.12f;
        float depth = 0.22f;
        float x = center.x - 0.06f;

        CreateColoredCube("Frame_Top", parent,
            new Vector3(x, center.y + h / 2f + bar / 2f, center.z),
            new Vector3(depth, bar, w + bar * 2f), FRAME_COLOR);
        CreateColoredCube("Frame_Bottom", parent,
            new Vector3(x, center.y - h / 2f - bar / 2f, center.z),
            new Vector3(depth, bar, w + bar * 2f), FRAME_COLOR);

        CreateColoredCube("Frame_Front", parent,
            new Vector3(x, center.y, center.z - w / 2f - bar / 2f),
            new Vector3(depth, h, bar), FRAME_COLOR);
        CreateColoredCube("Frame_Back", parent,
            new Vector3(x, center.y, center.z + w / 2f + bar / 2f),
            new Vector3(depth, h, bar), FRAME_COLOR);
    }

    /// 아바타가 서는 낮은 단
    private static void BuildPlatform(Transform parent) {
        Vector3 stand = StoreLayout.BoothStandPosition;

        CreateColoredCube("Platform", parent,
            new Vector3(stand.x, StoreLayout.WALL_THICKNESS / 2f + 0.025f, stand.z),
            new Vector3(1.5f, 0.05f, 1.8f), PLATFORM_COLOR);
    }

    /// 인물 촬영용 키/필 라이트. 광장 조명만으로는 배경 앞 인물이 밋밋하다.
    private static void BuildLights(Transform parent) {
        Vector3 stand = StoreLayout.BoothStandPosition;
        Vector3 target = stand + Vector3.up * 1.0f;

        // 키: 한쪽으로 치우쳐 입체감을 만든다
        CreateSpot(parent, "BoothKeyLight",
            new Vector3(stand.x - 2.0f, 2.9f, stand.z - 1.3f), target,
            10f, 60f, LightShadows.Soft);

        // 필: 반대편에서 그림자를 열어준다
        CreateSpot(parent, "BoothFillLight",
            new Vector3(stand.x - 1.8f, 2.2f, stand.z + 1.6f), target,
            4.5f, 75f, LightShadows.None);
    }

    private static void CreateSpot(Transform parent, string name, Vector3 position, Vector3 target,
                                   float intensity, float angle, LightShadows shadows) {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        obj.transform.LookAt(target);

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = KEY_LIGHT;
        light.intensity = intensity;
        light.range = 7f;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.55f;
        light.shadows = shadows;
        light.shadowStrength = 0.65f;
    }

    // ---------------------------------------------------------------- 텍스처

    private static Texture2D ResolveBackdropTexture() {
        Texture2D asset = Resources.Load<Texture2D>(BACKDROP_RESOURCE);
        if (asset != null) return asset;

        Debug.Log($"[PhotoBooth] Resources/{BACKDROP_RESOURCE} 이미지가 없어 기본 배경을 생성합니다. " +
                  "원하는 이미지를 그 경로에 넣으면 자동으로 교체됩니다.");
        return GenerateBackdrop();
    }

    /// 이미지 에셋이 없을 때 쓰는 기본 배경. 중앙을 밝게, 가장자리를 어둡게 해 시선을 인물로 모은다.
    private static Texture2D GenerateBackdrop() {
        const int size = 512;

        Color deep   = new Color(0.13f, 0.11f, 0.10f);
        Color warm   = new Color(0.33f, 0.27f, 0.23f);
        Color accent = new Color(0.85f, 0.72f, 0.42f);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            name = "GeneratedBackdrop",
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++) {
            float v = y / (float)(size - 1);
            Color baseColor = Color.Lerp(deep, warm, Mathf.SmoothStep(0f, 1f, v));

            for (int x = 0; x < size; x++) {
                float u = x / (float)(size - 1);

                float dx = u - 0.5f;
                float dy = v - 0.6f;

                float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 1.85f);
                Color color = baseColor + accent * (glow * glow * 0.26f);

                float stripe = Mathf.Sin((u * 13f + v * 8f) * Mathf.PI);
                if (stripe > 0.93f) color += accent * 0.04f;

                float vignette = Mathf.Clamp01(1f - (Mathf.Abs(dx) * 1.45f + Mathf.Abs(v - 0.5f) * 1.15f));
                color *= Mathf.Lerp(0.68f, 1f, vignette);

                color.a = 1f;
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    // ---------------------------------------------------------------- 헬퍼

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale) {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        return cube;
    }

    private static void CreateColoredCube(string name, Transform parent, Vector3 position,
                                          Vector3 scale, Color color) {
        GameObject cube = CreateCube(name, parent, position, scale);

        Material material = cube.GetComponent<Renderer>().material;
        material.color = color;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.2f);
    }
}
