using System.Text.Json;
using WordScapeBlazorWasm.Services;

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
    /// The permanent driveId and itemId of the owner's OldPictures folder.
    /// These are stable OneDrive identifiers — they never change unless the
    /// folder is deleted and recreated. Used as a fallback when sharedWithMe
    /// doesn't list the folder (e.g. recipient has never clicked the share link).
    /// </summary>
    private static readonly SharedDriveContext OwnerFolderContext = new(
        DriveId: "b!83dYf68A3UqwIndPKXO86ksbNT9FWcZDpoHwpxsFSmAvmEFhctIgTLOZOz0qfQS1",
        RootItemId: "D69F3552CEFC21!s99c97fcc716e491f80d1762f6db950d0");

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

            // --- Fallback: use the stable owner folder IDs directly.
            // sharedWithMe only lists folders the recipient has opened via a share link at
            // least once. The owner's driveId/itemId are permanent and never change, so we
            // can use them directly without requiring any prior interaction.
            await _telemetry.TrackEventAsync("SharedContext.FallbackToOwnerIds");
            var verified = await VerifyOwnerContextAccessAsync(httpClient);
            if (verified)
            {
                SharedContext = OwnerFolderContext;
                await _telemetry.TrackEventAsync("SharedContext.Initialized",
                    new Dictionary<string, string> { ["source"] = "ownerIds" });
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
    /// Verifies that the current user can actually read the owner's OldPictures folder
    /// using the hardcoded stable IDs. Returns true if accessible.
    /// </summary>
    private async Task<bool> VerifyOwnerContextAccessAsync(HttpClient httpClient)
    {
        try
        {
            var url = $"{MSGraphEndPoint}drives/{OwnerFolderContext.DriveId}/items/{OwnerFolderContext.RootItemId}?$select=id,name";
            var response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return true;

            var body = await response.Content.ReadAsStringAsync();
            await _telemetry.TrackEventAsync("SharedContext.OwnerIdsFailed",
                new Dictionary<string, string> { ["statusCode"] = response.StatusCode.ToString(), ["body"] = body[..Math.Min(500, body.Length)] });
            return false;
        }
        catch (Exception ex)
        {
            await _telemetry.TrackExceptionAsync(ex,
                new Dictionary<string, string> { ["context"] = "VerifyOwnerContextAccessAsync" });
            return false;
        }
    }
}
