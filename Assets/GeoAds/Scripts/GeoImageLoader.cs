using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoAds
{
    /// <summary>
    /// Throttled image downloader for the ad cards.
    /// - At most MaxConcurrent downloads run at once; the rest wait in a queue.
    /// - Timeouts, network errors (HTTP 0), 429 and 5xx are retried; 4xx (e.g. a dead 404 link) is not.
    /// - Every failed attempt is logged with its HTTP status and URL.
    /// Runs on its own hidden DontDestroyOnLoad host, so a card that is hidden or destroyed
    /// mid-download can never leak a concurrency slot.
    /// </summary>
    public static class GeoImageLoader
    {
        private const int MaxConcurrent = 4;
        private const int TimeoutSeconds = 15;
        private const int MaxRetries = 2;

        private sealed class Job
        {
            public string url;
            public Action<Texture2D> done;
        }

        private static readonly Queue<Job> queue = new Queue<Job>();
        private static int active;
        private static int generation; // bumped when the host is recreated; stale downloads are not counted
        private static Host host;

        /// <summary>
        /// Queues a download. done(texture) on success, done(null) on failure.
        /// The callback may arrive after the caller was destroyed: check "this == null" in it
        /// and Destroy the texture if it is no longer wanted. The caller owns the texture.
        /// </summary>
        public static void Load(string url, Action<Texture2D> done)
        {
            if (string.IsNullOrEmpty(url))
            {
                done?.Invoke(null);
                return;
            }

            EnsureHost();
            queue.Enqueue(new Job { url = url, done = done });
            Pump();
        }

        // Enter Play Mode without domain reload keeps statics; start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            queue.Clear();
            active = 0;
            generation++;
            host = null;
        }

        private static void EnsureHost()
        {
            if (host != null)
                return;

            // host gone (scene teardown / reload): its coroutines died with it
            queue.Clear();
            active = 0;
            generation++;

            var go = new GameObject("GeoImageLoader");
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.AddComponent<Host>();
        }

        private static void Pump()
        {
            while (active < MaxConcurrent && queue.Count > 0)
            {
                active++;
                host.StartCoroutine(Download(queue.Dequeue(), generation));
            }
        }

        private static IEnumerator Download(Job job, int jobGeneration)
        {
            Texture2D result = null;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                bool retry;
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(job.url, true)) // non-readable: half the memory
                {
                    request.timeout = TimeoutSeconds;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        result = DownloadHandlerTexture.GetContent(request);
                        break;
                    }

                    long code = request.responseCode;
                    retry = code == 0 || code == 429 || code >= 500; // timeout / network / throttled / server
                    Debug.LogWarning("[GeoAds] image attempt " + (attempt + 1) + " failed: HTTP " + code
                                     + " " + request.error + "  " + job.url);
                }

                if (!retry || attempt == MaxRetries)
                    break;
                yield return new WaitForSecondsRealtime(attempt + 1f); // 1 s, then 2 s
            }

            if (result == null)
                Debug.LogWarning("[GeoAds] image FAILED, card shows its fallback: " + job.url);

            if (jobGeneration == generation)
                active--;

            try
            {
                job.done?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e); // one bad callback must not stall the queue
            }

            if (jobGeneration == generation)
                Pump();
        }

        private sealed class Host : MonoBehaviour { }
    }
}
