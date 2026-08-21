/*
 * IntroSceneController.cs
 *
 * 전시 입장 전 사용자 프로필 입력을 담당한다.
 *
 * 입력 조건
 * - 닉네임: 비어 있지 않아야 함
 * - 키: 숫자이며 100~250cm 범위
 * - 개인정보 수집 동의: 체크되어 있어야 함
 *
 * 모든 조건을 만족하면 입장 버튼이 활성화된다.
 * 조건을 만족하지 않으면 버튼은 70% 투명도로 표시된다.
 */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IntroSceneController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_InputField heightInput;
    [SerializeField] private TMP_Text heightWarning;
    [SerializeField] private Toggle privacyToggle;

    [Header("Button")]
    [SerializeField] private Button enterButton;

    [Header("Button Appearance")]
    [SerializeField, Range(0f, 1f)]
    private float disabledAlpha = 0.7f;

    private CanvasGroup enterButtonCanvasGroup;

    private void Awake()
    {
        WireEvents();
        SetupButtonVisual();

        // 초기 UI 상태 갱신
        UpdateEnterButtonState();
    }

    private void WireEvents()
    {
        if (enterButton != null)
        {
            enterButton.onClick.RemoveAllListeners();
            enterButton.onClick.AddListener(OnEnterClicked);
        }

        if (nicknameInput != null)
        {
            nicknameInput.onValueChanged.AddListener(
                _ => UpdateEnterButtonState()
            );
        }

        if (heightInput != null)
        {
            heightInput.onValueChanged.AddListener(
                _ => UpdateEnterButtonState()
            );
        }

        if (privacyToggle != null)
        {
            privacyToggle.onValueChanged.AddListener(
                _ => UpdateEnterButtonState()
            );
        }
    }

    private void SetupButtonVisual()
    {
        if (enterButton == null)
        {
            return;
        }

        enterButtonCanvasGroup =
            enterButton.GetComponent<CanvasGroup>();

        if (enterButtonCanvasGroup == null)
        {
            enterButtonCanvasGroup =
                enterButton.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void UpdateEnterButtonState()
    {
        if (enterButton == null)
        {
            return;
        }

        // -----------------------------
        // 닉네임 검사
        // -----------------------------

        bool nicknameValid =
            nicknameInput != null &&
            !string.IsNullOrWhiteSpace(
                nicknameInput.text
            );

        // -----------------------------
        // 키 검사
        // -----------------------------

        bool heightHasInput =
            heightInput != null &&
            !string.IsNullOrWhiteSpace(
                heightInput.text
            );

        int height = 0;

        bool heightParsed =
            heightHasInput &&
            int.TryParse(
                heightInput.text,
                out height
            );

        bool heightInRange =
            heightParsed &&
            height >= 100 &&
            height <= 250;

        // -----------------------------
        // 키 경고 문구
        // -----------------------------

        if (heightWarning != null)
        {
            bool showWarning =
                heightHasInput &&
                (
                    !heightParsed ||
                    !heightInRange
                );

            heightWarning.gameObject.SetActive(
                showWarning
            );

            if (showWarning)
            {
                if (!heightParsed)
                {
                    heightWarning.text =
                        "키는 숫자로 입력해주세요.";
                }
                else
                {
                    heightWarning.text =
                        "키는 100~250cm 사이로 입력해주세요.";
                }
            }
        }

        // -----------------------------
        // 개인정보 동의
        // -----------------------------

        bool privacyAccepted =
            privacyToggle != null &&
            privacyToggle.isOn;

        // -----------------------------
        // 최종 입장 가능 여부
        // -----------------------------

        bool canEnter =
            nicknameValid &&
            heightInRange &&
            privacyAccepted;

        enterButton.interactable =
            canEnter;

        // -----------------------------
        // 버튼 투명도
        // -----------------------------

        if (enterButtonCanvasGroup != null)
        {
            enterButtonCanvasGroup.alpha =
                canEnter
                    ? 1f
                    : disabledAlpha;
        }
    }

    private void OnEnterClicked()
    {
        // 혹시 모를 직접 호출 방어
        if (enterButton == null ||
            !enterButton.interactable)
        {
            return;
        }

        if (nicknameInput == null ||
            heightInput == null)
        {
            Debug.LogError(
                "프로필 입력 UI가 연결되지 않았습니다."
            );

            return;
        }

        string nickname =
            nicknameInput.text.Trim();

        if (!int.TryParse(
                heightInput.text,
                out int height))
        {
            Debug.LogError(
                "키 입력값이 올바른 숫자가 아닙니다."
            );

            return;
        }

        // -----------------------------
        // 세션 정보 생성
        // -----------------------------

        SessionManager.Create(
            nickname,
            height
        );

        // -----------------------------
        // Loading Scene으로 이동
        // -----------------------------

        LoadingSceneController.Load(
            SceneNames.Gallery,
            BuildIntroTask
        );
    }

    private IEnumerator BuildIntroTask()
    {
        SessionManager sessionManager =
            SessionManager.Instance;

        if (sessionManager == null)
        {
            Debug.LogError(
                "SessionManager 생성 실패"
            );

            yield break;
        }

        // 세션 생성
        yield return sessionManager.StartSessionCoroutine();

        // 프로필 서버 반영
        yield return sessionManager.PatchProfileCoroutine();
    }
}