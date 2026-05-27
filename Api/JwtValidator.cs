using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;

namespace Api
{
    /// <summary>
    /// Validates AAD-issued JWT tokens by fetching and caching AAD's public signing keys (JWKS).
    /// Extracts the `oid` claim from a cryptographically verified token.
    ///
    /// Why X-Token header instead of Authorization:
    ///   Azure Static Web Apps (SWA) replaces the Authorization header with its own
    ///   Kudu token before forwarding requests to Azure Functions. Custom headers
    ///   like X-Token are not touched by SWA, so the original MSAL token arrives intact.
    ///
    /// Why we skip strict audience validation:
    ///   The client holds a Graph-scoped token (aud=https://graph.microsoft.com).
    ///   Registering a custom API scope would require an additional AAD app registration
    ///   and changes to the MSAL scopes. Since we control both client and server we
    ///   accept the Graph token and rely on signature + issuer + expiry for security.
    /// </summary>
    public static class JwtValidator
    {
        // AAD personal accounts (consumers) OpenID configuration endpoint
        private const string OpenIdMetadataUri =
            "https://login.microsoftonline.com/consumers/v2.0/.well-known/openid-configuration";

        // Cache the configuration manager — it handles JWKS key refresh automatically
        private static readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager =
            new(OpenIdMetadataUri,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });

        // Header name the client sends the token in
        public const string TokenHeaderName = "X-Token";

        /// <summary>
        /// Validates the JWT signature, issuer, and expiry using AAD's public signing keys.
        /// Returns the `oid` claim value on success, or null if validation fails.
        /// </summary>
        public static async Task<string?> ValidateAndGetOidAsync(string jwt, ILogger logger)
        {
            // ── Pre-validation diagnostics ──────────────────────────────────────────
            // Decode the payload WITHOUT verifying the signature so we can log exactly
            // what the token contains regardless of whether validation succeeds.
            // This is safe — we never trust these claims; we only log them.
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length == 3)
                {
                    // Read the header to see the algorithm
                    var hdrPad  = parts[0].Replace('-', '+').Replace('_', '/');
                    hdrPad = hdrPad.PadRight(hdrPad.Length + (4 - hdrPad.Length % 4) % 4, '=');
                    var hdrJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(hdrPad));

                    // Read the payload for issuer / audience / oid / expiry
                    var payPad  = parts[1].Replace('-', '+').Replace('_', '/');
                    payPad = payPad.PadRight(payPad.Length + (4 - payPad.Length % 4) % 4, '=');
                    var payJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payPad));

                    using var hdrDoc = System.Text.Json.JsonDocument.Parse(hdrJson);
                    using var payDoc = System.Text.Json.JsonDocument.Parse(payJson);
                    var p = payDoc.RootElement;

                    var iss = p.TryGetProperty("iss", out var issProp) ? issProp.GetString() : "(missing)";
                    var aud = p.TryGetProperty("aud", out var audProp) ? audProp.GetString() : "(missing)";
                    var oidRaw = p.TryGetProperty("oid", out var oidProp) ? oidProp.GetString() : "(missing)";
                    var alg = hdrDoc.RootElement.TryGetProperty("alg", out var algProp) ? algProp.GetString() : "?";
                    var expRaw = p.TryGetProperty("exp", out var expProp) ? expProp.GetInt64() : 0;
                    var expTime = DateTimeOffset.FromUnixTimeSeconds(expRaw).UtcDateTime;

                    logger.LogWarning("[JwtValidator] Token header: alg={alg}", alg);
                    logger.LogWarning("[JwtValidator] Token payload: iss={iss} | aud={aud} | oid={oid} | exp={exp} (UTC)",
                        iss, aud, oidRaw, expTime);
                }
                else
                {
                    // Not a 3-part JWT — likely an opaque/compact token (common for Graph MSA tokens)
                    logger.LogWarning("[JwtValidator] Token is NOT a JWT (parts={parts}) — first 40 chars: {prefix}",
                        parts.Length, jwt.Length > 40 ? jwt[..40] : jwt);
                }
            }
            catch (Exception diagEx)
            {
                logger.LogWarning("[JwtValidator] Pre-validation decode failed: {msg}", diagEx.Message);
            }
            // ── End diagnostics ─────────────────────────────────────────────────────

            try
            {
                // Fetch (or return cached) AAD signing keys
                var config = await _configManager.GetConfigurationAsync(CancellationToken.None);

                var validationParams = new TokenValidationParameters
                {
                    // Cryptographic signature must match an AAD public key
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = config.SigningKeys,

                    // Token must come from AAD personal accounts issuer
                    ValidateIssuer = true,
                    ValidIssuers = new[]
                    {
                        "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0",
                        "https://login.live.com"
                    },

                    // Skip audience check — we accept the Graph-scoped token (see class comment)
                    ValidateAudience = false,

                    // Token must not be expired
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(jwt, validationParams, out var validatedToken);

                // Extract oid claim — always present in AAD tokens, stable across token types
                var oid = principal.FindFirst("oid")?.Value
                       ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

                if (string.IsNullOrEmpty(oid))
                {
                    logger.LogWarning("[JwtValidator] Token valid but no oid claim found");
                    return null;
                }

                logger.LogInformation("[JwtValidator] Token valid, oid={oid}, expires={exp}",
                    oid, validatedToken.ValidTo);
                return oid;
            }
            catch (SecurityTokenExpiredException ex)
            {
                logger.LogWarning("[JwtValidator] Token expired: {msg}", ex.Message);
                return null;
            }
            catch (SecurityTokenException ex)
            {
                logger.LogWarning("[JwtValidator] Token invalid: {msg}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError("[JwtValidator] Unexpected error: {msg}", ex.Message);
                return null;
            }
        }
    }
}
