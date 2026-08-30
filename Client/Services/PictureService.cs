using System.Text;
using System.Text.Json;
using BlazorWasm.Services;

namespace Client.Services;

/// <summary>
/// Holds remote drive context for accessing a shared OneDrive folder.
/// </summary>
public record SharedDriveContext(string DriveId, string RootItemId);

/// <summary>
/// Service for picture/OneDrive shared-folder concerns: resolving the shared
/// drive context for guest users.
/// </summary>
public class PictureService
{
    private const string SharedFolderName = "OldPictures";
    private const string MSGraphEndPoint = "https://graph.microsoft.com/v1.0/";

    /// <summary>
    /// The permanent driveId of the owner's personal OneDrive (live.com CID).
    /// Stable unless the account is deleted.
    /// </summary>
    private const string OwnerDriveId = "00d69f3552cefc21";
    /// <summary>
    /// Path to OldPictures on the owner's OneDrive, relative to drive root.
    /// Matches MyPix.PathsToPix[1].
    /// </summary>
    private const string OwnerFolderPath = "Pictures/OldPictures";

    /// <summary>
    /// A OneDrive sharing link for the OldPictures folder, created by the owner
    /// (Share → People you specify → Copy link). Must be a FULL onedrive.live.com URL,
    /// NOT a 1drv.ms short URL (Graph API rejects short URLs with "Bad Argument").
    /// To get the full URL: OneDrive web → OldPictures → Share → embed/details link.
    /// Leave empty to skip and fall through to owner path lookup.
    /// </summary>
    private const string OldPicturesSharingUrl = ""; // 1drv.ms short URLs don't work; leave empty

    private readonly TelemetryService _telemetry;
    private readonly UserContextService _userContext;

    public PictureService(TelemetryService telemetry, UserContextService userContext)
    {
        _telemetry = telemetry;
        _userContext = userContext;
    }
    /*
    // Failure funnel — what method failed and for whom
customEvents
| where name in ("SharedContext.OwnerPathFailed", "SharedContext.AccessFailed", "SharedContext.FolderNotFound", "SharedContext.AllMethodsFailed")
| extend user = tostring(customDimensions.userEmail), error = tostring(customDimensions.statusCode)
| project timestamp, name, user, error, customDimensions
| order by timestamp desc


customEvents
| where name startswith "SharedContext."
| extend user = tostring(customDimensions.userEmail)
| where user == "email@example.com"
| project timestamp, name, user, customDimensions
| order by timestamp desc

traces
| where * contains 'pic' 

     */
    /// <summary>
    /// Returns a property dict pre-populated with the current user's email and role
    /// so every telemetry event can be filtered by user in Application Insights.
    /// </summary>
    private Dictionary<string, string> UserProps(Dictionary<string, string>? extra = null)
    {
        var props = new Dictionary<string, string>
        {
            ["userEmail"] = string.IsNullOrEmpty(_userContext.Email) ? "(anonymous)" : _userContext.Email,
            ["userRole"]  = _userContext.Role.ToString()
        };
        if (extra != null)
            foreach (var kv in extra)
                props[kv.Key] = kv.Value;
        return props;
    }

    /// <summary>
    /// When non-null, all file access is routed through this shared drive context.
    /// </summary>
    public SharedDriveContext? SharedContext { get; private set; }

    /// <summary>
    /// Call once after authentication to set up the shared context when the signed-in
    /// user is not the owner. Tries in order:
    ///   1. Owner drive path (/drives/{ownerDriveId}/root:/{path}) — direct, one round trip.
    ///   2. Encoded sharing link (/shares/{encoded}/driveItem) — needs full onedrive.live.com URL, not 1drv.ms.
    ///   3. sharedWithMe — works if the folder still appears in recipient's share history.
    /// Returns an error message if all three fail, or null on success.
    /// </summary>
    public async Task<string?> InitializeSharedContextAsync(HttpClient httpClient)
    {
        SharedContext = null;
        try
        {
            // --- Primary: resolve by path directly on owner's drive (simplest, one round trip) ---
            var resolvedItemId = await ResolveOwnerFolderItemIdAsync(httpClient);
            if (resolvedItemId != null)
            {
                SharedContext = new SharedDriveContext(OwnerDriveId, resolvedItemId);
                await _telemetry.TrackEventAsync("SharedContext.Initialized",
                    UserProps(new() { ["source"] = "ownerPath", ["itemId"] = resolvedItemId }));
                return null;
            }

            // --- Secondary: resolve via encoded sharing link (full onedrive.live.com URL required) ---
            if (!string.IsNullOrEmpty(OldPicturesSharingUrl))
            {
                var linkError = await TryInitFromSharingLinkAsync(httpClient, OldPicturesSharingUrl);
                if (SharedContext != null)
                    return null;
                Console.WriteLine($"[PictureService] Sharing link resolve failed: {linkError}");
            }

            // --- Tertiary: search sharedWithMe (may miss items if share was not recently accepted) ---
            var sharedWithMeError = await TryInitFromSharedWithMeAsync(httpClient);
            if (SharedContext != null)
                return null;

            var finalError = sharedWithMeError ?? $"Could not access shared folder '{SharedFolderName}' via any method.";
            await _telemetry.TrackEventAsync("SharedContext.AllMethodsFailed",
                UserProps(new() { ["error"] = finalError }));
            return finalError;
        }
        catch (Exception ex)
        {
            await _telemetry.TrackExceptionAsync(ex,
                UserProps(new() { ["context"] = "InitializeSharedContextAsync" }));
            return $"Error accessing shared folder: {ex.Message}";
        }
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves SharedContext from an encoded OneDrive sharing link in one Graph call.
    /// The Graph /shares/{encoded}/driveItem endpoint works for any user who has been
    /// granted access via the link, regardless of sharedWithMe history.
    /// </summary>
    private async Task<string?> TryInitFromSharingLinkAsync(HttpClient httpClient, string sharingUrl)
    {
        try
        {
            // Graph encoding: base64url("u!" + url), no padding
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("u!" + sharingUrl))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            var url = $"{MSGraphEndPoint}shares/{encoded}/driveItem?$select=id,parentReference,name";
            Console.WriteLine($"[PictureService] Trying sharing link resolve...");
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                await _telemetry.TrackEventAsync("SharedContext.SharingLinkFailed",
                    UserProps(new() { ["statusCode"] = response.StatusCode.ToString(), ["body"] = body[..Math.Min(300, body.Length)] }));
                return $"Sharing link resolve failed: {response.StatusCode}";
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var itemId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

            // Personal OneDrive sharing link resolution returns parentReference.driveId in
            // "b!..." (SharePoint-encoded) format, which does NOT work with the batch API's
            // /drives/{driveId}/items/{itemId}:/{path} syntax — that requires the CID hex
            // format (e.g. "00d69f3552cefc21"). Since OldPicturesSharingUrl is always for
            // the owner's folder, we use the known OwnerDriveId and log the raw value for
            // diagnostic purposes only.
            string? rawDriveId = null;
            if (root.TryGetProperty("parentReference", out var parentRef) &&
                parentRef.TryGetProperty("driveId", out var driveIdEl))
            {
                rawDriveId = driveIdEl.GetString();
            }
            Console.WriteLine($"[PictureService] Sharing link raw driveId='{rawDriveId}' (using OwnerDriveId for batch compatibility)");

            if (string.IsNullOrEmpty(itemId))
            {
                return $"Sharing link driveItem missing id. JSON: {json[..Math.Min(300, json.Length)]}";
            }

            // Always use OwnerDriveId (CID hex format) — the batch API requires this format
            // for personal OneDrive. The itemId from the sharing link response is accurate.
            SharedContext = new SharedDriveContext(OwnerDriveId, itemId);
            Console.WriteLine($"[PictureService] SharedContext set from sharing link: driveId={OwnerDriveId} itemId={itemId}");
            await _telemetry.TrackEventAsync("SharedContext.Initialized",
                UserProps(new() { ["source"] = "sharingLink", ["driveId"] = OwnerDriveId, ["itemId"] = itemId }));
            return null;
        }
        catch (Exception ex)
        {
            await _telemetry.TrackExceptionAsync(ex, UserProps(new() { ["context"] = "TryInitFromSharingLinkAsync" }));
            return $"Sharing link exception: {ex.Message}";
        }
    }

    private async Task<string?> TryInitFromSharedWithMeAsync(HttpClient httpClient)
    {
        // allowexternal=true is required for Microsoft personal (consumer) accounts
        // to see items shared from external OneDrive accounts.
        var response = await httpClient.GetAsync($"{MSGraphEndPoint}me/drive/sharedWithMe?allowexternal=true");
        if (!response.IsSuccessStatusCode)
        {
            var msg = $"Could not access sharedWithMe: {response.StatusCode}";
            await _telemetry.TrackEventAsync("SharedContext.AccessFailed",
                UserProps(new() { ["statusCode"] = response.StatusCode.ToString() }));
            return msg;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("value", out var valueArray))
        {
            await _telemetry.TrackEventAsync("SharedContext.NoValueArray", UserProps());
            return $"Shared folder '{SharedFolderName}' not found (no value array).";
        }

        var itemCount = valueArray.GetArrayLength();
        Console.WriteLine($"[PictureService] sharedWithMe returned {itemCount} item(s):");
        await _telemetry.TrackEventAsync("SharedContext.Searching",
            UserProps(new() { ["itemCount"] = itemCount.ToString() }));

        foreach (var item in valueArray.EnumerateArray())
        {
            var topName = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            item.TryGetProperty("remoteItem", out var remoteItem);
            var remoteName = remoteItem.ValueKind == JsonValueKind.Object &&
                             remoteItem.TryGetProperty("name", out var rn) ? rn.GetString() : null;

            // Log every item so it's visible in browser devtools (helps diagnose name mismatches)
            Console.WriteLine($"[PictureService]   item: name='{topName}' remoteName='{remoteName}' hasRemoteItem={remoteItem.ValueKind == JsonValueKind.Object}");

            // Use case-insensitive comparison to handle any capitalisation drift
            bool nameMatch = string.Equals(topName, SharedFolderName, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(remoteName, SharedFolderName, StringComparison.OrdinalIgnoreCase);

            if (nameMatch && remoteItem.ValueKind == JsonValueKind.Object)
            {
                var remoteItemId = remoteItem.GetProperty("id").GetString()!;

                string? remoteDriveId = null;
                if (remoteItem.TryGetProperty("parentReference", out var parentRef) &&
                    parentRef.TryGetProperty("driveId", out var driveIdEl))
                {
                    remoteDriveId = driveIdEl.GetString();
                }

                if (string.IsNullOrEmpty(remoteDriveId))
                {
                    await _telemetry.TrackEventAsync("SharedContext.MissingDriveId",
                        UserProps(new() { ["itemId"] = remoteItemId }));
                    return $"Shared folder '{SharedFolderName}' found but driveId is missing in remoteItem.parentReference.";
                }

                SharedContext = new SharedDriveContext(remoteDriveId, remoteItemId);
                Console.WriteLine($"[PictureService] SharedContext set from sharedWithMe: driveId={remoteDriveId} itemId={remoteItemId}");
                await _telemetry.TrackEventAsync("SharedContext.Initialized",
                    UserProps(new() { ["source"] = "sharedWithMe", ["driveId"] = remoteDriveId, ["itemId"] = remoteItemId }));
                return null;
            }
        }

        // No match — log every item's name individually so the full list is always visible
        // in telemetry regardless of total JSON size. Also collect a compact summary.
        var allNames = new System.Text.StringBuilder();
        int idx = 0;
        foreach (var item in valueArray.EnumerateArray())
        {
            var n   = item.TryGetProperty("name",       out var ne) ? ne.GetString() : null;
            var ri  = item.TryGetProperty("remoteItem", out var rie) && rie.ValueKind == JsonValueKind.Object;
            var rn  = ri && rie.TryGetProperty("name",  out var rne) ? rne.GetString() : null;
            var isFolder = ri && rie.TryGetProperty("folder", out _);
            allNames.Append($"[{idx}] name='{n}' remoteName='{rn}' isFolder={isFolder}; ");
            await _telemetry.TrackEventAsync("SharedContext.ItemEnumerated",
                UserProps(new()
                {
                    ["index"]         = idx.ToString(),
                    ["name"]          = n        ?? "(null)",
                    ["remoteName"]    = rn        ?? "(null)",
                    ["isFolder"]      = isFolder.ToString(),
                    ["hasRemoteItem"] = ri.ToString(),
                    ["looking_for"]   = SharedFolderName
                }));
            idx++;
        }
        var summary = allNames.ToString();
        Console.WriteLine($"[PictureService] '{SharedFolderName}' not matched. Items: {summary}");
        await _telemetry.TrackEventAsync("SharedContext.FolderNotFound",
            UserProps(new()
            {
                ["itemCount"]   = itemCount.ToString(),
                ["allNames"]    = summary.Length > 1000 ? summary[..1000] : summary,
                ["looking_for"] = SharedFolderName
            }));
        return $"Shared folder '{SharedFolderName}' not found in sharedWithMe ({itemCount} item(s) returned).";
    }

    /// <summary>
    /// Resolves the real itemId of the owner's OldPictures folder by path lookup.
    /// Returns null if inaccessible (e.g. not shared with the current user).
    /// </summary>
    private async Task<string?> ResolveOwnerFolderItemIdAsync(HttpClient httpClient)
    {
        var url = $"{MSGraphEndPoint}drives/{OwnerDriveId}/root:/{OwnerFolderPath}?$select=id,name";
        try
        {
            Console.WriteLine($"[PictureService] Trying owner path: {url}");
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[PictureService] Owner path failed: {response.StatusCode} — {body[..Math.Min(300, body.Length)]}");

                // Parse Graph's structured error so each field is a queryable telemetry property
                string graphErrorCode = "(unknown)";
                string graphErrorMessage = "(unknown)";
                string innerErrorCode = "(none)";
                try
                {
                    using var errDoc = JsonDocument.Parse(body);
                    if (errDoc.RootElement.TryGetProperty("error", out var errEl))
                    {
                        graphErrorCode    = errEl.TryGetProperty("code",    out var c) ? c.GetString() ?? graphErrorCode    : graphErrorCode;
                        graphErrorMessage = errEl.TryGetProperty("message", out var m) ? m.GetString() ?? graphErrorMessage : graphErrorMessage;
                        if (errEl.TryGetProperty("innerError", out var inner) &&
                            inner.TryGetProperty("code", out var ic))
                            innerErrorCode = ic.GetString() ?? innerErrorCode;
                    }
                }
                catch { /* body wasn't JSON — fall through with defaults */ }

                await _telemetry.TrackEventAsync("SharedContext.OwnerPathFailed",
                    UserProps(new()
                    {
                        ["statusCode"]        = ((int)response.StatusCode).ToString(),
                        ["statusText"]        = response.StatusCode.ToString(),
                        ["graphErrorCode"]    = graphErrorCode,
                        ["graphErrorMessage"] = graphErrorMessage,
                        ["innerErrorCode"]    = innerErrorCode,
                        ["driveId"]           = OwnerDriveId,
                        ["folderPath"]        = OwnerFolderPath,
                        ["url"]               = url
                    }));
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var itemId = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            Console.WriteLine($"[PictureService] Owner path resolved itemId={itemId}");
            return itemId;
        }
        catch (Exception ex)
        {
            await _telemetry.TrackExceptionAsync(ex,
                UserProps(new() { ["context"] = "ResolveOwnerFolderItemIdAsync", ["url"] = url }));
            return null;
        }
    }
}
