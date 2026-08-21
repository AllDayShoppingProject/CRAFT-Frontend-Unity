using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SessionManager : MonoBehaviour
{
    private const string ApiUrl = ProjectConfig.API_BASE_URL;

    /// 세션 요청과 session_start meta가 같은 값을 써야 서버 기록과 이벤트 로그가 어긋나지 않는다.
    private static string DeviceType =>
        Application.platform == RuntimePlatform.WebGLPlayer
            ? "webgl"
            : "standalone";

    private static SessionManager instance;

    public static SessionManager Instance => instance;

    public string Nickname { get; private set; }

    // 사용자가 실제로 입력한 원본 키
    public int? RawHeight { get; private set; }

    // 허용 키 목록에 맞게 정규화된 키
    public int? Height { get; private set; }

    public string CustomerId { get; private set; }

    public bool IsSessionReady { get; private set; }

    /// 서버 세션 발급에 실패해 클라이언트가 임시로 만든 세션의 ID 접두사.
    public const string LOCAL_ID_PREFIX = "local-";

    /// 서버가 발급한 적 없는 임시 customer_id인지. true면 서버로 무엇을 보내도 의미가 없다.
    /// (IsSessionReady는 로컬 세션에서도 true다)
    public bool IsLocalSession { get; private set; }


    private GazeAPIClient gazeAPIClient;
    private GazeTracker gazeTracker;
    private GazeLogger gazeLogger;


    private bool isEnding;


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }


    /// 세션을 끝내고 이벤트 배치를 전송한다. 전송이 끝날 때까지 돌아오지 않는다.
    ///
    /// 이벤트는 관람 내내 메모리에만 쌓여 있다가 여기서 한 번에 나간다.
    /// 기다리지 않고 씬을 넘기면 한 세션 분량의 로그가 통째로 사라진다.
    ///
    /// 집계값은 SessionDataManager가 들고 있어 여기서 모아 넘긴다.
    public IEnumerator EndSessionFromScene(string exitReason)
    {
        if (isEnding)
        {
            yield break;
        }

        isEnding = true;

        SessionDataManager data = SessionDataManager.Instance;

        yield return EndSessionCoroutine(
            data != null ? data.SessionDuration : 0f,
            data != null ? data.ProductsViewedCount : 0,
            data != null ? data.TryOnCount : 0,
            data != null ? data.PreregCount : 0,
            exitReason
        );
    }


    public static SessionManager Create(
        string nickname,
        int? height
    )
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
            instance = null;
        }

        GameObject sessionObject =
            new GameObject("SessionManager");

        SessionManager manager =
            sessionObject.AddComponent<SessionManager>();

        DontDestroyOnLoad(sessionObject);

        manager.SetSessionInfo(
            nickname,
            height
        );

        // session_start는 여기서 바로 버퍼에 쌓이는데, 로딩 씬은 씬 활성화를
        // allowSceneActivation=false로 미뤄놔서 Gallery의 GazeLogger가 아직 없다.
        // 그대로 두면 PushSessionStart()가 로거를 못 찾아 session_start가 매번 유실된다.
        // 그래서 세션 소유자인 여기서 먼저 만들어 둔다 - Gallery가 나중에 CreateLoggingServices()에서
        // Find로 이 인스턴스를 그대로 재사용하므로 중복 생성되지 않는다.
        manager.EnsureLoggingBootstrap();

        Debug.Log(
            $"SessionManager: created. " +
            $"nickname='{manager.Nickname}', " +
            $"rawHeight='{manager.RawHeight}', " +
            $"height='{manager.Height}'"
        );

        return manager;
    }


    public void SetSessionInfo(
        string nickname,
        int? rawHeight
    )
    {
        Nickname =
            nickname?.Trim();

        RawHeight =
            rawHeight;

        Height =
            rawHeight.HasValue
                ? NormalizeHeight(rawHeight.Value)
                : null;
    }


    private int NormalizeHeight(int rawHeight)
    {
        int[] allowedHeights =
            ProjectConfig.AllowedHeights;

        if (allowedHeights == null ||
            allowedHeights.Length == 0)
        {
            return rawHeight;
        }

        int closestHeight =
            allowedHeights[0];

        int smallestDifference =
            Mathf.Abs(
                rawHeight -
                closestHeight
            );

        for (int i = 1;
             i < allowedHeights.Length;
             i++)
        {
            int difference =
                Mathf.Abs(
                    rawHeight -
                    allowedHeights[i]
                );

            if (difference < smallestDifference)
            {
                smallestDifference =
                    difference;

                closestHeight =
                    allowedHeights[i];
            }
        }

        return closestHeight;
    }


    public void BindSceneServices(
        GazeAPIClient apiClient,
        GazeTracker tracker,
        GazeLogger logger
    )
    {
        gazeAPIClient = apiClient;
        gazeTracker = tracker;
        gazeLogger = logger;
    }

    /// session_start를 놓치지 않으려면 로딩 씬으로 넘어가기 전에 이벤트 버퍼가 있어야 한다.
    /// GazeAPIClient가 자기 Awake에서 이 GameObject를 DontDestroyOnLoad로 만들어주므로
    /// Gallery 씬까지 그대로 살아남고, GalleryController.CreateLoggingServices()는
    /// Find로 이 인스턴스를 찾아 재사용한다 (중복 생성 없음).
    private void EnsureLoggingBootstrap()
    {
        if (gazeLogger == null)
        {
            gazeLogger = FindFirstObjectByType<GazeLogger>();
        }

        if (gazeLogger == null)
        {
            gazeLogger = new GameObject("GazeLogger").AddComponent<GazeLogger>();
        }

        if (gazeAPIClient == null)
        {
            gazeAPIClient = gazeLogger.GetComponent<GazeAPIClient>();
        }

        if (gazeAPIClient == null)
        {
            gazeAPIClient = gazeLogger.gameObject.AddComponent<GazeAPIClient>();
        }
    }


    public void StartSession()
    {
        Debug.Log(
            "SessionManager: start session requested."
        );

        StartCoroutine(
            StartSessionCoroutine()
        );
    }


    public IEnumerator StartSessionCoroutine()
    {
        // 닉네임은 클라이언트 표시용이라 없어도 세션은 발급한다(명세서 §1 익명 세션).
        // 예전엔 닉네임이 비면 customer_id를 null로 만들어 그 세션 이벤트가 전부 유실됐다.
        if (string.IsNullOrWhiteSpace(Nickname))
        {
            Debug.Log(
                "SessionManager: 닉네임 없이 익명 세션으로 진행합니다."
            );
        }

        if (!ProjectConfig.IsApiConfigured)
        {
            Debug.LogWarning(
                "SessionManager: API 주소가 설정되지 않아 " +
                "로컬 세션으로 진행합니다."
            );

            OnSessionCreated(
                LOCAL_ID_PREFIX +
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 8)
            );

            yield break;
        }

        Debug.Log(
            "SessionManager: opening server session..."
        );

        using UnityWebRequest request =
            new UnityWebRequest(
                ApiUrl + "/sessions",
                "POST"
            );

        string json =
            JsonUtility.ToJson(
                new SessionRequest
                {
                    device_type = DeviceType,
                    viewport_w = Screen.width,
                    viewport_h = Screen.height
                }
            );

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(json)
            );

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );
        // ngrok 무료 터널이 끼워 넣는 브라우저 경고 페이지를 건너뛰는 헤더.
        request.SetRequestHeader(
            "ngrok-skip-browser-warning",
            "1"
        );
        Debug.Log(
            $"[SessionManager] POST URL: {ApiUrl}/sessions\n" +
            $"Body: {json}"
        );
        yield return request.SendWebRequest();


        if (request.result != UnityWebRequest.Result.Success)
        {
            // 여기서 404면 이후 호출도 전부 404다. 응답 본문으로 원인을 구분하려고 함께 남긴다.
            // ("Not Found"면 라우트 없음 / ERR_NGROK_3200이면 터널 주소 만료)
            string failureBody =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "(본문 없음)";

            if (failureBody.Length > 500)
            {
                failureBody = failureBody.Substring(0, 500) + "…";
            }

            Debug.LogWarning(
                $"세션 생성 실패: {request.responseCode} / {request.error}\n" +
                $"url={request.url}\n" +
                $"body={failureBody}\n" +
                "백엔드 세션 생성에 실패하여 로컬 세션으로 진행합니다."
            );

            OnSessionCreated(
                LOCAL_ID_PREFIX +
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 8)
            );

            yield break;
        }


        SessionResponse response;

        try
        {
            response =
                JsonUtility.FromJson<SessionResponse>(
                    request.downloadHandler.text
                );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"세션 응답 파싱 실패: " +
                $"{exception.Message}"
            );

            OnSessionCreated(null);

            yield break;
        }


        OnSessionCreated(
            response != null
                ? response.customer_id
                : null
        );

        Debug.Log(
            $"SessionManager: session ready = " +
            $"{IsSessionReady}, " +
            $"customerId='{CustomerId}'"
        );
    }


    private void OnSessionCreated(
        string customerId
    )
    {
        CustomerId =
            customerId;

        IsSessionReady =
            !string.IsNullOrEmpty(
                customerId
            );

        IsLocalSession =
            IsSessionReady &&
            customerId.StartsWith(
                LOCAL_ID_PREFIX
            );

        if (gazeTracker != null)
        {
            gazeTracker.SetCustomerId(
                customerId
            );
        }

        if (gazeAPIClient != null)
        {
            gazeAPIClient.SetSessionCustomerId(
                customerId
            );
        }

        // session_start는 서버 호출이 아니라 로컬 버퍼에 쌓였다가 events:batch로 나간다.
        PushSessionStart();

        Debug.Log(
            $"SessionManager: session ready = " +
            $"{IsSessionReady}, " +
            $"customerId='{CustomerId}'"
        );
    }

    /// 세션당 1회. 반복 이벤트가 아니므로 interaction_id / occurrence_index 는 붙이지 않는다.
    private void PushSessionStart()
    {
        if (gazeLogger == null)
        {
            gazeLogger = FindFirstObjectByType<GazeLogger>();
        }

        if (gazeLogger == null || string.IsNullOrEmpty(CustomerId))
        {
            return;
        }

        gazeLogger.AddEvent(
            "session_start",
            CustomerId,
            new GazeLogger.EventMeta
            {
                referrer = string.Empty,
                device_type = DeviceType,
                viewport_w = Screen.width,
                viewport_h = Screen.height
            }
        );
    }


    public IEnumerator PatchProfileCoroutine()
    {
        if (string.IsNullOrEmpty(CustomerId))
        {
            Debug.LogWarning(
                "SessionManager: customer_id가 없어 " +
                "profile을 전송하지 않습니다."
            );

            yield break;
        }

        // 신장 미입력(Skip)이면 PATCH 자체를 보내지 않는다(명세서 §2).
        // 예전엔 170을 보내서, 사용자가 입력한 적 없는 값이 customers.height에 남았다.
        if (!RawHeight.HasValue)
        {
            Debug.Log(
                "SessionManager: 신장 미입력(Skip) - profile 전송을 건너뜁니다."
            );

            yield break;
        }

        // 서버에는 입력한 원본 키(RawHeight)를 보낸다. Height(반올림값)는 3D 더미 선택용 클라이언트 값이다.
        string profileJson =
            JsonUtility.ToJson(
                new ProfileRequest
                {
                    height = RawHeight.Value
                }
            );


        using UnityWebRequest request =
            new UnityWebRequest(
                $"{ApiUrl}/sessions/{CustomerId}/profile",
                "PATCH"
            );

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(
                    profileJson
                )
            );

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.SetRequestHeader(
            "ngrok-skip-browser-warning",
            "1"
        );

        yield return request.SendWebRequest();


        // 프로필 PATCH 실패는 전시 진입을 막지 않는다.
        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            Debug.LogWarning(
                $"프로필 전송 실패: " +
                $"{request.error}"
            );
        }
        else
        {
            Debug.Log(
                "SessionManager: profile sent."
            );
        }
    }


    /// 이벤트 배치 전송이 끝난 뒤에 세션을 정리한다.
    /// 기다리지 않고 Destroy하면 씬 전환 때 요청이 끊겨 한 세션 분량의 로그가 통째로 사라진다.
    private IEnumerator EndSessionCoroutine(
        float totalDuration,
        int productsViewedCount,
        int tryonCount,
        int preregCount,
        string exitReason
    )
    {
        if (gazeAPIClient == null)
        {
            gazeAPIClient = FindFirstObjectByType<GazeAPIClient>();
        }

        if (gazeAPIClient != null)
        {
            yield return gazeAPIClient.EndSessionCoroutine(
                totalDuration,
                productsViewedCount,
                tryonCount,
                preregCount,
                exitReason
            );
        }


        CustomerId =
            string.Empty;

        IsSessionReady =
            false;

        Nickname =
            string.Empty;

        RawHeight =
            null;

        Height =
            null;


        Destroy(gameObject);
    }


    [Serializable]
    private class SessionRequest
    {
        public string device_type;
        public int viewport_w;
        public int viewport_h;
    }


    [Serializable]
    private class ProfileRequest
    {
        public int height;
    }


    [Serializable]
    private class SessionResponse
    {
        public string customer_id;
    }
}
