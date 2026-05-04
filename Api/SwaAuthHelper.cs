using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Api
{
    public class PictureUserSettings
    {
        public string Filter { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public static class SwaAuthHelper
    {
        private static readonly HashSet<string> AllowedRoles =
            new(StringComparer.OrdinalIgnoreCase) { "owner", "pictureQuery" };

        private static Dictionary<string, PictureUserSettings>? _pictureSettings;

        /// <summary>
        /// Loads PictureSettings.json from the first candidate path that exists.
        /// Keys become the allowed-email list; values carry optional filter/date constraints.
        /// </summary>
        public static void LoadPictureSettings(params string[] candidatePaths)
        {
            var path = candidatePaths.FirstOrDefault(File.Exists);
            if (path == null)
            {
                ApiIsolated.Program.StartupLog($"[PictureSettings] No settings file found in candidates: {string.Join(", ", candidatePaths)}");
                return;
            }
            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var dict = new Dictionary<string, PictureUserSettings>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var s = new PictureUserSettings();
                    if (prop.Value.TryGetProperty("filter", out var fEl))
                        s.Filter = fEl.GetString() ?? "";
                    if (prop.Value.TryGetProperty("StartDate", out var sdEl) && sdEl.GetString() is string sd)
                        s.StartDate = DateTime.Parse(sd);
                    if (prop.Value.TryGetProperty("EndDate", out var edEl) && edEl.GetString() is string ed)
                        s.EndDate = DateTime.Parse(ed);
                    dict[prop.Name] = s;
                }
                _pictureSettings = dict;
                ApiIsolated.Program.StartupLog($"[PictureSettings] Loaded {dict.Count} entries from {path}");
            }
            catch (Exception ex)
            {
                ApiIsolated.Program.StartupLog($"[PictureSettings] Error loading from {path}: {ex.Message}");
            }
        }

        /// <summary>Returns per-user picture settings, or null if not found.</summary>
        public static PictureUserSettings? GetUserSettings(string email)
        {
            if (string.IsNullOrEmpty(email) || _pictureSettings == null) return null;
            _pictureSettings.TryGetValue(email, out var settings);
            return settings;
        }

        /// <summary>
        /// Returns the allowed-emails set. Combines ALLOWED_EMAILS app setting,
        /// PictureSettings.json keys, and a hardcoded owner address.
        /// </summary>
        private static HashSet<string> GetAllowedEmails()
        {
            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "calvin_hsia@live.com" // always allowed
            };

            // Keys from PictureSettings.json are authorized users
            if (_pictureSettings != null)
                foreach (var key in _pictureSettings.Keys)
                    emails.Add(key);

            var envVal = Environment.GetEnvironmentVariable("ALLOWED_EMAILS");
            if (!string.IsNullOrWhiteSpace(envVal))
            {
                foreach (var e in envVal.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    emails.Add(e);
            }
            else if (_pictureSettings == null)
            {
                // Fallback for local dev (F5) — no portal app setting or settings file
                emails.Add("calvin_hsia_test@outlook.com");
                emails.Add("pamelahsia@hotmail.com");
            }

            return emails;
        }

        /// <summary>
        /// Returns the authorized user's email if the request is authorized, or null if not.
        /// Returns empty string when authorized but email cannot be determined (e.g. SWA role match).
        /// </summary>
        public static string? GetAuthorizedEmail(HttpRequestData req, ILogger logger)
        {
#if DEBUG
            logger.LogInformation("[SwaAuth] DEBUG build — bypassing auth");
            return "";
#endif
            // Log ALL incoming headers so we can see what SWA passes through
            var allHeaders = string.Join(", ", req.Headers.Select(h => $"{h.Key}=[{string.Join("|", h.Value)}]"));
            logger.LogInformation("[SwaAuth] All headers: {headers}", allHeaders);
            // Try SWA-injected header first (only present when using /.auth/login flow)
            if (req.Headers.TryGetValues("x-ms-client-principal", out var swaValues))
            {
                var result = CheckSwaHeader(swaValues, logger);
                if (result != null)
                    return result == UnauthorizedSentinel ? null : result;
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
                var allowedEmails = GetAllowedEmails();
                if (allowedEmails.Contains(userEmail))
                {
                    logger.LogInformation("[SwaAuth] Authorized via &u= email: {email}", userEmail);
                    return userEmail;
                }
                logger.LogWarning("[SwaAuth] &u= email '{email}' not in allowlist", userEmail);
            }

            logger.LogWarning("[SwaAuth] No x-ms-client-principal or &u= param — unauthorized");
            return null;
        }

        /// <summary>Returns true if the request is authorized.</summary>
        public static bool IsAuthorized(HttpRequestData req, ILogger logger)
            => GetAuthorizedEmail(req, logger) != null;

        // Sentinel used internally: CheckSwaHeader returns this string to signal "header present but unauthorized"
        private const string UnauthorizedSentinel = "\x00unauthorized";

        private static string? CheckSwaHeader(System.Collections.Generic.IEnumerable<string> values, ILogger logger)
        {
            try
            {
                var encoded = System.Linq.Enumerable.FirstOrDefault(values);
                if (string.IsNullOrEmpty(encoded))
                {
                    logger.LogWarning("[SwaAuth] x-ms-client-principal was empty");
                    return UnauthorizedSentinel;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                logger.LogInformation("[SwaAuth] client-principal JSON: {json}", json);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var userDetails = root.TryGetProperty("userDetails", out var ud) ? ud.GetString() : null;

                if (root.TryGetProperty("userRoles", out var rolesEl))
                {
                    foreach (var role in rolesEl.EnumerateArray())
                    {
                        var roleName = role.GetString();
                        if (roleName != null && AllowedRoles.Contains(roleName))
                        {
                            logger.LogInformation("[SwaAuth] Authorized via SWA role: {role}", roleName);
                            return userDetails ?? "";
                        }
                    }
                }

                logger.LogInformation("[SwaAuth] SWA userDetails='{email}'", userDetails);
                if (userDetails != null && GetAllowedEmails().Contains(userDetails))
                {
                    logger.LogInformation("[SwaAuth] Authorized via SWA userDetails email: {email}", userDetails);
                    return userDetails;
                }

                // Header present but no match — don't fall through to query-param check
                logger.LogWarning("[SwaAuth] SWA header present but no matching role or email");
                return UnauthorizedSentinel;
            }
            catch (Exception ex)
            {
                logger.LogError("[SwaAuth] Exception parsing client-principal: {ex}", ex.Message);
                return UnauthorizedSentinel;
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
