using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Api
{
    /// <summary>
    /// Reads the x-ms-client-principal header injected by Azure Static Web Apps
    /// and checks whether the caller has one of the required roles.
    /// In production, requires "owner" or "pictureQuery" role.
    /// In PR preview environments, the edge config (staticwebapp.config.json) is
    /// patched by CI to remove allowedRoles, so any authenticated user is allowed through.
    /// The function therefore only checks that the user is authenticated.
    /// </summary>
    public static class SwaAuthHelper
    {
        private static readonly HashSet<string> AllowedRoles =
            new(StringComparer.OrdinalIgnoreCase) { "owner", "pictureQuery" };

        // Fallback for PR preview environments where portal role invites aren't available.
        // Production is protected at the edge by staticwebapp.config.json allowedRoles.
        private static readonly HashSet<string> AllowedEmails =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "calvin_hsia@live.com",
                "calvin_hsia_test@outlook.com"
            };

        public static bool IsAuthorized(HttpRequestData req)
        {
#if DEBUG
            return true;
#endif
            if (!req.Headers.TryGetValues("x-ms-client-principal", out var values))
                return false;

            try
            {
                var encoded = System.Linq.Enumerable.FirstOrDefault(values);
                if (string.IsNullOrEmpty(encoded)) return false;

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check SWA roles first (production path — role-invited users)
                if (root.TryGetProperty("userRoles", out var rolesEl))
                {
                    foreach (var role in rolesEl.EnumerateArray())
                    {
                        var roleName = role.GetString();
                        if (roleName != null && AllowedRoles.Contains(roleName))
                            return true;
                    }
                }

                // Fallback: check email allowlist (PR preview path)
                var userDetails = root.TryGetProperty("userDetails", out var ud) ? ud.GetString() : null;
                if (userDetails != null && AllowedEmails.Contains(userDetails))
                    return true;
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
