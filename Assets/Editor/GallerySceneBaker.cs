#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// 매장 구조물의 초벌을 씬에 만들어 준다. 그 뒤로는 씬에서 직접 손보면 된다.
///
/// 실행할 때는 GalleryController가 매장을 짓지 않는다. 씬에 있는 것을 찾아 쓸 뿐이다.
/// (Awake에서 지으면 로딩이 끝난 뒤에야 만들어져서 진입 순간 빈 화면이 보인다)
///
/// 그래서 이 도구는 "한 번 만들어 놓고 손으로 다듬기 시작하는 자리"다.
/// 좌표는 StoreLayout 상수에서, 조명 각도는 뮤지엄 앵글 계산에서 나오기 때문에
/// 처음부터 손으로 놓으면 값이 어긋난다.
///
/// 사용법
///   1) 02_Gallery 씬을 연다
///   2) Tools > 갤러리 초기 배치 만들기
///   3) 씬 저장 (Ctrl+S)
///   4) 이후로는 씬에서 직접 옮기고 지우고 더한다
public static class GallerySceneBaker {

    [MenuItem("Tools/갤러리 초기 배치 만들기")]
    public static void Build() {
        GalleryController controller = ResolveController();
        if (controller == null) return;

        if (!EditorUtility.DisplayDialog("갤러리 초기 배치 만들기",
                "StoreEnvironment · Entrance · Products · Lighting 을 새로 만듭니다.\n" +
                "이미 있으면 지우고 다시 만듭니다 — 씬에서 직접 손본 내용이 사라집니다.\n\n" +
                "계속할까요?", "만들기", "취소")) {
            return;
        }

        // 지우고 시작하지 않으면 매장이 두 겹으로 쌓인다
        controller.ClearStaticScene();
        controller.BuildStaticScene();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        Debug.Log("[GallerySceneBaker] 초기 배치를 만들었습니다. " +
                  "Ctrl+S 로 씬을 저장해야 유지됩니다.\n" +
                  "가방 색은 GalleryController 인스펙터의 bagColors 에 기록되었습니다.");
    }

    [MenuItem("Tools/갤러리 배치 지우기")]
    public static void Clear() {
        GalleryController controller = ResolveController();
        if (controller == null) return;

        if (!EditorUtility.DisplayDialog("갤러리 배치 지우기",
                "StoreEnvironment · Entrance · Products · Lighting 을 지웁니다.\n\n" +
                "계속할까요?", "지우기", "취소")) {
            return;
        }

        controller.ClearStaticScene();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        Debug.Log("[GallerySceneBaker] 구조물을 지웠습니다.");
    }

    [MenuItem("Tools/갤러리 초기 배치 만들기", true)]
    [MenuItem("Tools/갤러리 배치 지우기", true)]
    private static bool NotPlaying() {
        return !Application.isPlaying;
    }

    private static GalleryController ResolveController() {
        GalleryController controller = Object.FindFirstObjectByType<GalleryController>();

        if (controller == null) {
            EditorUtility.DisplayDialog("갤러리 배치",
                "열려 있는 씬에서 GalleryController를 찾지 못했습니다.\n" +
                "02_Gallery 씬을 먼저 열어 주세요.", "확인");
        }

        return controller;
    }
}
#endif
