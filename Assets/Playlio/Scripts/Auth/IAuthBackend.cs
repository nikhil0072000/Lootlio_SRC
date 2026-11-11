using System;
using System.Collections;

namespace Playlio.Auth
{
    /// <summary>
    /// The account server the game talks to. Two implementations:
    /// - PlaylioApiClient: the real Better Auth server ("mode": "live").
    /// - MockAuthBackend: an offline fake for testing ("mode": "mock").
    /// Both answer with the same ApiResult shapes, so PlaylioAuthService works with either.
    /// </summary>
    public interface IAuthBackend
    {
        bool HasStoredSession { get; }
        void ClearSession();
        IEnumerator SignUpEmail(string name, string email, string password, Action<ApiResult> done);
        IEnumerator SignInEmail(string email, string password, Action<ApiResult> done);
        IEnumerator GetSession(Action<ApiResult> done);
        IEnumerator SignOut(Action<ApiResult> done);
    }
}
