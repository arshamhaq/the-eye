param([string]$Dotnet = "dotnet", [string]$OutputDirectory = "")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root 'artifacts\TheEye-win-x64' }
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
            if ($frame -like '*mountains.png') {
                if ($bitmap.Width -lt 1600 -or $bitmap.Height -lt 900) { throw "Wrong landscape resolution: $frame" }
            } elseif ($frame -like '*float.png') {
                if ($bitmap.Width -ne 912 -or $bitmap.Height -ne 1120) { throw "Sprite must retain its native resolution: $frame" }
            } elseif ($frame -like '*gaming-rest.png') {
                if ($bitmap.Width -ne 1280 -or $bitmap.Height -ne 720) { throw "Gaming artwork must retain its native resolution: $frame" }
            } elseif ($bitmap.Width -ne 2240 -or $bitmap.Height -ne 1260) { throw "Wrong landscape resolution: $frame" }
            if ($frame -like '*float.png' -and $bitmap.GetPixel(0,0).A -ne 0) { throw 'Sprite background is not transparent.' }
        } finally { $bitmap.Dispose() }
        $source = Join-Path $root ('src\TheEye\Pets\Triangle\' + $frame)
        if ((Get-FileHash -LiteralPath $path).Hash -ne (Get-FileHash -LiteralPath $source).Hash) { throw "Stale published asset: $frame" }
    }
}
Write-Output "Verified every published asset, hash, resolution and sprite transparency: $output"
