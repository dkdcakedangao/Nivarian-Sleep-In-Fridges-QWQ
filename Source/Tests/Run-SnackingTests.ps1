$ErrorActionPreference = 'Stop'
# 隔离测试：编译实际业务源码，游戏 API 使用替身；不代表游戏内兼容性测试。
$sourceRoot = Split-Path $PSScriptRoot -Parent
$files = @(
    'Buildings\Building_FridgeBedProxy.cs',
    'Core\FridgeSnackingUtility.cs',
    'Patches\SnackingMessagePatches.cs',
    'Tests\SnackingTestDoubles.cs'
)
$parts = foreach ($file in $files) {
    (Get-Content (Join-Path $sourceRoot $file) -Raw) -replace '(?m)^using [^;]+;\r?\n', ''
}
$modSource = Get-Content (Join-Path $sourceRoot 'Core\SleepInFridgesMod.cs') -Raw
$settingsClass = $modSource.Substring($modSource.IndexOf('    public sealed class SleepInFridgesSettings'))
$parts += "namespace NivarianSleepInFridges {`n$settingsClass"
Add-Type -TypeDefinition ("using System; using System.Collections.Generic; using System.Reflection; using RimWorld; using UnityEngine; using Verse; using HarmonyLib;`n" + ($parts -join "`n"))
[NivarianSleepInFridges.SnackingTests]::Run()
