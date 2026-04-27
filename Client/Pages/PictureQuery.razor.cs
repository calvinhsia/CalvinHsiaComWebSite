using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Web;
using WordScapeBlazorWasm.Services;
using Client.Shared; // Add this for MyPix class

namespace Client.Pages;

[Authorize]
public partial class PictureQuery : IDisposable
{
    // Injected services
    [Inject] private IHttpClientFactory ClientFactory { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private IAccessTokenProvider TokenProvider { get; set; } = null!;
    [Inject] private AuthTokenHelper AuthToken { get; set; } = null!;
    [Inject] private AlbumService AlbumService { get; set; } = null!;

    // Parameters
    [Parameter]
    public int PageNumber { get; set; } = 1;

    // Public properties (used in markup)
    public int NumberPerPage => NumberRowsPerPage * NumberPerRow;
    public int NumberTotalPix => myPixes.Count;
 
    // Owner identity
    private const string OwnerEmail = "calvin_hsia@live.com";
    private bool isGuestUser = false;

    // Private fields
    private int NumberRowsPerPage = 10;
    private int NumberPerRow = 1; // depends on width of browser
    private int maxpix = 10000; // max # pix per query

    private string date1 = "1/1/1950";
    private string date2 = "1/1/2030";
    private string notesFilter = @"weight";
    private string mediaType = "";
    private bool publishToAlbum = true;
    private string albumName = "weight"; // Initialize with default filter value
    private int albumMaxItems = 100; // Default album item limit
    private bool isPublishing = false;
    private bool isQuerying = false;
    private bool shouldRender = true;
    private string statusMessage = string.Empty;
    private HttpClient? _httpClient;
    private string userDisplayName = string.Empty;
    private List<MyPix> myPixes { get; set; } = new List<MyPix>();
    private BrowserDimensions browserDimensions = new BrowserDimensions();
    private const string MSGraphEndPoint = @"https://graph.microsoft.com/v1.0/";
    private MyPix? mainPix = null;
    private bool isLoading = false;
    private bool albumNameManuallyChanged = false;
    private bool wakeLockActive = false;

    // Filter history fields
    private List<string> filterHistory = new();
    private bool showFilterHistory = false;
    private System.Threading.Timer? hideHistoryTimer;
    private const string FILTER_HISTORY_KEY = "notesFilterHistory";
    private const int MAX_HISTORY_ITEMS = 10;

    // Enhanced timing tracking fields
    private DateTime albumStartTime;
    private List<double> itemProcessingTimes = new();
    private AlbumProgress? albumProgress = null;
    private CancellationTokenSource? albumCreationCancellationTokenSource;
    private DateTime? currentItemStartTime = null;
    private System.Threading.Timer? uiUpdateTimer;

    // Album progress
    private const string ALBUM_PROGRESS_KEY = "albumProgress";
    private bool isResuming = false;
    private AlbumProgress? resumedProgress = null;
    private string? currentBundleId = null;

    // Lifecycle methods
    protected override async Task OnInitializedAsync()
    {
        // ✅ LOG MYPIX VERSION TO VERIFY CORRECT DLL IS LOADED
        Console.WriteLine($"🔍 MyPix Version Check: {MyPix.MYPIX_VERSION}");
        Console.WriteLine($"🔍 MyPix has parameterless constructor: {typeof(MyPix).GetConstructor(Type.EmptyTypes) != null}");
        
        _httpClient = HttpClientFactory.CreateClient("GraphAPI");
        try
        {
            await LoadFiltersAsync();
            await LoadFilterHistoryAsync();

            // Check for interrupted progress
            await CheckForInterruptedProgressAsync();

            // Initialize album name with current filter if it wasn't loaded from storage
            if (!albumNameManuallyChanged && string.IsNullOrEmpty(albumName))
            {
                albumName = SanitizeAlbumName(notesFilter);
            }

            // Use shared AuthTokenHelper for centralized token expiration handling
            var token = await AuthToken.GetAccessTokenAsync(showExpiredMessage: false);
            if (string.IsNullOrEmpty(token))
            {
                statusMessage = "Session expired. Redirecting to sign in...";
                StateHasChanged();
                // AuthTokenHelper already handles the redirect
                return;
            }

            var dataRequest = await _httpClient.GetAsync($"{MSGraphEndPoint}me");

            if (dataRequest.IsSuccessStatusCode)
            {
                /*
[PictureQuery
] /me response: {
    "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#users/$entity",
    "userPrincipalName": "Calvin_Hsia@live.com",
    "id": "00d69f3552cefc21",
    "displayName": "Calvin Hsia",
    "surname": "Hsia",
    "givenName": "Calvin",
    "preferredLanguage": "en-US",
    "mail": null,
    "mobilePhone": null,
    "jobTitle": null,
    "officeLocation": null,
    "businessPhones": []
}                 */
                // Determine whether the signed-in user is the owner.
                // Personal Microsoft accounts (live.com) often have null `mail` and a mangled
                // `userPrincipalName` (e.g. "foo_live.com#EXT#@..."), so we check several fields.
                var meJson = await dataRequest.Content.ReadAsStringAsync();
                Console.WriteLine($"[PictureQuery] /me response: {meJson}");
                using var meDoc = JsonDocument.Parse(meJson);
                var root = meDoc.RootElement;

                // Collect candidate identity strings in priority order
                var candidates = new List<string?>();

                // 1. identities[].issuerAssignedId where issuer contains "live" or "microsoft"
                if (root.TryGetProperty("identities", out var identitiesEl))
                {
                    foreach (var identity in identitiesEl.EnumerateArray())
                    {
                        var issuer = identity.TryGetProperty("issuer", out var iss) ? iss.GetString() ?? "" : "";
                        if (issuer.Contains("live", StringComparison.OrdinalIgnoreCase) ||
                            issuer.Contains("microsoft", StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(identity.TryGetProperty("issuerAssignedId", out var iai) ? iai.GetString() : null);
                        }
                    }
                }

                // 2. mail
                candidates.Add(root.TryGetProperty("mail", out var mailEl) ? mailEl.GetString() : null);

                // 3. userPrincipalName (may be mangled for MSA, but use as last resort)
                candidates.Add(root.TryGetProperty("userPrincipalName", out var upnEl) ? upnEl.GetString() : null);

                var userMail = candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                Console.WriteLine($"Signed-in user resolved to: '{userMail}'");
                isGuestUser = !string.Equals(userMail, OwnerEmail, StringComparison.OrdinalIgnoreCase);

                if (isGuestUser)
                {
                    Console.WriteLine("Guest user detected — initializing shared drive context...");
                    var sharedError = await AlbumService.InitializeSharedContextAsync(_httpClient!);
                    if (sharedError != null)
                    {
                        statusMessage = $"⚠️ {sharedError}";
                        Console.WriteLine($"Shared context error: {sharedError}");
                    }
                    else
                    {
                        Console.WriteLine($"Shared context ready: driveId={AlbumService.SharedContext!.DriveId}");
                    }
                }
                else
                {
                    Console.WriteLine("Owner login — using personal OneDrive.");
                }
            }
        }
        catch (AccessTokenNotAvailableException ex)
        {
            Console.WriteLine($"Access token not available in OnInitializedAsync: {ex.Message}");
            ex.Redirect(); // Redirects to login
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in OnInitializedAsync: {ex.Message}");
            statusMessage = $"Error: {ex.Message}. Please refresh the page.";
        }
    }

    protected override bool ShouldRender() => shouldRender;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        mainPix = null;
        _ = JS.InvokeVoidAsync("setImageSrc", "imageMain", "null");
        _ = DoRefreshAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            try
            {
                var dimensions = await JS.InvokeAsync<BrowserDimensions>("getDimensions");
                browserDimensions = dimensions;
                Console.WriteLine($"Got Dimensions {dimensions}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    // Filter history methods
    private async Task LoadFilterHistoryAsync()
    {
        try
        {
            var json = await JS.InvokeAsync<string>("localStorage.getItem", FILTER_HISTORY_KEY);
            if (!string.IsNullOrEmpty(json))
            {
                var history = JsonSerializer.Deserialize<List<string>>(json);
                if (history != null)
                {
                    filterHistory = history;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading filter history: {ex.Message}");
            filterHistory = new List<string>();
        }
    }

    private async Task SaveFilterHistoryAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(filterHistory);
            await JS.InvokeVoidAsync("localStorage.setItem", FILTER_HISTORY_KEY, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving filter history: {ex.Message}");
        }
    }

    private async Task AddToFilterHistoryAsync(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return;

        // Remove if already exists (to move to top)
        filterHistory.RemoveAll(x => string.Equals(x, filter, StringComparison.OrdinalIgnoreCase));

        // Add to beginning
        filterHistory.Insert(0, filter);

        // Limit to MAX_HISTORY_ITEMS
        if (filterHistory.Count > MAX_HISTORY_ITEMS)
        {
            filterHistory = filterHistory.Take(MAX_HISTORY_ITEMS).ToList();
        }

        await SaveFilterHistoryAsync();
    }

    private void ShowHistory()
    {
        // Cancel any pending hide timer
        hideHistoryTimer?.Dispose();
        hideHistoryTimer = null;

        showFilterHistory = true;
        StateHasChanged();
    }

    private void HideHistoryDelayed()
    {
        // Use a timer to delay hiding so clicks on history items can be processed
        hideHistoryTimer?.Dispose();
        hideHistoryTimer = new System.Threading.Timer(_ =>
        {
            InvokeAsync(() =>
 {
     showFilterHistory = false;
     StateHasChanged();
 });
        }, null, TimeSpan.FromMilliseconds(150), Timeout.InfiniteTimeSpan);
    }

    private async Task SelectFromHistory(string historyItem)
    {
        notesFilter = historyItem;
        showFilterHistory = false;

        // Only update album name if user hasn't manually changed it
        if (!albumNameManuallyChanged)
        {
            albumName = SanitizeAlbumName(notesFilter);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearFilterHistory()
    {
        filterHistory.Clear();
        showFilterHistory = false;
        await SaveFilterHistoryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task CheckForInterruptedProgressAsync()
    {
        try
        {
            var progressJson = await JS.InvokeAsync<string>("localStorage.getItem", ALBUM_PROGRESS_KEY);
            if (!string.IsNullOrEmpty(progressJson))
            {
                var savedProgress = JsonSerializer.Deserialize<AlbumProgress>(progressJson);
                if (savedProgress != null)
                {
                    // If less than 30 minutes old, prepare for resume
                    if (DateTime.Now - savedProgress.StartTime < TimeSpan.FromMinutes(30))
                    {
                        resumedProgress = savedProgress;
                        statusMessage = $"🔄 Found interrupted album creation for '{savedProgress.AlbumName}' " +
                       $"from item {savedProgress.LastProcessedIndex + 1}/{savedProgress.TotalItems}. Re-executing query to resume...";
                        Console.WriteLine($"Found resumable progress: last processed index {savedProgress.LastProcessedIndex}");

                        await InvokeAsync(StateHasChanged); // Update UI first

                        // ✅ Re-execute the query first to populate myPixes, then resume album creation
                        _ = Task.Run(async () => await ResumeWithQueryAsync());

                        return; // Don't remove - we'll use this for resume
                    }
                }
            }

            // Clean up old/stale progress
            await JS.InvokeVoidAsync("localStorage.removeItem", ALBUM_PROGRESS_KEY);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking interrupted progress: {ex.Message}");
            await JS.InvokeVoidAsync("localStorage.removeItem", ALBUM_PROGRESS_KEY);
        }
    }

    // ✅ New method to handle resume with query
    private async Task ResumeWithQueryAsync()
    {
        try
        {
            // First, re-execute the query to populate myPixes
            await ExecuteQueryOnlyAsync();
            _ = DoRefreshAsync();

            // Then start the album creation process
            await CreateAlbumAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during resume with query: {ex.Message}");
            statusMessage = $"❌ Error resuming album creation: {ex.Message}";
            await InvokeAsync(StateHasChanged);
        }
    }

    // ✅ Extract query logic into separate method
    private async Task ExecuteQueryOnlyAsync()
    {
        Console.WriteLine("Re-executing query to populate myPixes for resume...");

        try
        {
            // Use shared AuthTokenHelper for centralized token expiration handling
            var token = await AuthToken.GetAccessTokenAsync(showExpiredMessage: false);
            if (string.IsNullOrEmpty(token))
            {
                statusMessage = "Session expired. Redirecting to sign in...";
                StateHasChanged();
                // AuthTokenHelper already handles the redirect
                throw new Exception("Authentication token not available");
            }

            var qpart = $"Date1={HttpUtility.UrlEncode(date1)}&Date2={HttpUtility.UrlEncode(date2)}&MaxPix={maxpix}&NotesFilter={HttpUtility.UrlEncode(notesFilter)}";
            if (!string.IsNullOrEmpty(mediaType))
            {
                qpart += $"&MediaType={mediaType.ToLower()}";
            }

            var urlQuery = $"/api/QueryPix?{qpart}";

            var serverJson = await FetchQueryPixAsync(urlQuery, token);
            var pixes = JsonSerializer.Deserialize<MyPix[]>(serverJson);

            // Clear and repopulate myPixes
            myPixes.Clear();
            if (pixes != null && pixes.Length > 0)
            {
                myPixes.AddRange(pixes);
                Console.WriteLine($"Re-populated myPixes with {myPixes.Count} items for resume");
            }
            else
            {
                throw new Exception("Query returned no results - cannot resume album creation");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error re-executing query: {ex.Message}");
            throw; // Re-throw to be handled by ResumeWithQueryAsync
        }
    }

    private void OnFilterChanged()
    {
        // Only update album name if user hasn't manually changed it
        if (!albumNameManuallyChanged)
        {
            albumName = SanitizeAlbumName(notesFilter);
        }

        StateHasChanged();
    }

    private void OnAlbumNameChanged(ChangeEventArgs e)
    {
        albumNameManuallyChanged = true;
        albumName = e.Value?.ToString() ?? string.Empty;
        StateHasChanged();
    }

    private string SanitizeAlbumName(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return "QueryAlbum";

        var sanitized = filter;

        // Remove special regex/filter prefixes that aren't meaningful for album names
        if (sanitized.StartsWith("$"))
            sanitized = sanitized[1..];
        if (sanitized.StartsWith("^"))
            sanitized = sanitized[1..];
        if (sanitized.StartsWith("|"))
            sanitized = sanitized[1..];

        // Replace spaces with underscores and remove problematic characters
        var invalidChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\r', '\n', '\t' };
        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Remove leading/trailing spaces and dots
        sanitized = sanitized.Trim().Trim('.');

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "QueryAlbum" : sanitized;
    }

    /// <summary>
    /// Calls /api/QueryPix with the given query string, handling Azure Functions cold-start
    /// (which returns an HTML "Starting..." meta-refresh page) by retrying once.
    /// Returns the raw JSON string, or throws if the response is not valid JSON after retry.
    /// </summary>
    private async Task<string> FetchQueryPixAsync(string urlQuery, string token)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, urlQuery);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await Http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new HttpRequestException($"Query failed ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var trimmed = body.TrimStart();
            if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase) &&
                (trimmed.StartsWith('[') || trimmed.StartsWith('{')))
                return body;

            // Azure Functions cold-start — HTML "Starting..." page
            Console.WriteLine($"[FetchQueryPix] attempt {attempt + 1} got non-JSON ({contentType}), body: {body[..Math.Min(200, body.Length)]}");
            if (attempt == 0)
            {
                statusMessage = "API warming up, retrying...";
                StateHasChanged();
                await Task.Delay(3000);
            }
        }

        throw new InvalidOperationException("Query failed: unexpected non-JSON response after retry. You may not be authorized.");
    }

    private async Task SaveFiltersAsync()
    {
        var filters = new
        {
            notesFilter,
            mediaType,
            date1,
            date2,
            publishToAlbum,
            albumName,
            albumMaxItems
        };
        await JS.InvokeVoidAsync("localStorage.setItem", "pictureQueryFilters", JsonSerializer.Serialize(filters));
    }

    private async Task LoadFiltersAsync()
    {
        var json = await JS.InvokeAsync<string>("localStorage.getItem", "pictureQueryFilters");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("notesFilter", out var notesFilterEl))
                    notesFilter = notesFilterEl.GetString() ?? notesFilter;
                if (root.TryGetProperty("mediaType", out var mediaTypeEl))
                    mediaType = mediaTypeEl.GetString() ?? mediaType;
                if (root.TryGetProperty("date1", out var date1El))
                    date1 = date1El.GetString() ?? date1;
                if (root.TryGetProperty("date2", out var date2El))
                    date2 = date2El.GetString() ?? date2;
                if (root.TryGetProperty("publishToAlbum", out var publishEl))
                    publishToAlbum = publishEl.GetBoolean();
                if (root.TryGetProperty("albumName", out var albumNameEl))
                    albumName = albumNameEl.GetString() ?? albumName;
                if (root.TryGetProperty("albumMaxItems", out var albumMaxEl))
                    albumMaxItems = albumMaxEl.GetInt32();
            }
            catch
            {
                // If deserialization fails, use defaults
                albumNameManuallyChanged = false;
            }
        }

        // If album name wasn't set in storage, initialize it with the filter
        if (string.IsNullOrEmpty(albumName))
        {
            albumName = SanitizeAlbumName(notesFilter);
        }
    }

    private CancellationTokenSource? _refreshCts;

    private async Task DoRefreshAsync()
    {
        // Cancel any in-progress refresh
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        // Clear all thumbnail images immediately so the user sees feedback right away
        var page = myPixes.Skip((PageNumber - 1) * NumberPerPage).Take(NumberPerPage).ToList();
        for (int i = 0; i < page.Count; i++)
            await JS.InvokeVoidAsync("clearImageSrc", $"image{i}");

        Console.WriteLine("Doing Refresh (batch)");
        try
        {
            await AlbumService.GetThumbnailUrlsBatchAsync(_httpClient!, page, "large",
                async (chunkResults, chunkStartIndex) =>
                {
                    if (ct.IsCancellationRequested) return;

                    // Fetch this chunk's images in parallel and render each as it arrives
                    var fetchTasks = chunkResults
                        .Select(async kv =>
                        {
                            if (ct.IsCancellationRequested) return;
                            var ndx = page.FindIndex(p => p.FullFileName == kv.Key);
                            if (ndx < 0 || kv.Value == null)
                            {
                                Console.WriteLine($"[Refresh] No thumbnail URL for index {ndx}");
                                return;
                            }
                            var resp = await _httpClient!.GetAsync(kv.Value, ct);
                            var strm = await resp.Content.ReadAsStreamAsync(ct);
                            var dotnetRef = new DotNetStreamReference(strm);
                            var byteCount = strm.Length;
                            await JS.InvokeVoidAsync("setImageSrc", ct, $"image{ndx}", dotnetRef);
                            Console.WriteLine($"[Refresh] Set image{ndx} = {page[ndx].FileName} ({byteCount} bytes)");
                        });

                    await Task.WhenAll(fetchTasks);
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Refresh] Cancelled.");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
        }
    }

    private async Task<DotNetStreamReference> GetImageStreamAsync(MyPix pix, string ThumbSize = "")
    {
        DotNetStreamReference? dotnetImageStream = null;
        if (!string.IsNullOrEmpty(ThumbSize))
        {
            var fileData = await AlbumService.GetFileMetadataAsync(_httpClient!, pix);
            if (fileData == null || !fileData.Value.TryGetProperty("id", out var idProp))
                throw new Exception($"Could not get metadata for {pix.FileName}");
            var thumbUrl = AlbumService.GetThumbnailUrl(idProp.GetString()!, ThumbSize);
            var thumreq = await _httpClient!.GetAsync(thumbUrl);
            var strm = await thumreq.Content.ReadAsStreamAsync();
            dotnetImageStream = new DotNetStreamReference(strm);
        }
        else
        {
            var fileData = await AlbumService.GetFileMetadataAsync(_httpClient!, pix);
            if (fileData == null || !fileData.Value.TryGetProperty("id", out var idProp))
                throw new Exception($"Could not get metadata for {pix.FileName}");
            var contentUrl = AlbumService.GetItemContentUrl(idProp.GetString()!);
            var picRequest = await _httpClient!.GetAsync(contentUrl);
            var strm = await picRequest.Content.ReadAsStreamAsync();
            dotnetImageStream = new DotNetStreamReference(strm);
        }
        Console.WriteLine($"ImgStrm {dotnetImageStream.Stream.Length}  {pix.FileName} {ThumbSize}");
        return dotnetImageStream;
    }

    private async Task resetUI()
    {
        mainPix = null;
        await JS.InvokeVoidAsync("setElementVisible", "MyMain", "none");
    }

    private async Task DoQueryAsync()
    {
        if (isQuerying) return; // Guard clause to prevent reentrancy

        isQuerying = true;
        StateHasChanged(); // Update UI immediately

        try
        {
            // Add current filter to history before executing query
            await AddToFilterHistoryAsync(notesFilter);

            await SaveFiltersAsync();
            await resetUI();
            PageNumber = 1;
            statusMessage = "Querying...";

            // Use shared AuthTokenHelper for centralized token expiration handling
            var token = await AuthToken.GetAccessTokenAsync(showExpiredMessage: false);
            if (string.IsNullOrEmpty(token))
            {
                statusMessage = "Session expired. Redirecting to sign in...";
                StateHasChanged();
                // AuthTokenHelper already handles the redirect
                return;
            }

            var qpart = $"Date1={HttpUtility.UrlEncode(date1)}&Date2={HttpUtility.UrlEncode(date2)}&MaxPix={maxpix}&NotesFilter={HttpUtility.UrlEncode(notesFilter)}";
            if (!string.IsNullOrEmpty(mediaType))
            {
                qpart += $"&MediaType={mediaType.ToLower()}";
            }

            // Don't pass album creation to server - we'll handle it client-side
            var urlQuery = $"/api/QueryPix?{qpart}";

            var request = new HttpRequestMessage(HttpMethod.Get, urlQuery);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await Http.SendAsync(request);
            var serverJson = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                statusMessage = $"Query failed ({response.StatusCode}). You may not have access.";
                return;
            }
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase) || 
                !serverJson.TrimStart().StartsWith('[') && !serverJson.TrimStart().StartsWith('{'))
            {
                statusMessage = "Query failed: unexpected response. You may not be authorized.";
                Console.WriteLine($"[PictureQuery] Non-JSON response ({contentType}): {serverJson[..Math.Min(200, serverJson.Length)]}");
                return;
            }
            var pixes = JsonSerializer.Deserialize<MyPix[]>(serverJson);
            myPixes.Clear();
            if (pixes != null && pixes.Length > 0)
            {
                myPixes.AddRange(pixes);
            }
            _ = DoRefreshAsync();

            if (myPixes.Count > 0)
            {
                statusMessage = "";

                // Handle client-side album creation if requested (owner only)
                if (!isGuestUser && publishToAlbum && !string.IsNullOrWhiteSpace(albumName))
                {
                    _ = Task.Run(async () => await CreateAlbumAsync());
                }
            }
            else
            {
                statusMessage = $"No results found";
            }
        }
        finally
        {
            isQuerying = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Format the current item processing time
    /// </summary>
    private string FormatCurrentItemTime()
    {
        if (currentItemStartTime == null) return "0.0";
        var elapsed = DateTime.Now - currentItemStartTime.Value;
        return Math.Round(elapsed.TotalSeconds, 1).ToString();
    }

    /// <summary>
    /// Start UI update timer for real-time current item display
    /// </summary>
    private void StartUIUpdateTimer()
    {
        // Update UI every 100ms to show current item time
        uiUpdateTimer = new System.Threading.Timer(async _ =>
        {
            if (currentItemStartTime != null && isPublishing)
            {
                await InvokeAsync(StateHasChanged);
            }
        }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Stop UI update timer
    /// </summary>
    private void StopUIUpdateTimer()
    {
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
    }

    private async Task SaveProgressAsync()
    {
        if (albumProgress != null && isPublishing)
        {
            // Save progress with current index
            await JS.InvokeVoidAsync("localStorage.setItem", ALBUM_PROGRESS_KEY,
                 JsonSerializer.Serialize(albumProgress));
        }
    }

    /// <summary>
    /// Note: OneDrive Albums sort by the Date of the item taken from the Exif data. If the item doesn't have Exif data (or was created from later digitization)'
    ///  then better use the Desktop version of MyPix to create the album, because it compares the date and the Exif Date, and updates the Exif date if different
    /// </summary>
    private async Task CreateAlbumAsync()
    {
        try
        {
            isPublishing = true;
            albumCreationCancellationTokenSource = new CancellationTokenSource();

            // Calculate albumItems once, early
            var albumItems = myPixes.Take(albumMaxItems).ToList();

            // Check if we're resuming
            if (resumedProgress != null)
            {
                isResuming = true;
                albumProgress = resumedProgress;
                albumStartTime = resumedProgress.StartTime;
                currentBundleId = resumedProgress.BundleId;

                Console.WriteLine($"Resuming album creation from index: {albumProgress.LastProcessedIndex + 1}");
            }
            else
            {
                // Fresh start
                albumStartTime = DateTime.Now;
                albumProgress = new AlbumProgress
                {
                    TotalItems = albumItems.Count,
                    CompletedItems = 0,
                    SuccessfullyAdded = 0,
                    FailedToAdd = 0,
                    AlreadyExists = 0,
                    StartTime = albumStartTime,
                    AlbumName = albumName,
                    LastProcessedIndex = -1 // Start before first item
                };
            }

            StartUIUpdateTimer();
            var wakeLockRequested = await JS.InvokeAsync<bool>("requestWakeLock");
            if (wakeLockRequested)
            {
                wakeLockActive = true;
            }

            statusMessage = $"📸 {(isResuming ? "Resuming" : "Creating")} album '{albumName} #items = {albumItems.Count} {(albumItems.Count == myPixes.Count ? "" : $"({myPixes.Count})")}'...";
            await InvokeAsync(StateHasChanged);

            // Use shared AuthTokenHelper for centralized token expiration handling
            var token = await AuthToken.GetAccessTokenAsync(showExpiredMessage: false);
            if (string.IsNullOrEmpty(token))
            {
                statusMessage = "❌ Session expired during album creation. Redirecting to sign in...";
                isPublishing = false;
                albumProgress = null;
                await InvokeAsync(StateHasChanged);
                // AuthTokenHelper already handles the redirect
                return;
            }
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                  new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            string? bundleId = currentBundleId;
            var didCreateNewAlbum = false;

            // If resuming and we have a bundle ID, use it; otherwise find/create album
            if (string.IsNullOrEmpty(bundleId))
            {
                bundleId = await FindExistingClientAlbumAsync(httpClient, albumName);
                if (string.IsNullOrEmpty(bundleId))
                {
                    bundleId = await CreateClientAlbumAsync(httpClient, albumName);
                    didCreateNewAlbum = true;
                }
                currentBundleId = bundleId;
                if (albumProgress != null)
                {
                    albumProgress.BundleId = bundleId ?? string.Empty;
                }
            }

            if (!string.IsNullOrEmpty(bundleId))
            {
                // Use the albumItems we calculated once at the beginning
                await AddItemsToClientAlbumAsync(httpClient, bundleId, albumItems, albumCreationCancellationTokenSource.Token);

                var shareLink = await GetClientAlbumShareLinkAsync(httpClient, bundleId);

                var summaryMessage = $"✅ Album '{albumName}' {(didCreateNewAlbum ? "Created" : isResuming ? "Resumed and completed" : "Appended to")} successfully! ";
                summaryMessage += $"Added: {albumProgress!.SuccessfullyAdded}, ";
                if (albumProgress.AlreadyExists > 0)
                    summaryMessage += $"Already existed: {albumProgress.AlreadyExists}, ";
                if (albumProgress.FailedToAdd > 0)
                    summaryMessage += $"Failed: {albumProgress.FailedToAdd}, ";
                summaryMessage += $"Total processed: {albumProgress.CompletedItems}/{albumItems.Count} ";
                summaryMessage += $"<a href='{shareLink}' target='_blank'>View Album</a>";

                statusMessage = summaryMessage;
            }
            else
            {
                statusMessage = $"❌ Failed to create album '{albumName}'";
            }
        }
        catch (OperationCanceledException)
        {
            var partialMessage = albumProgress != null ?
                  $" (Partial completion: {albumProgress.SuccessfullyAdded} added, {albumProgress.AlreadyExists} already existed, {albumProgress.FailedToAdd} failed)" : "";
            statusMessage = $"❌ Album creation was canceled{partialMessage}";
        }
        catch (Exception ex)
        {
            statusMessage = $"❌ Error creating album: {ex.Message}";
            Console.WriteLine($"Album creation error: {ex}");
        }
        finally
        {
            StopUIUpdateTimer();
            currentItemStartTime = null;

            if (wakeLockActive)
            {
                await JS.InvokeVoidAsync("releaseWakeLock");
                wakeLockActive = false;
            }

            isPublishing = false;
            isResuming = false;
            resumedProgress = null;
            albumProgress = null;
            currentBundleId = null;
            albumCreationCancellationTokenSource?.Dispose();
            albumCreationCancellationTokenSource = null;

            // Remove persisted progress only on successful completion
            await JS.InvokeVoidAsync("localStorage.removeItem", ALBUM_PROGRESS_KEY);

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task AddItemsToClientAlbumAsync(HttpClient httpClient, string bundleId, List<MyPix> items, CancellationToken cancellationToken)
    {
        // Start from the next index after last processed
        int startIndex = albumProgress?.LastProcessedIndex + 1 ?? 0;
        DateTime lastTokenRefresh = DateTime.Now;
        Console.WriteLine($"Processing items starting from index: {startIndex}");

        for (int i = startIndex; i < items.Count; i++)
        {
            var pix = items[i];
            cancellationToken.ThrowIfCancellationRequested();
            // Refresh token every 10 items OR every 50 minutes
            if (i > startIndex && (i % 10 == 0 || (DateTime.Now - lastTokenRefresh) > TimeSpan.FromMinutes(50)))
            {
                var (success, newRefreshTime) = await AuthToken.RefreshHttpClientTokenIfNeededAsync(httpClient, lastTokenRefresh, 50);
                if (!success)
                {
                    albumProgress!.FailedToAdd += (items.Count - i);
                    statusMessage = "❌ Auth expired. Please re-run to resume.";
                    await InvokeAsync(StateHasChanged);
                    return;
                }
                lastTokenRefresh = newRefreshTime;
            }
            currentItemStartTime = DateTime.Now;
            var itemStartTime = currentItemStartTime.Value;
            bool itemProcessed = false;

            try
            {
                if (string.IsNullOrEmpty(pix.FullFileName))
                {
                    Console.WriteLine($"Skipping pix with empty FullFileName: {pix.FileName}");
                    albumProgress!.FailedToAdd++;
                    itemProcessed = true;
                }
                else
                {
                    // Get file metadata using service
                    var fileData = await AlbumService.GetFileMetadataAsync(httpClient, pix, cancellationToken);

                    if (fileData.HasValue && fileData.Value.TryGetProperty("id", out var idProperty))
                    {
                        var fileId = idProperty.GetString();

                        // Add file to album using service - store result and deconstruct separately
                        var albumResult = await AlbumService.AddFileToAlbumAsync(
                             httpClient, bundleId, fileId!, cancellationToken);

                        bool success = albumResult.success;
                        string? errorMessage = albumResult.errorMessage;

                        if (success)
                        {
                            await UpdateDriveItemDescriptionAsync(httpClient, fileId!, pix.Notes, cancellationToken);
                            Console.WriteLine($"✅ Added {pix.FileName} to album (index {i})");
                            albumProgress!.SuccessfullyAdded++;
                            itemProcessed = true;
                        }
                        else
                        {
                            if (errorMessage == "already_exists")
                            {
                                Console.WriteLine($"📋 Item {pix.FileName} already exists in album (index {i})");
                                albumProgress!.AlreadyExists++;
                            }
                            else
                            {
                                Console.WriteLine($"❌ Failed to add {pix.FileName}: {errorMessage}");
                                albumProgress!.FailedToAdd++;
                            }
                            itemProcessed = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ No file metadata for {pix.FileName}");
                        albumProgress!.FailedToAdd++;
                        itemProcessed = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding {pix.FileName} to album: {ex.Message}");
                albumProgress!.FailedToAdd++;
                itemProcessed = true;
            }
            finally
            {
                if (itemProcessed)
                {
                    var itemEndTime = DateTime.Now;
                    var processingTime = (itemEndTime - itemStartTime).TotalMilliseconds;

                    if (albumProgress != null)
                    {
                        albumProgress.CompletedItems++;
                        albumProgress.LastProcessedIndex = i; // Update the index we've completed
                        albumProgress.ItemCompletionTimes.Add(itemEndTime);

                        if (albumProgress.ItemCompletionTimes.Count > 10)
                        {
                            albumProgress.ItemCompletionTimes.RemoveAt(0);
                        }

                        // Save progress after each item instead of using periodic timer
                        await SaveProgressAsync();
                    }

                    itemProcessingTimes.Add(processingTime);

                    if (itemProcessingTimes.Count > 10)
                    {
                        itemProcessingTimes.RemoveAt(0);
                    }

                    currentItemStartTime = null;
                    await InvokeAsync(StateHasChanged);
                }
            }
        }

        Console.WriteLine($"Album processing completed through index: {albumProgress?.LastProcessedIndex}");
    }

    private async Task<string?> FindExistingClientAlbumAsync(HttpClient httpClient, string albumName)
    {
        return await AlbumService.FindExistingAlbumAsync(httpClient, albumName);
    }

    private async Task<string?> CreateClientAlbumAsync(HttpClient httpClient, string albumName)
    {
        return await AlbumService.CreateNewAlbumAsync(httpClient, albumName);
    }

    private async Task<string> GetClientAlbumShareLinkAsync(HttpClient httpClient, string bundleId)
    {
        return await AlbumService.GetShareLinkAsync(httpClient, bundleId);
    }

    private void CancelAlbumCreation()
    {
        albumCreationCancellationTokenSource?.Cancel();
    }

    private async Task UpdateDriveItemDescriptionAsync(HttpClient httpClient, string itemId, string description, CancellationToken cancellationToken = default, string? userId = null)
    {
        await AlbumService.UpdateItemDescriptionAsync(httpClient, itemId, description, cancellationToken);
    }

    // Helper methods for progress display
    private double GetAverageProcessingTimeMs()
    {
        if (itemProcessingTimes.Count == 0) return 0;
        return itemProcessingTimes.Average();
    }

    private TimeSpan CalculateRemainingTime()
    {
        if (albumProgress == null || albumProgress.CompletedItems == 0 || albumProgress.CompletedItems >= albumProgress.TotalItems)
            return TimeSpan.Zero;

        var remainingItems = albumProgress.TotalItems - albumProgress.CompletedItems;
        var averageTimeMs = GetAverageProcessingTimeMs();

        if (averageTimeMs <= 0) return TimeSpan.Zero;

        var estimatedRemainingTimeMs = remainingItems * averageTimeMs;
        return TimeSpan.FromMilliseconds(estimatedRemainingTimeMs);
    }

    private DateTime CalculateCompletionTime()
    {
        var remainingTime = CalculateRemainingTime();
        return DateTime.Now.Add(remainingTime);
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days}d {timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        else if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        else
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    private async Task MyImgClickMain()
    {
        Console.WriteLine($"clicked on mainimage {mainPix!.FileName}");
        var strm = await GetImageStreamAsync(mainPix);
        await JS.InvokeVoidAsync("downloadFileFromStream", Path.GetFileName(mainPix.FileName), strm);
    }

    private async Task MyThumbClick(MyPix pix)
    {
        isLoading = true;
        StateHasChanged();
        try
        {
            mainPix = pix;
            await Task.Yield();
            {
                if (pix.IsVideo)
                {
                    var strm = await GetImageStreamAsync(pix, "");
                    await JS.InvokeVoidAsync("setImageSrc", "imageMain", "null");
                    await JS.InvokeVoidAsync("setImageSrc", "myVideo", strm);
                }
                else
                {
                    var dotnetImageStream = await GetImageStreamAsync(pix, "large");
                    await JS.InvokeVoidAsync("setImageSrc", "myVideo", "null");
                    await JS.InvokeVoidAsync("setImageSrc", "imageMain", dotnetImageStream);
                }
                await JS.InvokeVoidAsync("setElementVisible", "MyMain", "block");
            }
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        // Stop timers
        hideHistoryTimer?.Dispose();
        StopUIUpdateTimer();

        // Release wake lock if it's still active
        if (wakeLockActive)
        {
            _ = Task.Run(async () => await JS.InvokeVoidAsync("releaseWakeLock"));
        }

        albumCreationCancellationTokenSource?.Cancel();    // ✅ Cancel first
        albumCreationCancellationTokenSource?.Dispose(); // ✅ Dispose second
    }

    // Nested classes
    private class AlbumProgress
    {
        public int TotalItems { get; set; }
        public int CompletedItems { get; set; }
        public int SuccessfullyAdded { get; set; }
        public int FailedToAdd { get; set; }
        public int AlreadyExists { get; set; }
        public DateTime StartTime { get; set; }
        public List<DateTime> ItemCompletionTimes { get; set; } = new();
        public string AlbumName { get; set; } = string.Empty;
        public string BundleId { get; set; } = string.Empty;
        public int LastProcessedIndex { get; set; } = -1; // Track which index we last completed
    }

    public class BrowserDimensions
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public override string ToString() => $"{this.Width}, {Height}";
    }
}
