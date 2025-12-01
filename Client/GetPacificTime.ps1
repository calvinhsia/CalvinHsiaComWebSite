# Get current time in Pacific Time Zone (handles PST/PDT automatically)
$pacificZone = [System.TimeZoneInfo]::FindSystemTimeZoneById('Pacific Standard Time')
$pacificTime = [System.TimeZoneInfo]::ConvertTimeFromUtc([System.DateTime]::UtcNow, $pacificZone)
$abbreviation = if ($pacificZone.IsDaylightSavingTime($pacificTime)) { "PDT" } else { "PST" }
Write-Output "$($pacificTime.ToString('yyyy-MM-dd HH:mm:ss')) $abbreviation"
