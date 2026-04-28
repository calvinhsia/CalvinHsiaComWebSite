# Adding Guest Users to OldPictures

## How it works

Guest users see photos from the owner's (`Calvin_Hsia@live.com`) **OldPictures** OneDrive folder.
The app identifies a guest as anyone who logs in with an email other than `OwnerEmail` in `PictureQuery.razor.cs`.

When a guest logs in, `PictureService.InitializeSharedContextAsync` resolves the shared folder in two steps:

1. **Primary** — searches `sharedWithMe` (fast, works after the guest has clicked a share link at least once)
2. **Fallback** — calls `GET /drives/{driveId}/items/{itemId}` using the stable `OwnerFolderContext` constants in `PictureService.cs` (works immediately, no link click required)

So **guests do not need to click a share link** before using the app — permission granted in OneDrive is sufficient.

## Steps to add a new guest

### 1. Share the folder in OneDrive

1. Go to [OneDrive](https://onedrive.live.com) and sign in as `Calvin_Hsia@live.com`
2. Right-click **OldPictures** → **Share**
3. Enter the guest's Microsoft account email
4. Set permission to **Can view** (or **Can edit** if appropriate)
5. Click **Send** (or **Copy link** — the guest does not need to click it for the app to work)

### 2. Add the guest email to `SwaAuthHelper.cs`

The Azure Function API (`Api/SwaAuthHelper.cs`) has an email allowlist that gates access to `QueryPix` and other functions. Add the guest's email:

```csharp
private static readonly HashSet<string> AllowedEmails =
    new(StringComparer.OrdinalIgnoreCase)
    {
        "calvin_hsia@live.com",
        "calvin_hsia_test@outlook.com",
        "pamelahsia@hotmail.com",   // ← add new guest here
    };
```

Redeploy the API after this change.

### 3. No other code changes needed

The `OwnerFolderContext` constants in `PictureService.cs` are tied to the **OldPictures folder itself**, not to individual guests:

```csharp
private static readonly SharedDriveContext OwnerFolderContext = new(
    DriveId: "00d69f3552cefc21",
    RootItemId: "D69F3552CEFC21!s99c97fcc716e491f80d1762f6db950d0");
```

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
