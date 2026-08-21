using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// 시작 지점 앞에 뜨는 안내판. [클릭하세요] 아래에 조작 안내 이미지가 붙는다.
/// 월드 스페이스 캔버스라 오버레이와 달리 가까이 갈수록 커진다.
/// 클릭하면 통째로 사라지고 자유 이동이 시작된다.
public class EntrySigns : MonoBehaviour {

    /// 월드 스페이스 캔버스는 1유닛 = 1m다. 픽셀로 디자인하고 이 배율로 줄인다.
    private const float CANVAS_SCALE = 0.0025f;

    private const string CONTROLLER_IMAGE_PATH = "UI Images/ControllerUI";

    /// 안내판 레이아웃 (픽셀 기준, CANVAS_SCALE로 줄여서 월드에 놓인다)
    private const float GATE_WIDTH = 820f;
    private const float GUIDE_HEIGHT = 120f;
    private const float GUIDE_IMAGE_GAP = 40f;
    private const float BOARD_WIDTH = 500f;

    private static readonly Color INK = new Color(0.10f, 0.10f, 0.11f, 1f);

    private CameraController cameraController;
    private GameObject gateRoot;

    /// 게이트가 떠 있는 동안은 카메라 입력을 잠근다. 안내를 읽기 전에 걸어 나가지 못하게.
    public bool IsWaiting => gateRoot != null;

    public static EntrySigns Create(Transform parent, CameraController controller) {
        GameObject root = new GameObject("EntrySigns");
        root.transform.SetParent(parent, false);

        EntrySigns signs = root.AddComponent<EntrySigns>();
        signs.cameraController = controller;
        signs.Build();

        return signs;
    }

    private void Build() {
        // BagViewUI보다 먼저 만들어질 수 있어 한글 폰트를 직접 확보한다 (없으면 □□□로 나온다)
        if (UIFactory.Font == null) {
            UIFactory.Font = Resources.Load<TMP_FontAsset>("Fonts/KoreanSDF")
                          ?? TMP_Settings.defaultFontAsset;
        }

        CreateStartGate();

        if (cameraController != null) cameraController.InputLocked = true;
    }

    void Update() {
        if (gateRoot == null) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Begin();
    }

    private void Begin() {
        Destroy(gateRoot);
        gateRoot = null;

        if (cameraController != null) cameraController.InputLocked = false;

        Debug.Log("[EntrySigns] 시작 클릭 — 자유 이동 시작");
    }

    // ---------------------------------------------------------------- 시작 게이트

    /// 시작 지점 바로 앞 눈높이에 세운다. [클릭하세요] 아래로 조작 안내 이미지가 이어진다.
    private void CreateStartGate() {
        Texture2D texture = Resources.Load<Texture2D>(CONTROLLER_IMAGE_PATH);

        if (texture == null) {
            Debug.LogWarning($"[EntrySigns] Resources/{CONTROLLER_IMAGE_PATH} 를 찾지 못했습니다. " +
                             "조작 안내 이미지 없이 [클릭하세요]만 띄웁니다.");
        }

        // 원본 비율 유지 (안내 글자가 찌그러지지 않게)
        float boardHeight = texture != null
            ? BOARD_WIDTH * texture.height / Mathf.Max(1, texture.width)
            : 0f;

        float totalHeight = GUIDE_HEIGHT
                          + (texture != null ? GUIDE_IMAGE_GAP + boardHeight : 0f);

        /*
         * 캔버스가 아래로 길어져도 [클릭하세요]는 눈높이에 있어야 한다.
         * 그래서 캔버스 중심을 글자가 원래 있던 자리에서 아래로 내려 잡는다.
         */
        float guideOffsetY = totalHeight / 2f - GUIDE_HEIGHT / 2f;

        Vector3 anchor = StoreLayout.SPAWN_POSITION + new Vector3(0f, 0.05f, 1.6f);
        Vector3 position = anchor - Vector3.up * (guideOffsetY * CANVAS_SCALE);

        RectTransform canvas = CreateWorldCanvas("StartGate", position, GATE_WIDTH, totalHeight);
        gateRoot = canvas.gameObject;

        Image panel = UIFactory.CreateImage("Panel", canvas, new Color(0f, 0f, 0f, 0f));
        UIFactory.Stretch(panel.rectTransform);

        TextMeshProUGUI guide = UIFactory.CreateText("Guide", canvas, "[클릭하세요]", 76f, FontStyles.Bold, INK);
        Place(guide.rectTransform, 0f, guideOffsetY, GATE_WIDTH, GUIDE_HEIGHT);
        guide.alignment = TextAlignmentOptions.Center;

        if (texture == null) return;

        RawImage image = UIFactory.CreateRawImage("ControllerBoard", canvas, texture);
        Place(image.rectTransform, 0f, -(totalHeight / 2f - boardHeight / 2f), BOARD_WIDTH, boardHeight);
        image.raycastTarget = false;
    }

    // ---------------------------------------------------------------- 공통

    private RectTransform CreateWorldCanvas(string name, Vector3 position,
                                            float width, float height) {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.position = position;

        // 회전 0이 정답. 돌리면 뒷면이 보여 글자가 좌우로 뒤집힌다.
        // 읽는 사람은 캔버스의 -Z 쪽에 서야 하는데, 플레이어가 이미 그 자리(광장)에 있다.
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one * CANVAS_SCALE;

        Canvas canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        obj.AddComponent<CanvasScaler>();
        obj.AddComponent<GraphicRaycaster>();

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        return rect;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
