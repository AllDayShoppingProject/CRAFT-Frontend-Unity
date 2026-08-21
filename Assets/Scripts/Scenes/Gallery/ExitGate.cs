using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// 광장 뒷벽 출구 앞에 뜨는 [나가기] 버튼 (명세서 SCENE_06_EXIT_PROMPT).
///
/// 출구에 가까워지면 허공에 버튼이 나타나고, 누르면
///   세션 데이터 전송 → 페이드 아웃 → Intro 씬 복귀
/// 순서로 진행한다.
///
/// 전송이 끝나기 전에 씬을 넘기면 안 된다. 이벤트는 관람 내내 메모리에만 쌓여 있다가
/// 이때 한 번에 나가기 때문에, 기다리지 않으면 한 세션 분량이 통째로 사라진다.
public class ExitGate : MonoBehaviour {

    /// 출구 중심에서 이 거리 안에 들어오면 버튼이 뜬다.
    /// 통행 한계가 문턱 앞(-6.5)이라 실제로 닿을 수 있는 최단 거리는 약 0.5m다.
    private const float TRIGGER_DISTANCE = 2.6f;

    /// 버튼이 깜빡이지 않도록 사라지는 거리는 조금 더 멀게 잡는다
    private const float RELEASE_DISTANCE = 3.0f;

    private const float FADE_SECONDS = 0.9f;

    /// 버튼 높이는 눈높이가 아니라 고정값이다. 어느 각도에서 보든 문 앞 이 자리에 뜬다.
    private const float PROMPT_HEIGHT = 1.5f;

    /// 월드 스페이스 캔버스는 1유닛 = 1m다. 픽셀로 디자인하고 이 배율로 줄인다.
    private const float CANVAS_SCALE = 0.0022f;

    private static readonly Color BUTTON_BG = new Color(0.12f, 0.12f, 0.13f, 0.94f);

    private CameraController cameraController;
    private GameObject prompt;
    private bool isExiting;

    /// 출구에서 한 번이라도 충분히 멀어졌는지. 스폰 지점이 트리거 안쪽이라 필요하다.
    private bool armed;

    public static ExitGate Create(Transform parent, CameraController controller) {
        GameObject root = new GameObject("ExitGate");
        root.transform.SetParent(parent, false);

        ExitGate gate = root.AddComponent<ExitGate>();
        gate.cameraController = controller;
        gate.Build();

        return gate;
    }

    private void Build() {
        if (UIFactory.Font == null) {
            UIFactory.Font = Resources.Load<TMP_FontAsset>("Fonts/KoreanSDF")
                          ?? TMP_Settings.defaultFontAsset;
        }

        CreatePrompt();
    }

    void Update() {
        if (isExiting || prompt == null) return;

        Camera viewer = Camera.main;
        if (viewer == null) return;

        // 높이 차이는 무시한다. 눈높이 1.7m가 거리에 섞이면 판정이 들쭉날쭉해진다.
        Vector3 door = StoreLayout.ExitDoorCenter;
        Vector3 eye = viewer.transform.position;

        float distance = Vector2.Distance(
            new Vector2(eye.x, eye.z), new Vector2(door.x, door.z));

        // 스폰 지점(z = -5)은 출구(z = -7)에서 2.0m라 TRIGGER_DISTANCE(2.6m) 안쪽이다.
        // 그대로 두면 입장하자마자 [나가기]가 뜨면서 시야 회전이 잠기고,
        // 앞으로 걸어 z > -4.0 (RELEASE_DISTANCE 3.0m) 을 넘겨야 풀렸다.
        // 이동은 일부러 막지 않으니, 걸어야 시야가 도는 것처럼 보였다.
        // 한 번 출구에서 충분히 멀어진 뒤부터 버튼이 뜨도록 한다.
        // (덤으로 세션 시작 순간의 가짜 exit_prompt_open / "stay" 로그도 사라진다)
        if (!armed) {
            if (distance > RELEASE_DISTANCE) armed = true;
            return;
        }

        bool visible = prompt.activeSelf;

        // 버튼을 누르려면 커서가 보여야 한다 - 뜨고 사라질 때 시점 회전과 같이 맞바꾼다.
        if (!visible && distance <= TRIGGER_DISTANCE) {
            prompt.SetActive(true);
            if (cameraController != null) cameraController.SetLookPaused(true);

            if (SessionDataManager.Instance != null) SessionDataManager.Instance.LogExitPromptOpen();
        } else if (visible && distance > RELEASE_DISTANCE) {
            prompt.SetActive(false);
            if (cameraController != null) cameraController.SetLookPaused(false);

            // 나가기를 누르지 않고 멀어진 경우 - "머물기"로 응답한 것으로 본다
            if (SessionDataManager.Instance != null) SessionDataManager.Instance.LogExitPromptResponse("stay");
        }

        // 버튼이 떠 있는 동안엔 커서를 매 프레임 강제한다.
        //
        // CameraController.InputLocked 의 setter 가 lookPaused 를 보지 않기 때문이다.
        // 예를 들어 EntrySigns 가 [클릭하세요] 해제 시 InputLocked = false 로 되돌리면
        // 그 setter 가 곧바로 SetFreeRoamCursor() 를 불러 커서를 다시 잠근다 —
        // 이 버튼이 떠 있어 커서가 필요한 상황인데도.
        // 버튼이 보이는 동안은 무조건 커서가 보여야 하므로 상태를 계속 확인한다.
        if (prompt.activeSelf &&
            (!Cursor.visible || Cursor.lockState != CursorLockMode.None)) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ---------------------------------------------------------------- 버튼

    private void CreatePrompt() {
        Vector3 door = StoreLayout.ExitDoorCenter;

        // 문 앞 고정 높이에 띄운다. 벽에 겹치지 않게 광장 쪽으로 조금 당긴다.
        Vector3 position = new Vector3(door.x, PROMPT_HEIGHT, door.z + 0.9f);

        GameObject obj = new GameObject("ExitPrompt");
        obj.transform.SetParent(transform, false);
        obj.transform.position = position;

        // 고정 회전. 플레이어는 광장 안쪽(+Z)에서 출구 쪽(-Z)으로 다가오므로
        // 캔버스 정면(글자가 읽히는 면)이 -Z를 향해야 한다.
        obj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        obj.transform.localScale = Vector3.one * CANVAS_SCALE;

        Canvas canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // 월드 스페이스 캔버스는 이 카메라를 지정해야 클릭 판정이 선다
        canvas.worldCamera = Camera.main;

        obj.AddComponent<CanvasScaler>();
        obj.AddComponent<GraphicRaycaster>();

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 130f);

        Button button = UIFactory.CreateButton("ExitButton", rect, "[나가기]",
            BUTTON_BG, Color.white, HandleExitClicked);

        UIFactory.Stretch(button.GetComponent<RectTransform>());

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.fontSize = 54f;
        label.fontStyle = FontStyles.Bold;

        prompt = obj;
        prompt.SetActive(false);
    }

    // ---------------------------------------------------------------- 종료

    private void HandleExitClicked() {
        if (isExiting) return;

        isExiting = true;
        StartCoroutine(ExitRoutine());
    }

    private IEnumerator ExitRoutine() {
        Debug.Log("[ExitGate] 나가기 - 세션을 종료하고 Intro로 돌아갑니다.");

        if (SessionDataManager.Instance != null) SessionDataManager.Instance.LogExitPromptResponse("leave");

        // 페이드 도중에 움직이거나 다른 걸 누르지 못하게 막는다
        if (cameraController != null) cameraController.InputLocked = true;
        if (prompt != null) prompt.SetActive(false);

        CanvasGroup fade = CreateFadeOverlay();

        // 전송과 페이드를 같이 돌린다. 네트워크가 느려도 화면은 바로 어두워진다.
        Coroutine sending = null;
        if (SessionManager.Instance != null) {
            sending = SessionManager.Instance.StartCoroutine(
                SessionManager.Instance.EndSessionFromScene("exit_gate"));
        }

        yield return FadeIn(fade);

        // 페이드가 끝나도 전송이 남았으면 기다린다. 여기서 넘기면 로그가 유실된다.
        if (sending != null) yield return sending;

        // 갤러리 씬만 단독으로 Play하면 Intro가 빌드 목록에 없을 수 있다.
        // 그대로 부르면 예외가 나면서 검은 화면에 갇힌다.
        if (!Application.CanStreamedLevelBeLoaded(SceneNames.Intro)) {
            Debug.LogWarning($"[ExitGate] '{SceneNames.Intro}' 씬을 불러올 수 없습니다. " +
                             "File > Build Settings 에 추가되어 있는지 확인해 주세요.");

            fade.alpha = 0f;
            fade.blocksRaycasts = false;

            if (cameraController != null) cameraController.InputLocked = false;
            yield break;
        }

        SceneManager.LoadScene(SceneNames.Intro);
    }

    private CanvasGroup CreateFadeOverlay() {
        GameObject obj = new GameObject("ExitFade");

        // 씬 루트에 둔다. 이 게이트가 씬 전환으로 사라져도 화면이 다시 밝아지지 않도록.
        Canvas canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // BagViewUI(100)보다 위

        CanvasGroup group = obj.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true;

        Image black = UIFactory.CreateImage("Black", obj.transform, Color.black);
        UIFactory.Stretch(black.rectTransform);

        return group;
    }

    private IEnumerator FadeIn(CanvasGroup group) {
        float elapsed = 0f;

        while (elapsed < FADE_SECONDS) {
            // 씬을 넘기는 중이라 Time.timeScale에 휘둘리면 안 된다
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / FADE_SECONDS);

            yield return null;
        }

        group.alpha = 1f;
    }
}
