param(
    [Parameter(Mandatory = $true)]
    [string]$AgOpenWebRoot
)

$ErrorActionPreference = "Stop"

function Replace-ExactlyOnce {
    param([string]$Path,[string]$Anchor,[string]$Replacement)
    $text = Get-Content -LiteralPath $Path -Raw
    $count = ([regex]::Matches($text, [regex]::Escape($Anchor))).Count
    if ($count -ne 1) { throw "Expected exactly one anchor in $Path, found $($count): $Anchor" }
    Set-Content -LiteralPath $Path -Value ($text.Replace($Anchor, $Replacement)) -Encoding UTF8
}

$svcDir = Join-Path $AgOpenWebRoot "Shared\AgOpenWeb.Services\Coverage"
$vmDir  = Join-Path $AgOpenWebRoot "Shared\AgOpenWeb.ViewModels"
if (-not (Test-Path $svcDir) -or -not (Test-Path $vmDir)) {
    throw "AgOpenWeb source tree not found at $AgOpenWebRoot"
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "BeltFloYieldService.cs") -Destination (Join-Path $svcDir "BeltFloYieldService.cs") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "MainViewModel.BeltFloBridge.cs") -Destination (Join-Path $vmDir "MainViewModel.BeltFloBridge.cs") -Force

$iface = Join-Path $AgOpenWebRoot "Shared\AgOpenWeb.Services\Interfaces\ICoverageMapService.cs"
$ifaceAnchor = "    void AddCoveragePoint(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge);"
$ifaceInsert = @"
    void AddCoveragePoint(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge);

    /// <summary>
    /// Recolor a visible coverage quad without touching the detection bitmap,
    /// worked-area totals, or section-control state. Used by BeltFlo live yield.
    /// </summary>
    void PaintDisplayOnlyQuad(
        Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, CoverageColor color);
"@
Replace-ExactlyOnce -Path $iface -Anchor $ifaceAnchor -Replacement $ifaceInsert

$cov = Join-Path $AgOpenWebRoot "Shared\AgOpenWeb.Services\Coverage\CoverageMapService.cs"

$fieldAnchor = "    private readonly HashSet<(int, int)> _newCellsServerDedup = new();"
$fieldInsert = @"
    private readonly HashSet<(int, int)> _newCellsServerDedup = new();

    // Display-only changes (BeltFlo yield) are separate from detection coverage.
    // Key is LOCAL display pixel (same coordinate system the web client receives).
    private readonly Dictionary<(int X, int Y), CoverageColor> _displayOnlyServer = new();
"@
Replace-ExactlyOnce -Path $cov -Anchor $fieldAnchor -Replacement $fieldInsert

$text = Get-Content -LiteralPath $cov -Raw
$phrase = "    /// Fire the CoverageUpdated event if coverage has changed since last flush."
$phrasePos = $text.IndexOf($phrase)
if ($phrasePos -lt 0) { throw "Coverage insertion phrase not found." }
$idx = $text.LastIndexOf("    /// <summary>", $phrasePos)
if ($idx -lt 0) { throw "Coverage insertion point not found." }

$paintMethod = @"
    /// <summary>
    /// Paint one quad into the DISPLAY layer only. Detection bits remain untouched,
    /// so this cannot influence overlap detection or automatic section switching.
    /// </summary>
    public void PaintDisplayOnlyQuad(
        Vec2 p0v, Vec2 p1v, Vec2 p2v, Vec2 p3v, CoverageColor color)
    {
        lock (_coverageLock)
        {
            if (_displayPixels == null || !_fieldBoundsSet || _displayWidth <= 0 || _displayHeight <= 0)
                return;

            var p0 = (E: p0v.Easting, N: p0v.Northing);
            var p1 = (E: p1v.Easting, N: p1v.Northing);
            var p2 = (E: p2v.Easting, N: p2v.Northing);
            var p3 = (E: p3v.Easting, N: p3v.Northing);

            double minE = Math.Min(Math.Min(p0.E, p1.E), Math.Min(p2.E, p3.E));
            double maxE = Math.Max(Math.Max(p0.E, p1.E), Math.Max(p2.E, p3.E));
            double minN = Math.Min(Math.Min(p0.N, p1.N), Math.Min(p2.N, p3.N));
            double maxN = Math.Max(Math.Max(p0.N, p1.N), Math.Max(p2.N, p3.N));

            int absMinX = (int)Math.Floor(minE / _displayCellSize);
            int absMaxX = (int)Math.Floor(maxE / _displayCellSize);
            int absMinY = (int)Math.Floor(minN / _displayCellSize);
            int absMaxY = (int)Math.Floor(maxN / _displayCellSize);

            ushort rgb565 = (ushort)(
                ((color.R & 0xF8) << 8)
                | ((color.G & 0xFC) << 3)
                | (color.B >> 3));

            bool changed = false;
            for (int ax = absMinX; ax <= absMaxX; ax++)
            {
                int dx = ax - _displayOriginX;
                if (dx < 0 || dx >= _displayWidth) continue;

                double worldE = (ax + 0.5) * _displayCellSize;
                for (int ay = absMinY; ay <= absMaxY; ay++)
                {
                    int dy = ay - _displayOriginY;
                    if (dy < 0 || dy >= _displayHeight) continue;

                    double worldN = (ay + 0.5) * _displayCellSize;
                    if (!IsPointInTriangle(worldE, worldN, p0, p1, p2)
                        && !IsPointInTriangle(worldE, worldN, p0, p2, p3))
                        continue;

                    long index = (long)dy * _displayWidth + dx;
                    if (_displayPixels[index] == rgb565) continue;

                    _displayPixels[index] = rgb565;
                    _displayOnlyServer[(dx, dy)] = color;
                    ExpandDirty(dx, dy);

                    long tileKey = CoverageTileStore.Key(
                        ax >> CoverageTileStore.DisplayTileShift,
                        ay >> CoverageTileStore.DisplayTileShift);
                    _dirtyDisplayTiles.Add(tileKey);
                    changed = true;
                }
            }

            if (changed)
                _coverageDirty = true;
        }
    }

"@
$text = $text.Insert($idx, $paintMethod)
Set-Content -LiteralPath $cov -Value $text -Encoding UTF8

$drainAnchor = @"
        lock (_coverageLock)
        {
            if (_newCellsServer.Count == 0)
                return Array.Empty<(int, int, CoverageColor)>();
"@
$drainReplace = @"
        lock (_coverageLock)
        {
            if (_newCellsServer.Count == 0 && _displayOnlyServer.Count == 0)
                return Array.Empty<(int, int, CoverageColor)>();
"@
Replace-ExactlyOnce -Path $cov -Anchor $drainAnchor -Replacement $drainReplace

$dedupAnchor = @"
            _newCellsServerDedup.Clear();
            _newCellsServerResult.Clear();

            foreach (var (cellE, cellN, zone) in _newCellsServer)
"@
$dedupReplace = @"
            _newCellsServerDedup.Clear();
            _newCellsServerResult.Clear();

            // BeltFlo overrides go first so a normal coverage update at the same
            // display pixel cannot replace the yield color in this delta.
            foreach (var kv in _displayOnlyServer)
            {
                _newCellsServerDedup.Add(kv.Key);
                _newCellsServerResult.Add((kv.Key.X, kv.Key.Y, kv.Value));
            }
            _displayOnlyServer.Clear();

            foreach (var (cellE, cellN, zone) in _newCellsServer)
"@
Replace-ExactlyOnce -Path $cov -Anchor $dedupAnchor -Replacement $dedupReplace

$text = Get-Content -LiteralPath $cov -Raw
$clearAllPos = $text.IndexOf("    public void ClearAll()")
if ($clearAllPos -lt 0) { throw "ClearAll not found." }
$clearAnchor = "            _newCellsServer.Clear();"
$clearPos = $text.IndexOf($clearAnchor, $clearAllPos)
if ($clearPos -lt 0) { throw "ClearAll server clear not found." }
$text = $text.Insert($clearPos + $clearAnchor.Length, [Environment]::NewLine + "            _displayOnlyServer.Clear();")
Set-Content -LiteralPath $cov -Value $text -Encoding UTF8

$diFiles = @(
    "Platforms\AgOpenWeb.Desktop\DependencyInjection\ServiceCollectionExtensions.cs",
    "Platforms\AgOpenWeb.Android\DependencyInjection\ServiceCollectionExtensions.cs",
    "Platforms\AgOpenWeb.iOS\DependencyInjection\ServiceCollectionExtensions.cs"
)
foreach ($rel in $diFiles)
{
    $path = Join-Path $AgOpenWebRoot $rel
    $regAnchor = "        services.AddSingleton<ICoverageMapService, CoverageMapService>();"
    $regInsert = @"
        services.AddSingleton<ICoverageMapService, CoverageMapService>();
        services.AddSingleton<BeltFloYieldService>();
"@
    Replace-ExactlyOnce -Path $path -Anchor $regAnchor -Replacement $regInsert

    $wireAnchor = "        udpService?.SetAutoSteerService(autoSteerService);"
    $wireInsert = @"
        udpService?.SetAutoSteerService(autoSteerService);

        // Construct once so it subscribes to UDP PGN 0xC7.
        _ = serviceProvider.GetRequiredService<BeltFloYieldService>();
"@
    Replace-ExactlyOnce -Path $path -Anchor $wireAnchor -Replacement $wireInsert
}

$apply = Join-Path $AgOpenWebRoot "Shared\AgOpenWeb.ViewModels\MainViewModel.ApplyResults.cs"
$applyAnchor = "        // Status message (only if set"
$text = Get-Content -LiteralPath $apply -Raw
$pos = $text.IndexOf($applyAnchor)
if ($pos -lt 0) { throw "ApplyResults status anchor not found." }
$lineEnd = $text.IndexOf([Environment]::NewLine, $pos)
if ($lineEnd -lt 0) { throw "ApplyResults status line end not found." }
$insert = "        // BeltFlo uses the same GPS/section inputs with either guidance host." + [Environment]::NewLine +
          "        SendBeltFloGuidanceState(result);" + [Environment]::NewLine + [Environment]::NewLine
$text = $text.Insert($pos, $insert)
Set-Content -LiteralPath $apply -Value $text -Encoding UTF8

Write-Host "BeltFlo integration applied to AgOpenWeb."
