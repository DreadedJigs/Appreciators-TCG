using System.Collections;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AppreciatorsTcg.Data
{
    public class BackendApiClient : MonoBehaviour
    {
        public IEnumerator GetCards(System.Action<string> onSuccess, System.Action<string> onError)
        {
            yield return Get("/api/cards", onSuccess, onError);
        }

        public IEnumerator CheckHealth(System.Action<string> onSuccess, System.Action<string> onError)
        {
            yield return Get("/health", onSuccess, onError);
        }

        private static IEnumerator Get(string path, System.Action<string> onSuccess, System.Action<string> onError)
        {
            string url = $"{AppConfig.ApiBaseUrl.TrimEnd('/')}{path}";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    onError?.Invoke(request.error);
                }
            }
        }
    }
}
