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
    /// (Share → People you specify → Copy link). This is the primary resolution
    /// path for guest users and does not depend on sharedWithMe history.
    /// To update: go to onedrive.live.com → Pictures/OldPictures → Share → Copy link.
    /// Leave empty to skip and fall through to sharedWithMe.
    /// </summary>
    private const string OldPicturesSharingUrl = "https://1drv.ms/f/c/00d69f3552cefc21/IgAh_M5SNZ_WIIAASlkEAAAAAVYJSMeFXWHV93drG1QRNuM?e=EMzW38";

    private readonly TelemetryService _telemetry;

    public PictureService(TelemetryService telemetry)  
    {
        _telemetry = telemetry;
    }

    /// <summary>
    /// When non-null, all file access is routed through this shared drive context.
    /// </summary>
    public SharedDriveContext? SharedContext { get; private set; }

    /// <summary>
    /// Call once after authentication to set up the shared context when the signed-in
    /// user is not the owner. Tries in order:
    ///   1. Encoded sharing link (/shares/{encoded}/driveItem) — most reliable, no round-trip overhead.
    ///   2. sharedWithMe — works if the folder still appears in recipient's share history.
    ///   3. Owner path lookup — last resort (requires active permission grant on owner's drive).
    /// Returns an error message if all three fail, or null on success.
    /// </summary>
    public async Task<string?> InitializeSharedContextAsync(HttpClient httpClient)
    {
        SharedContext = null;
        try
        {
            // --- Primary: resolve via encoded sharing link (one round trip, no sharedWithMe dependency) ---
            if (!string.IsNullOrEmpty(OldPicturesSharingUrl))
            {
                var linkError = await TryInitFromSharingLinkAsync(httpClient, OldPicturesSharingUrl);
                if (SharedContext != null)
                    return null;
                Console.WriteLine($"[PictureService] Sharing link resolve failed: {linkError}");
            }

            // --- Secondary: search sharedWithMe (may miss items if share was not recently accepted) ---
            var sharedWithMeError = await TryInitFromSharedWithMeAsync(httpClient);
            if (SharedContext != null)
                return null;

            // --- Fallback: resolve OldPictures by path on the owner's drive ---
            await _telemetry.TrackEventAsync("SharedContext.FallbackToOwnerIds");
            var resolvedItemId = await ResolveOwnerFolderItemIdAsync(httpClient);
            if (resolvedItemId != null)
            {
                SharedContext = new SharedDriveContext(OwnerDriveId, resolvedItemId);
                await _telemetry.TrackEventAsync("SharedContext.Initialized",
                    new Dictionary<string, string> { ["source"] = "ownerPath", ["itemId"] = resolvedItemId });
                return null;
            }

            return sharedWithMeError;
        }
        catch (Exception ex)
        {
            await _telemetry.TrackExceptionAsync(ex,
                new Dictionary<string, string> { ["context"] = "InitializeSharedContextAsync" });
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
                    new Dictionary<string, string> { ["statusCode"] = response.StatusCode.ToString(), ["body"] = body[..Math.Min(300, body.Length)] });
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
                new Dictionary<string, string> { ["source"] = "sharingLink", ["driveId"] = OwnerDriveId, ["itemId"] = itemId });
            return null;
        }
        catch (Exception ex)
        {
            await _telemetry.TrackExceptionAsync(ex, new Dictionary<string, string> { ["context"] = "TryInitFromSharingLinkAsync" });
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
                new Dictionary<string, string> { ["statusCode"] = response.StatusCode.ToString() });
            return msg;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("value", out var valueArray))
        {
            await _telemetry.TrackEventAsync("SharedContext.NoValueArray");
            return $"Shared folder '{SharedFolderName}' not found (no value array).";
        }

        var itemCount = valueArray.GetArrayLength();
        Console.WriteLine($"[PictureService] sharedWithMe returned {itemCount} item(s):");
        await _telemetry.TrackEventAsync("SharedContext.Searching",
            new Dictionary<string, string> { ["itemCount"] = itemCount.ToString() });

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
                        new Dictionary<string, string> { ["itemId"] = remoteItemId });
                    return $"Shared folder '{SharedFolderName}' found but driveId is missing in remoteItem.parentReference.";
                }

                SharedContext = new SharedDriveContext(remoteDriveId, remoteItemId);
                Console.WriteLine($"[PictureService] SharedContext set from sharedWithMe: driveId={remoteDriveId} itemId={remoteItemId}");
                await _telemetry.TrackEventAsync("SharedContext.Initialized",
                    new Dictionary<string, string> { ["source"] = "sharedWithMe", ["driveId"] = remoteDriveId, ["itemId"] = remoteItemId });
                return null;
            }
        }

        // No match — log the full JSON (truncated) so it shows in telemetry and console
        Console.WriteLine($"[PictureService] '{SharedFolderName}' not matched. sharedWithMe JSON (first 2000 chars): {(json.Length > 2000 ? json[..2000] : json)}");
        await _telemetry.TrackEventAsync("SharedContext.FolderNotFound",
            new Dictionary<string, string>
            {
                ["sharedWithMeJson"] = json.Length > 2000 ? json[..2000] : json
            });
        return $"Shared folder '{SharedFolderName}' not found in sharedWithMe.";
    }

    /// <summary>
    /// Resolves the real itemId of the owner's OldPictures folder by path lookup.
    /// Returns null if inaccessible (e.g. not shared with the current user).
    /// </summary>
    private async Task<string?> ResolveOwnerFolderItemIdAsync(HttpClient httpClient)
    {
        try
        {
            var url = $"{MSGraphEndPoint}drives/{OwnerDriveId}/root:/{OwnerFolderPath}?$select=id,name";
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                await _telemetry.TrackEventAsync("SharedContext.OwnerPathFailed",
                    new Dictionary<string, string> { ["statusCode"] = response.StatusCode.ToString(), ["body"] = body[..Math.Min(500, body.Length)] });
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        }
        catch (Exception ex)
        {
            await _telemetry.TrackExceptionAsync(ex,
                new Dictionary<string, string> { ["context"] = "ResolveOwnerFolderItemIdAsync" });
            return null;
        }
    }
}
