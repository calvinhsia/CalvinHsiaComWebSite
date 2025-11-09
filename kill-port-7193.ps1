# PowerShell script to kill any process using port 7193
# Run this if you get "address already in use" errors

$port = 7193

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Port $port Process Killer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "??  Warning: Not running as Administrator" -ForegroundColor Yellow
    Write-Host "   Some processes may not be killable without admin rights" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "?? Searching for processes using port $port..." -ForegroundColor Yellow
Write-Host ""

# Get the process ID using the port - improved regex
$netstatOutput = netstat -ano | Select-String ":$port\s"

if ($netstatOutput) {
    Write-Host "? Found connections on port ${port}:" -ForegroundColor Green
    Write-Host ""
    $netstatOutput | ForEach-Object { 
      Write-Host "   $_" -ForegroundColor White
    }
    Write-Host ""
  
    # Extract PIDs from LISTENING or ESTABLISHED connections - improved regex
    $processIds = $netstatOutput | ForEach-Object {
        $line = $_.Line
    # Match the PID at the end of the line (last column after whitespace)
        if ($line -match '\s+(\d+)\s*$') {
      [int]$matches[1]
     }
    } | Where-Object { $_ -gt 0 } | Select-Object -Unique | Sort-Object
    
 if ($processIds) {
        Write-Host "?? Found $($processIds.Count) unique process(es) to kill" -ForegroundColor Cyan
        Write-Host ""
        
        $killedCount = 0
      $failedCount = 0
        
        foreach ($processId in $processIds) {
 Write-Host "???????????????????????????????????????" -ForegroundColor DarkGray
            Write-Host "?? Targeting PID: $processId" -ForegroundColor Yellow
     
       try {
      # Get process details before killing
      $process = Get-Process -Id $processId -ErrorAction Stop
    Write-Host "   Process name: $($process.ProcessName)" -ForegroundColor Cyan
        
 # Show more details if available
                if ($process.Path) {
        Write-Host "   Process path: $($process.Path)" -ForegroundColor Cyan
      }
    if ($process.StartTime) {
     Write-Host "   Started: $($process.StartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Cyan
      }
        
   # Kill the process
 Write-Host "   ??  Attempting to kill..." -ForegroundColor Yellow
Stop-Process -Id $processId -Force -ErrorAction Stop
     Write-Host "   ? Successfully killed process $processId" -ForegroundColor Green
    $killedCount++
    
           # Wait a moment for port to be released
     Start-Sleep -Milliseconds 500
            }
            catch [Microsoft.PowerShell.Commands.ProcessCommandException] {
          Write-Host "   ? Process $processId not found (may have already exited)" -ForegroundColor Red
      $failedCount++
            }
catch [System.ComponentModel.Win32Exception] {
     Write-Host "   ? Access denied - process $processId requires Administrator privileges" -ForegroundColor Red
        Write-Host "      ?? Try running this script as Administrator" -ForegroundColor Yellow
           $failedCount++
            }
      catch {
  Write-Host "   ? Failed to kill process ${processId}: $($_.Exception.Message)" -ForegroundColor Red
         $failedCount++
            }
 
            Write-Host ""
}
      
        Write-Host "???????????????????????????????????????" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "?? Summary:" -ForegroundColor Cyan
  Write-Host "   ? Killed: $killedCount" -ForegroundColor Green
    if ($failedCount -gt 0) {
     Write-Host "   ? Failed: $failedCount" -ForegroundColor Red
        }
        Write-Host ""
        
        # Verify port is now free
        Write-Host "?? Verifying port status..." -ForegroundColor Yellow
   Start-Sleep -Seconds 1
     $stillInUse = netstat -ano | Select-String ":$port\s.*LISTENING"
      
    if ($stillInUse) {
         Write-Host ""
    Write-Host "??  WARNING: Port $port is still in use!" -ForegroundColor Red
      Write-Host ""
     Write-Host "   Active connections:" -ForegroundColor Yellow
 $stillInUse | ForEach-Object { 
    Write-Host "   $_" -ForegroundColor White
          }
        Write-Host ""
            Write-Host "?? Suggestions:" -ForegroundColor Cyan
         Write-Host "   1. Run this script as Administrator" -ForegroundColor White
         Write-Host "   2. Manually close Visual Studio or other dev tools" -ForegroundColor White
            Write-Host "   3. Restart your computer if the problem persists" -ForegroundColor White
 }
        else {
            Write-Host ""
            Write-Host "?? SUCCESS: Port $port is now free!" -ForegroundColor Green
       Write-Host "   You can now start your Blazor dev server" -ForegroundColor White
        }
    }
    else {
        Write-Host "??  No valid process IDs found (connections may be closing)" -ForegroundColor Yellow
Write-Host "   Try running the script again in a few seconds" -ForegroundColor White
    }
}
else {
    Write-Host "? No process is using port $port" -ForegroundColor Green
    Write-Host "   Port is available - you can start your server" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Press any key to exit..." -ForegroundColor White
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
