using UnityEngine;

/// FBX마다 원점과 실제 크기가 달라서, 배치는 모두 렌더러 바운즈를 재는 방식으로 통일한다.
/// 회전 → 높이 맞춤 → 기준점 정렬 순서는 같고, 기준점만 다르다.
/// (전시대는 밑면 한가운데, 시착은 gripPoint)
public static class BagModelUtil {

    /// 가방 밑면 한가운데. 전시대에 세울 때 스툴 상판에 닿는 지점.
    public static readonly Vector3 BOTTOM_CENTER = new Vector3(0.5f, 0f, 0.5f);

    /// 가방 윗면 한가운데. 스트랩·손잡이 꼭대기라 몸에 닿는 지점이 된다.
    public static readonly Vector3 DEFAULT_GRIP_POINT = new Vector3(0.5f, 1f, 0.5f);

    /// 전시대 위에 모델을 놓는다. centerXZ 는 Vector2(x, z).
    /// modelRotation(모델별 보정)을 먼저 걸고 facingYaw(전시 방향)를 나중에 곱해야 yaw 축이 같이 눕지 않는다.
    public static GameObject Place(GameObject prefab, Transform parent, Vector2 centerXZ,
                                   float bottomWorldY, float targetHeight,
                                   Quaternion modelRotation, float facingYaw) {
        if (prefab == null) return null;

        GameObject instance = Object.Instantiate(prefab, parent);
        instance.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f) * modelRotation;

        FitToHeight(instance, targetHeight);
        AlignBoundsPoint(instance, BOTTOM_CENTER,
                         new Vector3(centerXZ.x, bottomWorldY, centerXZ.y));

        return instance;
    }

    /// 시착 자세 적용. 가방은 고정점(BagAnchor)의 자식이어야 한다.
    /// 고정점과 gripPoint 한 점을 맞물려 두면 신장이 달라져도 걸린 자리가 유지된다.
    /// 런타임과 에디터 도구가 같은 결과를 내야 해서 두 곳 모두 이 함수만 쓴다.
    public static void ApplyHoldPose(GameObject bag, BagModelEntry entry, float targetHeight) {
        if (bag == null) return;

        bag.transform.localRotation = entry != null ? entry.HoldRotation : Quaternion.identity;
        bag.transform.localScale = Vector3.one;
        bag.transform.localPosition = Vector3.zero;

        float multiplier = entry != null ? Mathf.Max(0.01f, entry.heightMultiplier) : 1f;
        FitToHeight(bag, targetHeight * multiplier);

        // localPosition을 0으로 둔 상태라 지금 위치가 곧 고정점의 월드 좌표다
        Vector3 anchorWorld = bag.transform.position;
        Vector3 grip = entry != null ? entry.gripPoint : DEFAULT_GRIP_POINT;

        AlignBoundsPoint(bag, grip, anchorWorld);
    }

    /// 바운즈 안의 한 지점을 지정한 월드 좌표로 옮긴다.
    /// ratio 는 바운즈 대비 비율. (0.5, 0, 0.5) = 밑면 한가운데, (0.5, 1, 0.5) = 윗면 한가운데.
    public static void AlignBoundsPoint(GameObject instance, Vector3 ratio, Vector3 worldTarget) {
        if (!TryGetWorldBounds(instance, out Bounds bounds)) return;

        Vector3 current = bounds.min + Vector3.Scale(ratio, bounds.size);
        instance.transform.position += worldTarget - current;
    }

    /// 월드 좌표를 바운즈 안 비율로 역산한다 (에디터에서 gripPoint를 저장할 때 쓴다).
    public static Vector3 ToGripRatio(Bounds bounds, Vector3 worldPoint) {
        return new Vector3(
            Ratio(bounds.min.x, bounds.size.x, worldPoint.x),
            Ratio(bounds.min.y, bounds.size.y, worldPoint.y),
            Ratio(bounds.min.z, bounds.size.z, worldPoint.z));
    }

    private static float Ratio(float min, float size, float value) {
        // 두께가 거의 0인 축(납작한 가방)은 비율을 구할 수 없으므로 가운데로 둔다
        return size <= 0.0001f ? 0.5f : (value - min) / size;
    }

    /// 모델 높이를 targetHeight 로 맞춘다 (부모 스케일이 있어도 결과 높이는 동일).
    public static void FitToHeight(GameObject instance, float targetHeight) {
        if (!TryGetWorldBounds(instance, out Bounds bounds)) return;
        if (bounds.size.y <= 0.0001f) return;

        instance.transform.localScale *= targetHeight / bounds.size.y;
    }

    /// 자식까지 포함한 월드 바운즈.
    /// FBX 원점은 파일마다 달라 믿을 수 없으므로, 눈에 보이는 형상을 월드 기준으로 재서 배치에 쓴다.
    public static bool TryGetWorldBounds(GameObject instance, out Bounds bounds) {
        bounds = new Bounds();

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return false;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    /// FBX에 콜라이더가 없어서 클릭 판정용 박스를 붙인다.
    /// size 는 로컬 기준이라 월드 바운즈를 lossyScale 로 나눠 넣어야 크기가 맞는다.
    public static void EnsureBoxCollider(GameObject instance) {
        if (instance.GetComponentInChildren<Collider>() != null) return;
        if (!TryGetWorldBounds(instance, out Bounds bounds)) return;

        BoxCollider collider = instance.AddComponent<BoxCollider>();
        collider.center = instance.transform.InverseTransformPoint(bounds.center);

        Vector3 lossy = instance.transform.lossyScale;
        collider.size = new Vector3(
            SafeDivide(bounds.size.x, lossy.x),
            SafeDivide(bounds.size.y, lossy.y),
            SafeDivide(bounds.size.z, lossy.z));
    }

    private static float SafeDivide(float value, float divisor) {
        return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
    }
}
