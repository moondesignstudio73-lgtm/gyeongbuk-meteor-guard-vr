Add-Type -AssemblyName System.Drawing
function Measure-Notice([string]$Path) {
 $bitmap=[Drawing.Bitmap]::new((Resolve-Path -LiteralPath $Path).Path)
 try {
  $rows=[Collections.Generic.List[int]]::new(); $lum=[Collections.Generic.List[double]]::new()
  for($y=[int]($bitmap.Height*.14);$y -lt [int]($bitmap.Height*.34);$y++) {
   $count=0
   for($x=[int]($bitmap.Width*.15);$x -lt [int]($bitmap.Width*.85);$x++) {
    $c=$bitmap.GetPixel($x,$y)
    if($c.G -gt 100 -and $c.G -gt $c.R*1.4 -and $c.G -gt $c.B*1.35) {
     $count++
     if($c.G -gt 180){$channels=@($c.R,$c.G,$c.B)|ForEach-Object {$v=$_/255.0;if($v -le .04045){$v/12.92}else{[Math]::Pow(($v+.055)/1.055,2.4)}}; $lum.Add(.2126*$channels[0]+.7152*$channels[1]+.0722*$channels[2])}
    }
   }
   if($count -gt 3){$rows.Add($y)}
  }
  $bands=@();$start=-1;$last=-2
  foreach($row in $rows){if($row -gt $last+1){if($start -ge 0){$bands+=,@($start,$last)};$start=$row};$last=$row};if($start -ge 0){$bands+=,@($start,$last)}
  $luminance=($lum|Measure-Object -Average).Average
  [pscustomobject]@{path=$Path;width=$bitmap.Width;height=$bitmap.Height;inkBands=$bands;inkHeights=@($bands|ForEach-Object {$_[1]-$_[0]+1});gap=if($bands.Count -eq 2){$bands[1][0]-$bands[0][1]-1}else{-1};solidInkContrastAgainstBlack=[Math]::Round(($luminance+.05)/.05,2);method='Green pixel threshold G>100, G>1.4R, G>1.35B; rows with >3 pixels. Contrast uses mean solid ink G>180 against black panel; not antialias edges or human/HMD readability.'}
 } finally {$bitmap.Dispose()}
}
$root=$PSScriptRoot
$results=@()
Get-ChildItem "$root/Before" -Filter '*.png'|ForEach-Object {$results+=Measure-Notice $_.FullName}
Get-ChildItem "$root/ApprovedEditor" -Recurse -Filter 'stability-game-render.png' -ErrorAction SilentlyContinue | Where-Object {$_.Directory.Name -eq 'response-145'} | ForEach-Object {$results+=Measure-Notice $_.FullName}
$results|ConvertTo-Json -Depth 5|Set-Content "$root/pixel-metrics.json"
$results|Select-Object width,height,inkHeights,gap,solidInkContrastAgainstBlack


