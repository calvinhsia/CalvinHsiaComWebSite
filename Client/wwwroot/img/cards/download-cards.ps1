# PowerShell script to download card images from deckofcardsapi.com
# Run this script once to download all 53 card images locally

$baseUrl = "https://deckofcardsapi.com/static/img/"
$outputDir = $PSScriptRoot

# Create output directory if it doesn't exist
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force
}

# Card codes
$suits = @("H", "D", "C", "S")  # Hearts, Diamonds, Clubs, Spades
$ranks = @("A", "2", "3", "4", "5", "6", "7", "8", "9", "0", "J", "Q", "K")  # 0 = 10

$cards = @()

# Generate all card filenames
foreach ($suit in $suits) {
    foreach ($rank in $ranks) {
        $cards += "$rank$suit.png"
    }
}

# Add card back
$cards += "back.png"

Write-Host "Downloading $($cards.Count) card images to $outputDir..."

$count = 0
foreach ($card in $cards) {
    $url = "$baseUrl$card"
    $output = Join-Path $outputDir $card
    
    if (Test-Path $output) {
        Write-Host "  Skipping $card (already exists)" -ForegroundColor Yellow
    } else {
        Write-Host "  Downloading $card..."
        try {
            Invoke-WebRequest -Uri $url -OutFile $output -ErrorAction Stop
            $count++
        } catch {
            Write-Host "  ERROR downloading $card : $_" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "Downloaded $count new images. Total cards: $($cards.Count)" -ForegroundColor Green
Write-Host "Images saved to: $outputDir" -ForegroundColor Cyan
