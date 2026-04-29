using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Web;
using WordScapeBlazorWasm.Services;
using Client.Shared; // Add this for MyPix class
using Client.Services; // UserRole, UserContextService

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
    [Inject] private Client.Services.PictureService PictureService { get; set; } = null!;
    [Inject] private Client.Services.ApplicationInsightsLogger AppInsights { get; set; } = null!;
    [Inject] private Client.Services.UserContextService UserContext { get; set; } = null!;

    // Parameters
    [Parameter]
    public int PageNumber { get; set; } = 1;

    // Public properties (used in markup)
    public int NumberPerPage => NumberRowsPerPage * NumberPerRow;
    public int NumberTotalPix => myPixes.Count;
 
    // Owner identity
    private const string OwnerEmail = "calvin_hsia@live.com";
    private bool isGuestUser = false;
    private string userMail = string.Empty;
    private bool userMailResolved = false; // true once /me has been called and userMail is set

    // Private fields
    private int NumberRowsPerPage = 40;
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
    private bool isMobile = false;
    private bool albumNameManuallyChanged = false;
    private bool wakeLockActive = false;
    private bool showLightbox = false;
    private int lightboxIndex = -1;
    private int sliderPreviewIndex = -1; // index while slider is dragging

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

    // Lightbox media cache — keyed by FullFileName|ThumbSize, value is a Task<byte[]>.
    // Storing the Task (not the bytes) means if a prefetch is in flight and the user
    // navigates to that same item, ShowLightboxItemAsync awaits the same download
    // rather than cancelling and restarting it. Cache persists across open/close
    // and tab navigation; cleared only when a new query runs.
    private readonly Dictionary<string, Task<byte[]>> _lightboxCache = new();
    // Cancels background prefetch when navigating away or running a new query.
    private CancellationTokenSource _prefetchCts = new();

    // Lifecycle methods
    protected override async Task OnInitializedAsync()
    {
        // ✅ LOG MYPIX VERSION TO VERIFY CORRECT DLL IS LOADED
        Console.WriteLine($"🔍 MyPix Version Check: {MyPix.MYPIX_VERSION}");
        Console.WriteLine($"🔍 MyPix has parameterless constructor: {typeof(MyPix).GetConstructor(Type.EmptyTypes) != null}");

        _ = AppInsights.TrackPageActivationAsync("PictureQuery");

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

                userMail = candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
                Console.WriteLine($"Signed-in user resolved to: '{userMail}'");
                userMailResolved = true;
                isGuestUser = !string.Equals(userMail, OwnerEmail, StringComparison.OrdinalIgnoreCase);
                UserContext.SetUser(userMail, isGuestUser ? UserRole.Guest : UserRole.Owner);
                AppInsights.SetUserId(userMail);

                if (isGuestUser)
                {
                    Console.WriteLine("Guest user detected — initializing shared drive context...");
                    var sharedError = await PictureService.InitializeSharedContextAsync(_httpClient!);
                    if (sharedError != null)
                    {
                        statusMessage = $"⚠️ {sharedError}";
                        Console.WriteLine($"Shared context error: {sharedError}");
                    }
                    else
                    {
                        Console.WriteLine($"Shared context ready: driveId={PictureService.SharedContext!.DriveId}");
                    }
                }
                else
                {
                    Console.WriteLine("Owner login — using personal OneDrive.");
                }

                // Now that userMail is resolved, it is safe to resume interrupted album creation.
                if (resumedProgress != null)
                {
                    _ = Task.Run(async () => await ResumeWithQueryAsync());
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
            _ = AppInsights.TrackErrorAsync("PictureQuery.OnInitializedAsync", ex);
        }
    }

    protected override bool ShouldRender() => shouldRender;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        mainPix = null;
        showLightbox = false;
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
                isMobile = await JS.InvokeAsync<bool>("eval", "/(android|iphone|ipad|ipod|mobile)/i.test(navigator.userAgent)");
                Console.WriteLine($"[PictureQuery] isMobile={isMobile}");
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
                        // Defer resume until after OnInitializedAsync resolves userMail.
                        // Setting the flag here; the actual kick-off happens at the end of OnInitializedAsync.
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
            // Include user email as &u= — SWA replaces Authorization header so we can't use it
            var url = urlQuery.Contains("&u=") ? urlQuery : urlQuery + $"&u={Uri.EscapeDataString(userMail)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
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

        var page = myPixes.Skip((PageNumber - 1) * NumberPerPage).Take(NumberPerPage).ToList();

        // Partition: items whose bytes are already in cache vs those that need Graph batch calls.
        // A cache entry is "ready" when the Task exists AND has already completed successfully,
        // so we never skip the batch for an item that's still in-flight or faulted.
        var cachedItems = new List<(int ndx, MyPix pix, Task<byte[]> task)>();
        var uncachedItems = new List<MyPix>();
        lock (_lightboxCache)
        {
            for (int i = 0; i < page.Count; i++)
            {
                var pix = page[i];
                var cacheKey = $"{pix.FullFileName}|large";
                if (_lightboxCache.TryGetValue(cacheKey, out var t) &&
                    t.IsCompletedSuccessfully)
                    cachedItems.Add((i, pix, t));
                else
                    uncachedItems.Add(pix);
            }
        }

        Console.WriteLine($"[Refresh] page={PageNumber} cached={cachedItems.Count} uncached={uncachedItems.Count}");

        // Render cached items immediately — no Graph round-trips needed.
        if (cachedItems.Count > 0)
        {
            // Clear only the slots that need network work; leave cached slots untouched
            // so the user sees them right away without a blank flash.
            foreach (var ndx in Enumerable.Range(0, page.Count)
                         .Except(cachedItems.Select(c => c.ndx)))
                await JS.InvokeVoidAsync("clearImageSrc", $"image{ndx}");

            var renderCached = cachedItems.Select(async c =>
            {
                var bytes = await c.task;          // already completed, just unwraps
                var dotnetRef = new DotNetStreamReference(new MemoryStream(bytes));
                await JS.InvokeVoidAsync("setImageSrc", ct, $"image{c.ndx}", dotnetRef);
                Console.WriteLine($"[Refresh] Cache hit image{c.ndx} = {c.pix.FileName} ({bytes.Length} bytes)");
            });
            await Task.WhenAll(renderCached);
        }
        else
        {
            // Nothing cached for this page — clear all slots so user sees loading feedback.
            for (int i = 0; i < page.Count; i++)
                await JS.InvokeVoidAsync("clearImageSrc", $"image{i}");
        }

        if (uncachedItems.Count == 0) return;   // entire page was cached — done

        // Only call the Graph batch for items not yet in cache.
        try
        {
            await AlbumService.GetThumbnailUrlsBatchAsync(_httpClient!, uncachedItems, "large",
                async (chunkResults, chunkStartIndex) =>
                {
                    if (ct.IsCancellationRequested) return;

                    var fetchTasks = chunkResults
                        .Select(async kv =>
                        {
                            if (ct.IsCancellationRequested) return;
                            // Map back to the original page index
                            var ndx = page.FindIndex(p => p.FullFileName == kv.Key);
                            if (ndx < 0 || kv.Value == null)
                            {
                                Console.WriteLine($"[Refresh] No thumbnail URL for {kv.Key}");
                                return;
                            }
                            var pix = page[ndx];
                            var cacheKey = $"{pix.FullFileName}|large";
                            Task<byte[]> downloadTask;
                            lock (_lightboxCache)
                            {
                                // Evict cancelled or faulted tasks — same as GetImageStreamAsync —
                                // so a previously interrupted download doesn't block a fresh attempt.
                                if (_lightboxCache.TryGetValue(cacheKey, out downloadTask!) &&
                                    (downloadTask.IsFaulted || downloadTask.IsCanceled))
                                {
                                    _lightboxCache.Remove(cacheKey);
                                    downloadTask = null!;
                                }
                                if (downloadTask == null)
                                {
                                    // Use CancellationToken.None for the actual byte download so that
                                    // navigating away (which cancels ct) cannot store a cancelled/partial
                                    // task in the cache and block the next attempt.
                                    downloadTask = _httpClient!.GetByteArrayAsync(kv.Value, CancellationToken.None);
                                    _lightboxCache[cacheKey] = downloadTask;
                                }
                            }
                            var thumbBytes = await downloadTask;
                            var dotnetRef = new DotNetStreamReference(new MemoryStream(thumbBytes));
                            await JS.InvokeVoidAsync("setImageSrc", ct, $"image{ndx}", dotnetRef);
                            Console.WriteLine($"[Refresh] Downloaded image{ndx} = {pix.FileName} ({thumbBytes.Length} bytes)");
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
    }

    private async Task<DotNetStreamReference> GetImageStreamAsync(MyPix pix, string ThumbSize = "", CancellationToken ct = default)
    {
        var cacheKey = $"{pix.FullFileName}|{ThumbSize}";
        Task<byte[]> downloadTask;
        bool isNewDownload;
        lock (_lightboxCache)
        {
            if (_lightboxCache.TryGetValue(cacheKey, out downloadTask!))
            {
                // Evict faulted or cancelled tasks — they hold corrupt/partial bytes
                // and would replay the same failure on every subsequent visit.
                if (downloadTask.IsFaulted || downloadTask.IsCanceled)
                {
                    Console.WriteLine($"[Lightbox] Evicting bad cache entry for {pix.FileName} ({ThumbSize}) faulted={downloadTask.IsFaulted} cancelled={downloadTask.IsCanceled}");
                    _lightboxCache.Remove(cacheKey);
                    downloadTask = null!;
                }
            }
            if (downloadTask == null)
            {
                // Start with CancellationToken.None for the actual download so that
                // navigating away (which cancels _prefetchCts) cannot truncate bytes
                // mid-flight and corrupt the cached content.
                downloadTask = DownloadBytesAsync(pix, ThumbSize, CancellationToken.None);
                _lightboxCache[cacheKey] = downloadTask;
                isNewDownload = true;
            }
            else
            {
                isNewDownload = false;
            }
        }
        // Check cancellation *before* awaiting so a cancelled prefetch doesn't block,
        // but the download itself runs to completion regardless.
        ct.ThrowIfCancellationRequested();
        var bytes = await downloadTask;
        Console.WriteLine($"[Lightbox] {(isNewDownload ? "Downloaded" : "Cache hit")} {pix.FileName} ({ThumbSize}) {bytes.Length} bytes, cache={_lightboxCache.Count}");
        return new DotNetStreamReference(new MemoryStream(bytes));
    }

    private async Task<byte[]> DownloadBytesAsync(MyPix pix, string ThumbSize, CancellationToken ct)
    {
        // Metadata lookup can be cancelled — it's cheap and makes no permanent change.
        var fileData = await AlbumService.GetFileMetadataAsync(_httpClient!, pix, ct);
        if (fileData == null || !fileData.Value.TryGetProperty("id", out var idProp))
        {
            var msg = $"Could not get metadata for {pix.FileName}";
            await AppInsights.TrackEvent("Lightbox_MetadataFailed", new() { ["fileName"] = pix.FileName, ["thumbSize"] = ThumbSize });
            throw new Exception(msg);
        }

        HttpResponseMessage response;
        if (!string.IsNullOrEmpty(ThumbSize))
        {
            var thumbUrl = AlbumService.GetThumbnailUrl(idProp.GetString()!, ThumbSize);
            // Use CancellationToken.None for the actual byte download — if the token
            // is cancelled mid-flight the partial bytes would be cached as corrupt data.
            // Cancellation only gates whether we *start* prefetch work, not the download itself.
            response = await _httpClient!.GetAsync(thumbUrl, CancellationToken.None);
        }
        else
        {
            var contentUrl = AlbumService.GetItemContentUrl(idProp.GetString()!);
            response = await _httpClient!.GetAsync(contentUrl, CancellationToken.None);
        }
        if (!response.IsSuccessStatusCode)
        {
            await AppInsights.TrackEvent("Lightbox_DownloadFailed", new()
            {
                ["fileName"] = pix.FileName,
                ["thumbSize"] = ThumbSize,
                ["statusCode"] = ((int)response.StatusCode).ToString(),
                ["isVideo"] = pix.IsVideo.ToString()
            });
            throw new Exception($"Download failed {response.StatusCode} for {pix.FileName}");
        }
        return await response.Content.ReadAsByteArrayAsync(CancellationToken.None);
    }

    /// <summary>
    /// Prefetches the next and previous items into the cache while the user views the current one.
    /// Because the cache holds Task&lt;byte[]&gt;, if the user navigates to a prefetching item
    /// ShowLightboxItemAsync awaits the same task — no cancel/restart needed.
    /// A new CancellationTokenSource is only created when the previous one was already cancelled
    /// (i.e. by LightboxClose or resetUI).
    /// </summary>
    private async Task PrefetchNeighboursAsync(int currentIndex)
    {
        if (_prefetchCts.IsCancellationRequested)
            _prefetchCts = new CancellationTokenSource();
        var ct = _prefetchCts.Token;

        // Prefetch next then prev (next is more likely to be needed)
        var neighbours = new[] { currentIndex + 1, currentIndex - 1 }
            .Where(i => i >= 0 && i < myPixes.Count)
            .Select(i => myPixes[i]);

        foreach (var pix in neighbours)
        {
            if (ct.IsCancellationRequested) return;   // stop starting new work
            try
            {
                var thumbSize = pix.IsVideo ? "" : "large";
                // On mobile, videos stream by URL — no point downloading bytes into cache.
                if (pix.IsVideo && isMobile)
                {
                    Console.WriteLine($"[Prefetch] Skipping video on mobile (streaming) {pix.FileName}");
                    continue;
                }
                var cacheKey = $"{pix.FullFileName}|{thumbSize}";
                bool needsFetch;
                lock (_lightboxCache)
                {
                    needsFetch = !_lightboxCache.TryGetValue(cacheKey, out var existing) ||
                                 existing.IsFaulted || existing.IsCanceled;
                }
                if (needsFetch)
                {
                    Console.WriteLine($"[Prefetch] Starting {pix.FileName}");
                    await GetImageStreamAsync(pix, thumbSize, ct);
                    Console.WriteLine($"[Prefetch] Completed {pix.FileName}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[Prefetch] Skipped (cancelled) {pix.FileName}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Prefetch] Error for {pix.FileName}: {ex.Message}");
            }
        }
    }

    private async Task resetUI()
    {
        // Cancel any background prefetch and clear the media cache so stale bytes from
        // the previous query result set don't carry over to the new one.
        _prefetchCts.Cancel();
        _prefetchCts = new CancellationTokenSource();
        lock (_lightboxCache) { _lightboxCache.Clear(); }
        mainPix = null;
        showLightbox = false;
        await JS.InvokeVoidAsync("setImageSrc", "imageMain", "null");
        await JS.InvokeVoidAsync("setVideoUrl", "myVideo", null, null);
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

            // SWA replaces the Authorization header — pass the email as a query param instead.
            var urlQuery = $"/api/QueryPix?{qpart}&u={Uri.EscapeDataString(userMail)}";

            var request = new HttpRequestMessage(HttpMethod.Get, urlQuery);
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
            _ = AppInsights.TrackPictureQueryFilterAsync(notesFilter, mediaType, myPixes.Count);
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
        lightboxIndex = myPixes.IndexOf(pix);
        await ShowLightboxItemAsync(lightboxIndex);
    }

    private async Task ShowLightboxItemAsync(int index)
    {
        if (index < 0 || index >= myPixes.Count) return;
        isLoading = true;
        showLightbox = true;
        lightboxIndex = index;
        StateHasChanged();
        try
        {
            mainPix = myPixes[index];
            await Task.Yield();
            if (mainPix.IsVideo)
            {
                var ext = Path.GetExtension(mainPix.FileName).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".mp4" => "video/mp4",
                    ".mov" => "video/quicktime",
                    ".avi" => "video/x-msvideo",
                    ".wmv" => "video/x-ms-wmv",
                    ".mpg" => "video/mpeg",
                    _ => "video/mp4"
                };
                await JS.InvokeVoidAsync("setImageSrc", "imageMain", "null");
                if (isMobile)
                {
                    // On mobile stream directly — avoids loading 100+ MB into WASM memory.
                    var (streamUrl, rotation, fileSize) = await AlbumService.GetDownloadUrlAsync(_httpClient!, mainPix);
                    Console.WriteLine($"[Video] {mainPix.FileName} rotation={rotation} fileSize={fileSize} hasUrl={!string.IsNullOrEmpty(streamUrl)}");
                    if (!string.IsNullOrEmpty(streamUrl))
                    {
                        await JS.InvokeVoidAsync("setVideoUrl", "myVideo", streamUrl, mimeType, rotation, fileSize);
                    }
                    else
                    {
                        var strm = await GetImageStreamAsync(mainPix, "");
                        await JS.InvokeVoidAsync("setImageSrc", "myVideo", strm);
                    }
                }
                else
                {
                    // On desktop use the cached blob path (instant if prefetched).
                    var strm = await GetImageStreamAsync(mainPix, "");
                    await JS.InvokeVoidAsync("setImageSrc", "myVideo", strm);
                }
            }
            else
            {
                var dotnetImageStream = await GetImageStreamAsync(mainPix, "large");
                await JS.InvokeVoidAsync("setImageSrc", "myVideo", "null");
                await JS.InvokeVoidAsync("setImageSrc", "imageMain", dotnetImageStream);
            }
            // Prefetch neighbours in background so next/prev is instant
            _ = PrefetchNeighboursAsync(index);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LightboxPrev()
    {
        if (lightboxIndex > 0)
            await ShowLightboxItemAsync(lightboxIndex - 1);
    }

    private async Task LightboxNext()
    {
        if (lightboxIndex < myPixes.Count - 1)
            await ShowLightboxItemAsync(lightboxIndex + 1);
    }

    private async Task RotateCurrentMediaAsync()
    {
        // Works for both video (rotateVideoBy90) and image (rotateImageBy90)
        if (mainPix?.IsVideo == true)
            await JS.InvokeVoidAsync("rotateVideoBy90", "myVideo");
        else
            await JS.InvokeVoidAsync("rotateImageBy90", "imageMain");
    }

    private async Task LightboxClose()
    {
        _prefetchCts.Cancel();
        _prefetchCts = new CancellationTokenSource();
        showLightbox = false;
        mainPix = null;
        sliderPreviewIndex = -1;
        // Cache is intentionally kept — user may reopen the lightbox or tab back.
        // It is cleared in resetUI() when a new query runs.
        await JS.InvokeVoidAsync("setImageSrc", "imageMain", "null");
        await JS.InvokeVoidAsync("setVideoUrl", "myVideo", null, null);
        StateHasChanged();
    }

    // Slider dragging: update title/counter in real time without loading the image
    private void OnSliderInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var idx) && idx >= 0 && idx < myPixes.Count)
        {
            sliderPreviewIndex = idx;
            lightboxIndex = idx;
            mainPix = myPixes[idx]; // update title immediately
            StateHasChanged();
        }
    }

    // Slider released: load the full image
    private async Task OnSliderChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var idx))
        {
            sliderPreviewIndex = -1;
            await ShowLightboxItemAsync(idx);
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
