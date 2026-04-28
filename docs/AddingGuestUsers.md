# Adding Guest Users to OldPictures

## How it works

Guest users see photos from the owner's (`Calvin_Hsia@live.com`) **OldPictures** OneDrive folder.
The app resolves roles after the Graph `/me` call:

| Role | Who | NavMenu |
|---|---|---|
| **Owner** | `calvin_hsia@live.com` | "My Stuff (Owner)" |
| **Guest** | email in `ALLOWED_EMAILS` app setting | "My Stuff (Guest)" |
| **Anonymous** | not signed in, or email not in allowlist | "My Stuff" hidden |

`UserContextService` (singleton) holds the resolved role. `NavMenu.razor` subscribes to it and re-renders immediately after login.

When a guest logs in, `PictureService.InitializeSharedContextAsync` resolves the shared folder in two steps:

1. **Primary** — searches `sharedWithMe` (fast, works after the guest has clicked a share link at least once)
2. **Fallback** — calls `GET /drives/{driveId}/items/{itemId}` using the stable `OwnerFolderContext` constants in `PictureService.cs` (works immediately, no link click required)

So **guests do not need to click a share link** before using the app — permission granted in OneDrive is sufficient.

---

## Steps to add a new guest

### 1. Share the folder in OneDrive

1. Go to [OneDrive](https://onedrive.live.com) and sign in as `Calvin_Hsia@live.com`
2. Right-click **OldPictures** → **Share**
3. Enter the guest's Microsoft account email
4. Set permission to **Can view**
5. Click **Send** (the guest does not need to click the link for the app to work)

### 2. Add the guest email to the Function App setting (no redeploy needed)

1. Azure Portal → **Function App** (`CalvinHWebSite`) → **Configuration → Application settings**
2. Find or create the setting `ALLOWED_EMAILS`
3. Value is semicolon-separated, e.g.:
   ```
   calvin_hsia@live.com;calvin_hsia_test@outlook.com;pamelahsia@hotmail.com;newguest@example.com
   ```
4. Click **Save** → **Continue** → the function app restarts automatically
5. The new guest can log in immediately — no code change or deployment needed

> **Note:** The `ALLOWED_EMAILS` setting also works in PR preview environments since they use the same Function App configuration.

### 3. No other changes needed

- The `OwnerFolderContext` constants in `PictureService.cs` are tied to the **OldPictures folder**, not individual guests
- Telemetry events automatically include the signed-in user's email via `AppInsights.SetUserId()` (set in `PictureQuery.razor.cs` after Graph `/me` resolves)

---

## Architecture note

The app uses **MSAL directly** for login — not SWA's `/.auth/login` flow. This means:
- SWA's `staticwebapp.config.json` `allowedRoles` **cannot** gate API calls (SWA edge always sees MSAL users as `anonymous`)
- API authorization is handled in `Api/SwaAuthHelper.cs` via the `&u=<email>` query param
- The `ALLOWED_EMAILS` check happens at **function execution time**, not at the SWA edge

See `.github/copilot-instructions.md` for the full authentication architecture explanation.

Any Microsoft account that has been granted permission to **OldPictures** in OneDrive will automatically get access — no `appsettings.json` changes, no redeployment.

### 3. Verify access (optional)

Have the guest navigate to the app and sign in. The browser console (F12) will show one of:

| Log message | Meaning |
|---|---|
| `SharedContext.Initialized` with `source=sharedWithMe` | Found via sharedWithMe (guest has opened a link before) |
| `SharedContext.Initialized` with `source=ownerIds` | Found via fallback (works without clicking any link) |
| `SharedContext.OwnerIdsFailed` with `statusCode=403` | Guest's account was not granted permission in OneDrive |

## If OldPictures is ever recreated

The `OwnerFolderContext` constants would need updating. To find the new values:

1. Log in as the owner and navigate to the PictureQuery page
2. The `/me` response is logged to the console — it contains the driveId
3. Or call `GET https://graph.microsoft.com/v1.0/me/drive/root:/OldPictures?$select=id,parentReference` in [Graph Explorer](https://developer.microsoft.com/en-us/graph/graph-explorer)
4. Update `DriveId` (from `parentReference.driveId`) and `RootItemId` (from `id`) in `PictureService.cs`

## Current guests

| Name | Email | Permission |
|---|---|---|
| Pamela Hsia | pamelahsia@hotmail.com | Can view |
| Calvin Hsia_Test | calvin_hsia_test@outlook.com | Can edit |
