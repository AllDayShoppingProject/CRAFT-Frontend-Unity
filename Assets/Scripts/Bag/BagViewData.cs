using System;

/// 가방 상세 보기 1회(popup_open ~ popup_close)의 집계용 중간 기록. 서버 DTO가 아니다.
[System.Serializable]
public class BagViewData {
    public int product_id;
    public string color;
    public string size;
    public float duration_sec;
    public int color_change_count;
    public DateTime start_time;
    public DateTime end_time;

    /// popup_open/popup_close 한 쌍을 묶는 값. 명세서 §4.1 - 이 방문(회차) 전체에서 공유한다.
    public string interaction_id;
    public int occurrence_index;
}
