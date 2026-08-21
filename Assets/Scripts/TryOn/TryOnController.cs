using System;
using System.Collections;
using UnityEngine;

/// 시착 모드. 별도 씬이 아니라 갤러리 씬 안의 "모드"로 동작한다.
/// 신장을 바꿔도 가방은 실제 크기를 유지한다(같이 키우면 크기 비교가 무의미해진다).
public class TryOnController : MonoBehaviour {

    public static TryOnController Instance { get; private set; }

    /// 더미 모델이 있는 신장 단계. 목록이 두 곳에 있으면 어긋나므로 ProjectConfig 한 곳만 쓴다.
    public static int[] HEIGHT_OPTIONS => ProjectConfig.AllowedHeights;

    /// 프로필 미입력(Skip) 시 기본값
    public const int DEFAULT_HEIGHT = 170;

    /// 이번 세션에 쓸 더미 신장. 프로필 height를 옵션 중 최근접 값으로 맞춘다.
    public static int SessionDummyHeight() {
        int? profileHeight = SessionManager.Instance != null ? SessionManager.Instance.Height : null;
        return MatchDummyHeight(profileHeight);
    }

    /// 미입력이면 기본값 기준으로 매칭한다.
    public static int MatchDummyHeight(int? userHeight) {
        int height = userHeight ?? DEFAULT_HEIGHT;

        int best = HEIGHT_OPTIONS[0];
        foreach (int option in HEIGHT_OPTIONS) {
            if (Mathf.Abs(option - height) < Mathf.Abs(best - height)) best = option;
        }

        return best;
    }

    /// 에디터 도구(시착 가방 위치 잡기)도 같은 값을 써야 미리보기와 실제가 일치한다.
    public const float DEFAULT_HELD_BAG_HEIGHT = 0.32f;

    [Tooltip("손에 든 가방의 실제 높이(m). 아바타 신장이 바뀌어도 이 크기를 유지한다")]
    [SerializeField] private float heldBagHeight = DEFAULT_HELD_BAG_HEIGHT;

    [Tooltip("손에 든 가방에 어떤 자세가 적용됐는지 콘솔에 찍는다 (위치가 반영 안 될 때 확인용)")]
    [SerializeField] private bool logHoldPose = false;

    private GameObject avatarPrefab;

    private GameObject avatar;
    private Transform bagAnchor;
    private GameObject heldBag;

    private ProductData currentProduct;
    private ProductColorOption currentColor;

    private int currentHeight = DEFAULT_HEIGHT;
    private float startTime;

    /// tryon_start/tryon_end 한 쌍을 묶는 값. CameraController가 LogTryOnStart 직후 넣어준다.
    private string currentInteractionId;
    private int currentOccurrenceIndex;

    public bool IsActive => avatar != null;
    public int CurrentHeight => currentHeight;
    public Transform AvatarTransform => avatar != null ? avatar.transform : null;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(this);
            return;
        }
        Instance = this;

        avatarPrefab = Resources.Load<GameObject>("Prefabs/Avatar");
    }

    void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    /// 시착 시작. 반환값은 카메라가 프레이밍할 아바타 Transform.
    public Transform Begin(ProductData product, ProductColorOption color,
                           Vector3 position, Quaternion rotation, int height) {
        End();

        currentProduct = product;
        currentColor = color;
        currentHeight = MatchDummyHeight(height);
        startTime = Time.time;

        avatar = avatarPrefab != null
            ? Instantiate(avatarPrefab)
            : AvatarBuilder.Build("TryOnAvatar");

        avatar.name = "TryOnAvatar";
        avatar.transform.position = position;
        avatar.transform.rotation = rotation;

        ApplyHeightImmediate(currentHeight);
        CreateHeldBag();

        Debug.Log($"[TryOnController] tryon_start - product: {product?.product_id}, " +
                  $"color: {color?.color}, dummy_height: {currentHeight}");

        return avatar.transform;
    }

    /// CameraController가 SessionDataManager.LogTryOnStart 직후 그 반환값을 넣어준다.
    public void SetSessionIds(string interactionId, int occurrenceIndex) {
        currentInteractionId = interactionId;
        currentOccurrenceIndex = occurrenceIndex;
    }

    public void End() {
        if (avatar == null) return;

        float durationSec = Time.time - startTime;

        if (SessionDataManager.Instance != null) {
            SessionDataManager.Instance.LogTryOnEnd(
                currentProduct != null ? currentProduct.product_id : 0,
                durationSec, currentInteractionId, currentOccurrenceIndex);
        }

        currentInteractionId = null;
        currentOccurrenceIndex = 0;

        Destroy(avatar);
        avatar = null;
        heldBag = null;
        bagAnchor = null;
        currentProduct = null;
        currentColor = null;
    }

    /// 인형만 커지고 가방은 실제 크기를 유지한다(고정점에 스케일 역수가 걸려 있어 자동으로 된다).
    public void SetHeight(int heightCm) {
        if (avatar == null) return;

        currentHeight = MatchDummyHeight(heightCm);
        ApplyHeightImmediate(currentHeight);
    }

    public void SetColor(ProductColorOption color) {
        currentColor = color;
        if (color == null) return;

        // 상세 보기로 돌아가도 유지되도록 이 가방의 색으로 확정한다
        if (currentProduct != null) BagColorState.SetColor(currentProduct.product_id, color.color);

        if (heldBag == null || bagAnchor == null) return;

        // 색마다 FBX가 달라서 머티리얼 교체로는 안 되고, 그 색의 모델로 다시 만들어야 한다
        int index = ProductIndex();
        GameObject source = BagLibrary.GetVariantModel(index, color.color);

        if (source != null) {
            Destroy(heldBag);

            heldBag = Instantiate(source, bagAnchor);
            heldBag.name = "HeldBag";

            foreach (Collider collider in heldBag.GetComponentsInChildren<Collider>()) {
                Destroy(collider);
            }

            BagLibrary.ApplyVariantMaterial(heldBag.transform, index, color.color);
            ApplyHoldPose(BagLibrary.GetModelEntry(index));
            return;
        }

        // 라이브러리가 없을 때(자리표시자 큐브)는 색만 바꾼다
        Renderer renderer = heldBag.GetComponentInChildren<Renderer>();
        if (renderer != null) {
            renderer.material.color = UIFactory.ParseHex(color.hex, Color.gray);
        }
    }

    // ----------------------------------------------------------------

    /// 아바타의 자식이라 신장이 바뀌어도 몸의 같은 지점을 유지한다.
    private Transform CreateBagAnchor(BagModelEntry entry) {
        GameObject anchor = new GameObject("BagAnchor");
        anchor.transform.SetParent(avatar.transform, false);
        anchor.transform.localPosition = entry != null ? entry.anchorPosition : Vector3.zero;
        anchor.transform.localRotation = Quaternion.identity;

        return anchor.transform;
    }

    /// 고정점에 아바타 스케일의 역수를 걸어 그 아래 가방이 실제 크기를 유지하게 한다.
    /// 위치는 아바타를 따라가므로 신장이 바뀌어도 가방 자세를 다시 계산할 필요가 없다.
    private void UpdateAnchorScale(float avatarScale) {
        if (bagAnchor == null || avatarScale <= 0.0001f) return;

        bagAnchor.localScale = Vector3.one / avatarScale;
    }

    private void CreateHeldBag() {
        int index = ProductIndex();
        BagModelEntry entry = BagLibrary.GetModelEntry(index);

        bagAnchor = CreateBagAnchor(entry);
        UpdateAnchorScale(avatar.transform.localScale.y);

        if (entry != null) {
            GameObject source = BagLibrary.GetVariantModel(
                index, currentColor != null ? currentColor.color : null);

            heldBag = Instantiate(source, bagAnchor);
        } else {
            heldBag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heldBag.transform.SetParent(bagAnchor, false);
        }

        heldBag.name = "HeldBag";

        foreach (Collider collider in heldBag.GetComponentsInChildren<Collider>()) {
            Destroy(collider);
        }

        if (currentColor == null
            || !BagLibrary.ApplyVariantMaterial(heldBag.transform, index, currentColor.color)) {
            Renderer renderer = heldBag.GetComponentInChildren<Renderer>();
            if (renderer != null) {
                renderer.material.color = currentColor != null
                    ? UIFactory.ParseHex(currentColor.hex, Color.gray)
                    : new Color(0.60f, 0.10f, 0.10f, 1f);
            }
        }

        ApplyHoldPose(entry);
    }

    /// product_id 는 1부터, 모델 인덱스는 0부터다.
    private int ProductIndex() {
        if (currentProduct == null) return 0;

        return currentProduct.product_id > 0 ? currentProduct.product_id - 1 : 0;
    }

    /// 가방을 새로 만들 때만 한 번 부른다. 바운즈 계산이 자식 렌더러를 전부 훑는데
    /// FBX가 커서 매 프레임 돌리면 낭비고, 지금은 고정점 스케일만 바꾸면 되므로 불필요하다.
    private void ApplyHoldPose(BagModelEntry entry) {
        if (heldBag == null) return;

        // 에디터 미리보기와 같은 함수를 쓴다 (순서가 어긋나면 맞춰둔 자세가 안 나온다)
        BagModelUtil.ApplyHoldPose(heldBag, entry, heldBagHeight);

        if (!logHoldPose) return;

        Debug.Log($"[TryOnController] 시착 자세 적용 — " +
                  $"entry {(entry != null ? "있음" : "없음(기본값 사용)")}, " +
                  $"pos {heldBag.transform.localPosition}, rot {heldBag.transform.localRotation.eulerAngles}");
    }

    private void ApplyHeightImmediate(int heightCm) {
        SetAvatarScale(heightCm / 100f);   // 아바타는 키 1.0m 기준으로 만들어져 있다
    }

    private void SetAvatarScale(float scale) {
        avatar.transform.localScale = Vector3.one * scale;
        UpdateAnchorScale(scale);
    }

}
