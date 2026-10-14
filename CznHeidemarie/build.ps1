<#
.SYNOPSIS
    编译 CznHeidemarie 并导出 pck 到游戏的 mods 目录。

.DESCRIPTION
    把「打包踩坑与解决方案.md」第七节的标准流程固化成一条命令，
    每一步都带前置校验和产物自检，避免重新踩已知的坑。

    执行顺序：
      0. 前置检查（.gitignore / spine 扩展 / 占用 DLL 的 Godot 进程）
      1. dotnet build  —— dll/json/pdb 由 csproj 的 post-build 自动复制到 mods
      2. Godot --import —— 生成 .spskel / .spatlas / .ctex
      3. Godot --export-pack —— 产出 pck 到 mods 目录
      4. 自检：pck 体积 + 关键资源是否齐全 + 工程内有没有混进 src/

.PARAMETER Configuration
    编译配置，默认 Debug。可传 Release。

.PARAMETER SkipBuild
    跳过 dotnet build（只重导 pck 时用；注意此时 dll 不会更新）。

.PARAMETER SkipImport
    跳过 Godot --import（确认资源没变时可省几秒）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1

.EXAMPLE
    # 只重新导一次 pck，不重新编译
    .\build.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$SkipImport
)

$ErrorActionPreference = 'Stop'

# ══════════════════════════════════════════════════════════════════════
# 路径配置 —— 换机器 / 换 Steam 库位置时只改这一段
# ══════════════════════════════════════════════════════════════════════

# 工程根目录（脚本所在目录，不要改成相对路径）
$ProjectDir = $PSScriptRoot

# 必须用 4.5.1 的完整文件名：同目录下还放着 4.7.2，无版本名的别名会认错。
# 4.7.2 编出的资源 4.5.1 的游戏读不了（见踩坑文档第一节）。
$GodotExe = 'F:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe'

# 游戏安装目录
$Sts2Dir = 'G:\SteamLibrary\steamapps\common\Slay the Spire 2'

# mod id，必须与 CznHeidemarie.json 的 id 一致
$ModName = 'CznHeidemarie'

# 导出预设名（含空格，调用时用 & 而不是 Start-Process）
$PresetName = 'Windows Desktop'

# ── 由上面推导，一般不用改 ──
$ModDir = Join-Path $Sts2Dir "mods\$ModName"
$PckOut = Join-Path $ModDir "$ModName.pck"
$ImportedDir = Join-Path $ProjectDir '.godot\imported'
$ExtListPath = Join-Path $ProjectDir '.godot\extension_list.cfg'
$GdextPath = Join-Path $ProjectDir 'bin\spine_godot_extension.gdextension'

# pck 里必须能找到的资源，少任何一个都说明导出出了问题
$RequiredInPck = @(
    'CznHeidemarie/animations/model/30093.skel',
    '30093.skel-',                    # .spskel（骨架二进制，含动画时间轴）
    '30093.atlas-',                   # .spatlas
    '30093.png-',                     # .ctex
    'bin/spine_godot_extension',      # 扩展本体，缺了说明 bin/ 被排除
    'localization/zhs/cards.json',
    'localization/zhs/characters.json',
    'localization/zhs/settings.json',
    'scenes/creature_visuals/heidemarie.tscn',
    'scenes/merchant/characters/heidemarie_merchant.tscn',
    'scenes/rest_site/characters/heidemarie_rest_site.tscn',
    'scenes/combat/energy_counters/heidemarie_energy_counter.tscn',
    'scenes/vfx/card_trail_heidemarie.tscn',
    'scenes/heidemarie_battle_ready.tscn',          # 出牌特写立绘
    'CznHeidemarie/animations/battle_ready/30093_battle_ready.skel',
    '30093_battle_ready.skel-',                     # 特写骨架的 .spskel
    '30093_battle_ready.png-',                      # 特写贴图的 .ctex
    'CznHeidemarie/audio/sfx/se_30093_attack_play1.ogg',   # 战斗音效（.bank 导出）
    'CznHeidemarie/audio/voice/vo_30093_attack_01.ogg',    # 角色语音（.bank 导出）
    'CznHeidemarie/images/defeat/heidemarie_defeat_cg_1.webp'  # 败北 CG
)

# pck 正常体积下限：低于这个值基本就是骨架丢了（正常约 3.6 MB）
$MinPckBytes = 1MB

# ══════════════════════════════════════════════════════════════════════
# 输出辅助
# ══════════════════════════════════════════════════════════════════════

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host "=== $Text ===" -ForegroundColor Cyan
}

function Write-Ok([string]$Text) {
    Write-Host "  [OK] $Text" -ForegroundColor Green
}

function Write-Note([string]$Text) {
    Write-Host "  [..] $Text" -ForegroundColor Gray
}

function Write-Warn([string]$Text) {
    Write-Host "  [!!] $Text" -ForegroundColor Yellow
}

# 调用 Godot CLI。
#
# Godot 会往 stderr 写大量 ERROR —— 其中包括 mod 工程对游戏本体资源的跨 pck 引用
# （res://src/... 、res://shaders/... 等）。这些在编辑器和导出阶段都是预期噪音，
# 不影响产物。但外部程序的 stderr 在 $ErrorActionPreference='Stop' 下会被
# PowerShell 包装成终止错误，直接把脚本打断，所以这里临时降级为 Continue。
function Invoke-Godot {
    param([string[]]$Arguments)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $GodotExe @Arguments 2>&1
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

# ══════════════════════════════════════════════════════════════════════
# 主流程
# ══════════════════════════════════════════════════════════════════════

try {
    # ── 0. 前置检查 ───────────────────────────────────────────────────
    Write-Step '前置检查'

    if (-not (Test-Path $GodotExe)) {
        throw "找不到 Godot 4.5.1：$GodotExe`n请修改脚本顶部的 `$GodotExe 指向本机的 Godot 4.5.1（mono）可执行文件。"
    }
    Write-Ok "Godot 4.5.1 就位"

    if (-not (Test-Path (Join-Path $ProjectDir "$ModName.csproj"))) {
        throw "当前目录不是 $ModName 工程根：$ProjectDir"
    }

    # 坑一：.gitignore 里的 bin/ 会让 Godot 导出时跳过 spine 导入器，
    # 导致 .skel/.atlas 连同 .spskel/.spatlas 整体从 pck 丢失。
    $gitignorePath = Join-Path $ProjectDir '.gitignore'
    if (Test-Path $gitignorePath) {
        if (Select-String -Path $gitignorePath -Pattern '^\s*/?bin/?\s*$' -Quiet) {
            throw ".gitignore 里有 bin/ —— 导出后骨架会全部丢失！`n参见 打包踩坑与解决方案.md 第二节，删掉那一行再重试。"
        }
        Write-Ok '.gitignore 没有排除 bin/'
    }

    # 坑二：spine 扩展必须在位，且已登记到 .godot/extension_list.cfg
    if (-not (Test-Path $GdextPath)) {
        throw "缺少 spine 扩展：$GdextPath`n可从 F:\Tools\Godot_v4.5.1-stable_mono_win64\spine-godot-4.2\bin 恢复。"
    }
    if (-not (Test-Path $ExtListPath)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $ExtListPath) | Out-Null
        Set-Content -Path $ExtListPath -Value 'res://bin/spine_godot_extension.gdextension' -Encoding UTF8
        Write-Warn 'extension_list.cfg 缺失，已补写'
    }
    Write-Ok 'spine 扩展与登记就位'

    # 坑三：两类进程必须在构建前关掉。
    #   - Godot 编辑器：锁住 bin\windows\*.dll，扩展加载失败会导致骨架从 pck 消失；
    #   - 运行中的游戏：锁住 mods 目录里的 <mod>.dll，让编译后的复制步骤报 MSB3021
    #     （Access to the path ... is denied），编译白跑一趟。
    # 两者统一按进程名匹配后强杀。
    $blockingProcs = @(Get-Process -Name 'Godot*', 'SlayTheSpire2' -ErrorAction SilentlyContinue | Where-Object { $_.Threads.Count -gt 0 })
    if ($blockingProcs.Count -gt 0) {
        $names = ($blockingProcs | Select-Object -ExpandProperty ProcessName -Unique) -join ', '
        Write-Warn "检测到 $($blockingProcs.Count) 个占用进程（$names），会锁住扩展 DLL 或 mods 目录，正在关闭"
        # 单个进程可能在检测与终止之间自行退出，或权限不足，都交给下面的复核统一报错。
        $blockingProcs | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2

        # 复核：Stop-Process 可能因权限不足被静默忽略，这里确认进程真的退出，
        # 否则后续 dotnet build 仍会卡在复制那一步，报错信息还看不出根因。
        $stillAlive = @(Get-Process -Name 'Godot*', 'SlayTheSpire2' -ErrorAction SilentlyContinue | Where-Object { $_.Threads.Count -gt 0 })
        if ($stillAlive.Count -gt 0) {
            throw "仍有 $($stillAlive.Count) 个进程未能关闭：$(($stillAlive | Select-Object -ExpandProperty ProcessName -Unique) -join ', ')`n请用管理员权限手工结束它们后再重试。"
        }
        Write-Ok '占用进程已关闭'
    }

    # 兜底：工程里不该出现 src/ 目录（会被打进 pck 覆盖游戏本体脚本）
    if (Test-Path (Join-Path $ProjectDir 'src')) {
        throw "工程里存在 src/ 目录 —— 它会被打进 pck 并覆盖游戏本体的 res://src/，请删掉后再打包。"
    }
    Write-Ok '工程内无 src/ 目录'

    # ── 1. 编译 C# ───────────────────────────────────────────────────
    if ($SkipBuild) {
        Write-Step '跳过编译（-SkipBuild）'
        Write-Note 'dll 不会更新，只有 pck 会重新导出'
    }
    else {
        Write-Step "编译 C#（$Configuration）"
        Push-Location $ProjectDir
        try {
            & dotnet build "$ModName.csproj" -c $Configuration -v minimal --nologo
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet build 失败（退出码 $LASTEXITCODE）"
            }
        }
        finally {
            Pop-Location
        }

        $dllPath = Join-Path $ModDir "$ModName.dll"
        if (-not (Test-Path $dllPath)) {
            throw "编译成功但 mods 目录里没有 dll：$dllPath"
        }
        Write-Ok "dll 已就位（$([math]::Round((Get-Item $dllPath).Length / 1KB, 1)) KB）"
    }

    # ── 2. 导入资源 ─────────────────────────────────────────────────
    if ($SkipImport) {
        Write-Step '跳过资源导入（-SkipImport）'
    }
    else {
        Write-Step '导入资源（Godot --import）'
        # --import 在 headless 下收尾时可能自己崩一次（C# 编辑器插件的已知噪音），
        # 所以不看退出码，改看导入产物是否真的生成。
        # 只挑真正要和 spine 扩展 / mod 自有资源有关的报错，其余（如 res://src/...）
        # 是跨 pck 引用的预期噪音，不刷屏。
        Invoke-Godot @('--headless', '--path', $ProjectDir, '--import') |
            Select-String -Pattern 'Cannot open GDExtension|Error loading extension|Failed loading resource: res://CznHeidemarie' |
            ForEach-Object { Write-Warn $_.ToString().Trim() }
    }

    $spskel = @(Get-ChildItem $ImportedDir -Filter '*.spskel' -ErrorAction SilentlyContinue)
    $spatlas = @(Get-ChildItem $ImportedDir -Filter '*.spatlas' -ErrorAction SilentlyContinue)
    if ($spskel.Count -eq 0 -or $spatlas.Count -eq 0) {
        throw "缺少 spine 导入产物（.spskel/.spatlas）—— 扩展没生效，导出会丢骨架。`n先确认没有 Godot 进程占用 bin\windows\*.dll，再重跑。"
    }
    Write-Ok "骨架产物就位：$($spskel.Name) $([math]::Round($spskel[0].Length / 1MB, 2)) MB"

    # ── 3. 导出 pck ─────────────────────────────────────────────────
    Write-Step '导出 pck'
    New-Item -ItemType Directory -Force -Path $ModDir | Out-Null
    Remove-Item $PckOut -Force -ErrorAction SilentlyContinue

    # 预设名含空格，必须按数组逐项传参；
    # Start-Process -ArgumentList @('--export-pack','Windows Desktop',$out)
    # 会被按空格拆成 'Windows' + 'Desktop'，报 Invalid export preset name: Windows。
    $godotOut = Invoke-Godot @('--headless', '--path', $ProjectDir, '--export-pack', $PresetName, $PckOut)
    $exportExitCode = $LASTEXITCODE
    $godotOut | Select-Object -Last 3 | ForEach-Object { Write-Note $_.ToString().Trim() }

    if ($exportExitCode -ne 0) {
        throw "导出失败（退出码 $exportExitCode）"
    }
    if (-not (Test-Path $PckOut)) {
        throw "导出命令结束了，但没有产出 pck：$PckOut"
    }
    Write-Ok 'pck 已写出'

    # ── 4. 自检 ─────────────────────────────────────────────────────
    Write-Step '自检'

    $pckSize = (Get-Item $PckOut).Length
    Write-Note ("pck 体积：{0:N2} MB" -f ($pckSize / 1MB))
    if ($pckSize -lt $MinPckBytes) {
        throw ("pck 只有 {0:N0} KB，远低于正常值（约 3.6 MB）—— 骨架大概率丢了。`n排错顺序：① .gitignore 有无 bin/ ② bin/.gdignore 是否存在 ③ bin/ 下扩展是否完整。" -f ($pckSize / 1KB))
    }

    # pck 未加密，文件表是明文路径，直接按文本搜关键资源
    $pckText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($PckOut))
    $missing = @($RequiredInPck | Where-Object { -not $pckText.Contains($_) })
    if ($missing.Count -gt 0) {
        throw "pck 缺少关键资源：`n  - $($missing -join "`n  - ")"
    }
    Write-Ok "关键资源全部命中（$($RequiredInPck.Count) 项）"

    # ── 5. 交付物 ───────────────────────────────────────────────────
    Write-Step '交付物'
    Get-ChildItem $ModDir |
        Select-Object Name,
            @{ Name = '大小'; Expression = {
                    if ($_.Length -ge 1MB) { "{0:N2} MB" -f ($_.Length / 1MB) }
                    elseif ($_.Length -ge 1KB) { "{0:N1} KB" -f ($_.Length / 1KB) }
                    else { "{0:N0} B" -f $_.Length }
                } },
            LastWriteTime |
        Format-Table -AutoSize

    Write-Host "打包完成" -ForegroundColor Green
    Write-Host "  $ModDir" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "打包失败：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
