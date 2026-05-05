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
    /// Stable unless the account is deleted. The OldPictures itemId is resolved
    /// dynamically by path to avoid hardcoding a value that could be wrong.
    /// </summary>
    private const string OwnerDriveId = "00d69f3552cefc21";
    /// <summary>
    /// Path to OldPictures on the owner's OneDrive, relative to drive root.
    /// Matches MyPix.PathsToPix[1].
    /// </summary>
    private const string OwnerFolderPath = "Pictures/OldPictures";

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
    /// user is not the owner. First tries sharedWithMe; if not found there, falls back
    /// to the hardcoded owner folder context (works even before recipient opens the share link).
    /// Returns an error message if access is denied, or null on success.
    /// </summary>
    public async Task<string?> InitializeSharedContextAsync(HttpClient httpClient)
    {
        SharedContext = null;
        try
        {
            // --- Primary: search sharedWithMe ---
            var sharedWithMeError = await TryInitFromSharedWithMeAsync(httpClient);
            if (SharedContext != null)
                return null; // success 

            // --- Fallback: resolve OldPictures by path on the owner's drive.
            // This works even if the recipient has never clicked a share link, and
            // avoids hardcoding an itemId that could be wrong.
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

    private async Task<string?> TryInitFromSharedWithMeAsync(HttpClient httpClient)
    {
        var response = await httpClient.GetAsync($"{MSGraphEndPoint}me/drive/sharedWithMe");
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

        await _telemetry.TrackEventAsync("SharedContext.Searching",
            new Dictionary<string, string> { ["itemCount"] = valueArray.GetArrayLength().ToString() });

        foreach (var item in valueArray.EnumerateArray())
        {
            var topName = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            item.TryGetProperty("remoteItem", out var remoteItem);
            var remoteName = remoteItem.ValueKind == JsonValueKind.Object &&
                             remoteItem.TryGetProperty("name", out var rn) ? rn.GetString() : null;

            bool nameMatch = topName == SharedFolderName || remoteName == SharedFolderName;
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
                await _telemetry.TrackEventAsync("SharedContext.Initialized",
                    new Dictionary<string, string> { ["source"] = "sharedWithMe", ["driveId"] = remoteDriveId, ["itemId"] = remoteItemId });
                return null;
            }
        }

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
