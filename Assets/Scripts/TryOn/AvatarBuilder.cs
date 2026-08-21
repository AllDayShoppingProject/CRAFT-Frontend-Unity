using UnityEngine;

/// 더미 인형(아바타)을 프리미티브로 만드는 자리표시자.
/// 키 1.0m 기준으로 만들고 루트 스케일로 신장을 맞춘다. 가방 위치도 같은 기준 좌표라
/// 신장이 달라도 몸의 같은 지점에 붙는다. (애니메이션 없는 고정 더미라 본 참조는 두지 않는다)
public static class AvatarBuilder {

    private static readonly Color BODY_COLOR = new Color(0.82f, 0.80f, 0.78f, 1f);

    /// 에디터에서 눈금처럼 보여주는 참고 지점 (키 1.0m 기준, 오른쪽). 실제로 붙는 자리는 아니다.
    public static readonly (string name, Vector3 position)[] LANDMARKS = {
        ("머리",    new Vector3(0f,     0.935f, 0f)),
        ("어깨",    new Vector3(0.132f, 0.792f, 0f)),
        ("팔꿈치",  new Vector3(0.150f, 0.668f, 0f)),
        ("손",      new Vector3(0.165f, 0.495f, 0.02f)),
        ("허리",    new Vector3(0f,     0.535f, 0f)),
        ("등 상단", new Vector3(0f,     0.775f, -0.072f)),
    };

    /// 가방 거는 방식별 추천 고정점. 몸 표면보다 살짝 바깥/위에 둬야 가방이 몸을 뚫지 않는다.
    public static readonly (string name, Vector3 position)[] CARRY_PRESETS = {
        ("등에 매기",   new Vector3(0f,     0.790f, -0.080f)),
        ("어깨에 걸기", new Vector3(0.132f, 0.805f,  0f)),
        ("손에 들기",   new Vector3(0.168f, 0.495f,  0.020f)),
    };

    public static GameObject Build(string name) {
        GameObject root = new GameObject(name);

        // 인체 비례(약 7.5등신) 기준
        CreatePart("Head",  root.transform, new Vector3(0f, 0.935f, 0f),      new Vector3(0.135f, 0.15f, 0.145f), PrimitiveType.Sphere);
        CreatePart("Neck",  root.transform, new Vector3(0f, 0.855f, 0f),      new Vector3(0.05f, 0.035f, 0.05f),  PrimitiveType.Capsule);
        CreatePart("Torso", root.transform, new Vector3(0f, 0.695f, 0f),      new Vector3(0.23f, 0.135f, 0.13f),  PrimitiveType.Capsule);
        CreatePart("Hips",  root.transform, new Vector3(0f, 0.535f, 0f),      new Vector3(0.20f, 0.075f, 0.12f),  PrimitiveType.Capsule);

        CreatePart("Arm_L", root.transform, new Vector3(-0.145f, 0.665f, 0f), new Vector3(0.062f, 0.16f, 0.062f), PrimitiveType.Capsule);
        CreatePart("Arm_R", root.transform, new Vector3(0.145f, 0.665f, 0f),  new Vector3(0.062f, 0.16f, 0.062f), PrimitiveType.Capsule);

        CreatePart("Leg_L", root.transform, new Vector3(-0.058f, 0.245f, 0f), new Vector3(0.085f, 0.245f, 0.085f), PrimitiveType.Capsule);
        CreatePart("Leg_R", root.transform, new Vector3(0.058f, 0.245f, 0f),  new Vector3(0.085f, 0.245f, 0.085f), PrimitiveType.Capsule);

        return root;
    }

    private static void CreatePart(string name, Transform parent, Vector3 localPosition,
                                   Vector3 localScale, PrimitiveType type) {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        // 클릭 대상이 아니므로 콜라이더 제거 (레이캐스트 방해 방지)
        DestroySafe(part.GetComponent<Collider>());

        // renderer.material 은 접근하는 순간 사본을 만든다. 에디트 모드에서 그 사본이 씬에 쌓이므로
        // 재질 하나를 sharedMaterial로 모든 파트가 같이 쓴다.
        part.GetComponent<Renderer>().sharedMaterial = BodyMaterial();
    }

    /// 몸 파트가 공유하는 재질. DontSave라 씬이나 에셋으로 저장되지 않는다.
    private static Material bodyMaterial;

    private static Material BodyMaterial() {
        if (bodyMaterial != null) return bodyMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        bodyMaterial = new Material(shader) {
            name = "AvatarBody",
            hideFlags = HideFlags.DontSave,
            color = BODY_COLOR,
        };

        if (bodyMaterial.HasProperty("_Smoothness")) bodyMaterial.SetFloat("_Smoothness", 0.15f);
        if (bodyMaterial.HasProperty("_Metallic")) bodyMaterial.SetFloat("_Metallic", 0f);

        return bodyMaterial;
    }

    /// 에디트 모드에서는 Destroy가 즉시 처리되지 않아 DestroyImmediate를 써야 한다.
    private static void DestroySafe(Object target) {
        if (target == null) return;

        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
