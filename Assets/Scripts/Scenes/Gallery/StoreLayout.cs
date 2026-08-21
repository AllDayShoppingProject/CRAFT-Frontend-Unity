using UnityEngine;

/// 매장 + 입구 광장의 치수와 통행 영역.
/// 구조물 생성(GalleryController)과 이동 제한(CameraController)이 같은 값을 봐야 어긋나지 않는다.
///
/// 평면도 (위에서 본 모습, +Z가 매장 안쪽)
///
///        z=10 ┌───────────────┐
///             │    갤러리      │   x: -4 ~ +4
///         z=0 ├────┐     ┌────┤   ← 파사드: 가운데 4.4m가 뚫린 출입구
///             │    입구 광장   │   x: -6.5 ~ +6.5
///        z=-7 └───────────────┘
public static class StoreLayout {

    public const float STORE_WIDTH = 8f;
    public const float STORE_DEPTH = 10f;
    public const float STORE_HEIGHT = 3.2f;

    public const float ENTRANCE_WIDTH = 13f;
    public const float ENTRANCE_DEPTH = 7f;
    public const float ENTRANCE_HEIGHT = 4.2f;

    // 출입구 (문 없는 오픈 파사드)
    public const float DOOR_WIDTH = 4.4f;
    public const float DOOR_HEIGHT = 2.7f;

    /// 문턱 구간(파사드 벽 두께보다 넉넉하게). 이 안에서는 x가 문 폭으로 제한된다.
    public const float DOOR_BAND = 0.4f;

    /// 벽에서 띄우는 여유
    public const float WALL_MARGIN = 0.5f;

    public const float WALL_THICKNESS = 0.1f;

    public const float EYE_HEIGHT = 1.7f;

    /// 시작 위치 (명세서 SCENE_01). 파사드에서 5m 물러나야 매장과 엠블럼이 한 화면에 들어온다.
    public static readonly Vector3 SPAWN_POSITION = new Vector3(0f, EYE_HEIGHT, -5f);

    // 출구 (광장 뒷벽 z = -ENTRANCE_DEPTH)
    //
    // 매장 출입구(z = 0)의 반대편이다. 통행 한계가 -ENTRANCE_DEPTH + WALL_MARGIN 이라
    // 문턱까지는 못 간다. 걸어 나가는 게 아니라, 가까이 가면 뜨는 [나가기]를 눌러 종료한다.
    public const float EXIT_WIDTH = 4.4f;
    public const float EXIT_HEIGHT = 2.7f;

    /// 출구 한가운데. [나가기] 버튼 위치와 근접 판정의 기준점.
    public static Vector3 ExitDoorCenter =>
        new Vector3(0f, EXIT_HEIGHT / 2f, -ENTRANCE_DEPTH);

    // 포토 부스 (광장 오른쪽 벽)
    public const float BOOTH_Z = -3.5f;
    public const float BOOTH_WIDTH = 3.6f;
    public const float BOOTH_HEIGHT = 2.8f;

    /// 배경 벽에서 아바타까지의 거리
    public const float BOOTH_STAND_OFFSET = 1.15f;

    /// 배경판 중심 (오른쪽 벽 안쪽에 살짝 띄워 붙인다)
    public static Vector3 BoothBackdropCenter =>
        new Vector3(ENTRANCE_WIDTH / 2f - 0.06f, BOOTH_HEIGHT / 2f, BOOTH_Z);

    /// 아바타가 서는 위치 (바닥 윗면에 발이 닿도록)
    public static Vector3 BoothStandPosition =>
        new Vector3(ENTRANCE_WIDTH / 2f - BOOTH_STAND_OFFSET, WALL_THICKNESS / 2f + 0.05f, BOOTH_Z);

    /// 배경을 등지고 광장 쪽(-X)을 바라보는 회전
    public static Quaternion BoothStandRotation => Quaternion.Euler(0f, -90f, 0f);

    /// 해당 좌표에 서 있을 수 있는지
    public static bool IsWalkable(Vector3 position) {
        float x = Mathf.Abs(position.x);
        float z = position.z;

        // 출입구 통로
        if (z > -DOOR_BAND && z < DOOR_BAND) {
            return x <= DOOR_WIDTH / 2f - WALL_MARGIN;
        }

        // 갤러리 내부
        if (z >= DOOR_BAND) {
            return x <= STORE_WIDTH / 2f - WALL_MARGIN
                && z <= STORE_DEPTH - WALL_MARGIN;
        }

        // 입구 광장
        return x <= ENTRANCE_WIDTH / 2f - WALL_MARGIN
            && z >= -ENTRANCE_DEPTH + WALL_MARGIN;
    }

    /// 이동 결과를 통행 가능한 위치로 보정한다.
    /// X와 Z를 따로 검사해야 벽에 비스듬히 부딪혀도 멈추지 않고 벽을 타고 미끄러진다.
    public static Vector3 Resolve(Vector3 current, Vector3 desired) {
        Vector3 result = current;

        Vector3 tryX = new Vector3(desired.x, current.y, current.z);
        if (IsWalkable(tryX)) result.x = desired.x;

        Vector3 tryZ = new Vector3(result.x, current.y, desired.z);
        if (IsWalkable(tryZ)) result.z = desired.z;

        result.y = desired.y;
        return result;
    }
}
