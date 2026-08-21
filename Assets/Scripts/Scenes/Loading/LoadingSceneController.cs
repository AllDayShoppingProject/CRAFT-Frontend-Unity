using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : MonoBehaviour
{
    private const string LoadingSceneName = SceneNames.Loading;
    private const float GearSpinSpeed = 240f;

    private static LoadingRequest pendingRequest;

    [Header("Loading UI")]
    [SerializeField] private RectTransform gearIcon;
    [SerializeField] private Button continueButton;

    private AsyncOperation targetSceneOperation;

    private bool targetSceneLoaded;
    private bool additionalTaskCompleted;
    private bool additionalTaskFailed;
    private bool activationRequested;
    private bool isCompleteUiVisible;

    private sealed class LoadingRequest
    {
        public string TargetSceneName;
        public IEnumerator AdditionalTask;
    }

    public static void Load(string sceneName)
    {
        Load(sceneName, (IEnumerator)null);
    }

    public static void Load(
        string sceneName,
        IEnumerator additionalTask
    )
    {
        LoadInternal(
            sceneName,
            additionalTask
        );
    }

    public static void Load(
        string sceneName,
        Func<IEnumerator> additionalTaskFactory
    )
    {
        LoadInternal(
            sceneName,
            additionalTaskFactory != null
                ? additionalTaskFactory()
                : null
        );
    }

    private static void LoadInternal(
        string sceneName,
        IEnumerator additionalTask
    )
    {
        string targetSceneName =
            (sceneName ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError(
                "LoadingSceneController.Load: TargetScene이 비어 있습니다."
            );

            return;
        }

        if (string.Equals(
                targetSceneName,
                LoadingSceneName,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            Debug.LogError(
                "LoadingSceneController.Load: Loading Scene 자체를 TargetScene으로 요청할 수 없습니다."
            );

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
                targetSceneName
            ))
        {
            Debug.LogError(
                $"LoadingSceneController.Load: Scene '{targetSceneName}'을(를) 찾을 수 없거나 Build Settings에 등록되지 않았습니다."
            );

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
                LoadingSceneName
            ))
        {
            Debug.LogError(
                $"LoadingSceneController.Load: Loading Scene '{LoadingSceneName}'을(를) 찾을 수 없습니다. Build Settings를 확인하세요."
            );

            return;
        }

        if (pendingRequest != null)
        {
            Debug.LogError(
                "LoadingSceneController.Load: 이미 진행 중인 로딩 요청이 있습니다."
            );

            return;
        }

        pendingRequest = new LoadingRequest
        {
            TargetSceneName = targetSceneName,
            AdditionalTask = additionalTask
        };

        Debug.Log(
            $"LoadingSceneController.Load: '{targetSceneName}' 로딩을 시작합니다."
        );

        SceneManager.LoadScene(
            LoadingSceneName
        );
    }

    private void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(
                OnContinueClicked
            );
        }

        ResetUiToLoadingState();
    }

    private void Start()
    {
        if (pendingRequest == null)
        {
            Debug.LogError(
                "LoadingSceneController: 대기 중인 로딩 요청이 없습니다."
            );

            return;
        }

        StartCoroutine(
            LoadRoutine()
        );
    }

    private void Update()
    {
        RotateGear();
    }

    private void OnDisable()
    {
        if (pendingRequest == null)
        {
            return;
        }

        if (!targetSceneLoaded ||
            !activationRequested)
        {
            Debug.LogWarning(
                "LoadingSceneController: 로딩 씬이 비정상 종료되어 요청 상태를 정리합니다."
            );
        }

        ClearStaticState();
    }

    private IEnumerator LoadRoutine()
    {
        Debug.Log(
            $"LoadingSceneController: target='{pendingRequest.TargetSceneName}' load routine started."
        );

        // 로딩 화면을 계속 보여줘야 하므로 활성화는 막아두고 미리 읽어둔다
        targetSceneOperation =
            SceneManager.LoadSceneAsync(
                pendingRequest.TargetSceneName
            );

        targetSceneOperation.allowSceneActivation = false;

        StartCoroutine(
            TrackAdditionalTask(
                pendingRequest.AdditionalTask
            )
        );

        Debug.Log(
            "LoadingSceneController: additional task coroutine started."
        );

        // allowSceneActivation=false면 progress는 0.9에서 멈춘다. 그게 로딩 완료 신호다.
        while (
            targetSceneOperation.progress < 0.9f
        )
        {
            yield return null;
        }

        targetSceneLoaded = true;

        Debug.Log(
            "LoadingSceneController: target scene loading reached activation gate."
        );

        // 씬과 추가 작업이 둘 다 끝난 뒤에 완료 UI를 띄운다
        while (!additionalTaskCompleted)
        {
            yield return null;
        }

        ShowCompleteUi();

        Debug.Log(
            "LoadingSceneController: loading completed, waiting for button."
        );

        // 실제 씬 전환은 버튼 클릭(OnContinueClicked)에서 allowSceneActivation=true로 일어난다
        while (!activationRequested)
        {
            yield return null;
        }
    }

    private IEnumerator TrackAdditionalTask(
        IEnumerator additionalTask
    )
    {
        if (additionalTask == null)
        {
            additionalTaskCompleted = true;

            Debug.Log(
                "LoadingSceneController: additional task is null, marked complete."
            );

            yield break;
        }

        Debug.Log(
            "LoadingSceneController: additional task started."
        );

        yield return RunSafeCoroutine(
            additionalTask
        );

        additionalTaskCompleted = true;

        Debug.Log(
            "LoadingSceneController: additional task completed."
        );
    }

    private IEnumerator RunSafeCoroutine(
        IEnumerator routine
    )
    {
        if (routine == null)
        {
            yield break;
        }

        while (true)
        {
            object current;

            try
            {
                if (!routine.MoveNext())
                {
                    yield break;
                }

                current = routine.Current;
            }
            catch (Exception exception)
            {
                additionalTaskFailed = true;

                Debug.LogException(
                    exception
                );

                Debug.LogWarning(
                    "LoadingSceneController: additional task failed but loading continues."
                );

                yield break;
            }

            if (current is IEnumerator nestedRoutine)
            {
                yield return RunSafeCoroutine(
                    nestedRoutine
                );
            }
            else
            {
                yield return current;
            }
        }
    }

    private void RotateGear()
    {
        if (gearIcon == null)
        {
            return;
        }

        if (!gearIcon.gameObject.activeSelf)
        {
            return;
        }

        gearIcon.Rotate(
            0f,
            0f,
            -GearSpinSpeed *
            Time.unscaledDeltaTime
        );
    }

    private void ShowCompleteUi()
    {
        if (isCompleteUiVisible)
        {
            return;
        }

        isCompleteUiVisible = true;

        if (gearIcon != null)
        {
            gearIcon.gameObject.SetActive(
                false
            );
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(
                true
            );
        }

        if (additionalTaskFailed)
        {
            Debug.LogWarning(
                "LoadingSceneController: 추가 작업 중 예외가 발생했지만 로딩 씬은 정상적으로 진행됩니다."
            );
        }
    }

    private void OnContinueClicked()
    {
        if (!isCompleteUiVisible)
        {
            return;
        }

        if (activationRequested)
        {
            return;
        }

        if (targetSceneOperation == null)
        {
            Debug.LogError(
                "LoadingSceneController: target scene operation이 없습니다."
            );

            return;
        }

        activationRequested = true;

        targetSceneOperation.allowSceneActivation =
            true;

        Debug.Log(
            "LoadingSceneController: 입장 버튼 클릭, target scene 활성화."
        );
    }

    private void ResetUiToLoadingState()
    {
        if (gearIcon != null)
        {
            gearIcon.gameObject.SetActive(
                true
            );

            gearIcon.localRotation =
                Quaternion.identity;
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(
                false
            );
        }
    }

    private void ClearStaticState()
    {
        pendingRequest = null;
    }
}