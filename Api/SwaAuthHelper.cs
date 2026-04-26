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
    /// </summary>
    public static class SwaAuthHelper
    {
        private static readonly HashSet<string> AllowedRoles =
            new(StringComparer.OrdinalIgnoreCase) { "owner", "pictureQuery" };

        public static bool IsAuthorized(HttpRequestData req)
        {
#if DEBUG
            return true;
#else
            // Allow bypass for PR preview environments (set BYPASS_AUTH=true in SWA staging config)
            if (string.Equals(Environment.GetEnvironmentVariable("BYPASS_AUTH"), "true", StringComparison.OrdinalIgnoreCase))
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

                if (!doc.RootElement.TryGetProperty("userRoles", out var rolesEl))
                    return false;

                foreach (var role in rolesEl.EnumerateArray())
                {
                    var roleName = role.GetString();
                    if (roleName != null && AllowedRoles.Contains(roleName))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
