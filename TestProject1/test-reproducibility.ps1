# Verify Debug Mode Reproducibility

Write-Host "=" * 80
Write-Host "Testing Debug Mode Reproducibility - Run 1"
Write-Host "=" * 80
Write-Host ""

$output1 = dotnet test --filter "FullyQualifiedName=TestProject1.InteractiveWordScapeTest.AutomatedTest_WordScapeGameInteraction" --logger "console;verbosity=detailed" 2>&1 | Out-String

# Extract key information from first run
$letters1 = if ($output1 -match "Letters in wheel: (.+)") { $matches[1] } else { "NOT FOUND" }
$target1 = if ($output1 -match "Target word: (\w+)") { $matches[1] } else { "NOT FOUND" }

Write-Host "Run 1 Results:"
Write-Host "  Letters: $letters1"
Write-Host "  Target:  $target1"
Write-Host ""

Write-Host "=" * 80
Write-Host "Testing Debug Mode Reproducibility - Run 2"
Write-Host "=" * 80
Write-Host ""

$output2 = dotnet test --filter "FullyQualifiedName=TestProject1.InteractiveWordScapeTest.AutomatedTest_WordScapeGameInteraction" --logger "console;verbosity=detailed" 2>&1 | Out-String

# Extract key information from second run
$letters2 = if ($output2 -match "Letters in wheel: (.+)") { $matches[1] } else { "NOT FOUND" }
$target2 = if ($output2 -match "Target word: (\w+)") { $matches[1] } else { "NOT FOUND" }

Write-Host "Run 2 Results:"
Write-Host "  Letters: $letters2"
Write-Host "  Target:  $target2"
Write-Host ""

Write-Host "=" * 80
Write-Host "Comparison"
Write-Host "=" * 80

if ($letters1 -eq $letters2) {
    Write-Host "? PASS: Letters are IDENTICAL between runs" -ForegroundColor Green
    Write-Host "  Letters: $letters1"
} else {
    Write-Host "? FAIL: Letters are DIFFERENT between runs" -ForegroundColor Red
    Write-Host "  Run 1: $letters1"
    Write-Host "  Run 2: $letters2"
}

if ($target1 -eq $target2) {
    Write-Host "? PASS: Target word is IDENTICAL between runs" -ForegroundColor Green
    Write-Host "  Target: $target1"
} else {
    Write-Host "? FAIL: Target word is DIFFERENT between runs" -ForegroundColor Red
    Write-Host "  Run 1: $target1"
    Write-Host "  Run 2: $target2"
}

Write-Host ""
if ($letters1 -eq $letters2 -and $target1 -eq $target2) {
    Write-Host "?? SUCCESS: Debug mode is working correctly!" -ForegroundColor Green
    Write-Host "   The random seed is being applied consistently." -ForegroundColor Green
} else {
    Write-Host "?? WARNING: Debug mode may not be working correctly" -ForegroundColor Yellow
    Write-Host "   The random seed is NOT being applied consistently." -ForegroundColor Yellow
}
