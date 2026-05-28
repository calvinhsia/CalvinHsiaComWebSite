using BlazorWasm.Services;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Client.Services
{
    /// <summary>
    /// Wraps HttpClient for calls to /api/* endpoints, automatically attaching the
    /// MSAL ID token in the X-Token header so every API call is authenticated without
    /// each page needing to fetch and attach the token itself.
    ///
    /// Token refresh: MSAL refreshes the ID token when the access token is refreshed.
    /// GetIdTokenAsync reads from the MSAL localStorage cache. If the cached ID token
    /// is expired or absent, we trigger an access token refresh first (which causes MSAL
    /// to also write a fresh ID token to the cache), then retry.
    /// </summary>
    public class ApiHttpService
    {
        private readonly HttpClient _http;
        private readonly AuthTokenHelper _authToken;

        public ApiHttpService(HttpClient http, AuthTokenHelper authToken)
        {
            _http = http;
            _authToken = authToken;
        }

        /// <summary>
        /// Sends a GET to the given URL with the X-Token header attached.
        /// Returns the HttpResponseMessage — caller reads content and checks status.
        /// </summary>
        public async Task<HttpResponseMessage> GetAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await AttachTokenAsync(request);
            return await _http.SendAsync(request);
        }

        /// <summary>
        /// Attaches a fresh, non-expired MSAL ID token to the request as X-Token.
        /// If the cached ID token is missing or expired, triggers an MSAL access-token
        /// refresh first (which also refreshes the ID token in localStorage), then retries.
        /// </summary>
        public async Task AttachTokenAsync(HttpRequestMessage request)
        {
            var token = await GetFreshIdTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.TryAddWithoutValidation("X-Token", token);
        }

        /// <summary>
        /// Returns a non-expired ID token, refreshing via MSAL if necessary.
        /// </summary>
        private async Task<string?> GetFreshIdTokenAsync()
        {
            var idToken = await _authToken.GetIdTokenAsync();

            if (!string.IsNullOrEmpty(idToken) && !IsTokenExpiredOrExpiringSoon(idToken))
                return idToken;

            // Token is missing or about to expire — trigger an access token refresh.
            // MSAL writes a fresh ID token to localStorage as a side-effect.
            Console.WriteLine("[ApiHttpService] ID token missing or expiring — refreshing via MSAL access token request");
            var accessToken = await _authToken.GetAccessTokenAsync(showExpiredMessage: false, delayBeforeRedirect: 0);
            if (string.IsNullOrEmpty(accessToken))
            {
                // GetAccessTokenAsync already redirected to login
                return null;
            }

            // Re-read the ID token from the now-refreshed cache
            idToken = await _authToken.GetIdTokenAsync();
            if (string.IsNullOrEmpty(idToken))
                Console.WriteLine("[ApiHttpService] Still no ID token after access token refresh — API call will fail");

            return idToken;
        }

        /// <summary>
        /// Returns true if the JWT is expired or expires within the next 5 minutes.
        /// Reads the exp claim from the payload without verifying the signature (display/timing use only).
        /// </summary>
        private static bool IsTokenExpiredOrExpiringSoon(string jwt, int bufferMinutes = 5)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return true;

                var pad = parts[1].Replace('-', '+').Replace('_', '/');
                pad = pad.PadRight(pad.Length + (4 - pad.Length % 4) % 4, '=');
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(pad));
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("exp", out var expProp))
                {
                    var exp = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64());
                    return exp < DateTimeOffset.UtcNow.AddMinutes(bufferMinutes);
                }
            }
            catch { /* malformed token — treat as expired */ }
            return true;
        }
    }
}
