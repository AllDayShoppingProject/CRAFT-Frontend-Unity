using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class BackEndAlive : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(SelectBackend());
    }

    private IEnumerator SelectBackend()
    {
        foreach (string url in ProjectConfig.API_BASE_URLS)
        {
            using (UnityWebRequest request =
                   UnityWebRequest.Get($"{url}/products?launch_status=dummy"))
            {
                request.timeout = 5;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log(url);
                    ProjectConfig.SetActiveApiBaseUrl(url);
                    yield break;
                }
            }
        }

        // 둘 다 실패하면 아무것도 하지 않고 종료
    }
}