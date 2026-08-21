using System;
using System.Collections.Generic;
using UnityEngine;

public class GazeLogger : MonoBehaviour
{
    /// 이벤트에는 식별자만 담는다. 이름·연락처 같은 개인정보는 절대 넣지 않는다.
    [Serializable]
    public class GazeEvent
    {
        public string event_id;
        public string event_type;
        public string customer_id;
        public string timestamp;
        public string interaction_id;
        public EventMeta meta;
    }

    /// 명세서 §4 의 meta{} 필드 모음.
    /// JsonUtility가 안 쓰는 필드까지 0/"" 로 내보내지만 서버가 무시하므로 한 클래스로 합쳤다.
    [Serializable]
    public class EventMeta
    {
        // 공통 - 대상 식별
        public int product_id;
        public int occurrence_index;
        public float duration_sec;

        // session_start
        public string referrer;
        public string device_type;
        public int viewport_w;
        public int viewport_h;

        public int scene_id;

        // tutorial_*
        public int page_index;

        public int max_zoom_level;

        // popup_open / popup_close
        public string trigger;
        public float gaze_duration_sec;
        public string close_reason;
        public float dwell_sec;

        // detail_view
        public string section;

        public string color;
        public string from_color;
        public string to_color;
        public int change_index;

        /// 마지막 컬러 구간을 한 번 더 발행하는 표시. 없으면 마지막 색의 유지 시간이 유실된다.
        public bool is_final;

        // tryon_*
        public int dummy_height;
        public string pose_id;
        public string size;

        // prereg_*
        public float delay_sec;
        public string stage;
        public bool has_consent;

        // exit_prompt_response
        public string choice;

        // session_end
        public float total_duration_sec;
        public int products_viewed_count;
        public int tryon_count;
        public int prereg_count;
        public string exit_reason;
    }

    public class GazeSession
    {
        public string interactionId;
        public int occurrenceIndex;
    }

    private readonly List<GazeEvent> eventBuffer = new();

    private readonly Dictionary<int, int> occurrenceCounts = new();

    /// popup_open/close, tryon_start/end처럼 응시(gaze)와 별도로 회차를 세야 하는 이벤트용.
    /// "category:productId"로 키를 잡아 같은 제품이라도 이벤트 종류별로 따로 카운트한다.
    private readonly Dictionary<string, int> categoryOccurrenceCounts = new();

    /// 같은 세션 + 같은 product_id 기준 몇 번째 발생인지 (1부터). category는 "popup", "tryon" 등.
    public int NextOccurrenceIndex(string category, int productId)
    {
        string key = category + ":" + productId;
        int next = categoryOccurrenceCounts.TryGetValue(key, out int current) ? current + 1 : 1;
        categoryOccurrenceCounts[key] = next;
        return next;
    }

    public GazeSession StartGaze(
        int productId,
        string customerId,
        string color
    )
    {
        string interactionId =
            Guid.NewGuid().ToString();

        int occurrenceIndex =
            GetNextOccurrenceIndex(productId);

        eventBuffer.Add(
            new GazeEvent
            {
                event_id = Guid.NewGuid().ToString(),
                event_type = "view_start",
                customer_id = customerId,
                timestamp = DateTime.UtcNow.ToString("o"),
                interaction_id = interactionId,

                meta = new EventMeta
                {
                    product_id = productId,
                    occurrence_index = occurrenceIndex,
                    duration_sec = 0f,
                    color = color
                }
            }
        );

        return new GazeSession
        {
            interactionId = interactionId,
            occurrenceIndex = occurrenceIndex
        };
    }

    public void EndGaze(
        int productId,
        string customerId,
        string interactionId,
        int occurrenceIndex,
        float duration
    )
    {
        const float minimumGazeTime = 2f;

        if (duration < minimumGazeTime)
        {
            RemoveStartEvent(interactionId);
            return;
        }

        eventBuffer.Add(
            new GazeEvent
            {
                event_id = Guid.NewGuid().ToString(),
                event_type = "view_end",
                customer_id = customerId,
                timestamp = DateTime.UtcNow.ToString("o"),
                interaction_id = interactionId,

                meta = new EventMeta
                {
                    product_id = productId,
                    occurrence_index = occurrenceIndex,
                    duration_sec = duration
                }
            }
        );
    }

    public void AddSessionEnd(
        string customerId,
        float totalDuration,
        int productsViewedCount,
        int tryonCount,
        int preregCount,
        string exitReason
    )
    {
        eventBuffer.Add(
            new GazeEvent
            {
                event_id = Guid.NewGuid().ToString(),
                event_type = "session_end",
                customer_id = customerId,
                timestamp = DateTime.UtcNow.ToString("o"),
                interaction_id = null,

                meta = new EventMeta
                {
                    total_duration_sec = totalDuration,
                    products_viewed_count = productsViewedCount,
                    tryon_count = tryonCount,
                    prereg_count = preregCount,
                    exit_reason = exitReason
                }
            }
        );
    }

    /// 응시 외의 이벤트도 같은 버퍼에 쌓는다. 전송/유실 처리를 한 곳에서만 하기 위해서다.
    public void AddEvent(
        string eventType,
        string customerId,
        EventMeta meta = null,
        string interactionId = null
    )
    {
        if (string.IsNullOrEmpty(eventType)) return;

        eventBuffer.Add(
            new GazeEvent
            {
                event_id = Guid.NewGuid().ToString(),
                event_type = eventType,
                customer_id = customerId,
                timestamp = DateTime.UtcNow.ToString("o"),
                interaction_id = interactionId,
                meta = meta ?? new EventMeta()
            }
        );
    }

    public int BufferedCount => eventBuffer.Count;

    private int GetNextOccurrenceIndex(int productId)
    {
        if (!occurrenceCounts.ContainsKey(productId))
        {
            occurrenceCounts[productId] = 1;
            return 1;
        }

        occurrenceCounts[productId]++;
        return occurrenceCounts[productId];
    }

    private void RemoveStartEvent(string interactionId)
    {
        eventBuffer.RemoveAll(
            e =>
                e.event_type == "view_start" &&
                e.interaction_id == interactionId
        );
    }

    public List<GazeEvent> GetEventBuffer()
    {
        return new List<GazeEvent>(eventBuffer);
    }

    public void Clear()
    {
        eventBuffer.Clear();
        occurrenceCounts.Clear();
        categoryOccurrenceCounts.Clear();
    }
}