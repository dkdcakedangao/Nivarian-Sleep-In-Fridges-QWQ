param(
    [string]$RimWorldDirectory = 'E:\steam\steamapps\common\RimWorld',
    [string]$NivarianDirectory = 'E:\steam\steamapps\common\RimWorld\Mods\Nivarian',
    [string]$HarmonyAssemblyPath = 'E:\steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll',
    [string]$AlienRaceAssemblyPath = 'E:\steam\steamapps\workshop\content\294100\839005762\1.7\Assemblies\AlienRace.dll',
    [string]$DotnetPath = (Join-Path $PSScriptRoot '../../../.serena/tools/dotnet10/dotnet.exe'),
    [switch]$Deploy
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modRoot = (Resolve-Path (Join-Path $sourceRoot '..')).Path
$referenceDir = Join-Path $sourceRoot 'obj/1.7/RimUnityReferences'
[void](New-Item -ItemType Directory -Force -Path $referenceDir)

# 只提取编译引用。绝不复制 Embedded 到成品，也不导入其发布 targets。
$providerPath = Join-Path $NivarianDirectory '1.7/RU/Assemblies/RimUnity.Embedded.dll'
$provider = [Reflection.Assembly]::LoadFile($providerPath)
$payload = $provider.GetManifestResourceStream('RimUnity.Payload.zip')
if ($null -eq $payload) { throw 'Host runtime did not contain RimUnity.Payload.zip.' }
$archive = [IO.Compression.ZipArchive]::new($payload)
try {
    foreach ($name in @('RimUnity.Core.dll', 'RimUnity.dll')) {
        $entry = $archive.GetEntry("Assemblies/$name")
        if ($null -eq $entry) { throw "Missing host compile reference: $name" }
        $inputStream = $entry.Open()
        $outputStream = [IO.File]::Create((Join-Path $referenceDir $name))
        try { $inputStream.CopyTo($outputStream) }
        finally { $inputStream.Dispose(); $outputStream.Dispose() }
    }
}
finally { $archive.Dispose(); $payload.Dispose() }

$mainProject = Join-Path $PSScriptRoot 'NivarianSleepInFridges.csproj'
& $DotnetPath build $mainProject -c Release "-p:RimWorldDirectory=$RimWorldDirectory" "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath" "-p:AlienRaceAssemblyPath=$AlienRaceAssemblyPath" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Main assembly build failed; release files unchanged.' }
$main = Join-Path $PSScriptRoot 'bin/Release/net10.0/NivarianSleepInFridges.dll'
& $DotnetPath build (Join-Path $sourceRoot 'OrbitalSupport/NivarianSleepInFridges.OrbitalSupport.csproj') -c Release "-p:MainAssemblyPath=$main" "-p:RimWorldDirectory=$RimWorldDirectory" "-p:NivarianDirectory=$NivarianDirectory" "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath" "-p:RimUnityReferenceDir=$referenceDir" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Adapter build failed; release files unchanged.' }
$adapter = Join-Path $sourceRoot 'OrbitalSupport/bin/Release/net10.0/NivarianSleepInFridges.OrbitalSupport.dll'

$retiredRoot = Join-Path $sourceRoot ('obj/1.7/RetiredArtifacts-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
function Move-ObsoleteReleaseFiles([string]$root, [string]$label) {
    # 精确目标，可恢复移动；不触碰本体文件或其他模组。
    foreach ($relative in @('1.7/Assemblies/RimUnity.Embedded.dll',
        '1.7/Assemblies/NivarianSleepInFridges.OrbitalSupport.rimunity',
        '1.7/Assemblies/NivarianSleepInFridges.OrbitalSupport.rimunity.json',
        '1.7/Assemblies/NivarianSleepInFridges.OrbitalSupport.dll',
        '1.7/Languages/ChineseSimplified/Keyed/OrbitalSupportDebug.xml',
        '1.7/Languages/English/Keyed/OrbitalSupportDebug.xml')) {
        $old = Join-Path $root $relative
        if (Test-Path -LiteralPath $old) {
            $destination = Join-Path (Join-Path $retiredRoot $label) $relative
            [void](New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent))
            Move-Item -LiteralPath $old -Destination $destination
        }
    }
}
Move-ObsoleteReleaseFiles $modRoot 'Workspace'
$release = Join-Path $modRoot '1.7/Assemblies'
$adapterRelease = Join-Path $modRoot '1.7/OrbitalSupport'
[void](New-Item -ItemType Directory -Force -Path $release, $adapterRelease)
Copy-Item -LiteralPath $main -Destination $release -Force
Copy-Item -LiteralPath $adapter -Destination $adapterRelease -Force
# 1.7 不再加载 1.6 DLL；复制共用 Def，保持旧版本发布内容不变。
Copy-Item -LiteralPath (Join-Path $modRoot '1.6/Defs') -Destination (Join-Path $modRoot '1.7') -Recurse -Force

if ($Deploy) {
    $target = [IO.Path]::GetFullPath((Join-Path $RimWorldDirectory 'Mods/Nivarian-Sleep-In-Fridges-QWQ'))
    if ($target -eq $modRoot -or (Split-Path $target -Leaf) -ne 'Nivarian-Sleep-In-Fridges-QWQ') { throw 'Invalid deployment target.' }
    Move-ObsoleteReleaseFiles $target 'Game'
    & robocopy $modRoot $target /E /XD Source .git .github .serena Test Tests bin obj /XF log.txt .gitignore .gitattributes .gitmodules /R:1 /W:1 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) { throw "Deployment failed: robocopy exit $LASTEXITCODE" }
    Write-Output "Deployed: $target"
}
Write-Output 'Release complete: ordinary main DLL + fixed-path adapter DLL; no runtime provider; no Tests run.'
