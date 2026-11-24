param([Parameter(Mandatory)][string]$Compiler,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$dir=New-Item -ItemType Directory -Force $OutputDirectory
$dll=Join-Path $dir.FullName 'BedrockLoaderPresence.dll'
& $Compiler -std=c++17 -shared -static -O2 -s "$PSScriptRoot/Presence.cpp" -o $dll -ladvapi32 -lrstrtmgr
if ($LASTEXITCODE){throw 'Native presence build failed'}
Copy-Item -LiteralPath $dll -Destination "$PSScriptRoot/../../Assets/BedrockLoaderPresence.dll" -Force
