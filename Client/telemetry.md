# Telemetry Reference

Events are sent to **Azure Application Insights** via `ApplicationInsightsLogger` (JS SDK interop).  
All events are **suppressed when running on `localhost`** — a `[Telemetry suppressed-dev]` message
appears in the browser console instead.

---

## Common Properties

Every event includes these dimensions:

| Property | Description | Example |
|---|---|---|
| `sessionId` | Random 12-char hex, one per page-load lifetime | `a3f9c12d8b04` |
| `os` | Parsed from `navigator.userAgent` | `Windows`, `iOS`, `Android`, `macOS`, `Linux` |
| `userId` | Authenticated user email; `anonymous` until login resolves | `calvin_hsia@live.com` |
| `url` | `window.location.href` at the moment the event fires | `https://calvinhsia.com/fish` |
| `environment` | Derived from `window.location.origin` | `dev`, `staging`, `prod` |

### Environment label rules (`DetectEnvironment`)
| Origin contains | Label |
|---|---|
| `localhost` or `127.0.0.1` | `dev` (events suppressed) |
| `.azurestaticapps.net` | `staging` |
| `calvinhsia.com` | `prod` |
| anything else | `prod` |

---

## Event Catalog

### `Site:Loaded`
Fired **once per session** from `Program.cs` ~5 seconds after the Blazor WASM host starts
(delay allows the AI SDK to finish loading from CDN).

| Extra property | Description |
|---|---|
| `buildTime` | `BuildInfo.BuildTime` — when the DLL was compiled |
| `gitBranch` | `BuildInfo.GitBranch` — active git branch at build time |
| `browser` | `Edge`, `Chrome`, `Firefox`, `Safari`, `Other` |
| `isMobile` | `true` / `false` |

---

### `PageActivation:<page>`  +  PageView `<page>`
Fired on **first render** of each page component (`OnAfterRenderAsync(firstRender)`).

Pages instrumented: `Bounce`, `Fish`, `Ant`, `Life`, `Mandelbrot`, `Snake`, `Tetris`,
`Minesweeper`, `Cartoon`, `FreeCell`, `Hearts`, `Solitaire`, `Wordament`, `WordScape`,
`Logo`, `PictureQuery`.

| Extra property | Description |
|---|---|
| `page` | Page name, e.g. `Fish` |

---

### `Auth:Login`
Fired at every stage of the MSAL login flow.

| `outcome` value | `reason` value | Where |
|---|---|---|
| `initiated` | `user_clicked_sign_in` | User clicks Sign In on `/LoginPage` |
| `started` | `action=login` | `Authentication.razor` loaded for `login` action |
| `started` | `action=login-callback` | MSAL redirect callback arrives |
| `success` | `token_ok` | Access token retrieved successfully |
| `success` | `auth_state_ok` | Authenticated via `AuthenticationState` fallback |
| `failure` | `no_token_after_retries` | All 3 retry attempts failed |
| `failure` | `AccessTokenNotAvailable: …` | `AccessTokenNotAvailableException` thrown |
| `failure` | exception message | Unexpected error in auth check |

On **success**, `userId` is also set from the resolved `preferred_username` / `ClaimTypes.Name`
claim and stored for the rest of the session.

---

### `Auth:Logout`
Fired at every stage of the MSAL logout flow.

| `outcome` value | `reason` value | Where |
|---|---|---|
| `started` | `action=logout` | `Authentication.razor` loaded for `logout` |
| `started` | `action=logout-callback` | Logout callback arrives |
| `success` | `logged-out` | `logged-out` action received |
| `success` | `storage_cleared` | `ForceLogout` cleared localStorage/sessionStorage |
| `failure` | exception message | Error during `ForceLogout` |

---

### `PictureQuery:Filter`
Fired every time the user executes a query on the `/PictureQuery` page.

| Extra property | Description |
|---|---|
| `filter` | The notes/filename filter text entered |
| `mediaType` | `photo`, `video`, or `all` |
| `resultCount` | Number of pictures returned |

---

### `AppError`  +  Exception
Fired by `TelemetryErrorBoundary` (wraps the entire `<Router>`) for any unhandled
component-tree exception, and by `PictureQuery` initialization errors.

| Extra property | Description |
|---|---|
| `source` | C# location, e.g. `ErrorBoundary`, `PictureQuery.OnInitializedAsync` |
| `errorType` | Exception class name |
| `errorMessage` | `Exception.Message` |
| `stackTrace` | First 512 chars of stack trace |

---

### Legacy startup events (via old `TelemetryService`)
These exist alongside `Site:Loaded` and are sent via the older JS interop path:

| Event name | Description |
|---|---|
| `ApplicationStartup` | Host/environment info at startup |
| `ClientEnvironment` | Detailed browser capabilities (screen, viewport, CPU, memory, connection) |
| `StartupComplete` | Debug mode + build branch after all initialization |

---

## Sample KQL Queries

> Paste these into **Azure Portal → Application Insights → Logs**.

---

### Page usage — visits per page

```kql
customEvents
| where name startswith "PageActivation:"
| summarize visits = count()
    by page     = tostring(customDimensions.page),
       env      = tostring(customDimensions.environment)
| order by visits desc
```

---

### Page usage over time (daily trend)

```kql
customEvents
| where name startswith "PageActivation:"
| summarize visits = count()
    by page = tostring(customDimensions.page),
       day  = startofday(timestamp)
| order by day desc, visits desc
```

---

### Unique users per page (last 30 days)

```kql
customEvents
| where name startswith "PageActivation:"
    and timestamp > ago(30d)
| summarize unique_users = dcount(tostring(customDimensions.userId))
    by page = tostring(customDimensions.page)
| order by unique_users desc
```

---

### OS/device breakdown

```kql
customEvents
| where name == "Site:Loaded"
| summarize sessions = count()
    by os       = tostring(customDimensions.os),
       browser  = tostring(customDimensions.browser),
       isMobile = tostring(customDimensions.isMobile)
| order by sessions desc
```

---

### Deployments — site loads by build + branch

```kql
customEvents
| where name == "Site:Loaded"
| summarize first_seen = min(timestamp), loads = count()
    by gitBranch = tostring(customDimensions.gitBranch),
       buildTime = tostring(customDimensions.buildTime)
| order by first_seen desc
```

---

### Login funnel

```kql
customEvents
| where name == "Auth:Login"
| summarize count()
    by outcome = tostring(customDimensions.outcome),
       reason  = tostring(customDimensions.reason)
| order by count_ desc
```

---

### Login failures — detail

```kql
customEvents
| where name == "Auth:Login"
    and tostring(customDimensions.outcome) == "failure"
| project timestamp,
          reason      = tostring(customDimensions.reason),
          userId      = tostring(customDimensions.userId),
          os          = tostring(customDimensions.os),
          browser     = tostring(customDimensions.browser),
          environment = tostring(customDimensions.environment)
| order by timestamp desc
```

---

### Logout success vs failure

```kql
customEvents
| where name == "Auth:Logout"
| summarize count() by outcome = tostring(customDimensions.outcome)
```

---

### PictureQuery — most used filters

```kql
customEvents
| where name == "PictureQuery:Filter"
| summarize queries    = count(),
            avg_results = avg(toint(customDimensions.resultCount))
    by filter    = tostring(customDimensions.filter),
       mediaType = tostring(customDimensions.mediaType)
| order by queries desc
```

---

### PictureQuery — filters with zero results

```kql
customEvents
| where name == "PictureQuery:Filter"
    and toint(customDimensions.resultCount) == 0
| project timestamp,
          filter    = tostring(customDimensions.filter),
          mediaType = tostring(customDimensions.mediaType),
          userId    = tostring(customDimensions.userId)
| order by timestamp desc
```

---

### Application errors — recent

```kql
customEvents
| where name == "AppError"
| project timestamp,
          source       = tostring(customDimensions.source),
          errorType    = tostring(customDimensions.errorType),
          errorMessage = tostring(customDimensions.errorMessage),
          userId       = tostring(customDimensions.userId),
          url          = tostring(customDimensions.url)
| order by timestamp desc
```

---

### Error rate over time

```kql
customEvents
| where name == "AppError"
| summarize errors = count() by hour = startofhour(timestamp)
| order by hour desc
```

---

### Sessions per day (unique sessionIds)

```kql
customEvents
| summarize sessions = dcount(tostring(customDimensions.sessionId))
    by day = startofday(timestamp)
| order by day desc
```

---

### Active users — last 7 days

```kql
customEvents
| where timestamp > ago(7d)
    and tostring(customDimensions.userId) != "anonymous"
| summarize days_active = dcount(startofday(timestamp))
    by userId = tostring(customDimensions.userId)
| order by days_active desc
```

---

## Retention & Cost

| Item | Value |
|---|---|
| Default retention | 90 days |
| Maximum retention | 730 days (2 years) — set in portal under *Usage and estimated costs* |
| Free ingestion | 5 GB / month |
| Overage | ~$2.30 / GB |
| Local dev | **All events suppressed** (`localhost`/`127.0.0.1`) |

Set a **daily ingestion cap** in the portal to prevent surprise bills:  
*Application Insights → Configure → Usage and estimated costs → Daily cap*
