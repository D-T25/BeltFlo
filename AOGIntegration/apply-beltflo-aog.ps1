param(
    [Parameter(Mandatory = $true)]
    [string]$AogRoot
)

$ErrorActionPreference = "Stop"

function Replace-ExactlyOnce {
    param(
        [string]$Path,
        [string]$Anchor,
        [string]$Replacement
    )

    $text = Get-Content -LiteralPath $Path -Raw
    $count = ([regex]::Matches($text, [regex]::Escape($Anchor))).Count
    if ($count -ne 1) {
        throw "Expected exactly one anchor in $Path, found ${count}: $Anchor"
    }

    $text = $text.Replace($Anchor, $Replacement)
    Set-Content -LiteralPath $Path -Value $text -Encoding UTF8
}

$gpsForms = Join-Path $AogRoot "SourceCode\GPS\Forms"
if (-not (Test-Path $gpsForms)) {
    throw "AgOpenGPS source tree not found at $AogRoot"
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "BeltFloYield.cs") -Destination (Join-Path $gpsForms "BeltFloYield.cs") -Force

$udp = Join-Path $gpsForms "UDPComm.Designer.cs"
$udpAnchor = "                    case 250:"
$udpInsert = @"
                    case 0xC7: // BeltFlo delay-corrected live yield
                        {
                            ReceiveBeltFloYield(data);
                            break;
                        }

                    case 250:
"@
Replace-ExactlyOnce -Path $udp -Anchor $udpAnchor -Replacement $udpInsert

$gl = Join-Path $gpsForms "OpenGL.Designer.cs"
$glAnchor = "                    GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);"
$glInsert = @"
                    GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);

                    // BeltFlo is a display overlay only. It is intentionally drawn
                    // here on the visible map and never in oglBack, which AOG uses
                    // for automatic section overlap detection.
                    DrawBeltFloYield();
"@
Replace-ExactlyOnce -Path $gl -Anchor $glAnchor -Replacement $glInsert

Write-Host "BeltFlo live-yield integration applied to AgOpenGPS."
