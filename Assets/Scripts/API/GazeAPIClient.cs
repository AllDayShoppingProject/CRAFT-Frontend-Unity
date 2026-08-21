using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GazeAPIClient : MonoBehaviour
{
    private string API_URL = ProjectConfig.API_BASE_URL;

    [SerializeField] private GazeLogger gazeLogger;
    [SerializeField] private GazeTracker gazeTracker;

    private string customerId;
    private bool isSending;

    [Serializable]
    private class EventBatchRequest
    {
        public string customer_id;
        public GazeLogger.GazeEvent[] events;
    }

    private void Awake()
    {
        // 이벤트는 관람 내내 버퍼에 쌓았다가 세션 끝에 한 번에 보낸다.
        // 그래서 버퍼(GazeLogger)와 이 스크립트가 씬 전환 뒤에도 함께 살아 있어야 한다.
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (gazeLogger == null)
        {
            gazeLogger =
                FindFirstObjectByType<GazeLogger>();
        }

        if (gazeTracker == null)
        {
            gazeTracker =
                FindFirstObjectByType<GazeTracker>();
        }
    }

    // 세션 생성(POST /v1/sessions)은 SessionManager 담당. 여기에 두면 세션이 두 번 생성된다.

    public void SetSessionCustomerId(string customerId)
    {
        this.customerId = customerId;
        Debug.Log(
            $"[GazeAPIClient] CustomerId 설정: {this.customerId}"
        );
    }

    public void EndSession(
        float totalDuration,
        int productsViewedCount,
        int tryonCount,
        int preregCount,
        string exitReason
    )
    {
        StartCoroutine(
            EndSessionCoroutine(
                totalDuration,
                productsViewedCount,
                tryonCount,
                preregCount,
                exitReason
            )
        );
    }

    /// 세션 종료 이벤트를 붙이고 배치를 전송한다.
    /// 호출한 쪽이 전송 완료를 기다려야 한다. 기다리지 않으면 씬 전환·파괴 때 요청이 끊긴다.
    public IEnumerator EndSessionCoroutine(
        float totalDuration,
        int productsViewedCount,
        int tryonCount,
        int preregCount,
        string exitReason
    )
    {
        if (string.IsNullOrEmpty(customerId))
        {
            Debug.LogWarning(
                "세션이 생성되지 않아 종료 이벤트를 전송할 수 없습니다."
            );

            yield break;
        }

        if (gazeLogger == null)
            yield break;

        gazeLogger.AddSessionEnd(
            customerId,
            totalDuration,
            productsViewedCount,
            tryonCount,
            preregCount,
            exitReason
        );

        yield return FlushCoroutine();
    }

    public void SendEvents()
    {
        StartCoroutine(FlushCoroutine());
    }

    /// 버퍼에 쌓인 이벤트를 한 번에 보낸다. 완료를 기다리려면 이 코루틴을 yield 하면 된다.
    public IEnumerator FlushCoroutine()
    {
        if (isSending)
            yield break;

        if (gazeLogger == null)
        {
            Debug.LogError(
                "GazeLogger를 찾을 수 없습니다."
            );

            yield break;
        }

        List<GazeLogger.GazeEvent> events =
            gazeLogger.GetEventBuffer();

        // 버퍼가 비어 있어도 전송은 해야 하므로 빈 배치를 걸러내지 않는다.

        // 백엔드 미설정이면 전송 대신 JSON 구조만 확인한다
        if (!ProjectConfig.IsApiConfigured)
        {
            EventBatchRequest requestData =
                new EventBatchRequest
                {
                    customer_id = customerId,
                    events = events.ToArray()
                };

            string json =
                JsonUtility.ToJson(requestData, true);

            Debug.Log(
                $"[GazeAPIClient] 백엔드 미설정 - 전송 대신 JSON 확인:\n{json}"
            );

            yield break;
        }

        yield return SendEventsCoroutine(events);
    }

    private IEnumerator SendEventsCoroutine(
        List<GazeLogger.GazeEvent> events
    )
    {
        isSending = true;

        EventBatchRequest requestData =
            new EventBatchRequest
            {
                customer_id = customerId,
                events = events.ToArray()
            };

        string json =
            JsonUtility.ToJson(requestData);
        Debug.Log($"[GazeAPIClient] 전송 JSON:\n{json}");

        byte[] bodyRaw =
            Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request =
            new UnityWebRequest(
                API_URL + "/events:batch",
                "POST"
            );

        request.uploadHandler =
            new UploadHandlerRaw(bodyRaw);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        // ngrok 무료 터널의 브라우저 경고 페이지를 건너뛰는 헤더
        request.SetRequestHeader(
            "ngrok-skip-browser-warning",
            "1"
        );

        yield return request.SendWebRequest();

        if (request.result ==
            UnityWebRequest.Result.Success)
        {
            Debug.Log(
                $"이벤트 전송 성공: {events.Count}개"
            );

            gazeLogger.Clear();
        }
        else
        {
            // 서버가 실패 원인을 본문에만 담아 보내서, 응답 body를 같이 찍어야 원인을 알 수 있다
            string failureBody =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "(본문 없음)";

            if (failureBody.Length > 500)
            {
                failureBody = failureBody.Substring(0, 500) + "…";
            }

            Debug.LogWarning(
                $"이벤트 전송 실패: {request.responseCode} / {request.error}\n" +
                $"url={request.url}\n" +
                $"body={failureBody}"
            );
        }

        isSending = false;
    }

    // OnApplicationQuit 에서는 코루틴이 다음 프레임을 못 받아 전송 전에 앱이 끝난다.
    // 그래서 Application.wantsToQuit 으로 종료를 붙잡고 flush 후에 직접 종료한다.
    // (에디터 Stop 버튼에서는 이 지연이 보장되지 않는다)
    private bool quitRequested;

    private void OnEnable()
    {
        Application.wantsToQuit += HandleWantsToQuit;
    }

    private void OnDisable()
    {
        Application.wantsToQuit -= HandleWantsToQuit;
    }

    private bool HandleWantsToQuit()
    {
        if (quitRequested) return true;
        if (string.IsNullOrEmpty(customerId)) return true;
        if (gazeLogger == null || gazeLogger.BufferedCount == 0) return true;

        StartCoroutine(FlushThenQuit());

        return false;   // 아직 끄지 마라
    }

    private IEnumerator FlushThenQuit()
    {
        yield return FlushCoroutine();

        quitRequested = true;
        Application.Quit();
    }
}
