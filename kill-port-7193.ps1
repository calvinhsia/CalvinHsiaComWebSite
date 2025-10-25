# PowerShell script to kill any process using port 7193
# Run this if you get "address already in use" errors

$port = 7193

Write-Host "Searching for process using port $port..." -ForegroundColor Yellow

# Get the process ID using the port
$netstatOutput = netstat -ano | Select-String ":$port"

if ($netstatOutput) {
    Write-Host "Found connections on port ${port}:" -ForegroundColor Cyan
    $netstatOutput | ForEach-Object { Write-Host $_ }
    
    # Extract PIDs from LISTENING connections
    $pids = $netstatOutput | ForEach-Object {
        if ($_ -match "LISTENING\s+(\d+)") {
$matches[1]
      }
    } | Select-Object -Unique
  
    if ($pids) {
    foreach ($pid in $pids) {
       Write-Host "`nAttempting to kill process with PID: $pid" -ForegroundColor Yellow
    
try {
  # Get process details before killing
             $process = Get-Process -Id $pid -ErrorAction Stop
     Write-Host "Process name: $($process.ProcessName)" -ForegroundColor Cyan
       Write-Host "Process path: $($process.Path)" -ForegroundColor Cyan
        
              # Kill the process
    Stop-Process -Id $pid -Force -ErrorAction Stop
       Write-Host "Successfully killed process $pid" -ForegroundColor Green
   
      # Wait a moment for port to be released
       Start-Sleep -Milliseconds 500
            }
   catch {
                Write-Host "Failed to kill process ${pid}: $_" -ForegroundColor Red
            }
        }
  
        # Verify port is now free
        Start-Sleep -Seconds 1
      $stillInUse = netstat -ano | Select-String ":$port.*LISTENING"
        if ($stillInUse) {
            Write-Host "`nWarning: Port $port is still in use!" -ForegroundColor Red
            $stillInUse | ForEach-Object { Write-Host $_ }
        }
    else {
     Write-Host "`nPort $port is now free!" -ForegroundColor Green
  }
    }
    else {
     Write-Host "No LISTENING processes found on port $port" -ForegroundColor Yellow
    }
}
else {
    Write-Host "No process is using port $port" -ForegroundColor Green
}

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
