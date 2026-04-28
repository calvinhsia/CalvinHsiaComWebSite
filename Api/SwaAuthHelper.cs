using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Api
{
    public static class SwaAuthHelper
    {
        private static readonly HashSet<string> AllowedRoles =
            new(StringComparer.OrdinalIgnoreCase) { "owner", "pictureQuery" };

        private static readonly HashSet<string> AllowedEmails =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "calvin_hsia@live.com",
                "calvin_hsia_test@outlook.com",
                "pamelahsia@hotmail.com"
            };

        public static bool IsAuthorized(HttpRequestData req, ILogger logger)
        {
#if DEBUG
            logger.LogInformation("[SwaAuth] DEBUG build — bypassing auth");
            return true;
#endif
            // Log ALL incoming headers so we can see what SWA passes through
            var allHeaders = string.Join(", ", req.Headers.Select(h => $"{h.Key}=[{string.Join("|", h.Value)}]"));
            logger.LogInformation("[SwaAuth] All headers: {headers}", allHeaders);
            // Try SWA-injected header first (only present when using /.auth/login flow)
            if (req.Headers.TryGetValues("x-ms-client-principal", out var swaValues))
            {
                var result = CheckSwaHeader(swaValues, logger);
                if (result.HasValue) return result.Value;
            }
            else
            {
                logger.LogInformation("[SwaAuth] No x-ms-client-principal — checking Bearer JWT (MSAL flow)");
            }

            // SWA replaces the Authorization header — identity is passed as &u= query param.
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var userEmail = query["u"];
            if (!string.IsNullOrEmpty(userEmail))
            {
                logger.LogInformation("[SwaAuth] &u= param: '{email}'", userEmail);
                if (AllowedEmails.Contains(userEmail))
                {
                    logger.LogInformation("[SwaAuth] Authorized via &u= email: {email}", userEmail);
                    return true;
                }
                logger.LogWarning("[SwaAuth] &u= email '{email}' not in allowlist", userEmail);
            }

            logger.LogWarning("[SwaAuth] No x-ms-client-principal or &u= param — unauthorized");
            return false;
        }

        private static bool? CheckSwaHeader(System.Collections.Generic.IEnumerable<string> values, ILogger logger)
        {
            try
            {
                var encoded = System.Linq.Enumerable.FirstOrDefault(values);
                if (string.IsNullOrEmpty(encoded))
                {
                    logger.LogWarning("[SwaAuth] x-ms-client-principal was empty");
                    return false;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                logger.LogInformation("[SwaAuth] client-principal JSON: {json}", json);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("userRoles", out var rolesEl))
                {
                    foreach (var role in rolesEl.EnumerateArray())
                    {
                        var roleName = role.GetString();
                        if (roleName != null && AllowedRoles.Contains(roleName))
                        {
                            logger.LogInformation("[SwaAuth] Authorized via SWA role: {role}", roleName);
                            return true;
                        }
                    }
                }

                var userDetails = root.TryGetProperty("userDetails", out var ud) ? ud.GetString() : null;
                logger.LogInformation("[SwaAuth] SWA userDetails='{email}'", userDetails);
                if (userDetails != null && AllowedEmails.Contains(userDetails))
                {
                    logger.LogInformation("[SwaAuth] Authorized via SWA userDetails email: {email}", userDetails);
                    return true;
                }

                // Header present but no match — don't fall through to Bearer check
                logger.LogWarning("[SwaAuth] SWA header present but no matching role or email");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError("[SwaAuth] Exception parsing client-principal: {ex}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Decodes the payload of a JWT (without signature validation — trust is established
        /// by the fact that the request reached the Azure Function with a valid AAD-issued token).
        /// Checks 'preferred_username', 'email', and 'upn' claims.
        /// </summary>
        private static string? GetEmailFromJwt(string jwt, ILogger logger)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return null;

                var payload = parts[1];
                // Base64Url → Base64
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                logger.LogInformation("[SwaAuth] JWT payload: {json}", json);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                foreach (var claim in new[] { "preferred_username", "email", "upn" })
                {
                    if (root.TryGetProperty(claim, out var el))
                    {
                        var val = el.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            logger.LogInformation("[SwaAuth] JWT claim '{claim}'='{val}'", claim, val);
                            return val;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("[SwaAuth] Exception decoding JWT: {ex}", ex.Message);
            }
            return null;
        }
    }
}
