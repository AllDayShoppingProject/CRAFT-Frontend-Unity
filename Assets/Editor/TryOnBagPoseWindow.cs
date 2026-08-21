#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// 아바타가 가방을 어떻게 걸치는지 씬에서 직접 놓아보고 저장하는 도구.
/// anchorPosition(고정점) — 몸의 어느 지점에 걸리는가 (아바타 로컬 좌표)
/// gripPoint(접점)        — 가방의 어느 부분이 그 지점에 닿는가 (바운즈 비율)
/// 둘을 겹치게 배치하므로 신장이 바뀌어도 걸린 자리가 유지된다.
/// 미리보기 오브젝트는 HideFlags.DontSave 라서 씬에 저장되지 않는다.
public class TryOnBagPoseWindow : EditorWindow {

    private const string PreviewName = "__TryOnBagPosePreview__";
    private BagLibrary library;
    private int bagIndex;

    /// 150/190에서도 자세가 버티는지 확인하는 용도
    private int previewHeightCm = TryOnController.DEFAULT_HEIGHT;

    private bool showAnchorHandle = true;

    private GameObject previewRoot;
    private Transform previewAnchor;
    private GameObject previewBag;

    [MenuItem("Tools/시착 가방 위치 잡기")]
    public static void Open() {
        GetWindow<TryOnBagPoseWindow>("시착 가방 위치");
    }

    private void OnEnable() {
        library = Resources.Load<BagLibrary>(BagLibrary.RESOURCE_PATH);
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable() {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearPreview();
    }

    private void OnSceneGUI(SceneView view) {
        if (previewRoot == null) return;

        DrawLandmarks();
        if (showAnchorHandle) DrawAnchorHandle();
    }

    private void DrawLandmarks() {
        Handles.color = new Color(0.3f, 0.8f, 1f, 0.75f);

        foreach ((string landmarkName, Vector3 local) in AvatarBuilder.LANDMARKS) {
            Vector3 world = previewRoot.transform.TransformPoint(local);
            float size = HandleUtility.GetHandleSize(world) * 0.03f;

            Handles.SphereHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
            Handles.Label(world + Vector3.up * size * 1.8f, landmarkName);
        }
    }

    /// 고정점을 직접 끌어 옮기는 핸들. 가방 메시는 커서 집기 어렵다.
    private void DrawAnchorHandle() {
        BagModelEntry entry = CurrentEntry();
        if (entry == null || previewAnchor == null) return;

        Vector3 world = previewAnchor.position;

        Handles.color = new Color(1f, 0.8f, 0.2f, 1f);
        float size = HandleUtility.GetHandleSize(world) * 0.05f;
        Handles.SphereHandleCap(0, world, Quaternion.identity, size, EventType.Repaint);
        Handles.Label(world + Vector3.up * size * 2f, "고정점");

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);

        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(library, "Move Bag Anchor");

        // 신장 스케일이 걸려 있어 InverseTransformPoint로 로컬 변환해야 한다
        entry.anchorPosition = previewRoot.transform.InverseTransformPoint(moved);
        previewAnchor.localPosition = entry.anchorPosition;

        ReapplyPose(entry);

        EditorUtility.SetDirty(library);
        Repaint();
    }

    private void OnGUI() {
        EditorGUILayout.Space(4);

        if (library == null) {
            EditorGUILayout.HelpBox(
                "Resources/BagLibrary.asset 이 없습니다.\n" +
                "메뉴의 Tools > 가방 라이브러리 갱신 을 먼저 실행하세요.",
                MessageType.Warning);

            if (GUILayout.Button("다시 찾기")) {
                library = Resources.Load<BagLibrary>(BagLibrary.RESOURCE_PATH);
            }
            return;
        }

        if (library.bagModels == null || library.bagModels.Length == 0) {
            EditorGUILayout.HelpBox("BagLibrary에 등록된 가방이 없습니다.", MessageType.Warning);
            return;
        }

        DrawBagSelector();
        DrawHeightSelector();
        showAnchorHandle = EditorGUILayout.Toggle("고정점 핸들 표시", showAnchorHandle);

        EditorGUILayout.Space(6);
        DrawPreviewButtons();
        EditorGUILayout.Space(6);
        DrawCarryPresets();
        EditorGUILayout.Space(6);
        DrawRotationButtons();
        EditorGUILayout.Space(6);
        DrawSaveButton();
        EditorGUILayout.Space(6);
        DrawCurrentValues();
        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "1) [미리보기 만들기]\n" +
            "2) 거는 방식(등/어깨/손)을 누르면 노란 고정점이 그 자리로 갑니다.\n" +
            "   미세 조정은 노란 핸들을 직접 끌면 됩니다.\n" +
            "3) Hierarchy에서 PreviewBag 을 골라, 평소처럼 이동·회전 툴로 자연스럽게 맞춥니다.\n" +
            "4) [현재 위치 저장] — 가방의 어느 부분이 고정점에 닿았는지 자동으로 계산해 저장합니다.\n" +
            "5) 신장을 150 / 190 으로 바꿔봅니다. 걸린 자리가 그대로면 성공입니다.",
            MessageType.Info);
    }

    private void DrawBagSelector() {
        string[] names = new string[library.bagModels.Length];
        for (int i = 0; i < names.Length; i++) {
            GameObject prefab = library.bagModels[i] != null ? library.bagModels[i].prefab : null;
            names[i] = prefab != null ? prefab.name : $"(비어있음 {i + 1})";
        }

        int newIndex = EditorGUILayout.Popup("가방", bagIndex, names);
        if (newIndex == bagIndex) return;

        bagIndex = newIndex;
        if (previewRoot != null) SpawnPreviewBag();
    }

    private void DrawHeightSelector() {
        int[] options = TryOnController.HEIGHT_OPTIONS;
        string[] labels = new string[options.Length];
        int selected = 0;

        for (int i = 0; i < options.Length; i++) {
            labels[i] = options[i] + "cm";
            if (options[i] == previewHeightCm) selected = i;
        }

        int newSelected = EditorGUILayout.Popup("미리보기 신장", selected, labels);
        if (options[newSelected] == previewHeightCm) return;

        previewHeightCm = options[newSelected];
        if (previewRoot != null) CreatePreview();
    }

    private void DrawPreviewButtons() {
        using (new EditorGUILayout.HorizontalScope()) {
            if (GUILayout.Button(previewRoot == null ? "미리보기 만들기" : "미리보기 다시 만들기",
                                 GUILayout.Height(28))) {
                CreatePreview();
            }

            using (new EditorGUI.DisabledScope(previewRoot == null)) {
                if (GUILayout.Button("미리보기 정리", GUILayout.Height(28), GUILayout.Width(110))) {
                    ClearPreview();
                }
            }
        }
    }

    private void DrawCarryPresets() {
        EditorGUILayout.LabelField("거는 방식", EditorStyles.boldLabel);

        BagModelEntry entry = CurrentEntry();

        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(entry == null)) {
            foreach ((string presetName, Vector3 position) in AvatarBuilder.CARRY_PRESETS) {
                if (!GUILayout.Button(presetName, GUILayout.Height(24))) continue;

                Undo.RecordObject(library, "Set Carry Preset");

                entry.anchorPosition = position;
                entry.gripPoint = BagModelUtil.DEFAULT_GRIP_POINT;   // 멜빵/손잡이 꼭대기

                if (previewAnchor != null) previewAnchor.localPosition = position;
                ReapplyPose(entry);

                EditorUtility.SetDirty(library);
            }
        }
    }

    /// 딱 떨어지는 각도는 마우스로 못 맞추므로 저장값(holdRotation)을 직접 돌린다.
    private void DrawRotationButtons() {
        BagModelEntry entry = CurrentEntry();

        EditorGUILayout.LabelField("빠른 회전 (누를 때마다 누적)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(entry == null || previewBag == null)) {
            if (GUILayout.Button("Y 180°")) RotateBy(entry, Vector3.up, 180f);
            if (GUILayout.Button("Y 90°")) RotateBy(entry, Vector3.up, 90f);
            if (GUILayout.Button("X 90°")) RotateBy(entry, Vector3.right, 90f);
            if (GUILayout.Button("Z 90°")) RotateBy(entry, Vector3.forward, 90f);
        }
    }

    private void RotateBy(BagModelEntry entry, Vector3 axis, float degrees) {
        Undo.RecordObject(library, "Rotate Bag");

        entry.holdRotation = (Quaternion.AngleAxis(degrees, axis) * entry.HoldRotation).eulerAngles;

        EditorUtility.SetDirty(library);
        ReapplyPose(entry);
    }

    private void DrawSaveButton() {
        using (new EditorGUI.DisabledScope(previewBag == null)) {
            GUI.backgroundColor = new Color(0.6f, 0.85f, 0.6f);
            if (GUILayout.Button("현재 위치 저장", GUILayout.Height(32))) {
                SaveCurrentPose();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    private void DrawCurrentValues() {
        BagModelEntry entry = CurrentEntry();
        if (entry == null) return;

        EditorGUILayout.LabelField("저장된 값", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true)) {
            EditorGUILayout.Vector3Field("고정점 - 몸 (anchorPosition)", entry.anchorPosition);
            EditorGUILayout.Vector3Field("접점 - 가방 비율 (gripPoint)", entry.gripPoint);
            EditorGUILayout.Vector3Field("회전 (holdRotation)", entry.holdRotation);
        }

        if (GUILayout.Button("이 가방 값 초기화")) {
            Undo.RecordObject(library, "Reset Bag Hold Pose");
            entry.anchorPosition = Vector3.zero;
            entry.gripPoint = BagModelUtil.DEFAULT_GRIP_POINT;
            entry.holdRotation = Vector3.zero;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            if (previewRoot != null) SpawnPreviewBag();
        }
    }

    private BagModelEntry CurrentEntry() {
        if (library == null || library.bagModels == null) return null;
        if (bagIndex < 0 || bagIndex >= library.bagModels.Length) return null;

        return library.bagModels[bagIndex];
    }

    private void ReapplyPose(BagModelEntry entry) {
        if (previewBag == null) return;

        BagModelUtil.ApplyHoldPose(previewBag, entry, TryOnController.DEFAULT_HELD_BAG_HEIGHT);
    }

    private void CreatePreview() {
        ClearPreview();

        previewRoot = AvatarBuilder.Build(PreviewName);
        previewRoot.transform.position = Vector3.zero;
        previewRoot.transform.localScale = Vector3.one * (previewHeightCm / 100f);

        SetHideFlagsRecursively(previewRoot, HideFlags.DontSave);

        SpawnPreviewBag();

        if (SceneView.lastActiveSceneView != null) {
            Vector3 focus = previewRoot.transform.TransformPoint(new Vector3(0f, 0.65f, 0f));
            SceneView.lastActiveSceneView.LookAt(focus, Quaternion.Euler(8f, 160f, 0f), 1.3f);
        }
    }

    private void SpawnPreviewBag() {
        if (previewRoot == null) return;

        if (previewBag != null) DestroyImmediate(previewBag);
        if (previewAnchor != null) DestroyImmediate(previewAnchor.gameObject);

        previewBag = null;
        previewAnchor = null;

        BagModelEntry entry = CurrentEntry();
        if (entry == null || entry.prefab == null) return;

        // 런타임과 같은 구조: 아바타 > 고정점 > 가방
        GameObject anchor = new GameObject("BagAnchor");
        anchor.transform.SetParent(previewRoot.transform, false);
        anchor.transform.localPosition = entry.anchorPosition;

        // 아바타 스케일을 상쇄한다. 없으면 신장을 바꿀 때 가방까지 같이 커진다.
        anchor.transform.localScale = Vector3.one * (100f / Mathf.Max(1, previewHeightCm));

        anchor.hideFlags = HideFlags.DontSave;
        previewAnchor = anchor.transform;

        // PrefabUtility.InstantiatePrefab 으로 만들면 안 된다. HideFlags.DontSave 인 오브젝트는
        // 오버라이드를 저장할 수 없어, 돌려놓은 회전이 다음 갱신 때 원본으로 되돌아간다.
        previewBag = Instantiate(entry.prefab, previewAnchor);
        if (previewBag == null) return;

        previewBag.name = "PreviewBag";
        SetHideFlagsRecursively(previewBag, HideFlags.DontSave);

        ReapplyPose(entry);

        Selection.activeGameObject = previewBag;
    }

    /// 가방이 놓인 자리에서 접점을 역산해 저장한다.
    /// 고정점이 가방 바운즈의 몇 % 지점인지를 기록하므로 어느 신장에서도 재현된다.
    private void SaveCurrentPose() {
        BagModelEntry entry = CurrentEntry();
        if (entry == null || previewBag == null || previewAnchor == null) return;

        if (!BagModelUtil.TryGetWorldBounds(previewBag, out Bounds bounds)) {
            Debug.LogWarning("[TryOnBagPose] 가방 렌더러를 찾지 못해 저장하지 못했습니다.");
            return;
        }

        Undo.RecordObject(library, "Save Bag Hold Pose");

        entry.anchorPosition = previewAnchor.localPosition;
        entry.holdRotation = previewBag.transform.localRotation.eulerAngles;
        entry.gripPoint = BagModelUtil.ToGripRatio(bounds, previewAnchor.position);

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        ReapplyPose(entry);

        Debug.Log($"[TryOnBagPose] {entry.prefab.name} 저장 완료 — " +
                  $"고정점 {entry.anchorPosition}, 접점 {entry.gripPoint}, 회전 {entry.holdRotation}");

        Repaint();
    }

    private void ClearPreview() {
        if (previewRoot != null) DestroyImmediate(previewRoot);

        // 리컴파일 등으로 참조를 잃은 미리보기가 남을 수 있어 이름으로도 찾는다
        GameObject stray = GameObject.Find(PreviewName);
        if (stray != null) DestroyImmediate(stray);

        previewRoot = null;
        previewAnchor = null;
        previewBag = null;
    }

    private static void SetHideFlagsRecursively(GameObject target, HideFlags flags) {
        target.hideFlags = flags;

        foreach (Transform child in target.transform) {
            SetHideFlagsRecursively(child.gameObject, flags);
        }
    }
}
#endif
