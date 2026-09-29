# Compares GhostAnalysis's results (<lens>_ghosts.csv, from --export-layouts) with LensHH-LT's
# values for the same unfolded ghosts (lenshh.csv, read from LensHH-LT for each exported .lhlt).
#
#   pwsh verification/compare.ps1 -Folder verification/lhlt/paper -ImageDistance 45.25 -Field 1
#
# ImageDistance: the lens's last surface to its image, to put GhostAnalysis's exit pupil (measured
# from the last surface) where LensHH-LT measures it (from the image). Field: the off-axis field
# compared, alongside the axis.
param([string]$Folder, [double]$ImageDistance, [double]$Field = 1.0, [string]$Wavelength = '')

$inv = [Globalization.CultureInfo]::InvariantCulture
function D([string]$s) { if ([string]::IsNullOrWhiteSpace($s)) { [double]::NaN } else { [double]::Parse($s, $inv) } }

$ga = Import-Csv (Get-ChildItem $Folder -Filter '*_ghosts.csv' | Select-Object -First 1).FullName
if ($Wavelength) { $ga = $ga | Where-Object { [math]::Abs((D $_.wavelength_um) - (D $Wavelength)) -lt 1e-9 } }
$lh = Import-Csv (Join-Path $Folder 'lenshh.csv')

$rows = foreach ($l in $lh) {
  $name = 'G' + ($l.ghost.Substring(1) -replace '-', ',')
  $mine = $ga | Where-Object { $_.ghost -eq $name }
  $axis = $mine | Where-Object { (D $_.field) -eq 0 -and $_.marker -eq '' } | Select-Object -First 1
  $off = $mine | Where-Object { [math]::Abs((D $_.field) - $Field) -lt 1e-12 -and $_.marker -eq '' } | Select-Object -First 1
  [pscustomobject]@{
    Ghost = $name
    'EFL diff' = (D $axis.efl) - (D $l.efl)
    'BFL diff' = (D $axis.bfd) - (D $l.bfl)
    'XPD diff' = (D $axis.exit_pupil_diameter) - (D $l.exit_pupil_diameter)
    'XP diff' = ((D $axis.exit_pupil_from_last) - $ImageDistance) - (D $l.exit_pupil_from_image)
    'chief diff' = (D $off.chief_y) - (D $l.chief_y_1deg)
    'centroid diff' = (D $off.centroid_y) - (D $l.centroid_y_1deg)
    'RMS axis %' = 100 * ((D $axis.rms_radius) / (D $l.rms_0) - 1)
    'RMS off %' = 100 * ((D $off.rms_radius) / (D $l.rms_1deg) - 1)
    'passed axis' = '{0:0.000} / {1:0.000}' -f (D $axis.transmitted), ((D $l.rays_0) / 325)
    'passed off' = '{0:0.000} / {1:0.000}' -f (D $off.transmitted), ((D $l.rays_1deg) / 325)
  }
}
$rows | Format-Table -AutoSize @{L='Ghost';E={$_.Ghost}},
  @{L='EFL';E={'{0:+0.0000;-0.0000}' -f $_.'EFL diff'}}, @{L='BFL';E={'{0:+0.0000;-0.0000}' -f $_.'BFL diff'}},
  @{L='XPD';E={'{0:+0.0000;-0.0000}' -f $_.'XPD diff'}}, @{L='XP';E={'{0:+0.0000;-0.0000}' -f $_.'XP diff'}},
  @{L='chief';E={'{0:+0.0000;-0.0000}' -f $_.'chief diff'}}, @{L='centroid';E={'{0:+0.0000;-0.0000}' -f $_.'centroid diff'}},
  @{L='RMS 0 %';E={'{0:+0.00;-0.00}' -f $_.'RMS axis %'}}, @{L='RMS f %';E={'{0:+0.00;-0.00}' -f $_.'RMS off %'}},
  @{L='passed 0 (GA / LH)';E={$_.'passed axis'}}, @{L='passed f (GA / LH)';E={$_.'passed off'}}
