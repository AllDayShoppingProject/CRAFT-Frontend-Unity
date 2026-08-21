using UnityEngine;
using UnityEngine.InputSystem;

public class BagRotator : MonoBehaviour {

    [SerializeField] private float rotationSpeed = 80f;

    [Tooltip("위아래로 뒤집히지 않도록 상하 회전 각도를 제한")]
    [SerializeField] private float maxPitch = 80f;

    // 누적 회전값 (yaw: 좌우, pitch: 상하)
    private float yaw;
    private float pitch;

    private Camera viewportCamera;
    private bool isDragging;

    /// 전시대에 놓였을 때의 원래 자세. 드래그 회전은 이 위에 얹는다.
    /// identity로 되돌리면 세워둔 방향과 통로 방향이 통째로 날아간다.
    private Quaternion baseRotation = Quaternion.identity;
    private Vector3 basePosition;

    /// 회전 중심. "세워져 있는 상태의 가방 중앙"이다.
    /// pivotWorld 는 고정된 월드 좌표, localPivot 은 같은 점을 오브젝트 로컬로 옮겨둔 값.
    private Vector3 pivotWorld;
    private Vector3 localPivot;
    private bool hasPivot;

    void Awake() {
        viewportCamera = Camera.main;
        CaptureBasePose();
    }

    /// 지금 자세를 '원래 자세'로 기록하고 회전 중심을 다시 잰다.
    ///
    /// BagModelUtil.Place() 로 배치와 크기 조정이 끝난 뒤에 불려야 한다.
    /// GalleryController.CreateBag() 은 Place() 다음에 이 컴포넌트를 붙이므로
    /// Awake 시점이 이미 스툴 위에 세워진 상태다.
    public void CaptureBasePose() {
        baseRotation = transform.rotation;
        basePosition = transform.position;

        // FBX 원점은 파일마다 제각각이라 회전축으로 쓸 수 없다.
        // 배치가 쓰는 기준과 똑같이, 눈에 보이는 형상의 바운즈를 재서 그 중심을 회전 중심으로 삼는다.
        hasPivot = BagModelUtil.TryGetWorldBounds(gameObject, out Bounds bounds);
        if (!hasPivot) return;

        pivotWorld = bounds.center;
        localPivot = transform.InverseTransformPoint(pivotWorld);
    }

    public void RotateBag() {
        if (Mouse.current == null) return;

        if (!Mouse.current.leftButton.isPressed) {
            isDragging = false;
            return;
        }

        // 3D 뷰포트 안에서 시작한 드래그만 회전시킨다. 사이드 패널 버튼을 눌러도 가방이 돌지 않도록.
        if (Mouse.current.leftButton.wasPressedThisFrame) {
            isDragging = viewportCamera == null
                      || viewportCamera.pixelRect.Contains(Mouse.current.position.ReadValue());
        }
        if (!isDragging) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        if (mouseDelta == Vector2.zero) return;

        // 마우스를 올리면 가방이 뒤로 기울며 윗면이 드러난다.
        yaw -= mouseDelta.x * rotationSpeed * Time.deltaTime;
        pitch += mouseDelta.y * rotationSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        ApplyRotation();
    }

    private void ApplyRotation() {
        // Quaternion.Euler는 회전 순서가 고정이라 좌우로 90도쯤 돌린 뒤 상하 드래그가 '굴리기'가 된다.
        // 그래서 yaw와 pitch를 따로 AngleAxis로 건다.
        // pitch 축은 월드 +X가 아니라 카메라의 오른쪽이어야 한다. 월드 축에 고정하면
        // 가방을 어느 방향에서 보느냐에 따라 상하가 뒤집힌다.
        Vector3 screenRight = viewportCamera != null
            ? viewportCamera.transform.right
            : Vector3.right;

        transform.rotation = Quaternion.AngleAxis(pitch, screenRight)
                           * Quaternion.AngleAxis(yaw, Vector3.up)
                           * baseRotation;

        if (!hasPivot) return;

        // rotation만 대입하면 transform 원점(= FBX 원점)을 축으로 돌아서,
        // 원점이 가방 중앙에서 벗어난 모델은 제자리에서 도는 대신 궤도를 그리며 휘둘린다.
        // 기록해 둔 중앙이 월드에서 안 움직이도록 위치를 되민다.
        //
        // 누적이 아니라 매 프레임 현재 상태에서 오차를 새로 재서 상쇄하는 방식이라
        // 오래 돌려도 값이 밀리지 않는다.
        transform.position += pivotWorld - transform.TransformPoint(localPivot);
    }

    /// 원래 전시 자세로 되돌린다 (상세 보기에서 나올 때)
    public void ResetRotation() {
        yaw = 0f;
        pitch = 0f;
        isDragging = false;

        // 회전 중에 위치도 같이 보정했으므로 위치까지 되돌려야 원래 자리에 선다.
        transform.rotation = baseRotation;
        transform.position = basePosition;
    }
}
