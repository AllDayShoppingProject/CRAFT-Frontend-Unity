using System.Collections.Generic;
using UnityEngine;

/// 상세 보기·시착·사전등록 데이터를 이벤트로 변환·집계해 GazeLogger에 넘기는 어댑터.
/// 실제 버퍼링과 전송은 GazeLogger가 담당한다 (전송 경로를 하나로 유지).
public class SessionDataManager : MonoBehaviour {

    public static SessionDataManager Instance { get; private set; }

    private GazeLogger gazeLogger;

    private readonly List<BagViewData> bagViewDataList = new List<BagViewData>();
    private readonly HashSet<int> viewedProducts = new HashSet<int>();

    private float sessionStartTime;

    public int ProductsViewedCount => viewedProducts.Count;
    public int TryOnCount { get; private set; }
    public int PreregCount { get; private set; }
    public float SessionDuration => Time.time - sessionStartTime;

    private string CustomerId =>
        SessionManager.Instance != null ? SessionManager.Instance.CustomerId : null;

    void Awake() {
        if (Instance != null && Instance != this) {
            // gameObject를 지우면 그 아래 매장·스툴·가방까지 사라지므로 컴포넌트만 지운다.
            Destroy(this);
            return;
        }

        Instance = this;
        sessionStartTime = Time.time;
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    void Start() {
        if (gazeLogger == null) gazeLogger = FindFirstObjectByType<GazeLogger>();
    }

    public void SetLogger(GazeLogger logger) {
        gazeLogger = logger;
    }

    /// 예전 API 호환용. 세션 소유자는 SessionManager라 여기서는 보관하지 않는다.
    public void SetCustomerId(string id) {
        if (SessionManager.Instance != null) return;
        Debug.Log($"[SessionDataManager] customerId={id} (SessionManager 없음, 로컬 로깅만)");
    }

    /// 씬 진입 (세션당 씬별로 한 번). 신규 응시/시착과 달리 반복 이벤트가 아니라
    /// interaction_id/occurrence_index는 붙이지 않는다.
    public void LogSceneEnter(int sceneId) {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("scene_enter", CustomerId, new GazeLogger.EventMeta {
            scene_id = sceneId,
        });
    }

    /// 출구 프롬프트가 허공에 뜬 순간.
    public void LogExitPromptOpen() {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("exit_prompt_open", CustomerId, new GazeLogger.EventMeta());
    }

    /// choice: "leave"(나가기 클릭) | "stay"(멀어져서 프롬프트가 그냥 닫힘)
    public void LogExitPromptResponse(string choice) {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("exit_prompt_response", CustomerId, new GazeLogger.EventMeta {
            choice = choice,
        });
    }

    /// 가방 상세 보기 1회가 끝났을 때
    public void AddBagViewData(BagViewData data) {
        if (data == null) return;

        bagViewDataList.Add(data);
        if (data.product_id > 0) viewedProducts.Add(data.product_id);

        Debug.Log($"[SessionDataManager] popup_close - {data.product_id}, " +
                  $"{data.duration_sec:F1}s, color: {data.color}, " +
                  $"color changes: {data.color_change_count}");

        if (gazeLogger == null) return;

        gazeLogger.AddEvent("popup_close", CustomerId, new GazeLogger.EventMeta {
            product_id = data.product_id,
            duration_sec = data.duration_sec,
            color = data.color,
            change_index = data.color_change_count,
            close_reason = "user",
            occurrence_index = data.occurrence_index,
        }, data.interaction_id);
    }

    /// 스툴 클릭으로 상세 팝업이 열렸을 때. 현재는 응시 10초 자동 오픈이 없어 trigger는 항상 card_click이다.
    /// interaction_id/occurrence_index를 data에 채워 넣는다 - popup_close가 같은 값을 다시 실어야
    /// 이번 방문(회차)이 서버에서 하나로 묶인다 (명세서 §4.1).
    public void LogPopupOpen(BagViewData data, string trigger, float gazeDurationSec) {
        if (data == null) return;

        data.interaction_id = System.Guid.NewGuid().ToString();
        data.occurrence_index = gazeLogger != null
            ? gazeLogger.NextOccurrenceIndex("popup", data.product_id)
            : 1;

        if (gazeLogger == null) return;

        gazeLogger.AddEvent("popup_open", CustomerId, new GazeLogger.EventMeta {
            product_id = data.product_id,
            trigger = trigger,
            gaze_duration_sec = gazeDurationSec,
            occurrence_index = data.occurrence_index,
        }, data.interaction_id);
    }

    /// 상세정보(헤리티지) 아코디언을 펼쳤다가 접은 구간 하나.
    public void LogDetailView(int productId, string section, float durationSeconds) {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("detail_view", CustomerId, new GazeLogger.EventMeta {
            product_id = productId,
            section = section,
            duration_sec = durationSeconds,
        });
    }

    public void LogColorChange(int productId, string fromColor, string toColor, float heldSeconds, int changeIndex) {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("color_change", CustomerId, new GazeLogger.EventMeta {
            product_id = productId,
            duration_sec = heldSeconds,
            color = toColor,
            from_color = fromColor,
            to_color = toColor,
            change_index = changeIndex,
        });
    }

    /// color_change는 직전 컬러의 체류 시간을 담기 때문에 마지막으로 고른 색은 기록되지 않는다.
    /// 그래서 닫는 시점에 to_color 없이 is_final로 마지막 구간을 한 번 더 발행한다 (명세서 §4.1).
    public void LogFinalColor(int productId, string color, float heldSeconds, int changeIndex) {
        if (gazeLogger == null || string.IsNullOrEmpty(color)) return;

        gazeLogger.AddEvent("color_change", CustomerId, new GazeLogger.EventMeta {
            product_id = productId,
            duration_sec = heldSeconds,
            color = color,
            from_color = color,
            to_color = null,
            change_index = changeIndex,
            is_final = true,
        });
    }

    /// 시착 1회 시작. 반환한 (interactionId, occurrenceIndex)를 호출한 쪽이 들고 있다가
    /// LogTryOnEnd에 그대로 넘겨야 시작/종료가 같은 회차로 묶인다 (명세서 §4.1).
    public (string interactionId, int occurrenceIndex) LogTryOnStart(int productId, string color, int dummyHeight) {
        TryOnCount++;

        string interactionId = System.Guid.NewGuid().ToString();
        int occurrenceIndex = gazeLogger != null
            ? gazeLogger.NextOccurrenceIndex("tryon", productId)
            : 1;

        if (gazeLogger != null) {
            gazeLogger.AddEvent("tryon_start", CustomerId, new GazeLogger.EventMeta {
                product_id = productId,
                color = color,
                dummy_height = dummyHeight,
                occurrence_index = occurrenceIndex,
            }, interactionId);
        }

        return (interactionId, occurrenceIndex);
    }

    public void LogTryOnEnd(int productId, float durationSeconds, string interactionId, int occurrenceIndex) {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("tryon_end", CustomerId, new GazeLogger.EventMeta {
            product_id = productId,
            duration_sec = durationSeconds,
            occurrence_index = occurrenceIndex,
        }, interactionId);
    }

    public void LogPreregFormOpen(int productId) {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("prereg_form_open", CustomerId, new GazeLogger.EventMeta {
            product_id = productId,
        });
    }

    public void LogPreregDismiss(string stage) {
        if (gazeLogger == null) return;

        gazeLogger.AddEvent("prereg_dismiss", CustomerId, new GazeLogger.EventMeta {
            stage = stage,
        });
    }

    public void LogPreregSubmit(int productId, string color, string size) {
        PreregCount++;

        if (gazeLogger == null) return;

        // 개인정보(이름/전화번호)는 events에 절대 기록하지 않는다 (명세서)
        gazeLogger.AddEvent("prereg_submit", CustomerId, new GazeLogger.EventMeta {
            product_id = productId,
            color = color,
            size = size,
            has_consent = true,
        });
    }

    public List<BagViewData> GetAllData() {
        return new List<BagViewData>(bagViewDataList);
    }

    public void ClearData() {
        bagViewDataList.Clear();
        viewedProducts.Clear();
        TryOnCount = 0;
        PreregCount = 0;
    }


}
