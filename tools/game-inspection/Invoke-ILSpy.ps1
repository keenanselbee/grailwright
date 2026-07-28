$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ilspy = Get-ChildItem -LiteralPath (Join-Path $root "ilspycmd") -Recurse -Filter "ilspycmd.dll" |
    Sort-Object FullName |
    Select-Object -First 1

if ($null -eq $ilspy) {
    throw "Could not find ilspycmd.dll under '$root\ilspycmd'."
}

& dotnet $ilspy.FullName @args
