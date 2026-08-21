using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// 가방을 어느 쪽으로 세워 전시할지. 조명 배치도 이 값을 따라간다.
public enum BagFacing {
    /// 입구(-Z) 쪽
    Entrance,

    /// 가운데 통로(x=0) 쪽. 양 열이 마주 본다
    Aisle,

    /// 회전 없이 +Z. bagYawOffset으로 직접 맞출 때
    Fixed,
}

public class GalleryController : MonoBehaviour {

    private const float STOOL_HEIGHT = 0.8f;

    /// ProjectSettings의 "Product" 레이어. 응시 레이캐스트가 이 레이어만 본다.
    private const int PRODUCT_LAYER = 3;

    /// 수직에서 30도 기울인 "뮤지엄 앵글". 더 눕히면 관람자 그림자와 눈부심이,
    /// 더 세우면 윗면만 밝고 정면 질감이 죽는다.
    private const float MUSEUM_ANGLE_DEG = 30f;

    // Resources/Prefabs 에서 찾아 쓴다. 인스펙터 슬롯은 한 번도 채운 적이 없어 없앴다.
    private GameObject stoolPrefab;
    private GameObject bagPrefab;
    private GameObject bagViewUIPrefab;

    [Header("가방 모델")]
    [Tooltip("전시대 위 가방의 높이(m). FBX 원본 크기와 무관하게 이 높이로 맞춰진다")]
    [SerializeField] private float bagDisplayHeight = 0.32f;

    [Tooltip("스툴 상판에서 가방을 얼마나 띄울지(m). 0이면 상판에 딱 닿는다")]
    [SerializeField] private float bagLiftHeight = 0f;

    [Tooltip("가방이 어느 쪽을 보고 전시될지. 조명(액센트 스포트·플린스)도 이 방향을 따라 함께 움직인다")]
    [SerializeField] private BagFacing bagFacing = BagFacing.Entrance;

    [Tooltip("가방 모델의 앞면이 -Z가 아니면 이 값으로 통째로 돌린다 (보통 0/90/180/270)")]
    [SerializeField] private float bagYawOffset = 0f;

    [Header("조명 (플레이 중에 바꾸면 바로 반영됨)")]
    [Tooltip("매장 전체 밝기. 액센트 조명과의 대비가 5:1이면 또렷하고 10:1 이상이면 극적이다")]
    [SerializeField, Range(0f, 1f)] private float ambientLevel = 0.55f;

    [Tooltip("제품 액센트 스포트라이트 세기")]
    [SerializeField] private float spotIntensity = 6f;

    [Tooltip("액센트 스포트의 빔 각도. 실제 매장용 액센트 스팟은 15~25도의 좁은 빔을 쓴다")]
    [SerializeField, Range(10f, 60f)] private float spotAngle = 24f;

    [Tooltip("스툴 안쪽에서 가방을 아래에서 받쳐주는 조명. 정면/옆면 색이 죽는 걸 막아준다")]
    [SerializeField] private float plinthIntensity = 0.8f;

    [Tooltip("입구 광장 천장 조명")]
    [SerializeField] private float concourseIntensity = 4f;

    [SerializeField] private float directionalIntensity = 0.35f;

    [Header("색 보정 (톤 매핑)")]
    [Tooltip("끄면 밝은 부분이 하얗게 잘려서 가방 색이 안 보인다. 특별한 이유가 없으면 켜둘 것")]
    [SerializeField] private bool useToneMapping = true;

    [Tooltip("전체 노출(EV). 화면이 어두우면 올리고 눈부시면 내린다")]
    [SerializeField, Range(-2f, 2f)] private float exposure = -0.15f;

    [SerializeField, Range(-40f, 40f)] private float contrast = 4f;

    [Tooltip("가죽 색이 밋밋하면 살짝 올린다")]
    [SerializeField, Range(-40f, 40f)] private float saturation = 6f;

    /*
     * 아래는 Tools > 갤러리 초기 배치 만들기 를 돌릴 때만 쓰인다.
     * 실행 중에는 읽지 않는다 — 이미 씬에 놓인 것을 바꾸지 못한다.
     * (showEntrySigns 만 예외로 실행할 때마다 쓰인다)
     */
    [Header("초기 배치 생성 옵션")]
    [Tooltip("천장 트랙과 조명 기구를 실제로 보이게 만들지")]
    [SerializeField] private bool showLightFixtures = true;

    [Tooltip("파사드 위에 매장 사인을 달지")]
    [SerializeField] private bool showStoreSign = true;

    [Tooltip("엠블럼 모델을 못 찾았을 때 대신 띄울 글자")]
    [SerializeField] private string storeName = "MCM";

    [Tooltip("입구 위에 달 3D 엠블럼. 비워두면 Resources/UI Images 에서 찾는다")]
    [SerializeField] private GameObject storeEmblemPrefab;

    [Tooltip("엠블럼 높이(m). 문 위 공간이 1.5m라 그보다 작아야 한다")]
    [SerializeField] private float storeEmblemHeight = 1.05f;

    [Tooltip("엠블럼 앞면이 광장(-Z)을 안 보면 이 값으로 돌린다 (보통 0/90/180/270)")]
    [SerializeField] private float storeEmblemYaw = 180f;

    [Tooltip("엠블럼을 비추는 조명 세기. 0이면 조명을 달지 않는다")]
    [SerializeField] private float storeEmblemLight = 2.5f;

    [Tooltip("광장 오른쪽 벽의 포토 부스 (시착 모드 배경)")]
    [SerializeField] private bool showPhotoBooth = true;

    [Header("실행")]
    [Tooltip("시작 안내판([클릭하세요])과 입구 조작 안내 이미지를 허공에 띄울지")]
    [SerializeField] private bool showEntrySigns = true;

    // 스툴/조명 배치 좌표 (x, z) - 2열 3행
    private static readonly Vector2[] SPOT_POSITIONS = {
        new Vector2(-1.8f, 4f),   new Vector2(1.8f, 4f),
        new Vector2(-1.8f, 6.3f), new Vector2(1.8f, 6.3f),
        new Vector2(-1.8f, 8.3f), new Vector2(1.8f, 8.3f),
    };

    private static readonly Color CEILING_COLOR   = new Color(0.88f, 0.88f, 0.90f, 1f);
    private static readonly Color WALL_COLOR      = new Color(0.78f, 0.78f, 0.81f, 1f);
    private static readonly Color FLOOR_COLOR     = new Color(0.46f, 0.46f, 0.49f, 1f);
    private static readonly Color STOOL_COLOR     = new Color(0.90f, 0.89f, 0.87f, 1f);
    private static readonly Color BAG_COLOR       = new Color(0.60f, 0.10f, 0.10f, 1f);
    private static readonly Color FIXTURE_COLOR   = new Color(0.13f, 0.13f, 0.14f, 1f);

    // 입구 광장은 매장보다 차갑고 어둡게 해서 안쪽이 더 밝아 보이게 한다
    private static readonly Color CONCOURSE_FLOOR = new Color(0.38f, 0.38f, 0.41f, 1f);
    private static readonly Color CONCOURSE_WALL  = new Color(0.62f, 0.62f, 0.66f, 1f);
    private static readonly Color FACADE_COLOR    = new Color(0.16f, 0.16f, 0.17f, 1f);
    private static readonly Color SIGN_COLOR      = new Color(0.93f, 0.86f, 0.68f, 1f);

    // 2700~3000K 웜화이트. 4000K 이상 쿨화이트는 가죽 색을 바래 보이게 한다.
    private static readonly Color WARM_WHITE = new Color(1f, 0.90f, 0.78f, 1f);

    // 가방 옆면이 받는 건 equator 색이다. 위아래 차이를 크게 두면 카메라가 보는 면이 제일 어두워진다.
    private static readonly Color AMBIENT_SKY     = new Color(0.82f, 0.83f, 0.88f, 1f);
    private static readonly Color AMBIENT_EQUATOR = new Color(0.70f, 0.70f, 0.74f, 1f);
    private static readonly Color AMBIENT_GROUND  = new Color(0.52f, 0.52f, 0.55f, 1f);

    /*
     * 매장 구조물은 씬에 직접 놓는다. Awake는 짓지 않고 찾아 쓰기만 한다.
     *
     * 예전에는 Awake에서 매장을 지었는데, 그러면 로딩 화면이 끝난 뒤에야 실행돼서
     * 진입 시점에 벽·가방이 아직 없는 상태가 된다.
     * 로딩 진행률은 씬 파일에 들어 있는 것만 세지, Awake가 앞으로 만들 것은 세지 못한다.
     */

    // 이름으로 자동 연결된다. 이름이 다르면 여기에 직접 끌어다 놓으면 그대로 쓴다.
    [Header("씬 연결 (비워두면 이름으로 자동 연결)")]
    [SerializeField] private Transform[] stoolTransforms;
    [SerializeField] private Light[] accentLights;
    [SerializeField] private Light[] plinthLights;
    [SerializeField] private Light[] concourseLights;
    [SerializeField] private Light directionalLight;

    [Tooltip("씬에 놓은 가방의 색을 순서대로. 예: black, cognac, green. " +
             "비워두면 실행할 때마다 무작위로 뽑는다 (모델과 어긋날 수 있음)")]
    [SerializeField] private string[] bagColors;

    /// 자동 연결이 찾는 이름 규칙
    private const string STOOL_NAME = "Stool_";
    private const string ACCENT_NAME = "AccentSpot_";
    private const string PLINTH_NAME = "PlinthLight_";
    private const string CONCOURSE_NAME = "ConcourseLight_";
    private const int CONCOURSE_COUNT = 4;

    /// 컬러를 바꿀 때 가방을 다시 만들어야 해서, UI 쪽에서 이 컨트롤러를 찾을 수 있어야 한다.
    public static GalleryController Instance { get; private set; }

    void Awake() {
        Instance = this;

        ResolvePrefabs();
        BindSceneObjects();
        ApplyBagColors();

        ApplyLightingValues();

        // 톤 매핑 볼륨은 씬에 저장할 수 없다. 프로파일이 메모리에만 있는 객체라
        // 씬을 다시 열면 비어 있고, Apply 안의 Destroy는 에디터 모드에서 동작하지 않는다.
        if (useToneMapping) {
            GalleryGrading.Apply(Camera.main, exposure, contrast, saturation);
        }

        InitializeCamera();
        CreateUI();
        CreateLoggingServices();
    }

    /// 씬에 놓인 스툴과 조명을 이름으로 찾아 배열에 담는다.
    ///
    /// 인스펙터에 이미 채워져 있으면 그대로 둔다 — 이름 규칙을 안 지킨 경우
    /// 직접 끌어다 놓은 것이 자동 연결에 덮이면 안 되기 때문.
    void BindSceneObjects() {
        Transform products = transform.Find("Products");
        Transform lighting = transform.Find("Lighting");

        if (IsEmpty(stoolTransforms) && products != null) {
            stoolTransforms = new Transform[SPOT_POSITIONS.Length];
            for (int i = 0; i < stoolTransforms.Length; i++) {
                stoolTransforms[i] = products.Find(STOOL_NAME + i);
            }
        }

        if (IsEmpty(accentLights)) {
            accentLights = FindLights(lighting, ACCENT_NAME, SPOT_POSITIONS.Length);
        }
        if (IsEmpty(plinthLights)) {
            plinthLights = FindLights(lighting, PLINTH_NAME, SPOT_POSITIONS.Length);
        }
        if (IsEmpty(concourseLights)) {
            concourseLights = FindLights(lighting, CONCOURSE_NAME, CONCOURSE_COUNT);
        }

        if (directionalLight == null) {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if (light.type != LightType.Directional) continue;
                directionalLight = light;
                break;
            }
        }

        WarnIfUnbound();
    }

    private static bool IsEmpty<T>(T[] array) {
        if (array == null || array.Length == 0) return true;

        foreach (T item in array) {
            if (item != null) return false;
        }
        return true;
    }

    private Light[] FindLights(Transform parent, string prefix, int count) {
        var lights = new Light[count];
        if (parent == null) return lights;

        for (int i = 0; i < count; i++) {
            Transform found = parent.Find(prefix + i);
            if (found != null) lights[i] = found.GetComponent<Light>();
        }

        return lights;
    }

    /// 연결이 비면 증상이 조용하다 — 컬러 변경이 안 먹거나 조명 슬라이더가 무반응이 된다.
    /// 원인을 씬에서 찾기 어려우니 시작할 때 알려준다.
    void WarnIfUnbound() {
        if (IsEmpty(stoolTransforms)) {
            Debug.LogWarning($"[GalleryController] 스툴을 찾지 못했습니다. " +
                             $"Products 아래에 {STOOL_NAME}0 ~ {STOOL_NAME}{SPOT_POSITIONS.Length - 1} 이름으로 두거나 " +
                             "인스펙터에 직접 넣어 주세요. (컬러 변경이 동작하지 않습니다)");
        }

        if (IsEmpty(accentLights) && IsEmpty(plinthLights) && IsEmpty(concourseLights)) {
            Debug.LogWarning("[GalleryController] 조명을 찾지 못했습니다. " +
                             "Lighting 아래 이름 규칙을 확인해 주세요. (조명 슬라이더가 동작하지 않습니다)");
        }
    }

    /// 씬에 놓은 가방 색을 세션 상태에 넣어준다.
    ///
    /// 이걸 빼먹으면 화면에는 코냑 가방이 서 있는데 상태는 블랙이라고 답한다.
    /// 그 결과 상세 보기 스와치가 엉뚱한 색을 선택 상태로 표시하고 시착에도 다른 색이 넘어간다.
    void ApplyBagColors() {
        if (bagColors == null) return;

        for (int i = 0; i < bagColors.Length; i++) {
            if (!string.IsNullOrWhiteSpace(bagColors[i])) {
                BagColorState.SetColor(i + 1, bagColors[i].Trim());
            }
        }
    }

    /// 씬에 놓을 구조물의 초벌을 만든다 (Tools > 갤러리 초기 배치 만들기).
    /// 한 번 만들어 두고 그 뒤로는 씬에서 직접 손보면 된다. 실행할 때는 부르지 않는다.
    public void BuildStaticScene() {
        CreateSceneHierarchy();
        CreateEnvironment();
        CreateEntrance();
        CreateProducts();
        CreateLighting();
    }

#if UNITY_EDITOR
    /// 만들어둔 구조물을 지운다. 다시 만들기 전에 먼저 부른다.
    public void ClearStaticScene() {
        foreach (string name in new[] { "StoreEnvironment", "Entrance", "Products", "Lighting" }) {
            Transform child = transform.Find(name);
            if (child != null) DestroyImmediate(child.gameObject);
        }

        stoolTransforms = null;
        accentLights = null;
        plinthLights = null;
        concourseLights = null;
    }

#endif

    /// 로깅 서비스를 씬에 준비하고 SessionManager에 연결한다. 갤러리 씬 단독 실행에도 대비해 없으면 만든다.
    void CreateLoggingServices() {
        GazeLogger logger = FindFirstObjectByType<GazeLogger>();
        if (logger == null) {
            logger = new GameObject("GazeLogger").AddComponent<GazeLogger>();
        }

        // GazeAPIClient는 Awake에서 GazeLogger를 찾으므로 logger 생성 뒤에 붙인다
        GazeAPIClient apiClient = FindFirstObjectByType<GazeAPIClient>();
        if (apiClient == null) {
            apiClient = logger.gameObject.AddComponent<GazeAPIClient>();
        }

        GazeTracker tracker = FindFirstObjectByType<GazeTracker>();
        if (tracker == null && Camera.main != null) {
            tracker = Camera.main.gameObject.AddComponent<GazeTracker>();
        }
        if (tracker != null) tracker.SetLogger(logger);

        // 같은 GameObject의 컴포넌트끼리는 Awake 순서가 보장되지 않아 .Instance가 아직 null일 수 있다.
        // 그걸 믿으면 중복 인스턴스를 만들게 되므로 Find로 확인한다.
        SessionDataManager sessionData = FindFirstObjectByType<SessionDataManager>();
        if (sessionData == null) {
            sessionData = new GameObject("SessionDataManager").AddComponent<SessionDataManager>();
        }
        sessionData.SetLogger(logger);

        SessionManager session = SessionManager.Instance;

        if (session == null) {
            // 갤러리 씬 단독 실행. customer_id가 없으면 응시 로그도 예약도 전부 막히는데,
            // 명세서 §1의 L0는 익명 세션이라 여기서 바로 발급해도 된다.
            Debug.Log("[GalleryController] SessionManager가 없어 익명 세션을 새로 발급합니다. " +
                      "(인트로 씬부터 실행하면 프로필까지 함께 전송됩니다)");

            session = SessionManager.Create(null, null);
            session.StartSession();
        }

        session.BindSceneServices(apiClient, tracker, logger);

        if (!string.IsNullOrEmpty(session.CustomerId)) {
            if (tracker != null) tracker.SetCustomerId(session.CustomerId);
            apiClient.SetSessionCustomerId(session.CustomerId);

            // 명세서 SCENE_02_GALLERY. 세션당 한 번, interaction_id/occurrence_index 불필요.
            sessionData.LogSceneEnter(2);
        }
        Debug.Log(
            $"[GalleryController] Session 연결 완료: " +
            $"CustomerId={session.CustomerId}, " +
            $"Tracker={tracker != null}, " +
            $"APIClient={apiClient != null}, " +
            $"Logger={logger != null}"
        );
    }

    /// 이 가방이 바라보는 방향(월드, 수평). 배치와 조명이 어긋나면 관람자가 보는 면이 그늘진다.
    Vector3 BagFacingDir(Vector2 spot) {
        switch (bagFacing) {
            case BagFacing.Entrance: return Vector3.back;

            case BagFacing.Aisle: return spot.x < 0f ? Vector3.right : Vector3.left;

            default: return Vector3.forward;
        }
    }

    /// 위 방향을 모델 회전각으로 바꾼 값. yaw 0이 +Z를 보는 기준이다.
    float FacingYaw(Vector2 spot) {
        Vector3 dir = BagFacingDir(spot);

        return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + bagYawOffset;
    }

    static void SetLayerRecursively(GameObject target, int layer) {
        target.layer = layer;

        foreach (Transform child in target.transform) {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    void ResolvePrefabs() {
        if (stoolPrefab == null)      stoolPrefab      = Resources.Load<GameObject>("Prefabs/Stool");
        if (bagPrefab == null)        bagPrefab        = Resources.Load<GameObject>("Prefabs/Bag");
        if (bagViewUIPrefab == null)  bagViewUIPrefab  = Resources.Load<GameObject>("Prefabs/BagViewUI");
    }

    void CreateSceneHierarchy() {
        new GameObject("StoreEnvironment").transform.SetParent(transform);
        new GameObject("Entrance").transform.SetParent(transform);
        new GameObject("Products").transform.SetParent(transform);
        new GameObject("Lighting").transform.SetParent(transform);
    }

    void CreateEnvironment() {
        var storeEnv = transform.Find("StoreEnvironment");

        float w = StoreLayout.STORE_WIDTH;
        float d = StoreLayout.STORE_DEPTH;
        float h = StoreLayout.STORE_HEIGHT;
        float t = StoreLayout.WALL_THICKNESS;

        CreateCube("Floor", storeEnv, new Vector3(0, 0, d / 2), new Vector3(w, t, d), FLOOR_COLOR, 0.62f);
        CreateCube("Ceiling", storeEnv, new Vector3(0, h, d / 2), new Vector3(w, t, d), CEILING_COLOR, 0.05f);

        // 정면(z=0)은 파사드가 대신하므로 여기서 만들지 않는다
        CreateCube("Wall_Back", storeEnv, new Vector3(0, h / 2, d), new Vector3(w, h, t), WALL_COLOR, 0.08f);
        CreateCube("Wall_Left", storeEnv, new Vector3(-w / 2, h / 2, d / 2), new Vector3(t, h, d), WALL_COLOR, 0.08f);
        CreateCube("Wall_Right", storeEnv, new Vector3(w / 2, h / 2, d / 2), new Vector3(t, h, d), WALL_COLOR, 0.08f);
    }

    void CreateEntrance() {
        var entrance = transform.Find("Entrance");

        float ew = StoreLayout.ENTRANCE_WIDTH;
        float ed = StoreLayout.ENTRANCE_DEPTH;
        float eh = StoreLayout.ENTRANCE_HEIGHT;
        float t = StoreLayout.WALL_THICKNESS;

        // 광장은 z가 음수 구간 (-ed ~ 0)
        CreateCube("Concourse_Floor", entrance, new Vector3(0, 0, -ed / 2), new Vector3(ew, t, ed), CONCOURSE_FLOOR, 0.55f);
        CreateCube("Concourse_Ceiling", entrance, new Vector3(0, eh, -ed / 2), new Vector3(ew, t, ed), CEILING_COLOR, 0.05f);

        CreateCube("Concourse_Wall_Left", entrance, new Vector3(-ew / 2, eh / 2, -ed / 2), new Vector3(t, eh, ed), CONCOURSE_WALL, 0.08f);
        CreateCube("Concourse_Wall_Right", entrance, new Vector3(ew / 2, eh / 2, -ed / 2), new Vector3(t, eh, ed), CONCOURSE_WALL, 0.08f);
        CreateExitWall(entrance);
        CreateFacade(entrance);

        if (showPhotoBooth) PhotoBooth.Build(entrance);
    }

    /// z=0 파사드. 광장 폭 전체를 막되 가운데를 뚫어 출입구를 만든다. 문짝은 없다(오픈 매장 구조).
    /// 광장 뒷벽. 가운데를 뚫어 출구를 만든다 (매장 파사드와 같은 방식).
    /// 문짝은 없다. 통행 한계가 문턱 앞이라 실제로 지나가지는 못하고,
    /// 가까이 가면 뜨는 [나가기] 버튼으로 관람을 끝낸다.
    void CreateExitWall(Transform parent) {
        float ew = StoreLayout.ENTRANCE_WIDTH;
        float ed = StoreLayout.ENTRANCE_DEPTH;
        float eh = StoreLayout.ENTRANCE_HEIGHT;
        float dw = StoreLayout.EXIT_WIDTH;
        float dh = StoreLayout.EXIT_HEIGHT;
        float t = StoreLayout.WALL_THICKNESS;

        float sideWidth = (ew - dw) / 2f;
        float sideCenterX = dw / 2f + sideWidth / 2f;

        CreateCube("Exit_Wall_Left", parent, new Vector3(-sideCenterX, eh / 2f, -ed),
            new Vector3(sideWidth, eh, t), CONCOURSE_WALL, 0.08f);
        CreateCube("Exit_Wall_Right", parent, new Vector3(sideCenterX, eh / 2f, -ed),
            new Vector3(sideWidth, eh, t), CONCOURSE_WALL, 0.08f);

        float headerHeight = eh - dh;
        CreateCube("Exit_Wall_Header", parent, new Vector3(0f, dh + headerHeight / 2f, -ed),
            new Vector3(dw, headerHeight, t), CONCOURSE_WALL, 0.08f);

        // 문 너머는 아무것도 없다. 어두운 판을 하나 세워 빈 공간이 비치지 않게 한다.
        CreateCube("Exit_Backdrop", parent, new Vector3(0f, eh / 2f, -ed - 0.6f),
            new Vector3(dw, eh, t), new Color(0.06f, 0.06f, 0.07f, 1f), 0f);
    }

    void CreateFacade(Transform parent) {
        float ew = StoreLayout.ENTRANCE_WIDTH;
        float eh = StoreLayout.ENTRANCE_HEIGHT;
        float dw = StoreLayout.DOOR_WIDTH;
        float dh = StoreLayout.DOOR_HEIGHT;
        float t = StoreLayout.WALL_THICKNESS;

        float sideWidth = (ew - dw) / 2f;
        float sideCenterX = dw / 2f + sideWidth / 2f;

        CreateCube("Facade_Left", parent, new Vector3(-sideCenterX, eh / 2f, 0f),
            new Vector3(sideWidth, eh, t), FACADE_COLOR, 0.25f);
        CreateCube("Facade_Right", parent, new Vector3(sideCenterX, eh / 2f, 0f),
            new Vector3(sideWidth, eh, t), FACADE_COLOR, 0.25f);

        float headerHeight = eh - dh;
        CreateCube("Facade_Header", parent, new Vector3(0f, dh + headerHeight / 2f, 0f),
            new Vector3(dw, headerHeight, t), FACADE_COLOR, 0.25f);

        if (showStoreSign) CreateStoreSign(parent);
    }

    /// 파일명에 타임스탬프가 붙어 경로를 박으면 에셋 교체 때마다 깨진다. 그래서 접두사로 찾는다.
    private const string EMBLEM_RESOURCE_FOLDER = "UI Images";
    private const string EMBLEM_NAME_PREFIX = "Meshy_AI_Golden_MCM";

    void CreateStoreSign(Transform parent) {
        if (CreateStoreEmblem(parent)) return;

        CreateStoreSignText(parent);
    }

    /// 입구 위 금색 MCM 엠블럼(Meshy FBX). 못 찾으면 false를 돌려 호출 쪽이 글자 사인으로 대체한다.
    /// 임포터의 머티리얼 자동 생성이 꺼져 있어 머티리얼은 직접 입혀야 한다.
    bool CreateStoreEmblem(Transform parent) {
        GameObject source = storeEmblemPrefab != null
            ? storeEmblemPrefab
            : FindResource<GameObject>();

        if (source == null) {
            Debug.LogWarning($"[GalleryController] '{EMBLEM_NAME_PREFIX}...' 모델을 " +
                             $"Resources/{EMBLEM_RESOURCE_FOLDER} 에서 찾지 못해 글자 사인을 씁니다.");
            return false;
        }

        float signY = StoreLayout.DOOR_HEIGHT
                    + (StoreLayout.ENTRANCE_HEIGHT - StoreLayout.DOOR_HEIGHT) / 2f;

        // 벽면에 파묻히지 않게 광장 쪽으로 조금 띄운다
        Vector3 target = new Vector3(0f, signY, -StoreLayout.WALL_THICKNESS - 0.12f);

        GameObject emblem = Instantiate(source, parent);
        emblem.name = "StoreEmblem";

        emblem.transform.position = target;
        emblem.transform.rotation = Quaternion.Euler(0f, storeEmblemYaw, 0f);

        BagModelUtil.FitToHeight(emblem, storeEmblemHeight);

        // FitToHeight는 크기만 맞춘다. FBX 원점이 중심이 아닌 경우가 많아 바운즈를 다시 중앙에 맞춘다.
        if (BagModelUtil.TryGetWorldBounds(emblem, out Bounds bounds)) {
            emblem.transform.position += target - bounds.center;
        }

        // 콜라이더가 딸려오면 관람자가 입구 위 허공에서 막힌다
        foreach (Collider collider in emblem.GetComponentsInChildren<Collider>()) {
            Destroy(collider);
        }

        ApplyEmblemMaterial(emblem);

        if (storeEmblemLight > 0f) CreateEmblemLight(emblem.transform, signY);

        return true;
    }

    void ApplyEmblemMaterial(GameObject emblem) {
        Material material = FindEmblemMaterial();

        if (material == null) {
            Debug.LogWarning("[GalleryController] 엠블럼 머티리얼을 찾지 못했습니다. " +
                             "Tools > Meshy 에셋 정리 를 한 번 실행해 주세요.");
            return;
        }

        foreach (Renderer renderer in emblem.GetComponentsInChildren<Renderer>(true)) {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;

            renderer.sharedMaterials = materials;
        }
    }

    /// 천장 조명만으로는 벽에 붙은 엠블럼 정면이 그늘져서, 광장 쪽에서 올려 비추는 스포트를 하나 단다.
    void CreateEmblemLight(Transform emblem, float signY) {
        GameObject obj = new GameObject("EmblemSpot");
        obj.transform.SetParent(emblem.parent, false);
        obj.transform.position = new Vector3(0f, signY - 0.9f, -2.2f);
        obj.transform.LookAt(new Vector3(0f, signY, -StoreLayout.WALL_THICKNESS));

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = WARM_WHITE;
        light.intensity = storeEmblemLight;
        light.spotAngle = 45f;
        light.range = 6f;
        light.shadows = LightShadows.None;
    }

    private static T FindResource<T>() where T : Object {
        foreach (T candidate in Resources.LoadAll<T>(EMBLEM_RESOURCE_FOLDER)) {
            if (candidate != null && candidate.name.StartsWith(EMBLEM_NAME_PREFIX)) return candidate;
        }
        return null;
    }

    /// 엠블럼 머티리얼. 임포터가 만든 쓰레기 머티리얼(노멀맵이 BaseMap에 꽂힌 것들)도
    /// 같은 접두사로 시작하므로, 맵 이름이 붙은 건 전부 걸러낸다.
    private static Material FindEmblemMaterial() {
        foreach (Material candidate in Resources.LoadAll<Material>(EMBLEM_RESOURCE_FOLDER)) {
            if (candidate == null) continue;
            if (!candidate.name.StartsWith(EMBLEM_NAME_PREFIX)) continue;

            string name = candidate.name;
            if (name.Contains("_texture")) continue;
            if (name.EndsWith("_normal") || name.EndsWith("_roughness")
                || name.EndsWith("_metallic") || name.EndsWith("_MetallicSmoothness")) continue;

            return candidate;
        }
        return null;
    }

    /// 엠블럼을 못 찾았을 때 쓰는 대체 사인
    void CreateStoreSignText(Transform parent) {
        float signY = StoreLayout.DOOR_HEIGHT + (StoreLayout.ENTRANCE_HEIGHT - StoreLayout.DOOR_HEIGHT) / 2f;

        GameObject signObj = new GameObject("StoreSign", typeof(RectTransform));
        signObj.transform.SetParent(parent, false);

        signObj.transform.position = new Vector3(0f, signY, -StoreLayout.WALL_THICKNESS);
        signObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        TextMeshPro sign = signObj.AddComponent<TextMeshPro>();
        sign.font = ResolveFont();
        sign.text = storeName;
        sign.fontSize = 5f;
        sign.characterSpacing = 18f;
        sign.alignment = TextAlignmentOptions.Center;
        sign.color = SIGN_COLOR;

        RectTransform rect = signObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(StoreLayout.DOOR_WIDTH, 1.2f);

        // 조명을 못 받아도 읽히도록 자체 발광
        Material material = sign.fontMaterial;
        if (material != null && material.HasProperty("_FaceColor")) {
            material.SetColor("_FaceColor", SIGN_COLOR * 1.6f);
        }
    }

    TMP_FontAsset ResolveFont() {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/KoreanSDF");
        return font != null ? font : TMP_Settings.defaultFontAsset;
    }

    void CreateProducts() {
        var productsRoot = transform.Find("Products");
        stoolTransforms = new Transform[SPOT_POSITIONS.Length];
        bagColors = new string[SPOT_POSITIONS.Length];

        for (int i = 0; i < SPOT_POSITIONS.Length; i++) {
            Vector2 spot = SPOT_POSITIONS[i];

            // 루트는 스케일 1로 두고 메시만 자식에서 늘린다. 루트를 누르면 자식 가방까지 찌그러진다.
            GameObject stool = new GameObject($"Stool_{i}");
            stool.transform.SetParent(productsRoot);
            stool.transform.localPosition = new Vector3(spot.x, 0f, spot.y);
            stool.tag = "Stool";
            stoolTransforms[i] = stool.transform;

            if (stoolPrefab != null) {
                GameObject mesh = Instantiate(stoolPrefab, stool.transform);
                mesh.name = "StoolMesh";
                mesh.transform.localPosition = Vector3.up * (STOOL_HEIGHT / 2f);
            } else {
                CreateCube("StoolMesh", stool.transform, Vector3.up * (STOOL_HEIGHT / 2f),
                    new Vector3(0.4f, STOOL_HEIGHT, 0.4f), STOOL_COLOR, 0.35f);
            }

            CreateBag(i, stool.transform, spot);
        }
    }

    /// 가방 모델을 스툴 위에 올린다. BagLibrary가 없으면 큐브 자리표시자로 대체한다.
    GameObject CreateBag(int index, Transform stool, Vector2 spot) {
        BagModelEntry entry = BagLibrary.GetModelEntry(index);
        GameObject bag;

        ProductData product = ProductCatalog.GetByIndex(index);

        // 한 번 뽑힌 색은 BagColorState가 세션 내내 들고 있어, 상세 보기·시착을 오가도 유지된다.
        ProductColorOption color = BagColorState.ResolveOrAssignRandom(product);
        string colorCode = color != null ? color.color : null;

        // 초벌을 만들 때 뽑힌 색을 인스펙터 값으로 남긴다 (ApplyBagColors 참고)
        if (bagColors != null && index >= 0 && index < bagColors.Length) {
            bagColors[index] = colorCode;
        }

        if (entry != null) {
            // 컬러마다 FBX가 다르다. 머티리얼이 아니라 모델 자체를 고른다.
            GameObject source = BagLibrary.GetVariantModel(index, colorCode);

            // FBX마다 실제 크기가 제각각이라 바운즈를 재서 목표 높이로 맞춘다.
            bag = BagModelUtil.Place(source, stool, spot, BagBottomY(),
                bagDisplayHeight * entry.heightMultiplier,
                entry.Rotation, FacingYaw(spot));
            BagModelUtil.EnsureBoxCollider(bag);
        } else if (bagPrefab != null) {
            bag = Instantiate(bagPrefab, stool);
            bag.transform.localPosition = Vector3.up * BagBottomY();
        } else {
            bag = CreateCube("Bag", stool, Vector3.up * (BagBottomY() + bagDisplayHeight / 2f),
                new Vector3(0.24f, bagDisplayHeight, 0.12f), BAG_COLOR, 0.45f);
        }

        bag.name = $"Bag_{index}";
        bag.tag = "Bag";

        // FBX 임포트에서 머티리얼 자동 생성이 꺼져 있어, 여기서 안 입히면 기본 회색으로 나온다.
        BagLibrary.ApplyVariantMaterial(bag.transform, index, colorCode);

        // 응시 추적에 필요. 스툴·벽이 레이를 가로막지 않도록 가방만 Product 레이어에 올린다.
        SetLayerRecursively(bag, PRODUCT_LAYER);

        ProductInfo info = bag.GetComponent<ProductInfo>();
        if (info == null) info = bag.AddComponent<ProductInfo>();
        info.Initialize(index + 1, product != null ? product.name : bag.name);

        if (bag.GetComponent<BagRotator>() == null) bag.AddComponent<BagRotator>();

        return bag;
    }

    /// 컬러마다 FBX가 달라 모델을 갈아 끼운다. 바꿀 색은 미리 BagColorState에 기록해 둔다.
    /// 새 오브젝트를 돌려주므로 이전 가방을 참조하던 쪽은 반드시 이 값으로 바꿔야 한다.
    public Transform SwapBagColor(Transform bagRoot) {
        if (bagRoot == null || stoolTransforms == null) return null;

        int index = IndexOfBag(bagRoot.name);
        if (index < 0 || index >= stoolTransforms.Length) return null;

        Transform stool = stoolTransforms[index];
        if (stool == null) return null;

        // Destroy는 프레임 끝에 처리된다. 이름을 먼저 비켜두지 않으면 잠깐 같은 이름의 가방이 둘이 된다.
        bagRoot.name = "Bag_Discarded";
        Destroy(bagRoot.gameObject);

        GameObject rebuilt = CreateBag(index, stool, SPOT_POSITIONS[index]);
        return rebuilt != null ? rebuilt.transform : null;
    }

    /// "Bag_3" → 3. 못 읽으면 -1.
    private static int IndexOfBag(string objectName) {
        if (string.IsNullOrEmpty(objectName)) return -1;

        int underscore = objectName.LastIndexOf('_');
        if (underscore < 0 || underscore >= objectName.Length - 1) return -1;

        return int.TryParse(objectName.Substring(underscore + 1), out int index) ? index : -1;
    }

    void CreateLighting() {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
            if (light.type == LightType.Directional) {
                directionalLight = light;
                break;
            }
        }

        var lightingRoot = transform.Find("Lighting");

        accentLights = new Light[SPOT_POSITIONS.Length];
        plinthLights = new Light[SPOT_POSITIONS.Length];

        float trackY = StoreLayout.STORE_HEIGHT - 0.08f;
        float bagY = BagWorldY();

        // 수직에서 30도 기울이려면 천장과 제품의 높이차만큼 수평으로 물러나야 한다.
        float aimOffset = (trackY - bagY) * Mathf.Tan(MUSEUM_ANGLE_DEG * Mathf.Deg2Rad);

        if (showLightFixtures) {
            // 레일은 조명이 실제로 걸리는 x를 지나가야 한다 (입구를 보면 조명은 z로만 물러난다).
            float railX = 1.8f - Mathf.Abs(BagFacingDir(SPOT_POSITIONS[0]).x) * aimOffset;

            CreateTrackRail(lightingRoot, -railX, trackY);
            CreateTrackRail(lightingRoot, +railX, trackY);
        }

        for (int i = 0; i < SPOT_POSITIONS.Length; i++) {
            Vector2 spot = SPOT_POSITIONS[i];
            Vector3 target = new Vector3(spot.x, bagY, spot.y);

            // 제품 바로 위가 아니라 '제품이 바라보는 쪽'으로 물러나 겨눈다.
            // 바로 위에서 쏘면 윗면만 타고 관람자가 보는 정면은 그늘진다.
            Vector3 facing = BagFacingDir(spot);
            Vector3 lightPos = new Vector3(
                spot.x + facing.x * aimOffset, trackY, spot.y + facing.z * aimOffset);

            accentLights[i] = CreateAccentSpot(lightingRoot, i, lightPos, target);
            plinthLights[i] = CreatePlinthLight(lightingRoot, i, spot, facing);

            if (showLightFixtures) CreateFixtureHead(lightingRoot, i, lightPos, target);
        }

        CreateConcourseLights(lightingRoot);
        ApplyLightingValues();
    }

    /// 입구 광장 천장 조명. 사거리를 짧게 잡아 매장 안 제품 조명 예산을 잡아먹지 않게 한다.
    void CreateConcourseLights(Transform parent) {
        float[] xs = { -3.2f, 3.2f };
        float[] zs = { -2.2f, -5.2f };

        concourseLights = new Light[xs.Length * zs.Length];
        int index = 0;

        foreach (float z in zs) {
            foreach (float x in xs) {
                GameObject obj = new GameObject($"ConcourseLight_{index}");
                obj.transform.SetParent(parent);
                obj.transform.position = new Vector3(x, StoreLayout.ENTRANCE_HEIGHT - 0.15f, z);
                obj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                Light light = obj.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.96f, 0.92f, 1f);
                light.range = 6f;
                light.spotAngle = 95f;
                light.innerSpotAngle = 50f;
                light.shadows = LightShadows.None;

                concourseLights[index++] = light;
            }
        }
    }

    Light CreateAccentSpot(Transform parent, int index, Vector3 position, Vector3 target) {
        GameObject obj = new GameObject($"AccentSpot_{index}");
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.LookAt(target);

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = WARM_WHITE;
        light.range = 5f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.7f;

        return light;
    }

    /// 스툴(플린스)에서 가방을 아래에서 받쳐주는 조명. 천장 조명만으로는 어두워지는 옆면·정면을 살린다.
    Light CreatePlinthLight(Transform parent, int index, Vector2 spot, Vector3 facing) {
        GameObject obj = new GameObject($"PlinthLight_{index}");
        obj.transform.SetParent(parent);

        // 스툴 바로 위에 얹으면 상판만 하얗게 타므로, 가방이 보는 쪽으로 살짝 비켜 세운다.
        obj.transform.position = new Vector3(
            spot.x + facing.x * 0.26f,
            BagBottomY() + 0.16f,
            spot.y + facing.z * 0.26f);

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = WARM_WHITE;
        light.range = 0.9f;          // 자기 제품만 비추도록 짧게 (오브젝트당 조명 개수 한도 관리)
        light.shadows = LightShadows.None;

        return light;
    }

    void CreateTrackRail(Transform parent, float x, float y) {
        GameObject rail = CreateCube($"Track_{(x < 0 ? "L" : "R")}", parent,
            new Vector3(x, y + 0.03f, StoreLayout.STORE_DEPTH * 0.62f),
            new Vector3(0.06f, 0.05f, StoreLayout.STORE_DEPTH * 0.62f), FIXTURE_COLOR, 0.5f);

        DisableShadowCasting(rail);
    }

    /// 빛이 허공에서 나오는 것처럼 보이지 않게 조명 기구 헤드를 놓아준다.
    void CreateFixtureHead(Transform parent, int index, Vector3 position, Vector3 target) {
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        head.name = $"Fixture_{index}";
        head.transform.SetParent(parent);

        Vector3 direction = (target - position).normalized;

        // 실린더의 축은 Y라서 90도 돌려 조명 방향과 맞춘다.
        head.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
        head.transform.position = position - direction * 0.07f;
        head.transform.localScale = new Vector3(0.08f, 0.07f, 0.08f);

        Material material = head.GetComponent<Renderer>().material;
        material.color = FIXTURE_COLOR;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.55f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.8f);

        Destroy(head.GetComponent<Collider>());
        DisableShadowCasting(head);
    }

    void DisableShadowCasting(GameObject target) {
        Renderer renderer = target.GetComponent<Renderer>();
        // 기구가 자기 빛을 가려 그림자를 만들지 않도록
        if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    void ApplyLightingValues() {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = AMBIENT_SKY * ambientLevel;
        RenderSettings.ambientEquatorColor = AMBIENT_EQUATOR * ambientLevel;
        RenderSettings.ambientGroundColor  = AMBIENT_GROUND * ambientLevel;

        if (directionalLight != null) {
            directionalLight.intensity = directionalIntensity;
            directionalLight.shadows = LightShadows.Soft;
        }

        if (accentLights != null) {
            foreach (Light light in accentLights) {
                if (light == null) continue;
                light.intensity = spotIntensity;
                light.spotAngle = spotAngle;
                light.innerSpotAngle = spotAngle * 0.6f;
            }
        }

        if (plinthLights != null) {
            foreach (Light light in plinthLights) {
                if (light == null) continue;
                light.intensity = plinthIntensity;
            }
        }

        if (concourseLights != null) {
            foreach (Light light in concourseLights) {
                if (light == null) continue;
                light.intensity = concourseIntensity;
            }
        }
    }

#if UNITY_EDITOR
    // 플레이 중 인스펙터에서 값을 바꾸면 바로 반영되도록.
    void OnValidate() {
        if (Application.isPlaying) ApplyLightingValues();
    }
#endif

    /// 가방 밑면이 놓이는 월드 높이. 실제 배치는 이 값 하나로 정해진다.
    float BagBottomY() {
        return STOOL_HEIGHT + bagLiftHeight;
    }

    /// 가방의 중심 높이. 조명 각도 계산에만 쓰며, 여기를 바꿔도 가방은 움직이지 않는다.
    float BagWorldY() {
        return BagBottomY() + bagDisplayHeight / 2f;
    }

    void InitializeCamera() {
        // 여기서도 Camera.main 을 믿으면 안 된다 (CreateUI 주석 참고).
        // 실제로 플레이어가 조종하는 카메라는 CameraController 가 붙어 있는 쪽이다.
        CameraController controller = FindFirstObjectByType<CameraController>();

        Transform player = controller != null
            ? controller.transform
            : (Camera.main != null ? Camera.main.transform : null);

        if (player == null) return;

        player.position = StoreLayout.SPAWN_POSITION;
        player.rotation = Quaternion.identity;
    }

    /// 가방 상세 보기 / 시착 UI와 시착 컨트롤러 (프리팹이 있으면 그걸, 없으면 코드로 생성)
    void CreateUI() {
        // Awake 순서상 .Instance가 아직 null일 수 있다. 중복 생성하면 상대가 자신을 지우므로 Find로 확인한다.
        if (FindFirstObjectByType<BagViewUI>() == null) {
            if (bagViewUIPrefab != null) {
                Instantiate(bagViewUIPrefab).name = "BagViewUI";
            } else {
                // Canvas는 스케일/위치 영향을 받지 않도록 씬 루트에 둔다
                new GameObject("BagViewUI").AddComponent<BagViewUI>();
            }
        }

        // 피팅 화면은 BagViewUI 사이드 패널 안에서 처리한다 (별도 캔버스 없음)

        if (FindFirstObjectByType<TryOnController>() == null) {
            new GameObject("TryOnController").AddComponent<TryOnController>();
        }

        // Camera.main 을 먼저 보면 안 된다.
        // Intro 씬의 Main Camera 가 CursorManager 의 DontDestroyOnLoad 를 타고 갤러리까지 따라오는데,
        // 그쪽도 MainCamera 태그라 Camera.main 이 그 카메라를 집을 수 있다.
        // 그 카메라엔 CameraController 가 없어서, 예전 코드는 Camera.main != null 이라는 이유로
        // 폴백도 못 타고 controller 에 null 을 담았다.
        // 그러면 EntrySigns 는 입력을 못 잠그고, ExitGate 는 커서를 못 띄우고, Crosshair 는 아예 안 뜬다.
        CameraController controller = FindFirstObjectByType<CameraController>();
        if (controller == null && Camera.main != null) {
            controller = Camera.main.GetComponent<CameraController>();
        }

        if (controller == null) {
            Debug.LogError("[GalleryController] CameraController 를 찾지 못했습니다. " +
                           "안내판·조준점·나가기 버튼의 커서 제어가 동작하지 않습니다.");
        }

        // 시작 게이트 + 조작 안내판. 클릭 전까지 카메라를 잠근다.
        if (showEntrySigns && FindFirstObjectByType<EntrySigns>() == null) {
            EntrySigns.Create(transform, controller);
        }

        // 광장 뒷벽 출구의 [나가기] 버튼
        if (FindFirstObjectByType<ExitGate>() == null) {
            ExitGate.Create(transform, controller);
        }

        // 화면 중앙 조준점 (자유 이동 중에만 표시)
        if (FindFirstObjectByType<Crosshair>() == null) {
            Crosshair.Create(transform, controller);
        }
    }

    GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale,
                          Color color, float smoothness) {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;

        // renderer.material은 접근 시점에 인스턴스를 만들어 반환하므로 따로 Instantiate 하지 않는다.
        Material material = cube.GetComponent<Renderer>().material;
        material.color = color;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

        return cube;
    }
}
