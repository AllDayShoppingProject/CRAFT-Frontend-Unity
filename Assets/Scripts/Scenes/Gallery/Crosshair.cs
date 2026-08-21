using UnityEngine;
using UnityEngine.UI;

/// 화면 중앙 조준점. 자유 이동(FPS 시점) 중에만 보이고,
/// 가방 상세/시착 사이드 패널이 열리면 숨는다.
///
/// 씬에 손으로 배치한 Canvas/Crosshair는 렌더 모드·카메라 참조·정렬 순서를
/// 하나라도 잘못 잡으면 안 보이기 쉽다. 다른 오버레이 UI(BagViewUI, ExitGate)처럼
/// 코드로 만들어 항상 같은 설정으로 뜨게 한다.
public class Crosshair : MonoBehaviour {

    private const float SIZE = 18f;
    private const float THICKNESS = 2f;
    private static readonly Color DOT_COLOR = new Color(1f, 1f, 1f, 0.85f);

    private CameraController cameraController;
    private GameObject dot;

    public static Crosshair Create(Transform parent, CameraController controller) {
        GameObject root = new GameObject("Crosshair");
        root.transform.SetParent(parent, false);

        Crosshair crosshair = root.AddComponent<Crosshair>();
        crosshair.cameraController = controller;
        crosshair.Build();

        return crosshair;
    }

    private void Build() {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // BagViewUI(100)보다는 아래, 3D 화면보다는 위

        gameObject.AddComponent<CanvasScaler>();

        dot = new GameObject("Dot");
        dot.transform.SetParent(transform, false);

        RectTransform rect = dot.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(SIZE, SIZE);

        BuildCross(rect);
    }

    /// 십자 모양: 가로/세로 얇은 막대 두 개, 가운데는 살짝 비운다.
    private void BuildCross(Transform parent) {
        CreateBar(parent, "H_Left", new Vector2(-SIZE * 0.35f, 0f), new Vector2(SIZE * 0.3f, THICKNESS));
        CreateBar(parent, "H_Right", new Vector2(SIZE * 0.35f, 0f), new Vector2(SIZE * 0.3f, THICKNESS));
        CreateBar(parent, "V_Top", new Vector2(0f, SIZE * 0.35f), new Vector2(THICKNESS, SIZE * 0.3f));
        CreateBar(parent, "V_Bottom", new Vector2(0f, -SIZE * 0.35f), new Vector2(THICKNESS, SIZE * 0.3f));
    }

    private void CreateBar(Transform parent, string name, Vector2 offset, Vector2 size) {
        Image bar = UIFactory.CreateImage(name, parent, DOT_COLOR);
        bar.raycastTarget = false;

        RectTransform rect = bar.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    void Update() {
        // 자유 이동 중일 때만 보인다 - 사이드 패널이 열려 있으면 숨긴다.
        bool shouldShow = cameraController != null
            && !cameraController.IsBagViewMode
            && !cameraController.InputLocked
            && !cameraController.IsLookPaused;

        if (dot != null && dot.activeSelf != shouldShow) dot.SetActive(shouldShow);
    }
}
