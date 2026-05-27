using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlazorWasm.Services
{
    /// <summary>
    /// Centralized helper for handling authentication token requests with automatic expiration handling.
    /// Use this service whenever you need to get access tokens for Microsoft Graph API or other authenticated endpoints.
    /// </summary>
    public class AuthTokenHelper
    {
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly NavigationManager _navigationManager;
        private readonly IJSRuntime _js;

        public AuthTokenHelper(IAccessTokenProvider tokenProvider, NavigationManager navigationManager, IJSRuntime js)
        {
            _tokenProvider = tokenProvider;
            _navigationManager = navigationManager;
            _js = js;
        }

        /// <summary>
        /// Gets the MSAL ID token — a proper signed JWT containing the `oid` claim.
        /// Used for API authorization because the Graph access token for MSA accounts is
        /// an opaque token that cannot be cryptographically validated server-side.
        /// Returns null if no ID token is found in the MSAL cache.
        /// </summary>
        public async Task<string?> GetIdTokenAsync()
        {
            try
            {
                var idToken = await _js.InvokeAsync<string?>("getMsalIdToken");
                if (string.IsNullOrEmpty(idToken))
                    Console.WriteLine("[AuthTokenHelper] GetIdTokenAsync: no ID token found");
                return idToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthTokenHelper] GetIdTokenAsync error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets an access token with automatic handling of expiration and refresh.
        /// Returns null if the token is not available (user will be redirected to login).
        /// </summary>
        /// <param name="showExpiredMessage">Whether to show a "Session expired" message before redirect</param>
        /// <param name="delayBeforeRedirect">Optional delay in milliseconds before redirecting (default 1000ms)</param>
        public async Task<string?> GetAccessTokenAsync(bool showExpiredMessage = true, int delayBeforeRedirect = 1000)
        {
            try
            {
                var tokenResult = await _tokenProvider.RequestAccessToken();

                if (!tokenResult.TryGetToken(out var token))
                {
                    // Token is not available - redirect to re-authenticate
                    Console.WriteLine("Token expired or not available - redirecting to login");

                    if (showExpiredMessage && delayBeforeRedirect > 0)
                    {
                        // Give time for caller to show message
                        await Task.Delay(delayBeforeRedirect);
                    }

                    _navigationManager.NavigateTo("authentication/login");
                    return null;
                }

                return token.Value;
            }
            catch (AccessTokenNotAvailableException ex)
            {
                // Token expired or not available - redirect to login using the exception's built-in method
                Console.WriteLine($"Access token not available: {ex.Message}");
                ex.Redirect();
                return null;
            }
        }

        /// <summary>
        /// Refreshes the HttpClient's authorization header with a fresh token if needed.
        /// Call this periodically during long-running operations to prevent token expiration.
        /// Returns true if token was refreshed successfully, false if authentication is required.
        /// </summary>
        /// <param name="httpClient">The HttpClient to update with a fresh token</param>
        /// <param name="lastRefreshTime">The last time the token was refreshed</param>
        /// <param name="refreshIntervalMinutes">How often to refresh (default 50 minutes, before 60-minute expiration)</param>
        public async Task<(bool success, DateTime refreshTime)> RefreshHttpClientTokenIfNeededAsync(
            HttpClient httpClient,
            DateTime lastRefreshTime,
            int refreshIntervalMinutes = 50)
        {
            var timeSinceLastRefresh = DateTime.Now - lastRefreshTime;

            // Only refresh if enough time has passed
            if (timeSinceLastRefresh < TimeSpan.FromMinutes(refreshIntervalMinutes))
            {
                return (true, lastRefreshTime); // No refresh needed yet
            }

            Console.WriteLine($"[AuthTokenHelper] Token refresh needed (last refresh: {timeSinceLastRefresh.TotalMinutes:F1} minutes ago)");

            var token = await GetAccessTokenAsync(showExpiredMessage: false, delayBeforeRedirect: 0);

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[AuthTokenHelper] Failed to refresh token - authentication required");
                return (false, lastRefreshTime);
            }

            // Update the HttpClient's authorization header
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var now = DateTime.Now;
            Console.WriteLine($"[AuthTokenHelper] Token refreshed successfully at {now:HH:mm:ss}");

            return (true, now);
        }

        /// <summary>
        /// Creates an HttpClient configured with the Bearer authentication header.
        /// Returns null if token is not available (user will be redirected).
        /// </summary>
        public async Task<HttpClient?> CreateAuthenticatedHttpClientAsync(bool showExpiredMessage = true)
        {
            var token = await GetAccessTokenAsync(showExpiredMessage);

            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return httpClient;
        }

        /// <summary>
        /// Wraps an async operation that requires authentication.
        /// Automatically handles token expiration and redirects to login if needed.
        /// Returns true if the operation was executed, false if token was not available.
        /// </summary>
        public async Task<bool> ExecuteWithAuthAsync(
            Func<string, Task> operation,
            string sessionExpiredMessage = "Session expired. Redirecting to sign in...",
            Action<string>? statusUpdater = null)
        {
            try
            {
                var token = await GetAccessTokenAsync(showExpiredMessage: false);

                if (string.IsNullOrEmpty(token))
                {
                    statusUpdater?.Invoke(sessionExpiredMessage);
                    await Task.Delay(1000);
                    _navigationManager.NavigateTo("authentication/login");
                    return false;
                }

                await operation(token);
                return true;
            }
            catch (AccessTokenNotAvailableException ex)
            {
                Console.WriteLine($"Access token not available: {ex.Message}");
                statusUpdater?.Invoke(sessionExpiredMessage);
                ex.Redirect();
                return false;
            }
        }

        /// <summary>
        /// Wraps an async operation that requires authentication and returns a result.
        /// Automatically handles token expiration and redirects to login if needed.
        /// Returns (success: bool, result: T?) tuple.
        /// </summary>
        public async Task<(bool success, T? result)> ExecuteWithAuthAsync<T>(
            Func<string, Task<T>> operation,
            string sessionExpiredMessage = "Session expired. Redirecting to sign in...",
            Action<string>? statusUpdater = null)
        {
            try
            {
                var token = await GetAccessTokenAsync(showExpiredMessage: false);

                if (string.IsNullOrEmpty(token))
                {
                    statusUpdater?.Invoke(sessionExpiredMessage);
                    await Task.Delay(1000);
                    _navigationManager.NavigateTo("authentication/login");
                    return (false, default);
                }

                var result = await operation(token);
                return (true, result);
            }
            catch (AccessTokenNotAvailableException ex)
            {
                Console.WriteLine($"Access token not available: {ex.Message}");
                statusUpdater?.Invoke(sessionExpiredMessage);
                ex.Redirect();
                return (false, default);
            }
        }
    }
}
