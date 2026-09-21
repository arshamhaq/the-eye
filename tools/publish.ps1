param([string]$Dotnet = "dotnet")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts\TheEye-win-x64'
& $Dotnet publish (Join-Path $root 'src\TheEye\TheEye.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Add-Type -AssemblyName System.Drawing
$petRoot = Join-Path $output 'Pets\Triangle'
$manifest = Get-Content -LiteralPath (Join-Path $petRoot 'pet.json') -Raw | ConvertFrom-Json
foreach ($animation in $manifest.animations.PSObject.Properties) {
    foreach ($frame in $animation.Value.frames) {
        $path = Join-Path $petRoot $frame
        if (!(Test-Path -LiteralPath $path)) { throw "Missing published asset: $frame" }
        $bitmap = [Drawing.Bitmap]::FromFile($path)
        try {
            if ($bitmap.Width -ne 1920 -or $bitmap.Height -ne 1080) { throw "Wrong resolution: $frame" }
            if ($frame -like '*float.png' -and $bitmap.GetPixel(0,0).A -ne 0) { throw 'Sprite background is not transparent.' }
        } finally { $bitmap.Dispose() }
        $source = Join-Path $root ('src\TheEye\Pets\Triangle\' + $frame)
        if ((Get-FileHash -LiteralPath $path).Hash -ne (Get-FileHash -LiteralPath $source).Hash) { throw "Stale published asset: $frame" }
    }
}
Write-Output "Verified every published asset, hash, resolution and sprite transparency: $output"
