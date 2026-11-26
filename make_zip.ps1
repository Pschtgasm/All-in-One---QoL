# Packt einen Ordner als Zip mit "/" in den Pfaden (Thunderstore verlangt das; Compress-Archive nimmt "\").
param([string]$Source, [string]$Target)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $Target) { Remove-Item $Target -Force }
$root = (Resolve-Path $Source).Path.TrimEnd('\') + '\'
$zip = [System.IO.Compression.ZipFile]::Open($Target, 'Create')
try {
  Get-ChildItem $Source -Recurse -File | ForEach-Object {
    $name = $_.FullName.Substring($root.Length).Replace('\', '/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $name) | Out-Null
  }
} finally { $zip.Dispose() }
