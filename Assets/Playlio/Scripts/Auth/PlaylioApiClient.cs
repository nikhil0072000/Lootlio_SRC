using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Playlio.Auth
{
    [Serializable] public class EmailSignUpRequest { public string email; public string password; public string name; }
    [Serializable] public class EmailSignInRequest { public string email; public string password; }

    /// <summary>Outcome of one HTTP call. Text is the response body (or the transport error).</summary>
    public struct ApiResult
    {
        public bool success;
        public long statusCode;
        public string text;
    }

    /// <summary>
    /// Talks to the real Better Auth server (email only). Routes come from auth_config.json
    /// "endpoints" (defaults: POST /api/auth/sign-up/email, POST /api/auth/sign-in/email,
    /// GET /api/auth/get-session, POST /api/auth/sign-out).
    /// Captures the session cookie from Set-Cookie and sends it back as Cookie.
    /// The cookie is never logged.
    /// </summary>
    public sealed class PlaylioApiClient : IAuthBackend
    {
        private const int TimeoutSeconds = 15;
        private const string SessionMarker = "session_token=";
        private static readonly char[] CookieEnd = { ';', ',', '\n' };

        private readonly string apiBaseUrl;
        private readonly string origin;
        private readonly PlaylioAuthConfig.Endpoints endpoints;
        private readonly ISessionStore sessionStore;

        public PlaylioApiClient(PlaylioAuthConfig config, ISessionStore store)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            sessionStore = store ?? throw new ArgumentNullException(nameof(store));
            apiBaseUrl = config.apiBaseUrl;
            origin = config.origin;
            endpoints = config.endpoints ?? new PlaylioAuthConfig.Endpoints();
        }

        public bool HasStoredSession => !string.IsNullOrEmpty(sessionStore.ReadCookie());

        public void ClearSession() => sessionStore.Clear();

        public IEnumerator SignUpEmail(string name, string email, string password, Action<ApiResult> done)
        {
            string body = JsonUtility.ToJson(new EmailSignUpRequest { name = name, email = email, password = password });
            yield return Send("POST", endpoints.signUp, body, false, done);
        }

        public IEnumerator SignInEmail(string email, string password, Action<ApiResult> done)
        {
            string body = JsonUtility.ToJson(new EmailSignInRequest { email = email, password = password });
            yield return Send("POST", endpoints.signIn, body, false, done);
        }

        public IEnumerator GetSession(Action<ApiResult> done)
        {
            yield return Send("GET", endpoints.session, null, true, done);
        }

        public IEnumerator SignOut(Action<ApiResult> done)
        {
            yield return Send("POST", endpoints.signOut, "{}", true, result =>
            {
                sessionStore.Clear();
                done?.Invoke(result);
            });
        }

        private IEnumerator Send(string method, string path, string json, bool authenticated, Action<ApiResult> done)
        {
            using (var request = new UnityWebRequest(apiBaseUrl + path, method))
            {
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = TimeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrEmpty(origin))
                    request.SetRequestHeader("Origin", origin);

                if (authenticated)
                {
                    string cookie = sessionStore.ReadCookie();
                    if (!string.IsNullOrEmpty(cookie))
                        request.SetRequestHeader("Cookie", cookie);
                }

                yield return request.SendWebRequest();

                CaptureSessionCookie(request);

                bool ok = request.result == UnityWebRequest.Result.Success;
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                done?.Invoke(new ApiResult
                {
                    success = ok,
                    statusCode = request.responseCode,
                    text = ok || !string.IsNullOrWhiteSpace(body) ? body : request.error,
                });
            }
        }

        /// <summary>
        /// Keeps only the Better Auth session cookie pair (name ending in "session_token"),
        /// whichever Set-Cookie header or position it comes in. An emptied cookie clears the store.
        /// </summary>
        private void CaptureSessionCookie(UnityWebRequest request)
        {
            string pair = ExtractSessionCookie(request.GetResponseHeader("Set-Cookie"));
            if (pair == null)
            {
                Dictionary<string, string> headers = request.GetResponseHeaders();
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        if (string.Equals(header.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
                        {
                            pair = ExtractSessionCookie(header.Value);
                            if (pair != null)
                                break;
                        }
                    }
                }
            }

            if (pair == null)
                return;

            if (pair.EndsWith("=", StringComparison.Ordinal))
                sessionStore.Clear();   // server expired the cookie (sign-out)
            else
                sessionStore.WriteCookie(pair);
        }

        private static string ExtractSessionCookie(string header)
        {
            if (string.IsNullOrEmpty(header))
                return null;

            int marker = header.IndexOf(SessionMarker, StringComparison.Ordinal);
            if (marker < 0)
                return null;

            int start = marker;
            while (start > 0)
            {
                char c = header[start - 1];
                if (c == ',' || c == ';' || c == ' ' || c == '\n')
                    break;
                start--;
            }

            int end = header.IndexOfAny(CookieEnd, marker);
            if (end < 0)
                end = header.Length;

            return header.Substring(start, end - start).Trim();
        }
    }
}
