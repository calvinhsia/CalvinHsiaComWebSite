using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Api
{
    public class PictureUserSettings
    {
        public string Filter { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public override string ToString()
        {
            return $"{Filter} {StartDate?.ToString("yyyy-MM-dd") ?? ""} {EndDate?.ToString("yyyy-MM-dd") ?? ""}";
        }
    }

    public static class SwaAuthHelper
    {
        private static Dictionary<string, PictureUserSettings>? _pictureSettings;
        private static string? _settingsLoadError;

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
                    // Skip non-object entries (e.g. _README string)
                    if (prop.Value.ValueKind != JsonValueKind.Object)
                        continue;
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
                _settingsLoadError = $"Parse error from {path}: {ex.GetType().Name}: {ex.Message}";
                ApiIsolated.Program.StartupLog($"[PictureSettings] {_settingsLoadError}");
                Console.WriteLine($"[PictureSettings] {_settingsLoadError}");
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
        /// Returns the set of authorised OIDs sourced from PictureSettings.json keys.
        /// Keys that contain '@' are legacy email entries and are skipped.
        /// </summary>
        private static HashSet<string> GetAllowedOids()
        {
            var oids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_pictureSettings != null)
            {
                foreach (var key in _pictureSettings.Keys)
                {
                    // OID keys do not contain '@'; skip any legacy email-keyed entries
                    if (!key.Contains('@'))
                        oids.Add(key);
                }
            }
            else
            {
                var reason = _settingsLoadError ?? $"file not found (tried:{AppContext.BaseDirectory}PictureSettings.json)";
                oids.Add($"SETTINGS-NOT-LOADED({reason})");
            }

            return oids;
        }

        /// <summary>
        /// Validates the bearer token sent in the X-Token header and returns the
        /// authorized user's OID on success, or null if the request is unauthorized.
        ///
        /// In DEBUG builds auth is bypassed so F5 local development works without
        /// an actual MSAL token.
        ///
        /// Security model:
        ///   1. Read the raw JWT from the X-Token request header.
        ///   2. Cryptographically verify the signature using AAD's public JWKS keys.
        ///   3. Confirm issuer and lifetime (expiry + 2 min clock skew).
        ///   4. Extract the `oid` claim — stable across token types and not spoofable.
        ///   5. Check that oid is a key in PictureSettings.json (the authorised-user store).
        ///
        /// Returning the OID (rather than void/bool) lets callers look up per-user
        /// settings without a second identity lookup.
        /// </summary>
        public static async Task<string?> GetAuthorizedOidAsync(HttpRequestData req, ILogger logger)
        {
            if (Environment.GetEnvironmentVariable("BYPASS_JWT_AUTH") == "1")
            {
                logger.LogInformation("[SwaAuth] BYPASS_JWT_AUTH=1 — skipping token validation (local dev only)");
                return "";
            }

            var headerKeys = string.Join(", ", req.Headers.Select(h => h.Key));
            logger.LogInformation("[SwaAuth] Incoming headers: {headers}", headerKeys);

            if (!req.Headers.TryGetValues(JwtValidator.TokenHeaderName, out var tokenValues))
            {
                var msg = $"No {JwtValidator.TokenHeaderName} header present";
                logger.LogWarning("[SwaAuth] {msg}", msg);
                return "REJECT:" + msg;  // special prefix — caller turns this into 401 body
            }

            var jwt = tokenValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(jwt))
            {
                var msg = $"{JwtValidator.TokenHeaderName} header was empty";
                logger.LogWarning("[SwaAuth] {msg}", msg);
                return "REJECT:" + msg;
            }

            var oid = await JwtValidator.ValidateAndGetOidAsync(jwt, logger);
            if (oid == null)
            {
                var msg = "JWT validation failed (bad signature, wrong issuer, or expired)";
                logger.LogWarning("[SwaAuth] {msg}", msg);
                return "REJECT:" + msg;
            }

            if (oid.Length == 0)
                return oid;  // DEBUG bypass

            var allowed = GetAllowedOids();
            logger.LogInformation("[SwaAuth] Allowed OIDs: [{list}]", string.Join(", ", allowed));
            if (!allowed.Contains(oid))
            {
                var msg = $"OID '{oid}' not in allowed list [{string.Join(", ", allowed)}]";
                logger.LogWarning("[SwaAuth] {msg}", msg);
                return "REJECT:" + msg;
            }

            logger.LogInformation("[SwaAuth] Authorised oid: {oid}", oid);
            return oid;
        }

        /// <summary>
        /// Returns null if authorized (or empty string for debug bypass).
        /// Returns a "REJECT:reason" string if the request should be denied.
        /// Callers must check IsRejected() and write the reason to the 401 body.
        /// </summary>
        public static bool IsRejected(string? oid) =>
            oid == null || (oid.StartsWith("REJECT:"));

        public static string RejectionReason(string? oid) =>
            oid?.StartsWith("REJECT:") == true ? oid[7..] : "Unauthorized";
    }
}
