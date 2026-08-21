using UnityEngine;


/// 화면 중심 기준 응시 추적. 카메라 정면 레이에 걸린 제품의 view_start/view_end를 GazeLogger에 쌓는다.
/// GazeLogger·customerId 주입은 GalleryController가 한다.
public class GazeTracker : MonoBehaviour {

    [Header("Gaze (명세서 config 기본값)")]
    [Tooltip("gaze_max_distance_m")]
    [SerializeField] private float maxDistance = 4f;
    private Camera cam;

    // Product 레이어만 레이캐스트한다. 스툴·벽이 레이를 가로막지 않게 하기 위함.
    [Tooltip("제품 레이어. 스툴·벽이 레이를 가로막지 않도록 제품만 걸러낸다")]
    [SerializeField] private LayerMask productLayer = 1 << 3;

    [Tooltip("gaze_grace_sec - 이 시간 이하의 시선 이탈은 같은 응시로 이어붙인다")]
    [SerializeField] private float graceSeconds = 0.5f;
    private GazeLogger gazeLogger;

    private string customerId;
    private bool paused;

    private ProductInfo currentProduct;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (cam == null)
        {
            cam = Camera.main;
        }
        Debug.Log("gallery scene awake()");
    }
    private float currentGazeTime;
    private string currentInteractionId;
    private int currentOccurrenceIndex;

    /// 시선이 벗어난 뒤 흐른 시간. 음수면 이탈 중이 아님.
    private float lostSeconds = -1f;

    void Start() {
        if (gazeLogger == null) gazeLogger = FindFirstObjectByType<GazeLogger>();
    }

    public void SetCustomerId(string id) {
        customerId = id;
        Debug.Log(
            $"[GazeTracker] CustomerId 설정: {customerId}"
        );
    }

    public void SetLogger(GazeLogger logger) {
        gazeLogger = logger;
    }

    /// 상세 보기·시착 중에는 멈춘다 (팝업 뒤 제품의 응시 시간이 중복 계상되는 것을 막는다).
    public void SetPaused(bool value) {
        if (value == paused) return;

        paused = value;
        if (paused) EndGaze();
    }

    void Update() {
        if (paused || cam == null) return;
        TrackGaze();
    }

    private void TrackGaze() {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, productLayer)) {
            ProductInfo product = hit.collider.GetComponentInParent<ProductInfo>();

            if (product != null) {
                if (product == currentProduct) {
                    lostSeconds = -1f;
                    currentGazeTime += Time.deltaTime;
                } else {
                    EndGaze();
                    BeginGaze(product);
                }
                return;
            }
        }

        // 유예 시간 안의 짧은 이탈은 같은 응시로 이어붙인다.
        if (currentProduct == null) return;

        if (lostSeconds < 0f) lostSeconds = 0f;
        lostSeconds += Time.deltaTime;

        if (lostSeconds > graceSeconds) EndGaze();
    }
    private void BeginGaze(ProductInfo product)
    {
        if (gazeLogger == null)
        {
            Debug.LogWarning("GazeTracker: GazeLogger를 찾을 수 없습니다.");
            return;
        }

        currentProduct = product;
        currentGazeTime = 0f;
        lostSeconds = -1f;

        // ProductInfo.ProductId가 곧 서버 product_id(1부터 시작)다.
        string color = BagColorState.GetColor(product.ProductId);

        GazeLogger.GazeSession session = gazeLogger.StartGaze(product.ProductId, customerId, color);

        currentInteractionId = session.interactionId;
        currentOccurrenceIndex = session.occurrenceIndex;
    }

    private void EndGaze() {
        if (currentProduct == null) return;

        if (gazeLogger != null) {
            gazeLogger.EndGaze(
                currentProduct.ProductId,
                customerId,
                currentInteractionId,
                currentOccurrenceIndex,
                currentGazeTime);
        }

        currentProduct = null;
        currentGazeTime = 0f;
        currentInteractionId = null;
        currentOccurrenceIndex = 0;
        lostSeconds = -1f;
    }

    void OnDisable() {
        EndGaze();
    }
}
