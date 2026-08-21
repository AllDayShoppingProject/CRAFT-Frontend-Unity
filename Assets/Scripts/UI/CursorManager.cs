/*
 * CursorManager.cs
 *
 * 프로젝트 전체의 마우스 커서 상태를 관리한다.
 *
 * 기본 정책
 * - Intro / Loading 등 일반 씬:
 *   커서 표시 + Unlock
 *
 * - Gallery 씬:
 *   기본적으로 커서 숨김 + Lock
 *
 * Gallery에서:
 * - ESC → 커서 Unlock
 * - UI가 닫혀 있는 상태에서 화면 클릭 → 다시 Lock
 * - UI가 열려 있는 동안 → 커서는 Unlock 상태 유지
 * - UI 클릭은 기존 UI 시스템이 그대로 처리한다.
 *
 * 이 오브젝트는 Intro 씬에서 한 번만 생성되며
 * DontDestroyOnLoad를 통해 모든 씬에서 유지된다.
 */
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Gallery")]
    [SerializeField] private string gallerySceneName = "Gallery";

    private void Awake()
    {
        //빌드에서 로그 안보이게 하는 설정
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.logEnabled = true;
        Debug.unityLogger.filterLogType = LogType.Error;
        Debug.unityLogger.logHandler =
        new ReleaseLogHandler(Debug.unityLogger.logHandler);
#endif
        // 이미 CursorManager가 존재하면 중복 생성 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 모든 씬에서 유지
        DontDestroyOnLoad(gameObject);

        // 씬 전환 감지
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 현재 씬에 맞는 커서 상태 적용
        ApplyCurrentSceneCursorState();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneCursorState(scene);
    }

    private void Update()
    {
        // Gallery가 아니면 여기서 커서 입력을 처리하지 않는다.
        if (!IsGalleryScene())
        {
            return;
        }

        HandleGalleryCursorInput();
    }

    // =========================================================
    // Gallery Cursor Input
    // =========================================================

    private void HandleGalleryCursorInput()
    {
        // ESC를 누르면 언제든 커서를 Unlock한다.
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
            return;
        }

        // UI가 열려 있으면 클릭으로 다시 Lock하지 않는다.
        // BagViewUI가 없는 경우에는 UI가 닫혀 있는 것으로 취급한다.
        bool uiVisible =
            BagViewUI.Instance != null &&
            BagViewUI.Instance.IsVisible;

        if (uiVisible)
        {
            return;
        }

        // 이미 Lock되어 있다면 아무것도 하지 않는다.
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            return;
        }

        // UI가 닫혀 있고 커서가 Unlock 상태일 때
        // 화면 아무 곳이나 클릭하면 다시 Lock한다.
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    // =========================================================
    // Scene Cursor State
    // =========================================================

    private void ApplyCurrentSceneCursorState()
    {
        ApplySceneCursorState(SceneManager.GetActiveScene());
    }

    private void ApplySceneCursorState(Scene scene)
    {
        bool isGallery =
            scene.name == gallerySceneName;

        if (isGallery)
        {
            LockCursor();
        }
        else
        {
            UnlockCursor();
        }
    }

    private bool IsGalleryScene()
    {
        return SceneManager.GetActiveScene().name == gallerySceneName;
    }

    // =========================================================
    // Public Cursor Control
    // =========================================================

    /// <summary>
    /// 커서를 숨기고 화면 중앙에 Lock한다.
    /// Gallery 자유 시점 이동에 사용한다.
    /// </summary>
    public void LockCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// 커서를 표시하고 Lock을 해제한다.
    /// UI 조작 및 화면 밖으로 커서를 이동할 때 사용한다.
    /// </summary>
    public void UnlockCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// 현재 커서 상태를 반전한다.
    /// </summary>
    public void ToggleCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
        }
    }
    //에러 필터링용 클래스
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
    private class ReleaseLogHandler : ILogHandler
    {
        private readonly ILogHandler original;

        public ReleaseLogHandler(ILogHandler original)
        {
            this.original = original;
        }

        public void LogFormat(
            LogType logType,
            UnityEngine.Object context,
            string format,
            params object[] args)
        {
            if (logType == LogType.Log ||
                logType == LogType.Warning)
                return;

            string message = string.Format(format, args);

            // WebGL에서 발생하는 SphereCollider 관련 에러만 숨김
            if (message.Contains("SphereCollider"))
                return;

            original.LogFormat(logType, context, format, args);
        }

        public void LogException(
            Exception exception,
            UnityEngine.Object context)
        {
            original.LogException(exception, context);
        }
    }
#endif
}