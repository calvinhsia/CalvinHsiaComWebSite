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
            if (!req.Headers.TryGetValues("x-ms-client-principal", out var values))
            {
                logger.LogWarning("[SwaAuth] No x-ms-client-principal header found — unauthorized");
                return false;
            }

            try
            {
                var encoded = System.Linq.Enumerable.FirstOrDefault(values);
                if (string.IsNullOrEmpty(encoded))
                {
                    logger.LogWarning("[SwaAuth] x-ms-client-principal header was empty — unauthorized");
                    return false;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                logger.LogInformation("[SwaAuth] client-principal JSON: {json}", json);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check SWA roles first (production path — role-invited users)
                if (root.TryGetProperty("userRoles", out var rolesEl))
                {
                    foreach (var role in rolesEl.EnumerateArray())
                    {
                        var roleName = role.GetString();
                        logger.LogInformation("[SwaAuth] Checking role: '{role}'", roleName);
                        if (roleName != null && AllowedRoles.Contains(roleName))
                        {
                            logger.LogInformation("[SwaAuth] Authorized via role: {role}", roleName);
                            return true;
                        }
                    }
                }

                // Fallback: check email allowlist (PR preview path)
                var userDetails = root.TryGetProperty("userDetails", out var ud) ? ud.GetString() : null;
                logger.LogInformation("[SwaAuth] userDetails (email): '{email}'", userDetails);
                if (userDetails != null && AllowedEmails.Contains(userDetails))
                {
                    logger.LogInformation("[SwaAuth] Authorized via email allowlist: {email}", userDetails);
                    return true;
                }

                logger.LogWarning("[SwaAuth] Not authorized — no matching role or email");
            }
            catch (Exception ex)
            {
                logger.LogError("[SwaAuth] Exception parsing client-principal: {ex}", ex.Message);
            }

            return false;
        }
    }
}
