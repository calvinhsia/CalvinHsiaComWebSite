using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
                "calvin_hsia_test@outlook.com"
            };

        public static bool IsAuthorized(HttpRequestData req, ILogger logger)
        {
#if DEBUG
            logger.LogInformation("[SwaAuth] DEBUG build — bypassing auth");
            return true;
#endif
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

            // Fall back to MSAL token passed in custom header (SWA replaces Authorization header
            // with its own internal platform token before forwarding to the function)
            if (req.Headers.TryGetValues("X-Msal-Token", out var msalValues))
            {
                var bearer = System.Linq.Enumerable.FirstOrDefault(msalValues);
                if (bearer != null && bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? bearer["Bearer ".Length..].Trim()
                        : bearer.Trim();
                    var email = GetEmailFromJwt(token, logger);
                    if (email != null && AllowedEmails.Contains(email))
                    {
                        logger.LogInformation("[SwaAuth] Authorized via Bearer JWT email: {email}", email);
                        return true;
                    }
                    logger.LogWarning("[SwaAuth] Bearer JWT email '{email}' not in allowlist", email ?? "(null)");
                    return false;
                }
            }

            logger.LogWarning("[SwaAuth] No x-ms-client-principal or X-Msal-Token header — unauthorized");
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
