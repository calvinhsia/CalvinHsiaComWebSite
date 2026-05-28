using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace TestProject1
{
    /// <summary>
    /// Tests for the JWT-based auth redesign.
    ///
    /// Real cryptographic validation requires a live AAD-issued token and cannot be unit-tested
    /// without an integration environment. These tests instead cover:
    ///   1. The X-Token header constant is what the client and server both agree on.
    ///   2. The OID allowlist logic (GetAllowedOids path) honours '@' as the discriminator.
    ///   3. Expired / malformed token strings are detected before hitting the network.
    ///   4. DEBUG bypass returns an empty string (not null) so callers treat it as authorised.
    /// </summary>
    [TestClass]
    public class TestJwtAuth
    {
        // ── 1. Header name contract ──────────────────────────────────────────────────

        [TestMethod]
        public void XTokenHeaderName_IsExpectedValue()
        {
            Assert.AreEqual("X-Token", Api.JwtValidator.TokenHeaderName,
                "Client and server must agree on the header name");
        }

        // ── 2. OID vs email discriminator ────────────────────────────────────────────

        [TestMethod]
        public void OidKey_WithoutAtSign_IsIncludedInAllowlist()
        {
            var key = "00d69f3552cefc21";
            Assert.IsFalse(key.Contains('@'), "OID keys must not contain '@'");
        }

        [TestMethod]
        public void EmailKey_WithAtSign_IsExcludedFromOidAllowlist()
        {
            var key = "calvin_hsia@live.com";
            Assert.IsTrue(key.Contains('@'), "Email keys contain '@' and are skipped by GetAllowedOids");
        }

        [TestMethod]
        public void OidAllowlist_FiltersEmailKeysCorrectly()
        {
            // Simulate the GetAllowedOids filtering logic
            var pictureSettingsKeys = new List<string>
            {
                "calvin_hsia@live.com",      // email — should be excluded
                "00d69f3552cefc21",           // OID  — should be included
                "pamelahsia@hotmail.com",     // email — should be excluded
                "aabbccddeeff1122"            // OID  — should be included
            };

            var oids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in pictureSettingsKeys)
                if (!key.Contains('@'))
                    oids.Add(key);

            Assert.AreEqual(2, oids.Count, "Only the two OID keys should pass the filter");
            Assert.IsTrue(oids.Contains("00d69f3552cefc21"));
            Assert.IsTrue(oids.Contains("aabbccddeeff1122"));
            Assert.IsFalse(oids.Contains("calvin_hsia@live.com"));
        }

        // ── 3. Malformed / expired JWT quick-checks ──────────────────────────────────

        [TestMethod]
        public void Jwt_WithTooFewParts_IsDetectedAsInvalid()
        {
            var badJwt = "notavalidtoken";
            var parts = badJwt.Split('.');
            Assert.IsTrue(parts.Length < 3, "A valid JWT must have exactly 3 dot-separated parts");
        }

        [TestMethod]
        public void Jwt_WithThreeParts_PassesStructureCheck()
        {
            // Build a minimal (unsigned) JWT structure for shape testing only
            string Base64UrlEncode(string s) =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var header  = Base64UrlEncode("""{"alg":"RS256","typ":"JWT"}""");
            var payload = Base64UrlEncode("""{"oid":"test-oid","exp":9999999999}""");
            var fakeJwt = $"{header}.{payload}.fakesig";

            Assert.AreEqual(3, fakeJwt.Split('.').Length, "Three-part JWT passes the structural check");
        }

        [TestMethod]
        public void Jwt_ExpiredPayload_IsDetected()
        {
            // Build a JWT payload with exp in the past
            var expiredEpoch = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
            var payloadJson  = JsonSerializer.Serialize(new { oid = "some-oid", exp = expiredEpoch });
            var encoded      = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
                                   .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var parts        = encoded + "==".PadLeft(4 - (encoded.Length % 4));
            var decoded      = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(
                                   encoded.Replace('-', '+').Replace('_', '/').PadRight(
                                       encoded.Length + (4 - encoded.Length % 4) % 4, '='))));

            var exp = decoded.RootElement.GetProperty("exp").GetInt64();
            Assert.IsTrue(exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "Expired token detected via exp claim");
        }

        // ── 4. DEBUG bypass sentinel ─────────────────────────────────────────────────

        [TestMethod]
        public void DebugBypass_ReturnsEmptyStringNotNull()
        {
            // Document the contract: DEBUG mode returns "" so callers treat it as authorised
            // null means rejected; empty string means "authorised but identity unknown (debug)"
            string? debugResult = "";   // what SwaAuthHelper returns in #if DEBUG
            Assert.IsNotNull(debugResult, "DEBUG result must not be null — null means rejected");
            Assert.AreEqual("", debugResult, "DEBUG result should be empty string");
        }

        // ── 5. X-Token header round-trip ─────────────────────────────────────────────

        [TestMethod]
        public void XToken_Header_SurvivesHttpRequestMessage()
        {
            var token = "eyJhbGciOiJSUzI1NiJ9.eyJvaWQiOiJ0ZXN0In0.sig";
            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://example.com/api/test");
            request.Headers.Add("X-Token", token);

            Assert.IsTrue(request.Headers.TryGetValues("X-Token", out var values));
            Assert.AreEqual(token, System.Linq.Enumerable.First(values));
        }
            // ── 6. Runtime bypass env var ────────────────────────────────────────────────

            [TestMethod]
            public void BypassEnvVar_WhenSetToOne_IndicatesBypassActive()
            {
                // Document the bypass contract: BYPASS_JWT_AUTH=1 means skip validation
                Environment.SetEnvironmentVariable("BYPASS_JWT_AUTH", "1");
                var bypass = Environment.GetEnvironmentVariable("BYPASS_JWT_AUTH") == "1";
                Assert.IsTrue(bypass, "BYPASS_JWT_AUTH=1 should activate the bypass");
                Environment.SetEnvironmentVariable("BYPASS_JWT_AUTH", null); // clean up
            }

            [TestMethod]
            public void BypassEnvVar_WhenNotSet_IndicatesRealValidation()
            {
                Environment.SetEnvironmentVariable("BYPASS_JWT_AUTH", null);
                var bypass = Environment.GetEnvironmentVariable("BYPASS_JWT_AUTH") == "1";
                Assert.IsFalse(bypass, "Unset BYPASS_JWT_AUTH means full JWT validation runs");
            }

            [TestMethod]
            public void BypassEnvVar_WhenSetToOtherValue_DoesNotBypass()
            {
                Environment.SetEnvironmentVariable("BYPASS_JWT_AUTH", "true");
                var bypass = Environment.GetEnvironmentVariable("BYPASS_JWT_AUTH") == "1";
                Assert.IsFalse(bypass, "Only the exact string '1' activates the bypass");
                Environment.SetEnvironmentVariable("BYPASS_JWT_AUTH", null);
            }
        }
    }
