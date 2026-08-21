using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class PreRegAPIClient : MonoBehaviour
{
    private const string API_URL =
        ProjectConfig.API_BASE_URL;


    [Serializable]
    private class PreRegRequest
    {
        public string customer_id;
        public int product_id;
        public string name;
        public string phone;
        public bool consent;

        // color / size 는 요청 본문에 없다. 컬러·사이즈는 prereg_submit 이벤트로 따로 나간다.
        // JsonUtility가 public 필드를 전부 직렬화하므로, 여기 두면 서버가 모르는 값이 바디에 섞인다.
    }

    [Serializable]
    private class PreRegResponse
    {
        public string prereg_id;
        public string created_at;
    }


    public void Submit(
        int productId,
        string name,
        string phone,
        bool consent,
        Action<bool, bool> onComplete = null
    )
    {
        if (SessionManager.Instance == null ||
            string.IsNullOrEmpty(
                SessionManager.Instance.CustomerId
            ))
        {
            Debug.LogWarning(
                "[PreRegAPIClient] 세션이 없어 " +
                "사전등록을 전송할 수 없습니다."
            );

            onComplete?.Invoke(false, false);
            return;
        }


        // 로컬 세션의 customer_id는 서버가 발급한 적이 없어 보내도 거절당한다.
        // 전송만 건너뛰고 화면은 성공과 똑같이 이어간다. 입력값은 저장하지 않고 버린다.
        if (SessionManager.Instance.IsLocalSession)
        {
            Debug.LogWarning(
                "[PreRegAPIClient] 서버 세션이 없어(로컬 세션) 전송을 건너뜁니다. " +
                "화면만 완료 처리합니다 — 입력값은 저장되지 않습니다.\n" +
                $"백엔드 주소를 확인하세요: {API_URL}"
            );

            onComplete?.Invoke(true, false);
            return;
        }


        StartCoroutine(
            SubmitCoroutine(
                productId,
                name,
                phone,
                consent,
                onComplete
            )
        );
    }


    private IEnumerator SubmitCoroutine(
        int productId,
        string name,
        string phone,
        bool consent,
        Action<bool, bool> onComplete
    )
    {
        string customerId =
            SessionManager.Instance.CustomerId;


        PreRegRequest requestData =
            new PreRegRequest
            {
                customer_id = customerId,
                product_id = productId,
                name = name,
                phone = phone,
                consent = consent
            };


        string json =
            JsonUtility.ToJson(
                requestData
            );


        Debug.Log(
            $"[PreRegAPIClient] 사전등록 요청:\n{json}"
        );


        byte[] bodyRaw =
            Encoding.UTF8.GetBytes(
                json
            );


        using UnityWebRequest request =
            new UnityWebRequest(
                API_URL + "/preregistrations",
                "POST"
            );

        request.uploadHandler =
            new UploadHandlerRaw(
                bodyRaw
            );

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        // ngrok 무료 터널이 끼워 넣는 경고 페이지를 건너뛴다 (값은 아무거나 상관없다)
        request.SetRequestHeader(
            "ngrok-skip-browser-warning",
            "1"
        );


        yield return request.SendWebRequest();


        int statusCode =
            (int)request.responseCode;


        if (statusCode == 201)
        {
            PreRegResponse response = null;

            try
            {
                response =
                    JsonUtility.FromJson<PreRegResponse>(
                        request.downloadHandler.text
                    );
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PreRegAPIClient] 성공 응답 파싱 실패: " +
                    exception.Message
                );
            }


            Debug.Log(
                "[PreRegAPIClient] 사전등록 성공. " +
                $"prereg_id={response?.prereg_id}"
            );


            onComplete?.Invoke(
                true,
                false
            );

            yield break;
        }


        if (statusCode == 409)
        {
            Debug.Log(
                "[PreRegAPIClient] 이미 등록된 제품입니다."
            );


            // 정상 처리이지만 중복 상태
            onComplete?.Invoke(
                true,
                true
            );

            yield break;
        }


        // request.error 는 상태줄까지만 알려준다. 404가 라우트 미구현인지 터널이 죽은 건지
        // 구분하려면 응답 본문을 봐야 해서 함께 로그에 남긴다.
        string responseBody =
            request.downloadHandler != null
                ? request.downloadHandler.text
                : "(본문 없음)";

        if (responseBody.Length > 500)
        {
            responseBody = responseBody.Substring(0, 500) + "…";
        }

        Debug.LogWarning(
            "[PreRegAPIClient] 사전등록 실패. " +
            $"status={statusCode}, " +
            $"error={request.error}\n" +
            $"url={request.url}\n" +
            $"body={responseBody}"
        );


        // 실패해도 개인정보는 저장하지 않는다. 재시도는 호출한 쪽의 입력값으로 한다.
        onComplete?.Invoke(
            false,
            false
        );
    }
}