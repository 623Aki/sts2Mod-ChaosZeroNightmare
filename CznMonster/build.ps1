<#
.SYNOPSIS
    编译 CznMonster 并把 Spine / 场景 / 图标资源导出成 pck。

.DESCRIPTION
    设计文档《波勒米卡设计文档》§5.0：CznMonster 已从「纯 dll」升级为「dll + pck」。
    流程：
      0. 前置检查（工程、Godot 4.5.1、Spine 扩展、本地化、.gitignore）
      1. dotnet build ── dll/pdb/json 由 csproj 的 post-build 自动复制到 mods
      2. Godot --import ── 产出 .spskel / .spatlas / .ctex
      3. Godot --export-pack ── 先导出到 .godot/build/pack/CznMonster.pck
      4. pck 自检 ── 解析文件索引，确认骨架 / 场景 / 图标 / 本地化都在包里
      5. 自检通过才把暂存 pck 移到 mods ── 失败时上一版 pck 原封不动

.PARAMETER Configuration
    构建配置，默认 Debug，可传 Release。

.PARAMETER SkipBuild
    跳过 dotnet build，只重导 pck。

.PARAMETER SkipImport
    跳过 Godot --import，确定资源没变时可用。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1

.NOTES
    Godot 调用判定、pck 文件表解析、进程占用检查等通用逻辑在
    工作区根的 build-common.ps1；本脚本只保留怪物专属的资源清单与流程。
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$SkipImport,
    # 强制完整重编译（dotnet build -t:Rebuild）。
    # 仅改了 .cs 但 DLL 时间没变时使用，见构建末尾的 DLL 时间输出。
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'

# 共用能力：Godot 调用 / pck 解析 / 进程与文件占用 / 预设校验 / 输出辅助
$CommonPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'build-common.ps1'
if (-not (Test-Path $CommonPath)) {
    throw "找不到共用脚本：$CommonPath`n它应位于工作区根（与 一键构建.bat 同级），不能单独拷贝本目录使用。"
}
. $CommonPath

# ══════════════════════════════════════════════════════════════════════════
# 配置区 —— 换机器 / 换 Steam 库位置时只改这一段
# ══════════════════════════════════════════════════════════════════════════
$ProjectDir = $PSScriptRoot
$GodotExe   = 'F:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe'
$Sts2Dir    = 'G:\SteamLibrary\steamapps\common\Slay the Spire 2'
$ModName    = 'CznMonster'
$PresetName = 'Windows Desktop'

$ModDir = Join-Path $Sts2Dir "mods\$ModName"
$PckOut = Join-Path $ModDir "$ModName.pck"
# 导出目标：先落在工程内的暂存目录（必须以 .pck 结尾，Godot 会校验扩展名），
# 自检通过后才移到 mods。失败/被杀时正式 pck 原封不动。
$PckStageDir = Join-Path $ProjectDir '.godot\build\pack'
$PckNew = Join-Path $PckStageDir "$ModName.pck"
$DllPath = Join-Path $ModDir "$ModName.dll"
$JsonPath = Join-Path $ModDir "$ModName.json"
$ImportedDir = Join-Path $ProjectDir '.godot\imported'
$PresetPath = Join-Path $ProjectDir 'export_presets.cfg'
$GodotLogDir = Join-Path $ProjectDir '.godot\build'

# ──────────────────────────────────────────────────────────────────────────
# 规模下限 / 体积区间
#
# 实测当前 pck：约 5.84 MB、222 条文件表条目、.spskel 9 / .spatlas 9 / .ctex 20。
# spine 扩展没生效时 pck 会缩到 1 MB 以下，导入产物整体缺失。
# 下面留了余量：既能拦住故障态，又允许正常增删少量资源。
# ──────────────────────────────────────────────────────────────────────────
$MinEntryCount  = 180
$MinSpskelCount = 8
$MinSpatlasCount = 8
$MinCtexCount   = 15
$MinPckBytes    = 2MB
$MaxPckBytes    = 200MB

# ──────────────────────────────────────────────────────────────────────────
# 必需导入产物（前缀）
#
# 指向 .godot/imported/ 下的产物 —— 真正承载骨架二进制、图集描述、纹理数据。
# 后缀是导入器按源文件内容算出的哈希，无法写死，只能匹配前缀。
# 六个形态共用 polemica.skel/.atlas/.png 与各自的骨架（见 pck 实测条目）。
# ──────────────────────────────────────────────────────────────────────────
$RequiredPrefix = @(
    '.godot/imported/polemica.skel-',
    '.godot/imported/polemica.atlas-',
    '.godot/imported/polemica.png-',
    '.godot/imported/polemica_fatal_b.skel-',
    '.godot/imported/polemica_fatal_b.atlas-',
    '.godot/imported/polemica_fatal_f.skel-',
    '.godot/imported/polemica_fatal_f.atlas-',
    '.godot/imported/polemica_fatal_sub.skel-',
    '.godot/imported/polemica_fatal_sub.atlas-',
    '.godot/imported/polemica_legion_soldier.skel-',
    '.godot/imported/polemica_legion_shield.skel-',
    '.godot/imported/polemica_rock_shell_insect.skel-',
    '.godot/imported/polemica_wing_shell_insect.skel-',
    '.godot/imported/polemica_shell_insect.skel-'
)

<#
.SYNOPSIS
    把指定配置的编译产物复制到游戏 mods 目录。

.DESCRIPTION
    必须在 Godot --export-pack 之后再调用一次：Godot 导出会触发 ExportRelease 构建，
    csproj 的部署 Target 已显式排除 ExportRelease，所以最终部署以这里为准。
#>
function Copy-ModArtifacts {
    param([Parameter(Mandatory)][string]$SourceDll)

    if (-not (Test-Path $SourceDll)) { throw "编译产物不存在：$SourceDll" }
    New-Item -ItemType Directory -Force -Path $ModDir | Out-Null
    Copy-Item $SourceDll $DllPath -Force

    $sourcePdb = [IO.Path]::ChangeExtension($SourceDll, '.pdb')
    if (Test-Path $sourcePdb) {
        Copy-Item $sourcePdb (Join-Path $ModDir "$ModName.pdb") -Force
    }

    $manifest = Join-Path $ProjectDir "$ModName.json"
    if (-not (Test-Path $manifest)) {
        throw "工程根缺少清单 json：$manifest"
    }
    Copy-Item $manifest $JsonPath -Force

    $deployed = Get-Item $DllPath
    Write-Note "已部署：$SourceDll -> $DllPath"
    Write-Note "         $($deployed.LastWriteTime)"
}


# ══════════════════════════════════════════════════════════════════════════
# 主流程
# ══════════════════════════════════════════════════════════════════════════
try {
    Write-Step '前置检查'

    if (-not (Test-Path (Join-Path $ProjectDir "$ModName.csproj"))) {
        throw "当前目录不是 $ModName 工程：$ProjectDir"
    }
    if (-not (Test-Path $GodotExe)) {
        throw "找不到 Godot 4.5.1：$GodotExe"
    }
    if (-not (Test-Path (Join-Path $ProjectDir 'bin\spine_godot_extension.gdextension'))) {
        throw '缺少 bin\spine_godot_extension.gdextension（Spine 扩展，铁律 7）'
    }
    # 导出预设：导出开关一旦被改坏，pck 内容会静默缺斤少两，这里提前拦。
    # 解析按 [preset.N] 分段，避免同一份 cfg 里别的预设把值盖掉。
    $null = Assert-ExportPreset -Path $PresetPath -PresetName $PresetName `
        -ExpectedPckPath $PckOut -ManifestName "$ModName.json"
    Write-Ok "导出预设「$PresetName」开关正常"

    # .gitignore / bin/.gdignore 若排除了 bin/，导出后骨架会全灭
    $binExcluded = Test-BinExcluded -ProjectDir $ProjectDir
    if ($binExcluded) {
        throw "$binExcluded`n这会让 spine 骨架整体丢失。"
    }

    foreach ($lang in @('zhs', 'eng')) {
        foreach ($table in @('settings.json', 'monsters.json', 'encounters.json', 'powers.json')) {
            # 游戏只认 res://<mod id>/localization/...，所以文件必须放在 CznMonster/localization 下
            $p = Join-Path $ProjectDir "CznMonster\localization\$lang\$table"
            if (-not (Test-Path $p)) { throw "缺少本地化文件：$p" }
        }
    }

    # 只关会锁住本工程文件的进程：
    #   - 运行中的游戏会锁住 mods\<mod>.dll，让部署报 MSB3021
    #   - 打开了本工程的 Godot 会锁住 bin\windows\*.dll，扩展加载失败会让骨架整体从 pck 消失
    # 别的工程 / 别人的编辑器不会被误伤；文件锁检测是最终判据。
    $spineDlls = @(Get-ChildItem (Join-Path $ProjectDir 'bin\windows') -Filter '*.dll' -ErrorAction SilentlyContinue)
    $lockTargets = @($DllPath) + @($spineDlls | Select-Object -ExpandProperty FullName)
    $null = Stop-BlockingProcesses -ProjectDir $ProjectDir -LockPaths $lockTargets

    Write-Ok '工程 / Godot / Spine 扩展 / 本地化 / 资源排除检查通过'

    $intermediateDll = Join-Path $ProjectDir ".godot\mono\temp\bin\$Configuration\$ModName.dll"
    $dllBefore = if (Test-Path $intermediateDll) { (Get-Item $intermediateDll).LastWriteTime } else { $null }

    if (-not $SkipBuild) {
        $buildLabel = if ($Rebuild) { "$Configuration，Rebuild" } else { $Configuration }
        Write-Step "编译 C#（$buildLabel）"
        Invoke-DotnetBuild -ProjectDir $ProjectDir -ProjectFile "$ModName.csproj" `
            -Configuration $Configuration -Rebuild:$Rebuild
        Write-Ok '编译成功'

        if (Test-Path $intermediateDll) {
            $dllAfter = (Get-Item $intermediateDll).LastWriteTime
            Write-Note "中间 DLL：$intermediateDll"
            Write-Note "          $dllAfter"
            if ($dllBefore -and $dllAfter -eq $dllBefore) {
                $newestSource = Get-NewestSourceFile -ProjectDir $ProjectDir
                if ($newestSource -and $newestSource.LastWriteTime -gt $dllBefore) {
                    Write-Warn "DLL 时间未变化，但源文件更新：$($newestSource.FullName)"
                    Write-Warn 'MSBuild 没有重新编译，请加 -Rebuild 重试'
                }
                else {
                    Write-Note 'DLL 未重编译（源文件没有比 DLL 更新，增量构建正常跳过）。'
                }
            }
            Copy-ModArtifacts -SourceDll $intermediateDll
        }
        else {
            Write-Warn "找不到中间 DLL（$intermediateDll），以 csproj 的 post-build 部署结果为准"
        }
    }
    else {
        Write-Note '已跳过 dotnet build（-SkipBuild）：DLL 不会更新，只会重导 pck'
    }

    # 部署断言：mods 里必须有 dll + 清单，且 dll 不能比源码旧。
    # 少了这一步，中间 DLL 路径写错时会静默用上一次的旧 dll 打包。
    if (-not (Test-Path $DllPath)) {
        throw "mods 目录里没有 dll：$DllPath`n（csproj 的 post-build 会把 dll 复制过去；-SkipBuild 时请先完整构建一次）"
    }
    if (-not (Test-Path $JsonPath)) {
        throw "mods 目录里没有清单 json：$JsonPath`n请确认工程根有 $ModName.json"
    }

    $dllItem = Get-Item $DllPath
    if ($SkipBuild) {
        Write-Note "dll 已存在（$($dllItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))，未校验是否为最新编译产物）"
    }
    else {
        $newestSource = Get-NewestSourceFile -ProjectDir $ProjectDir
        if ($newestSource -and $dllItem.LastWriteTime -lt $newestSource.LastWriteTime.AddSeconds(-10)) {
            $msg = "mods 目录的 dll 比最新源码还旧，部署没生效：`n" +
            "  dll  $($dllItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))`n" +
            "  源码 $($newestSource.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))  $($newestSource.Name)`n" +
            '先重跑一次；仍然如此就加 -Rebuild 强制完整重编译。'
            throw $msg
        }
    }

    $jsonText = Get-Content -Path $JsonPath -Raw
    if ($jsonText -notmatch '"has_pck"\s*:\s*true') {
        Write-Warn "$ModName.json 里 has_pck 不是 true，游戏可能不会挂载 pck"
    }
    if ($jsonText -notmatch '"has_dll"\s*:\s*true') {
        Write-Warn "$ModName.json 里 has_dll 不是 true，游戏可能不会加载 dll"
    }
    Write-Ok "dll 已就位（$([math]::Round($dllItem.Length / 1KB, 1)) KB），清单 json 已就位"

    if (-not $SkipImport) {
        Write-Step 'Godot 资源导入（--import）'
        $imp = Invoke-GodotTimed -GodotExe $GodotExe -LogDir $GodotLogDir `
            -Arguments @('--headless', '--path', $ProjectDir, '--import') `
            -LogName 'godot_import' -TimeoutSec 600
        if ($imp.Status -ne 'exited') {
            Write-Warn "--import 以 $($imp.Status) 结束（耗时 $($imp.ElapsedSec) 秒），导入产物是否齐全由下面判定"
        }
        $imp.Log | Select-Object -Last 12 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
        Write-Ok '资源导入完成'
    }
    else {
        Write-Note '已跳过 Godot --import'
    }

    # 导入产物断言：扩展没生效时继续导出必然丢骨架
    $importCounts = Assert-ImportArtifacts -ImportedDir $ImportedDir
    Write-Ok "导入产物就位：.spskel $($importCounts.Spskel) 个、.spatlas $($importCounts.Spatlas) 个"

    Write-Step '导出 pck（--export-pack）'
    New-Item -ItemType Directory -Force -Path $ModDir | Out-Null
    # 先导出到工程内的暂存目录（.godot/build/pack，不会被扫进包），
    # 自检全部通过后才移到 mods。这样导出中途失败 / 进程被强杀时，
    # mods 里仍是上一版可用的 pck；那个目录也不会有半成品干扰。
    New-Item -ItemType Directory -Force -Path $PckStageDir | Out-Null
    Remove-Item $PckNew -Force -ErrorAction SilentlyContinue

    $exp = Invoke-GodotTimed -GodotExe $GodotExe -LogDir $GodotLogDir `
        -Arguments @('--headless', '--path', $ProjectDir, '--export-pack', $PresetName, $PckNew) `
        -LogName 'godot_export' -ArtifactPath $PckNew -ArtifactMinBytes $MinPckBytes -TimeoutSec 900
    $exp.Log | Select-Object -Last 12 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

    # 判定只认产物：PS 5.1 读 Start-Process 的 ExitCode 常为 $null，
    # 且 Godot 写完 pck 后经常不退出、是我们强杀的，退出码本来就无意义。
    if (-not (Test-Path $PckNew)) {
        throw "Godot 导出没有产出 pck（结束状态 $($exp.Status)）。完整日志：$($exp.LogDir)"
    }
    if ($exp.Status -eq 'artifact-killed') {
        Write-Note "Godot 写完 pck 后没有自行退出，已强制结束（导出耗时 $($exp.ElapsedSec) 秒）"
    }
    elseif ($exp.Status -eq 'timeout-killed') {
        Write-Warn "Godot 导出超时被强制结束（耗时 $($exp.ElapsedSec) 秒），pck 是否完整交由自检判定"
    }
    Write-Ok "pck 已生成（待自检）：$PckNew（$(Format-Size ([long](Get-Item $PckNew).Length))，导出耗时 $($exp.ElapsedSec) 秒）"

    Write-Step 'pck 自检'
    $pckSize = [long](Get-Item $PckNew).Length
    Write-Note ("pck 体积 {0}" -f (Format-Size $pckSize))
    if ($pckSize -lt $MinPckBytes -or $pckSize -gt $MaxPckBytes) {
        throw ("pck 体积 {0} 超出预期区间 {1}–{2}。`n太小通常是骨架 / 贴图没导进去，排查顺序：`n  ① bin/ 是否被 .gitignore 或 .gdignore 排除 ② bin\windows\*.dll 是否被占用 ③ Godot --import 是否报错" -f `
            (Format-Size $pckSize), (Format-Size $MinPckBytes), (Format-Size $MaxPckBytes))
    }

    $index = Read-PckIndex -Path $PckNew
    Write-Note "文件表条目：$($index.Count)，格式 ver=$($index.Version) pack=$($index.PackVersion)"
    if ($index.Version -ne '4.5.1') {
        throw "pck 格式版本是 $($index.Version)，不是游戏要求的 4.5.1 —— 多半是用了更高版本的 Godot 导出。"
    }
    if ($index.Count -lt $MinEntryCount) {
        throw "pck 只有 $($index.Count) 条条目（下限 $MinEntryCount），资源疑似大面积缺失。"
    }

    $required = @(
        'CznMonster/animations/monsters/polemica/polemica.skel.import',
        'CznMonster/animations/monsters/polemica/polemica.atlas.import',
        'CznMonster/animations/monsters/polemica/polemica.png.import',
        'CznMonster/animations/monsters/polemica/polemica_skel_data.tres',
        'CznMonster/animations/monsters/polemica_legion_soldier/polemica_legion_soldier_skel_data.tres',
        'CznMonster/animations/monsters/polemica_legion_shield/polemica_legion_shield_skel_data.tres',
        'CznMonster/animations/monsters/polemica_rock_shell_insect/polemica_rock_shell_insect_skel_data.tres',
        'CznMonster/animations/monsters/polemica_wing_shell_insect/polemica_wing_shell_insect_skel_data.tres',
        'CznMonster/animations/monsters/polemica_shell_insect/polemica_shell_insect_skel_data.tres',
        'CznMonster/images/map/polemica_boss_icon.png.import',
        'images/ui/run_history/polemica_boss.png.import',
        'images/ui/run_history/polemica_boss_outline.png.import',
        'scenes/creature_visuals/polemica.tscn',
        'scenes/creature_visuals/polemica_legion_soldier.tscn',
        'scenes/creature_visuals/polemica_legion_shield.tscn',
        'scenes/creature_visuals/polemica_rock_shell_insect.tscn',
        'scenes/creature_visuals/polemica_wing_shell_insect.tscn',
        'scenes/creature_visuals/polemica_shell_insect.tscn',
        'scenes/encounters/polemica_boss.tscn',
        # ── §1.2.1 ③ 处决/换防演出（三层特效 + 两个 VFX 场景）──
        'CznMonster/animations/effects/polemica_fatal_b/polemica_fatal_b.skel.import',
        'CznMonster/animations/effects/polemica_fatal_b/polemica_fatal_b.atlas.import',
        'CznMonster/animations/effects/polemica_fatal_b/1001013_fatal_eff_b.png.import',
        'CznMonster/animations/effects/polemica_fatal_b/polemica_fatal_b_skel_data.tres',
        'CznMonster/animations/effects/polemica_fatal_f/polemica_fatal_f.skel.import',
        'CznMonster/animations/effects/polemica_fatal_f/polemica_fatal_f.atlas.import',
        'CznMonster/animations/effects/polemica_fatal_f/1001013_fatal_eff_f.png.import',
        'CznMonster/animations/effects/polemica_fatal_f/polemica_fatal_f_skel_data.tres',
        'CznMonster/animations/effects/polemica_fatal_sub/polemica_fatal_sub.skel.import',
        'CznMonster/animations/effects/polemica_fatal_sub/polemica_fatal_sub.atlas.import',
        'CznMonster/animations/effects/polemica_fatal_sub/1001013_fatal_sub.png.import',
        'CznMonster/animations/effects/polemica_fatal_sub/polemica_fatal_sub_skel_data.tres',
        'scenes/vfx/polemica_fatal_b.tscn',
        'scenes/vfx/polemica_fatal_f.tscn',
        # ── §1.2.1 ① 28 条采样（ogg 由 Godot 导入）──
        'CznMonster/audio/sfx/polemica/1001013/se_corpsleader_formica_normal_enter.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_corpsleader_formica_normal_skill1_3.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_corpsleader_formica_normal_skill2_3.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_corpsleader_formica_normal_skill3.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_corpsleader_formica_normal_skill4.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_1001013_normal_fatal.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_1001013_normal_death.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_1001013_groan_1.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_1001013_groan_2.ogg.import',
        'CznMonster/audio/sfx/polemica/1001013/se_1001013_groan_3.ogg.import',
        'CznMonster/audio/sfx/polemica/1001009/se_coorte_normal_enter.ogg.import',
        'CznMonster/audio/sfx/polemica/1001009/se_coorte_normal_skill1.ogg.import',
        'CznMonster/audio/sfx/polemica/1001009/se_coorte_normal_skill2.ogg.import',
        'CznMonster/audio/sfx/polemica/1001010/se_armored_coorte_normal_enter.ogg.import',
        'CznMonster/audio/sfx/polemica/1001010/se_armored_coorte_normal_skill1.ogg.import',
        'CznMonster/audio/sfx/polemica/1001010/se_armored_coorte_normal_skill2.ogg.import',
        'CznMonster/audio/sfx/polemica/1001004/se_shell_insect_normal_enter.ogg.import',
        'CznMonster/audio/sfx/polemica/1001004/se_shell_insect_normal_skill1.ogg.import',
        'CznMonster/audio/sfx/polemica/1001004/se_shell_insect_normal_skill2.ogg.import',
        'CznMonster/audio/sfx/polemica/1001004/se_1001004_normal_fatal.ogg.import',
        'CznMonster/audio/sfx/polemica/1001018/se_shell_insect_normal_enter.ogg.import',
        'CznMonster/audio/sfx/polemica/1001018/se_shell_insect_normal_skill1.ogg.import',
        'CznMonster/audio/sfx/polemica/1001018/se_shell_insect_normal_skill2.ogg.import',
        'CznMonster/audio/sfx/polemica/1001018/se_1001018_normal_fatal.ogg.import',
        'CznMonster/audio/sfx/polemica/1001019/se_shell_insect_normal_enter.ogg.import',
        'CznMonster/audio/sfx/polemica/1001019/se_shell_insect_normal_skill1.ogg.import',
        'CznMonster/audio/sfx/polemica/1001019/se_shell_insect_normal_skill2.ogg.import',
        'CznMonster/audio/sfx/polemica/1001019/se_1001019_normal_fatal.ogg.import',
        'images/powers/polemica_brood.png.import',
        'images/packed/powers/polemica_brood.png.import',
        'images/atlases/power_atlas.sprites/polemica_brood.tres',
        'images/powers/polemica_brood_power.png.import',
        'images/packed/powers/polemica_brood_power.png.import',
        'images/atlases/power_atlas.sprites/polemica_brood_power.tres',
        'images/powers/czn_action_count_power.png.import',
        'images/packed/powers/czn_action_count_power.png.import',
        'images/atlases/power_atlas.sprites/czn_action_count_power.tres',
        'CznMonster/localization/zhs/monsters.json',
        'CznMonster/localization/eng/monsters.json',
        'CznMonster/localization/zhs/encounters.json',
        'CznMonster/localization/zhs/powers.json'
    )

    # 条目数 / 导入产物计数 / 关键资源命中一次遍历完成
    $coverage = Test-PckContent -Index $index -RequiredExact $required -RequiredPrefix $RequiredPrefix

    $scale = ".spskel $($coverage.Spskel) / .spatlas $($coverage.Spatlas) / .ctex $($coverage.Ctex)"
    if ($coverage.Spskel -lt $MinSpskelCount -or $coverage.Spatlas -lt $MinSpatlasCount -or $coverage.Ctex -lt $MinCtexCount) {
        throw "pck 里导入产物数量不足（$scale），下限 .spskel $MinSpskelCount / .spatlas $MinSpatlasCount / .ctex $MinCtexCount。`n骨架多半丢了。"
    }
    Write-Ok "pck 内导入产物数量正常（$scale）"

    if ($coverage.MissingExact.Count -gt 0) {
        Write-Warn 'pck 缺少以下条目：'
        $coverage.MissingExact | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        throw 'pck 自检失败'
    }

    if ($coverage.MissingPrefix.Count -gt 0) {
        throw "pck 缺少导入产物（前缀）：`n  - $($coverage.MissingPrefix -join "`n  - ")`n说明源资源在，但导入产物没进 pck。"
    }

    if ($index.TailBytes -ne 0) {
        Write-Warn "文件表解析后有 $($index.TailBytes) 字节尾数据（可能是格式扩展），非致命。"
    }

    # 自检通过才把新包换上去（同盘 Move 是重命名，瞬间完成）
    Move-Item $PckNew $PckOut -Force
    Write-Ok '骨架 / 场景 / 图标 / 本地化 全部在 pck 内'
    Write-Ok "pck 已更新：$PckOut（$(Format-Size ([long](Get-Item $PckOut).Length))）"

    # Godot 导出会触发 ExportRelease 构建；csproj 已不再自动复制，
    # 这里在 pck 自检通过后再部署一次显式编译的 Debug DLL。
    # 顺序很重要：自检失败时 mods 里应保持「旧 dll + 旧 pck」的一致状态。
    if (-not $SkipBuild -and (Test-Path $intermediateDll)) {
        Copy-ModArtifacts -SourceDll $intermediateDll
    }

    Write-Step '交付物'
    Show-Deliverables -Directory $ModDir

    Write-Host '构建完成' -ForegroundColor Green
    Write-Host "  $ModDir" -ForegroundColor Green
    Write-Host ''
    exit 0
}
catch {
    # 正式 pck 全程没被碰过，这里只需清掉暂存区的半成品
    # （进程被强杀时 catch 不会执行，但残留文件下次导出前也会被清掉）
    Remove-Item $PckNew -Force -ErrorAction SilentlyContinue
    Write-Host ''
    Write-Host "构建失败：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  mods 里的上一版 pck 未被改动：$PckOut" -ForegroundColor Yellow
    exit 1
}