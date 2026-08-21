using UnityEngine;
using UnityEngine.InputSystem;

public class Highlighter : MonoBehaviour {

    [SerializeField] private float proximityDistance = 2f;

    [Tooltip("하이라이트 색. 스툴이 이미 밝은 흰색이라 색을 곱하면 구별이 안 되므로 발광으로 표시한다")]
    [SerializeField] private Color highlightColor = new Color(0.95f, 0.78f, 0.42f, 1f);

    [Tooltip("발광 세기. 올릴수록 확실히 눈에 띈다")]
    [SerializeField] private float highlightIntensity = 1.6f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Camera cam;
    private CameraController camController;

    private Transform[] stools;
    private Renderer[] stoolRenderers;
    private Transform[] stoolBags;
    private Color[] stoolBaseColors;

    private int highlightedIndex = -1;

    void Start() {
        cam = GetComponent<Camera>();
        camController = GetComponent<CameraController>();
        CacheStools();
    }

    void CacheStools() {
        GameObject[] found = GameObject.FindGameObjectsWithTag("Stool");

        stools = new Transform[found.Length];
        stoolRenderers = new Renderer[found.Length];
        stoolBags = new Transform[found.Length];
        stoolBaseColors = new Color[found.Length];

        for (int i = 0; i < found.Length; i++) {
            stools[i] = found[i].transform;

            Transform bag = FindBag(stools[i]);
            stoolBags[i] = bag;

            // 메시가 자식에 있을 수 있어 자식까지 뒤지되, 가방 쪽 렌더러는 제외한다.
            Renderer renderer = found[i].GetComponent<Renderer>();
            if (renderer == null) {
                foreach (Renderer candidate in found[i].GetComponentsInChildren<Renderer>()) {
                    if (bag != null && candidate.transform.IsChildOf(bag)) continue;
                    renderer = candidate;
                    break;
                }
            }

            stoolRenderers[i] = renderer;

            if (renderer != null) stoolBaseColors[i] = renderer.material.color;
        }
    }

    void Update() {
        // 상세 보기 중 회전 드래그의 클릭이 복귀 위치를 덮어써서 아예 처리하지 않는다.
        if (camController != null && camController.IsBagViewMode) return;

        UpdateHighlight();
        HandleStoolClick();
    }

    void UpdateHighlight() {
        if (stools == null || stools.Length == 0) return;

        Vector3 camPos = cam.transform.position;

        int closestIndex = -1;
        float closestSqr = proximityDistance * proximityDistance;

        for (int i = 0; i < stools.Length; i++) {
            if (stools[i] == null) continue;

            Vector3 diff = stools[i].position - camPos;
            float sqrDistance = diff.x * diff.x + diff.z * diff.z;  // XZ 평면 거리

            if (sqrDistance < closestSqr) {
                closestSqr = sqrDistance;
                closestIndex = i;
            }
        }

        if (closestIndex == highlightedIndex) return;

        if (highlightedIndex >= 0) SetHighlight(highlightedIndex, false);
        if (closestIndex >= 0) SetHighlight(closestIndex, true);

        highlightedIndex = closestIndex;
    }

    void SetHighlight(int index, bool highlight) {
        Renderer renderer = stoolRenderers[index];
        if (renderer == null) return;

        // renderer.material은 이 스툴만의 인스턴스 사본이라 다른 스툴에 영향이 없다.
        Material material = renderer.material;

        // 흰 스툴은 색을 곱해도 티가 안 나서 자체 발광으로 표시한다.
        if (highlight) {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor(EmissionColorId, highlightColor * highlightIntensity);
        } else {
            material.SetColor(EmissionColorId, Color.black);
            material.DisableKeyword("_EMISSION");
        }

        material.color = stoolBaseColors[index];
    }

    void HandleStoolClick() {
        if (highlightedIndex < 0) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Transform bag = ResolveBag(highlightedIndex);
        if (bag != null && camController != null) {
            camController.EnterBagViewMode(bag);
        }
    }

    /// 컬러를 바꾸면 가방 모델이 새로 생성되어 캐시된 Transform이 파괴된다.
    /// 그래서 죽어 있으면 다시 찾아 캐시를 갱신한다 (안 하면 색 바꾼 스툴은 클릭이 안 먹는다).
    Transform ResolveBag(int index) {
        if (stoolBags[index] != null) return stoolBags[index];
        if (stools[index] == null) return null;

        stoolBags[index] = FindBag(stools[index]);
        return stoolBags[index];
    }

    /// 스툴 바로 아래의 가방. 교체 중 잠시 남는 "Bag_Discarded"는 건너뛴다.
    static Transform FindBag(Transform stool) {
        foreach (Transform child in stool) {
            if (!child.name.Contains("Bag")) continue;
            if (child.name.Contains("Discarded")) continue;

            return child;
        }
        return null;
    }
}
