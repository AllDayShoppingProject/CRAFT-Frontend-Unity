using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class BackEndAlive : MonoBehaviour
{
    private const string serverUrl=ProjectConfig.API_BASE_URL;

    private void Start()
    {
        StartCoroutine(PingBackendThreeTimes());
    }

    private IEnumerator PingBackendThreeTimes()
    {
        for (int i = 0; i < 3; i++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(
                $"{serverUrl}/products?launch_status=dummy"))
            {
                yield return request.SendWebRequest();
            }

            if (i < 2)
                yield return new WaitForSeconds(10f);
        }
    }
}