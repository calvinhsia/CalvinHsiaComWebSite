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
    $processIds = $netstatOutput | ForEach-Object {
        $line = $_.Line
        # The PID is the last column in netstat output
        if ($line -match '\s+(\d+)\s*$') {
            $matches[1]
      }
    } | Select-Object -Unique
  
    if ($processIds) {
    foreach ($processId in $processIds) {
       Write-Host "`nAttempting to kill process with PID: $processId" -ForegroundColor Yellow
    
try {
  # Get process details before killing
             $process = Get-Process -Id $processId -ErrorAction Stop
     Write-Host "Process name: $($process.ProcessName)" -ForegroundColor Cyan
       Write-Host "Process path: $($process.Path)" -ForegroundColor Cyan
        
              # Kill the process
    Stop-Process -Id $processId -Force -ErrorAction Stop
       Write-Host "Successfully killed process $processId" -ForegroundColor Green
   
      # Wait a moment for port to be released
       Start-Sleep -Milliseconds 500
            }
   catch {
                Write-Host "Failed to kill process ${processId}: $_" -ForegroundColor Red
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
