using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour {

    private enum FocusMode { None, BagView, TryOn }

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float mouseSensitivity = 0.5f;

    [Tooltip("가방을 자세히 볼 때 가방으로부터 떨어지는 거리")]
    [SerializeField] private float bagViewDistance = 0.5f;

    [Header("시착 오빗 카메라")]
    [Tooltip("아바타 키의 몇 배 거리에서 볼지 (1.5면 170cm 기준 약 2.55m)")]
    [SerializeField] private float orbitDistanceRatio = 1.5f;
    [SerializeField] private float orbitMinDistance = 1.2f;
    [SerializeField] private float orbitMaxDistance = 4.5f;
    [SerializeField] private float orbitSensitivity = 0.22f;
    [SerializeField] private float orbitZoomSpeed = 1f;
    [Tooltip("아래에서 올려다보는 각도 제한 (바닥 아래로 못 내려가게)")]
    [SerializeField] private float orbitMinPitch = -12f;
    [Tooltip("위에서 내려다보는 각도 제한")]
    [SerializeField] private float orbitMaxPitch = 65f;

    [Tooltip("정면 기준 좌우로 돌 수 있는 범위. 부스가 벽에 붙어 있어 뒤로는 못 돌아간다")]
    [SerializeField] private float orbitYawRange = 105f;

    [Header("오빗 드래그 방향 (느낌이 반대면 체크)")]
    [SerializeField] private bool orbitInvertHorizontal = false;
    [SerializeField] private bool orbitInvertVertical = false;

    [Header("상세 보기 조명")]
    [Tooltip("천장 조명은 위에서만 떨어지므로, 상세 보기에서는 카메라에 붙은 필 라이트로 정면을 살린다")]
    [SerializeField] private float bagViewFillIntensity = 1.5f;
    [SerializeField] private float bagViewFillRange = 3f;

    private Camera cam;
    private Rect fullViewportRect;
    private Light bagViewFillLight;
    private GazeTracker gazeTracker;

    private float rotationX;
    private float rotationY;

    private FocusMode focusMode = FocusMode.None;
    private Transform focusTarget;
    private BagRotator focusRotator;

    private Vector3 orbitPivot;
    private float orbitBaseYaw;
    private float orbitYaw;
    private float orbitPitch;
    private float orbitDistance;
    private float orbitZoom = 1f;
    private bool isOrbitDragging;

    private Transform bagTransform;
    private BagViewData currentBagViewData;
    private ProductData currentProduct;
    private ProductColorOption currentColorOption;
    private float lastColorChangeTime;

    private Vector3 savedPosition;
    private float savedRotationX;
    private float savedRotationY;

    /// 마우스 커서가 자유 상태인지
    private bool IsCursorUnlocked =>
        Cursor.lockState == CursorLockMode.None;

    void Awake() {
        cam = GetComponent<Camera>();
        if (cam != null) fullViewportRect = cam.rect;

        CreateBagViewFillLight();

        // 안내판(EntrySigns)이 안 뜨는 경우에도 자유 이동 기본값은 잠금이어야 한다
        SetFreeRoamCursor();
    }

    /// 천장광만으로는 정면이 어두워서, 카메라에 붙여 따라다니는 필 라이트를 만든다.
    void CreateBagViewFillLight() {
        GameObject fillObj = new GameObject("FocusFillLight");
        fillObj.transform.SetParent(transform, false);
        fillObj.transform.localPosition = new Vector3(-0.35f, 0.4f, 0.1f);

        bagViewFillLight = fillObj.AddComponent<Light>();
        bagViewFillLight.type = LightType.Point;
        bagViewFillLight.color = new Color(1f, 0.97f, 0.93f, 1f);
        bagViewFillLight.intensity = bagViewFillIntensity;
        bagViewFillLight.range = bagViewFillRange;
        bagViewFillLight.shadows = LightShadows.None;

        fillObj.SetActive(false);
    }

    private bool inputLocked;

    /// 시작 안내가 떠 있는 동안처럼 입력을 통째로 막아야 할 때 켠다.
    /// 켜고 끌 때마다 커서 상태도 같이 맞춘다 - 안내판/페이드 중엔 보이고,
    /// 풀리는 순간 자유 이동 중이면 바로 잠긴다.
    public bool InputLocked {
        get => inputLocked;
        set {
            inputLocked = value;

            if (value) SetPanelCursor();
            else if (focusMode == FocusMode.None) SetFreeRoamCursor();
        }
    }

    // ---------------------------------------------------------------- 커서

    private bool lookPaused;

    /// 출구 앞 [나가기] 버튼처럼, 걸어 다니는 건 막지 않고 시점 회전만 잠깐 멈추면서
    /// 커서를 눌러야 할 때 쓴다. (ExitGate)
    public void SetLookPaused(bool paused) {
        lookPaused = paused;

        if (paused) SetPanelCursor();
        else if (!inputLocked && focusMode == FocusMode.None) SetFreeRoamCursor();
    }

    /// FPS 시점으로 자유 이동할 때: 화면 가운데 잠기고 안 보인다.
    void SetFreeRoamCursor() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// 가방 상세/시착 사이드 패널이 떠 있는 동안: 클릭해야 하니 항상 보이고 안 잠긴다.
    void SetPanelCursor() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update() {
        if (InputLocked) return;

        if (focusMode == FocusMode.None) {
            HandleMovement();
            if (!lookPaused) HandleLook();
        } else {
            HandleFocusMode();
        }
    }

    // ---------------------------------------------------------------- 자유 이동

    void HandleMovement() {
        if (Keyboard.current == null) return;

        float moveZ = 0f;
        float moveX = 0f;

        if (Keyboard.current.wKey.isPressed) moveZ += 1f;
        if (Keyboard.current.sKey.isPressed) moveZ -= 1f;
        if (Keyboard.current.dKey.isPressed) moveX += 1f;
        if (Keyboard.current.aKey.isPressed) moveX -= 1f;

        if (moveZ == 0f && moveX == 0f) return;

        float speed = moveSpeed;
        if (Keyboard.current.leftShiftKey.isPressed) speed *= sprintMultiplier;

        // 위/아래를 봐도 속도가 안 느려지게 XZ로 투영하고, 대각선이 빨라지지 않게 크기를 1로 제한
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = Vector3.ClampMagnitude(forward * moveZ + right * moveX, 1f);
        Vector3 desired = transform.position + direction * speed * Time.deltaTime;
        desired.y = StoreLayout.EYE_HEIGHT;

        // 매장 모양이 사각형이 아니라 단순 Clamp 대신 통행 가능 여부로 판정한다
        transform.position = StoreLayout.Resolve(transform.position, desired);
    }

    void HandleLook() {
        if (Mouse.current == null) return;

        // UI 조작을 위해 커서가 풀려 있는 동안에는
        // 마우스를 움직여도 카메라 시점이 따라가지 않는다.
        if (IsCursorUnlocked) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        if (mouseDelta == Vector2.zero) return;

        rotationY += mouseDelta.x * mouseSensitivity * Time.deltaTime * 10f;
        rotationX -= mouseDelta.y * mouseSensitivity * Time.deltaTime * 10f;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);

        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }

    // ---------------------------------------------------------------- 포커스 모드 공통

    void HandleFocusMode() {
        if (focusTarget == null) {
            ExitToFreeRoam();
            return;
        }

        if (focusMode == FocusMode.TryOn) {
            HandleOrbit();
            return;
        }

        if (focusRotator != null) focusRotator.RotateBag();
    }

    /// 포커스 진입. 사이드 패널이 우측을 덮으므로 3D 화면을 그만큼 좁힌다.
    void EnterFocus(Transform target) {
        focusTarget = target;

        // 사이드 패널이 뜨는 동안은 항상 커서가 보여야 클릭할 수 있다
        SetPanelCursor();

        // 팝업 뒤 제품의 응시 시간이 중복 계상되지 않게 추적을 멈춘다
        SetGazePaused(true);

        if (cam != null) {
            cam.rect = new Rect(0f, 0f, 1f - BagViewUI.PANEL_WIDTH_RATIO, 1f);
        }

        if (bagViewFillLight != null) {
            bagViewFillLight.intensity = bagViewFillIntensity;
            bagViewFillLight.range = bagViewFillRange;
            bagViewFillLight.gameObject.SetActive(true);
        }
    }

    void AttachDragRotator(Transform target) {
        focusRotator = target.GetComponent<BagRotator>();
        if (focusRotator == null) focusRotator = target.gameObject.AddComponent<BagRotator>();
    }

    /// 색을 바꾸면 가방 오브젝트가 새로 만들어져서(색마다 FBX가 다름) 참조를 갈아 끼워야 한다.
    public void NotifyBagReplaced(Transform newBag) {
        if (newBag == null || focusMode != FocusMode.BagView) return;

        bagTransform = newBag;
        focusTarget = newBag;

        AttachDragRotator(newBag);

        // 색마다 FBX가 달라 크기와 원점이 조금씩 다르다. 다시 맞추지 않으면 색을 바꿀 때마다 가방이 튄다.
        FrameBag();
    }

    void SetGazePaused(bool paused) {
        if (gazeTracker == null) gazeTracker = GetComponent<GazeTracker>();
        if (gazeTracker != null) gazeTracker.SetPaused(paused);
    }

    /// 가방을 화면 가운데 놓고 정면에서 본다.
    ///
    /// 기준을 transform.position으로 잡으면 안 된다. FBX 원점이 모델 가운데가 아니라서
    /// 가방마다 화면에서 뜨거나 가라앉는다. 렌더러 바운즈의 중심이 눈에 보이는 가운데다.
    ///
    /// 보는 방향도 월드 -Z로 고정하면 안 된다. 전시 방향(FacingYaw)과 가방별 보정
    /// 회전(BagLibrary rotationEuler)이 더해져 있어서, 조금만 돌아가 있어도 옆면이 잡힌다.
    /// 가방이 실제로 향하는 쪽(로컬 +Z)에서 본다.
    void FrameBag() {
        if (bagTransform == null) return;

        Vector3 center = BagModelUtil.TryGetWorldBounds(bagTransform.gameObject, out Bounds bounds)
            ? bounds.center
            : bagTransform.position + Vector3.up * 0.2f;

        // 가방이 기울어 있어도 카메라는 수평을 유지해야 한다
        Vector3 viewDirection = bagTransform.forward;
        viewDirection.y = 0f;

        if (viewDirection.sqrMagnitude < 0.0001f) viewDirection = Vector3.back;
        viewDirection.Normalize();

        transform.position = center + viewDirection * bagViewDistance;
        transform.LookAt(center);
    }

    // ---------------------------------------------------------------- 오빗 카메라

    void HandleOrbit() {
        if (Mouse.current == null) return;

        Vector2 pointer = Mouse.current.position.ReadValue();
        bool pointerInViewport = cam == null || cam.pixelRect.Contains(pointer);

        // 우측 패널 버튼을 누를 때 같이 돌지 않도록, 3D 화면에서 시작한 드래그만 받는다
        if (Mouse.current.leftButton.wasPressedThisFrame) {
            isOrbitDragging = pointerInViewport;
        }
        if (!Mouse.current.leftButton.isPressed) {
            isOrbitDragging = false;
        }

        if (isOrbitDragging) {
            Vector2 delta = Mouse.current.delta.ReadValue();

            float dx = orbitInvertHorizontal ? -delta.x : delta.x;
            float dy = orbitInvertVertical ? -delta.y : delta.y;

            // 드래그한 쪽으로 아바타가 따라오는 방향. 반대로 하면 조작감이 뒤집혀 보였다.
            orbitYaw += dx * orbitSensitivity;
            orbitPitch -= dy * orbitSensitivity;

            orbitPitch = Mathf.Clamp(orbitPitch, orbitMinPitch, orbitMaxPitch);

            // 부스가 벽에 붙어 있어 배경 뒤로는 못 돌게 정면 기준으로 제한
            orbitYaw = orbitBaseYaw + Mathf.Clamp(orbitYaw - orbitBaseYaw, -orbitYawRange, orbitYawRange);
        }

        // 거리가 아니라 배율을 바꿔야 신장을 바꿔도 맞춰둔 줌이 풀리지 않는다
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (pointerInViewport && Mathf.Abs(scroll) > 0.01f) {
            orbitZoom = Mathf.Clamp(orbitZoom - scroll * 0.0012f * orbitZoomSpeed, 0.5f, 2f);
            UpdateOrbitDistance();
        }

        ApplyOrbit();
    }

    void ApplyOrbit() {
        Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        transform.position = orbitPivot + rotation * (Vector3.back * orbitDistance);
        transform.LookAt(orbitPivot);
    }

    void RestoreFreeRoamCamera() {
        if (cam != null) cam.rect = fullViewportRect;
        if (bagViewFillLight != null) bagViewFillLight.gameObject.SetActive(false);

        SetGazePaused(false);
        SetFreeRoamCursor();

        focusMode = FocusMode.None;
        focusTarget = null;
        focusRotator = null;

        // rotationX/Y까지 되돌려야 복귀 후 첫 마우스 입력에서 시점이 튀지 않는다
        rotationX = savedRotationX;
        rotationY = savedRotationY;
        transform.position = savedPosition;
        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }

    // ---------------------------------------------------------------- 가방 상세 보기

    public void EnterBagViewMode(Transform bag) {
        if (bag == null || focusMode != FocusMode.None) return;

        savedPosition = transform.position;
        savedRotationX = rotationX;
        savedRotationY = rotationY;

        bagTransform = bag;
        currentProduct = ProductCatalog.GetByObjectName(bag.name);

        // 스와치를 안 눌러도 시착에 같은 색이 넘어가도록 현재 색을 미리 잡아둔다
        currentColorOption = BagColorState.ResolveOrAssignRandom(currentProduct);

        currentBagViewData = new BagViewData {
            product_id = currentProduct != null ? currentProduct.product_id : 0,
            start_time = System.DateTime.Now
        };
        lastColorChangeTime = Time.time;

        ShowBagView();

        // 현재는 응시 10초 자동 오픈이 없어 항상 카드 클릭으로 연다.
        // popup_close(AddBagViewData)가 같은 interaction_id/occurrence_index를 다시 실어야
        // 이번 방문(회차)이 서버에서 하나로 묶인다 (명세서 §4.1) - LogPopupOpen이 currentBagViewData에 채워 넣는다.
        if (SessionDataManager.Instance != null) {
            SessionDataManager.Instance.LogPopupOpen(currentBagViewData, "card_click", 0f);
        }

        Debug.Log($"[CameraController] popup_open - {bag.name}");
    }

    /// 시착에서 돌아올 때도 쓴다. 세션 데이터는 건드리지 않는다.
    void ShowBagView() {
        focusMode = FocusMode.BagView;
        EnterFocus(bagTransform);
        AttachDragRotator(bagTransform);

        // 지난번에 돌려둔 각도가 남아 있으면 열자마자 옆면이 보인다
        if (focusRotator != null) focusRotator.ResetRotation();

        FrameBag();

        if (BagViewUI.Instance != null) {
            BagViewUI.Instance.Show(
                currentProduct,
                bagTransform,   // 머티리얼 교체는 자식 메시까지 훑어야 하므로 루트를 넘긴다
                HandleColorChanged,
                EnterTryOnMode,
                ExitToFreeRoam);
        }
    }

    /// 컬러 스와치 클릭. 컬러별 유지 시간이 이 프로젝트 데이터의 핵심 지표다.
    private void HandleColorChanged(ProductColorOption option) {
        if (option == null) return;

        currentColorOption = option;

        if (currentBagViewData == null) return;

        float heldSec = Time.time - lastColorChangeTime;
        lastColorChangeTime = Time.time;

        string fromColor = currentBagViewData.color;
        currentBagViewData.color = option.color;
        currentBagViewData.color_change_count++;

        if (SessionDataManager.Instance != null) {
            SessionDataManager.Instance.LogColorChange(
                currentBagViewData.product_id, fromColor, option.color,
                heldSec, currentBagViewData.color_change_count);
        }
    }

    public void ExitToFreeRoam() {
        if (focusMode == FocusMode.None) return;

        if (focusMode == FocusMode.TryOn && TryOnController.Instance != null) {
            TryOnController.Instance.End();
            if (BagViewUI.Instance != null) BagViewUI.Instance.ExitFittingMode();
        }

        if (currentBagViewData != null && SessionDataManager.Instance != null) {
            // 마지막 컬러 구간을 안 닫으면 그 색의 유지 시간이 통째로 유실된다
            SessionDataManager.Instance.LogFinalColor(
                currentBagViewData.product_id,
                currentBagViewData.color,
                Time.time - lastColorChangeTime,
                currentBagViewData.color_change_count);

            currentBagViewData.end_time = System.DateTime.Now;
            currentBagViewData.duration_sec = (float)(currentBagViewData.end_time - currentBagViewData.start_time).TotalSeconds;
            SessionDataManager.Instance.AddBagViewData(currentBagViewData);
        }

        if (BagViewUI.Instance != null) BagViewUI.Instance.Hide();

        if (bagTransform != null) {
            BagRotator rotator = bagTransform.GetComponent<BagRotator>();
            if (rotator != null) rotator.ResetRotation();
        }

        RestoreFreeRoamCamera();

        bagTransform = null;
        currentBagViewData = null;
        currentProduct = null;
        currentColorOption = null;
    }

    /// 이전 이름 호환 (Highlighter 등 외부에서 부르던 이름)
    public void ExitBagViewMode() {
        ExitToFreeRoam();
    }

    // ---------------------------------------------------------------- 시착 모드

    /// 시착 진입. 별도 씬으로 가지 않고 같은 씬에서 아바타를 세운다.
    public void EnterTryOnMode() {
        if (focusMode != FocusMode.BagView || TryOnController.Instance == null) return;
        if (bagTransform == null) return;

        // 색의 정답은 BagColorState 한 곳. 시착에도 같은 색을 가져간다.
        ProductColorOption stored = BagColorState.ResolveOrAssignRandom(currentProduct);
        if (stored != null) currentColorOption = stored;

        // 사이드 패널은 닫지 않는다. 아래쪽만 키 선택 화면으로 바뀐다.

        BagRotator bagRotator = bagTransform.GetComponent<BagRotator>();
        if (bagRotator != null) bagRotator.ResetRotation();

        Transform avatar = TryOnController.Instance.Begin(
            currentProduct, currentColorOption,
            StoreLayout.BoothStandPosition, StoreLayout.BoothStandRotation,
            TryOnController.SessionDummyHeight());

        if (avatar == null) return;

        focusMode = FocusMode.TryOn;
        focusRotator = null;
        EnterFocus(avatar);

        // 부스가 어느 벽에 붙어 있든 앞모습부터 보이도록 기준 각도를 아바타 정면에 맞춘다
        orbitBaseYaw = avatar.eulerAngles.y + 180f;
        orbitYaw = orbitBaseYaw;
        orbitPitch = 8f;
        orbitZoom = 1f;
        isOrbitDragging = false;
        UpdateOrbitFraming();

        if (SessionDataManager.Instance != null) {
            (string interactionId, int occurrenceIndex) = SessionDataManager.Instance.LogTryOnStart(
                currentProduct != null ? currentProduct.product_id : 0,
                currentColorOption != null ? currentColorOption.color : null,
                TryOnController.Instance.CurrentHeight);

            // tryon_end가 같은 값을 실어야 시작/종료가 같은 회차로 묶인다
            TryOnController.Instance.SetSessionIds(interactionId, occurrenceIndex);
        }

        if (BagViewUI.Instance != null) {
            BagViewUI.Instance.EnterFittingMode(
                TryOnController.Instance.CurrentHeight,
                HandleTryOnHeightChanged,
                ExitTryOnMode);
        }
    }

    /// 키 칩을 눌렀을 때. 인형만 커지고 가방은 실제 크기를 유지한다.
    private void HandleTryOnHeightChanged(int height) {
        if (focusMode != FocusMode.TryOn || TryOnController.Instance == null) return;

        TryOnController.Instance.SetHeight(height);

        UpdateOrbitFraming();
    }

    /// 키에 맞춰 궤도 중심·반지름만 갱신. 각도는 유지해 신장을 바꿔도 시점이 안 튄다.
    private void UpdateOrbitFraming() {
        if (focusTarget == null) return;

        float heightMeters = TryOnController.Instance != null
            ? TryOnController.Instance.CurrentHeight / 100f
            : 1.7f;

        // 발 밑이 원점이라 화면 중앙에 오려면 키의 절반 조금 위를 본다
        orbitPivot = focusTarget.position + Vector3.up * (heightMeters * 0.55f);
        UpdateOrbitDistance();

        ApplyOrbit();
    }

    private void UpdateOrbitDistance() {
        float heightMeters = TryOnController.Instance != null
            ? TryOnController.Instance.CurrentHeight / 100f
            : 1.7f;

        orbitDistance = Mathf.Clamp(
            heightMeters * orbitDistanceRatio * orbitZoom,
            orbitMinDistance, orbitMaxDistance);
    }

    /// 시착 종료 → 가방 상세 보기로 복귀
    public void ExitTryOnMode() {
        if (focusMode != FocusMode.TryOn) return;

        if (TryOnController.Instance != null) TryOnController.Instance.End();
        if (BagViewUI.Instance != null) BagViewUI.Instance.ExitFittingMode();

        if (bagTransform == null) {
            ExitToFreeRoam();
            return;
        }

        ShowBagView();
    }

    /// 현재 포커스 모드 여부 (Highlighter가 클릭 처리를 건너뛸 때 사용)
    public bool IsBagViewMode => focusMode != FocusMode.None;

    /// 출구 프롬프트 등으로 시점 회전만 잠시 멈춘 상태인지 (Crosshair가 숨길 때 참고)
    public bool IsLookPaused => lookPaused;
}
