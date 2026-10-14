<#
.SYNOPSIS
    编译 CznHeidemarie 并把资源导出成 pck 放进游戏的 mods 目录。

.DESCRIPTION
    把「打包踩坑与解决方案.md」里踩出来的标准流程固化成一条命令，
    每一步都有前置校验和产物自检，避免重复踩已知的坑。

    执行顺序
      0. 前置检查（.gitignore / bin 排除 / spine 扩展 / 导出预设 / 占用进程）
      1. dotnet build ── dll/pdb/json 由 csproj 的 post-build 自动复制到 mods
      2. Godot --import ── 产出 .spskel / .spatlas / .ctex
      3. Godot --export-pack ── 产出 pck 到 mods 目录
      4. 自检 ── 流式解析 pck 真实文件索引（只读头部 + 索引区，不整包读入）
                + 体积区间 + 规模下限 + 关键资源命中

    自检为什么必须解析索引而不是搜文本：
    pck 里打进了 468 个 .cs 源码，任何出现在 C# 字符串常量里的资源路径
    都会让「全文 Contains」误判为存在。只有读文件表才能得到真值。

.PARAMETER Configuration
    构建配置，默认 Debug，可传 Release。

.PARAMETER SkipBuild
    跳过 dotnet build，只重导 pck。会校验 dll 已存在，但无法保证它是最新的。

.PARAMETER SkipImport
    跳过 Godot --import，确定资源没变时可用（省时间）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1

.EXAMPLE
    # 只重导一次 pck，不重新编译
    .\build.ps1 -SkipBuild
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$SkipImport
)

$ErrorActionPreference = 'Stop'

# ══════════════════════════════════════════════════════════════════════════
# 配置区 —— 换机器 / 换 Steam 库位置时只改这一段
# ══════════════════════════════════════════════════════════════════════════

# 工程根目录（脚本所在目录）
$ProjectDir = $PSScriptRoot

# 必须是 Godot 4.5.1 mono 的 console 版。
# 游戏本体 pck 头为 GDPC / pack_version=3 / ver=4.5.1，Godot 只保证向后兼容，
# 用更高版本编出的导入产物（.spskel/.ctex/.spatlas），4.5.1 的游戏读不了。
$GodotExe = 'F:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe'

# 游戏安装目录（须与 CznHeidemarie.csproj 里的 Sts2Dir 一致）
$Sts2Dir = 'G:\SteamLibrary\steamapps\common\Slay the Spire 2'

# mod id —— 须与 CznHeidemarie.json 里的 id 一致
$ModName = 'CznHeidemarie'

# 导出预设名（含空格，传参时必须整体作为一个参数）
$PresetName = 'Windows Desktop'

# 派生路径（下面这组不要手改）
$ModDir      = Join-Path $Sts2Dir "mods\$ModName"
$PckOut      = Join-Path $ModDir "$ModName.pck"
$ImportedDir = Join-Path $ProjectDir '.godot\imported'
$ExtListPath = Join-Path $ProjectDir '.godot\extension_list.cfg'
$GdextPath   = Join-Path $ProjectDir 'bin\spine_godot_extension.gdextension'
$PresetPath  = Join-Path $ProjectDir 'export_presets.cfg'

# ──────────────────────────────────────────────────────────────────────────
# 必需资源清单（精确路径）
#
# 取值依据：当前 pck 的真实文件表（1812 条）逐条比对得到，不是猜的。
# 注意两个反直觉的事实：
#   1. 源资源本体（.skel/.atlas/.png/.ogg/.webp）不进 pck，
#      进 pck 的是它们的 .import 索引文件。所以这里校验的是 .import。
#   2. 扩展只打进了描述文件 bin/spine_godot_extension.gdextension，
#      扩展的 DLL（bin/windows/*.dll）从来不在 pck 里 —— 运行期由游戏本体提供。
# ──────────────────────────────────────────────────────────────────────────
$RequiredExact = @(
    # spine 扩展描述文件（缺它说明 bin/ 被 .gitignore 排除了）
    'bin/spine_godot_extension.gdextension',

    # 战斗骨架（主角模型）
    'CznHeidemarie/animations/model/30093.skel.import',
    'CznHeidemarie/animations/model/30093.atlas.import',
    'CznHeidemarie/animations/model/30093.png.import',
    'CznHeidemarie/animations/model/30093_skel_data.tres',

    # 战斗开始演出骨架
    'CznHeidemarie/animations/battle_ready/30093_battle_ready.skel.import',
    'CznHeidemarie/animations/battle_ready/30093_battle_ready.atlas.import',
    'CznHeidemarie/animations/battle_ready/30093_battle_ready.png.import',
    'CznHeidemarie/animations/battle_ready/30093_battle_ready_skel_data.tres',

    # 文本
    'CznHeidemarie/localization/zhs/cards.json',
    'CznHeidemarie/localization/zhs/characters.json',
    'CznHeidemarie/localization/zhs/settings.json',

    # 场景
    'scenes/creature_visuals/heidemarie.tscn',
    'scenes/heidemarie_battle_ready.tscn',
    'scenes/merchant/characters/heidemarie_merchant.tscn',
    'scenes/rest_site/characters/heidemarie_rest_site.tscn',
    'scenes/combat/energy_counters/heidemarie_energy_counter.tscn',
    'scenes/vfx/card_trail_heidemarie.tscn',

    # 音频与立绘（战斗音效、角色语音、选角语音、败北 CG）
    'CznHeidemarie/audio/sfx/se_30093_attack_play1.ogg.import',
    'CznHeidemarie/audio/voice/vo_30093_attack_01.ogg.import',
    'CznHeidemarie/audio/voice/vo_30093_team_join_01.ogg.import',
    'CznHeidemarie/images/defeat/heidemarie_defeat_cg_1.webp.import',

    # 卡面：卡框材质 + 「解放极光」的全屏卡原画（静态兜底）
    'materials/cards/frames/card_frame_heidemarie_mat.tres',
    'CznHeidemarie/images/card_art/heidemarie_liberated_aurora.png.import',

    # 动态卡面：Spine 三件套 + 骨架数据 + 渲染器场景
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora.skel.import',
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora.atlas.import',
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora.png.import',
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora_skel_data.tres',
    'CznHeidemarie/scenes/cards/heidemarie_card_art.tscn'
)

# ──────────────────────────────────────────────────────────────────────────
# 必需资源清单（前缀）
#
# 指向 .godot/imported/ 下的导入产物 —— 真正承载骨架二进制、图集描述、纹理数据。
# 后缀是导入器按源文件内容算出的哈希，无法写死，只能匹配前缀。
# 骨架整体丢失时，最先消失的就是这些。
# ──────────────────────────────────────────────────────────────────────────
$RequiredPrefix = @(
    '.godot/imported/30093.skel-',                    # 骨架二进制 .spskel
    '.godot/imported/30093.atlas-',                   # 图集描述 .spatlas
    '.godot/imported/30093.png-',                     # 纹理 .ctex
    '.godot/imported/30093_battle_ready.skel-',
    '.godot/imported/30093_battle_ready.atlas-',
    '.godot/imported/30093_battle_ready.png-',
    '.godot/imported/se_30093_attack_play1.ogg-',     # 音效 .oggvorbisstr
    '.godot/imported/vo_30093_attack_01.ogg-',        # 语音 .oggvorbisstr
    '.godot/imported/vo_30093_team_join_01.ogg-',     # 选角语音 .oggvorbisstr
    '.godot/imported/heidemarie_defeat_cg_1.webp-',   # 败北 CG .ctex
    '.godot/imported/heidemarie_liberated_aurora.png-', # 全屏卡原画 .ctex
    '.godot/imported/heidemarie_card_liberated_aurora.skel-',  # 卡面骨架 .spskel
    '.godot/imported/heidemarie_card_liberated_aurora.atlas-', # 卡面图集 .spatlas
    '.godot/imported/heidemarie_card_liberated_aurora.png-'    # 卡面图集贴图 .ctex
)

# ──────────────────────────────────────────────────────────────────────────
# 规模下限 / 体积区间
#
# 实测当前 pck：135,026,540 字节（约 128.8 MB）、1812 条文件表条目、
#   .spskel 157 / .spatlas 157 / .ctex 211。
# 骨架丢失的故障态是 pck 缩到 0.53 MB、上述导入产物全灭。
# 下面留了余量：既能拦住故障态，又允许正常增删几个动画。
# ──────────────────────────────────────────────────────────────────────────
$MinEntryCount  = 1500
$MinSpskelCount = 140
$MinSpatlasCount = 140
$MinCtexCount   = 180
$MinPckBytes    = 40MB      # 故障态约 0.5 MB，正常约 128.8 MB
$MaxPckBytes    = 400MB     # 防止误把别的大文件导进来

# ══════════════════════════════════════════════════════════════════════════
# 输出辅助
# ══════════════════════════════════════════════════════════════════════════

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
# Godot 会往 stderr 写 ERROR 行（里面包含 mod 跨 pck 引用游戏本体资源导致的
# 预期噪音，如 res://src/... ），这些行在 $ErrorActionPreference='Stop' 下会被
# PowerShell 当成终止错误，直接打断脚本。所以临时降级为 Continue。
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

<#
.SYNOPSIS
    解析 Godot 4.x pck 的文件表，返回真实条目清单。
.DESCRIPTION
    布局（pack_version=3，本机游戏与 mod pck 实测一致）：
      0   u32  magic 'GDPC'
      4   u32  pack_version
      8   u32  ver_major
      12  u32  ver_minor
      16  u32  ver_patch
      20  u32  pack_flags
      24  u64  file_base        （数据区起点）
      32  u64  dir_offset       （文件表起点）
      48  ...  文件表：
                 u32 count
                 重复 count 次：u32 nameLen + name(UTF8) + u64 offset + u64 size + 16B MD5 + u32 flags

    性能：只做两次流式读取 —— ① 48 字节文件头；② dir_offset → 文件末尾的文件表
    （实测约 150 KB）。绝不把整个 pck（约 129 MB）读进内存。
    旧实现用 [IO.File]::ReadAllBytes 整包读入，正是「卡在校验 pck」的根因：
    在 PS 5.1 上单次分配上百 MB 托管数组、再对该大数组反复取值，代价极高，
    叠加 mods 目录位于 G: 盘时更明显。文件表只需要读一遍名字，条目里的
    offset/size/MD5/flags 本函数用不到，直接跳过 36 字节，不再构造对象。
    全程用 [long] 运算，避免 PS 5.1 把 >2GB 的字节偏移当成 Int32 转换失败。
.PARAMETER Path
    pck 文件路径。
.OUTPUTS
    PSCustomObject：Names(List[string]) / NameSet(HashSet[string]) /
    Count / FileSize / DirOffset / Version / PackVersion / TailBytes
#>
function Read-PckIndex {
    param([Parameter(Mandatory)][string]$Path)

    # FileShare.ReadWrite：导出刚结束时 Godot 可能仍短暂持有写句柄，放宽共享避免误判失败
    $fs = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $size = [long]$fs.Length
        if ($size -lt 48) {
            throw "不是合法的 pck（只有 $size 字节）：$Path"
        }

        # ① 只读 48 字节文件头
        $head = New-Object byte[] 48
        $got = 0
        while ($got -lt 48) {
            $n = $fs.Read($head, $got, 48 - $got)
            if ($n -le 0) { break }
            $got += $n
        }
        if ($got -lt 48) { throw "pck 文件头不完整（只读到 $got 字节）：$Path" }

        $magic = [Text.Encoding]::ASCII.GetString($head, 0, 4)
        if ($magic -ne 'GDPC') {
            throw "pck magic 不是 GDPC 而是 '$magic'：$Path"
        }

        $packVersion = [BitConverter]::ToUInt32($head, 4)
        $version = '{0}.{1}.{2}' -f `
            [BitConverter]::ToUInt32($head, 8), `
            [BitConverter]::ToUInt32($head, 12), `
            [BitConverter]::ToUInt32($head, 16)

        # 文件表偏移：v3 固定放在 u64@32
        $dirOff = [long][BitConverter]::ToUInt64($head, 32)
        if ($dirOff -lt 48 -or $dirOff -gt $size - 4) {
            throw "pck 文件表偏移不合理（$dirOff / 共 $size 字节）：$Path"
        }

        # ② 只读文件表区段（dir_offset → EOF，实测约 150 KB）
        $dirLen = [int]($size - $dirOff)
        if ($dirLen -gt 32MB) {
            # 正常文件表只占末尾一小段；异常大说明头部损坏，宁可报错也不要把整包读进来
            throw "pck 文件表区段异常大（$dirLen 字节），疑似头部损坏：$Path"
        }
        $dir = New-Object byte[] $dirLen
        $fs.Position = $dirOff
        $got = 0
        while ($got -lt $dirLen) {
            $n = $fs.Read($dir, $got, $dirLen - $got)
            if ($n -le 0) { break }
            $got += $n
        }
        if ($got -lt 4) { throw "pck 文件表读取不完整（只读到 $got 字节）：$Path" }
    }
    finally {
        $fs.Dispose()
    }

    $count = [int][BitConverter]::ToUInt32($dir, 0)
    if ($count -le 0 -or $count -gt 2000000) {
        throw "pck 文件表条目数不合理（$count）：$Path"
    }

    $names = New-Object 'System.Collections.Generic.List[string]' ([Math]::Min($count, 4096))
    $set   = New-Object 'System.Collections.Generic.HashSet[string]'

    # 位置相对文件表缓冲区起点：4=count 之后，每条 = u32 nameLen + name + 36 字节定长字段
    $pos = 4
    for ($i = 0; $i -lt $count; $i++) {
        if ($pos + 4 -gt $dir.Length) { throw "pck 文件表在解析到第 $i 条时越界：$Path" }

        $nameLen = [int][BitConverter]::ToUInt32($dir, $pos)
        $pos += 4
        if ($pos + $nameLen + 36 -gt $dir.Length) { throw "pck 第 $i 条条目越界：$Path" }

        # 实测条目名不带尾部 NUL，但旧版本可能带，统一裁掉
        $name = [Text.Encoding]::UTF8.GetString($dir, $pos, $nameLen).TrimEnd([char]0)
        $pos += $nameLen
        $pos += 36   # offset(8) + size(8) + md5(16) + flags(4)，校验用不到，跳过

        [void]$names.Add($name)
        [void]$set.Add($name)
    }

    return [pscustomobject]@{
        Names       = $names
        NameSet     = $set
        Count       = $count
        FileSize    = $size
        DirOffset   = $dirOff
        Version     = $version
        PackVersion = $packVersion
        TailBytes   = $size - ($dirOff + $pos)   # 正常为 0；非 0 说明尾部还有别的区段
    }
}

<#
.SYNOPSIS
    检查 .gitignore 或 bin/.gdignore 是否会把 bin/ 目录排除掉。
.DESCRIPTION
    Godot 导出时会用 .gitignore 当排除依据，bin/ 一旦被排除，
    spine 导入器就不注册，.skel/.atlas 连同 .spskel/.spatlas 产物
    会整体从 pck 丢失 —— 这是本项目历史上最大的故障。
.OUTPUTS
    被排除时返回原因字符串，否则返回 $null。
#>
function Test-BinExcluded {
    param(
        [Parameter(Mandatory)][string]$GitignorePath,
        [Parameter(Mandatory)][string]$ProjectDir
    )

    # 能命中 bin 目录的常见写法
    $patterns = @(
        'bin', 'bin/', '/bin', '/bin/',
        'bin/**', '/bin/**',
        '**/bin', '**/bin/', '**/bin/**'
    )

    if (Test-Path $GitignorePath) {
        $lines = @(Get-Content -Path $GitignorePath -ErrorAction SilentlyContinue)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = "$($lines[$i])".Trim()
            if ($line -eq '' -or $line.StartsWith('#')) { continue }
            if ($patterns -contains $line) {
                return ".gitignore 第 $($i + 1) 行「$line」会排除 bin/"
            }
        }
    }

    # bin/.gdignore 是同一机制：让 Godot 的资源扫描整体跳过 bin/
    $gdignore = Join-Path $ProjectDir 'bin\.gdignore'
    if (Test-Path $gdignore) {
        return 'bin/.gdignore 存在，会让 Godot 资源扫描跳过 bin/'
    }

    return $null
}

<#
.SYNOPSIS
    判断文件是否被其它进程独占（无法以读写方式打开）。
.DESCRIPTION
    进程还在不代表文件被锁 —— 别的 Godot 工程、别的用户会话都会留下进程，
    但不会锁本工程的 bin\windows\*.dll。只有真被锁住才会让扩展加载失败、
    骨架整体从 pck 消失，所以这里直接测文件，不看进程数量。
.OUTPUTS
    被锁住返回 $true，否则返回 $false。
#>
function Test-FileLocked {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) { return $false }
    try {
        $fs = [IO.File]::Open($Path, 'Open', 'ReadWrite', 'None')
        $fs.Close()
        return $false
    }
    catch {
        return $true
    }
}

# ══════════════════════════════════════════════════════════════════════════
# 主流程
# ══════════════════════════════════════════════════════════════════════════

try {
    # ── 步骤 0. 前置检查 ──────────────────────────────────────────────────
    Write-Step '前置检查'

    if (-not (Test-Path $GodotExe)) {
        throw "找不到 Godot 4.5.1：$GodotExe`n请修改本脚本的 `$GodotExe 指向本机 Godot 4.5.1（mono）可执行文件。"
    }
    Write-Ok 'Godot 4.5.1 就位'

    if (-not (Test-Path (Join-Path $ProjectDir "$ModName.csproj"))) {
        throw "当前目录不是 $ModName 工程：$ProjectDir"
    }

    # .gitignore / bin/.gdignore 若排除了 bin/，导出后骨架会全灭
    $binExcluded = Test-BinExcluded -GitignorePath (Join-Path $ProjectDir '.gitignore') -ProjectDir $ProjectDir
    if ($binExcluded) {
        throw "$binExcluded`n这会让 spine 骨架整体丢失，见「打包踩坑与解决方案.md」第二节。"
    }
    Write-Ok 'bin/ 没有被 .gitignore 或 .gdignore 排除'

    # spine 扩展本体 + 已登记的扩展清单
    if (-not (Test-Path $GdextPath)) {
        throw "缺少 spine 扩展：$GdextPath`n可从 F:\Tools\Godot_v4.5.1-stable_mono_win64\spine-godot-4.2\bin 恢复。"
    }
    if (-not (Test-Path $ExtListPath)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $ExtListPath) | Out-Null
        # 用无 BOM 的 UTF-8 写，与 Godot 自己生成的格式保持一致（带 BOM 可能不被识别）
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [IO.File]::WriteAllText($ExtListPath, "res://bin/spine_godot_extension.gdextension`n", $utf8NoBom)
        Write-Warn 'extension_list.cfg 缺失，已补写（无 BOM）'
    }
    Write-Ok 'spine 扩展已登记'

    # 导出预设：导出开关一旦被改坏，pck 内容会静默缺斤少两，这里提前拦
    if (-not (Test-Path $PresetPath)) {
        throw "缺少导出预设：$PresetPath"
    }
    $presetText = Get-Content -Path $PresetPath -Raw
    if ($presetText -notmatch 'export_filter\s*=\s*"all_resources"') {
        throw "export_presets.cfg 的 export_filter 不是 all_resources，导出会漏资源。"
    }
    if ($presetText -notmatch 'exclude_filter\s*=\s*"CznHeidemarie\.json"') {
        Write-Warn 'export_presets.cfg 的 exclude_filter 不是 "CznHeidemarie.json"（预期值），请确认是否有意为之。'
    }
    if ($presetText -notmatch [regex]::Escape(('export_path="{0}"' -f $PckOut))) {
        throw "export_presets.cfg 的 export_path 与脚本的 `$PckOut 不一致：`n  期望 $PckOut"
    }
    Write-Ok "导出预设「$PresetName」开关正常"

    # 关掉会占用的进程：
    #   - Godot 编辑器会锁住 bin\windows\*.dll，导致扩展加载失败、骨架从 pck 消失
    #   - 运行中的游戏会锁住 mods 目录的 <mod>.dll，让 post-build 复制报 MSB3021
    $blockingProcs = @(Get-Process -Name 'Godot*', 'SlayTheSpire2' -ErrorAction SilentlyContinue)
    if ($blockingProcs.Count -gt 0) {
        $names = ($blockingProcs | Select-Object -ExpandProperty ProcessName -Unique) -join ', '
        Write-Warn "检测到 $($blockingProcs.Count) 个占用进程（$names）正在关闭"
        $blockingProcs | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    # 关进程只是尽力而为：Stop-Process 在权限不足时会被静默忽略，而且别的工程、
    # 别的用户会话留下的 Godot 进程与本工程无关。真正要保证的是关键文件没被独占，
    # 所以这里直接测文件锁，不看进程还在不在。
    $spineDlls = @(Get-ChildItem (Join-Path $ProjectDir 'bin\windows') -Filter '*.dll' -ErrorAction SilentlyContinue)
    $lockedDlls = @($spineDlls | Where-Object { Test-FileLocked -Path $_.FullName })
    if ($lockedDlls.Count -gt 0) {
        $alive = @(Get-Process -Name 'Godot*', 'SlayTheSpire2' -ErrorAction SilentlyContinue)
        $aliveNames = ($alive | Select-Object -ExpandProperty ProcessName -Unique) -join ', '
        throw "spine 扩展被占用，无法加载扩展（导出后骨架会整体丢失）：`n  - $(($lockedDlls | Select-Object -ExpandProperty Name) -join "`n  - ")`n仍存活的进程：$aliveNames`n请手动关闭这些进程（可能需管理员权限），或重启后再试。"
    }
    if ($blockingProcs.Count -gt 0) {
        Write-Ok '占用进程已关闭'
    }
    Write-Ok 'spine 扩展未被占用'

    # 兜底：工程根不能出现 src/，它会被打进 pck 并和游戏本体的 res://src/ 撞名
    if (Test-Path (Join-Path $ProjectDir 'src')) {
        throw "工程根存在 src/ 目录，会被打进 pck 并和游戏本体的 res://src/ 冲突，请删除后重试。"
    }
    Write-Ok '工程根没有 src/ 目录'

    # ── 步骤 1. 编译 C# ───────────────────────────────────────────────────
    if ($SkipBuild) {
        Write-Step '跳过编译（-SkipBuild）'
        Write-Note 'dll 不会更新，下面的产物校验只能确认它存在、不比源码旧'
    }
    else {
        Write-Step "编译 C#（$Configuration）"
        Push-Location $ProjectDir
        try {
            & dotnet build "$ModName.csproj" -c $Configuration -v minimal --nologo
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet build 失败（退出码 $LASTEXITCODE）。"
            }
        }
        finally {
            Pop-Location
        }
    }

    # 编译产物断言：dll/json 必须在 mods 目录，且 dll 必须是本次编译的产物
    $dllPath  = Join-Path $ModDir "$ModName.dll"
    $jsonPath = Join-Path $ModDir "$ModName.json"

    if (-not (Test-Path $dllPath)) {
        throw "mods 目录里没有 dll：$dllPath`n（csproj 的 post-build 会把 dll 复制过去；-SkipBuild 时请先完整构建一次）"
    }
    if (-not (Test-Path $jsonPath)) {
        throw "mods 目录里没有清单 json：$jsonPath`n（csproj 的 post-build 会复制它，请确认工程根有 $ModName.json）"
    }

    $dllItem = Get-Item $dllPath
    if ($SkipBuild) {
        Write-Note "dll 已存在（$($dllItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))，未校验是否为最新编译产物）"
    }
    else {
        # 不能用「dll 时间戳 ≥ 本次编译开始时间」判断 —— dotnet build 是增量的，
        # 源码没改时输出 dll 的时间戳保持上一次的值，post-build 的 Copy
        # SkipUnchangedFiles 也正因此不再复制，会把正常情况误判成复制失败。
        # 真正要保证的是「mods 里的 dll 不比源码旧」，即没有漏掉某次编译。
        $newestSource = Get-ChildItem -Path $ProjectDir -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(obj|bin|\.godot|\.vs|\.nuget-feed|\.nuget-packages|\.stage_out)\\' } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($newestSource -and $dllItem.LastWriteTime -lt $newestSource.LastWriteTime.AddSeconds(-10)) {
            throw "mods 目录的 dll 比最新源码还旧，post-build 复制没生效：`n  dll  $($dllItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))`n  源码 $($newestSource.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))  $($newestSource.Name)"
        }
    }

    $jsonText = Get-Content -Path $jsonPath -Raw
    if ($jsonText -notmatch '"has_pck"\s*:\s*true') {
        Write-Warn "$ModName.json 里 has_pck 不是 true，游戏可能不会挂载 pck"
    }
    if ($jsonText -notmatch '"has_dll"\s*:\s*true') {
        Write-Warn "$ModName.json 里 has_dll 不是 true，游戏可能不会加载 dll"
    }
    Write-Ok "dll 已就位（$([math]::Round($dllItem.Length / 1KB, 1)) KB），清单 json 已就位"

    # ── 步骤 2. 导入资源 ─────────────────────────────────────────────────
    if ($SkipImport) {
        Write-Step '跳过资源导入（-SkipImport）'
    }
    else {
        Write-Step '导入资源（Godot --import）'
        # --import 只关心 spine 扩展与 mod 资源相关的报错，
        # 别的地方（如 res://src/... 这类跨 pck 引用）是预期噪音，刷屏无用。
        Invoke-Godot @('--headless', '--path', $ProjectDir, '--import') |
            Select-String -Pattern 'Cannot open GDExtension|Error loading extension|Failed loading resource: res://CznHeidemarie' |
            ForEach-Object { Write-Warn $_.ToString().Trim() }
    }

    $spskel  = @(Get-ChildItem $ImportedDir -Filter '*.spskel'  -ErrorAction SilentlyContinue)
    $spatlas = @(Get-ChildItem $ImportedDir -Filter '*.spatlas' -ErrorAction SilentlyContinue)
    if ($spskel.Count -eq 0 -or $spatlas.Count -eq 0) {
        throw "缺少 spine 导入产物（.spskel/.spatlas），说明扩展没生效，导出后必然丢骨架。`n请确认没有 Godot 进程占用 bin\windows\*.dll 后重试。"
    }
    Write-Ok "导入产物就位：.spskel $($spskel.Count) 个、.spatlas $($spatlas.Count) 个"

    # ── 步骤 3. 导出 pck ─────────────────────────────────────────────────
    Write-Step '导出 pck'
    New-Item -ItemType Directory -Force -Path $ModDir | Out-Null
    Remove-Item $PckOut -Force -ErrorAction SilentlyContinue

    # 预设名含空格，必须用调用运算符 & 整体传入一个参数。
    # 用 Start-Process -ArgumentList @('--export-pack','Windows Desktop',$out) 会被
    # 按空格拆成 'Windows' + 'Desktop'，报 Invalid export preset name: Windows。
    $godotOut = Invoke-Godot @('--headless', '--path', $ProjectDir, '--export-pack', $PresetName, $PckOut)
    $exportExitCode = $LASTEXITCODE
    $godotOut | Select-Object -Last 3 | ForEach-Object { Write-Note $_.ToString().Trim() }

    if ($exportExitCode -ne 0) {
        throw "导出失败（退出码 $exportExitCode）。"
    }
    if (-not (Test-Path $PckOut)) {
        throw "Godot 报成功，但没产出 pck：$PckOut"
    }
    Write-Ok 'pck 已写出'

    # ── 步骤 4. 自检 ─────────────────────────────────────────────────────
    Write-Step '自检'

    $pckItem = Get-Item $PckOut
    $pckSize = [long]$pckItem.Length
    Write-Note ("pck 体积 {0:N2} MB" -f ($pckSize / 1MB))

    if ($pckSize -lt $MinPckBytes -or $pckSize -gt $MaxPckBytes) {
        throw ("pck 体积 {0:N2} MB 超出预期区间 {1:N0}–{2:N0} MB。`n太小通常是骨架丢失（故障态约 0.5 MB），排查顺序：`n  ① .gitignore 是否含 bin/ ② bin/.gdignore 是否存在 ③ bin/ 下扩展是否齐全" -f `
            ($pckSize / 1MB), ($MinPckBytes / 1MB), ($MaxPckBytes / 1MB))
    }

    # 解析真实文件表。pck 里含 468 个 .cs，全文搜文本会被源码里的路径常量骗到，
    # 所以必须读索引拿真值。Read-PckIndex 只读头部 + 索引区，不整包读入内存。
    $swVerify = [Diagnostics.Stopwatch]::StartNew()
    $idx = Read-PckIndex -Path $PckOut
    $swVerify.Stop()
    Write-Note "文件表解析成功：$($idx.Count) 条条目，格式 ver=$($idx.Version) pack=$($idx.PackVersion)（耗时 $($swVerify.ElapsedMilliseconds) ms）"

    if ($idx.Version -ne '4.5.1') {
        throw "pck 格式版本是 $($idx.Version)，不是游戏要求的 4.5.1 —— 多半是用了更高版本的 Godot 导出。"
    }

    # 4-1. 规模下限
    if ($idx.Count -lt $MinEntryCount) {
        throw "pck 只有 $($idx.Count) 条条目（下限 $MinEntryCount），资源疑似大面积缺失。"
    }

    # 计数与前缀命中一次遍历完成：旧实现用 Where-Object 管道把 1800+ 个名字过滤 12 遍，
    # 每遍都要走脚本块，是校验阶段的次要耗时点。
    $spskelInPck = 0; $spatlasInPck = 0; $ctexInPck = 0
    $hitPrefix = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($name in $idx.Names) {
        if ($name.EndsWith('.spskel')) { $spskelInPck++ }
        elseif ($name.EndsWith('.spatlas')) { $spatlasInPck++ }
        elseif ($name.EndsWith('.ctex')) { $ctexInPck++ }

        foreach ($prefix in $RequiredPrefix) {
            if ($name.StartsWith($prefix)) { [void]$hitPrefix.Add($prefix); break }
        }
    }

    $scale = ".spskel $spskelInPck / .spatlas $spatlasInPck / .ctex $ctexInPck"
    if ($spskelInPck -lt $MinSpskelCount -or $spatlasInPck -lt $MinSpatlasCount -or $ctexInPck -lt $MinCtexCount) {
        throw "pck 里导入产物数量不足（$scale），下限 .spskel $MinSpskelCount / .spatlas $MinSpatlasCount / .ctex $MinCtexCount。`n骨架多半丢了，见「打包踩坑与解决方案.md」第五节决策树。"
    }
    Write-Ok "pck 内导入产物数量正常（$scale）"

    # 4-2. 关键资源精确匹配
    $missingExact = @($RequiredExact | Where-Object { -not $idx.NameSet.Contains($_) })
    if ($missingExact.Count -gt 0) {
        throw "pck 缺少关键资源：`n  - $($missingExact -join "`n  - ")"
    }

    # 4-3. 导入产物前缀匹配（后缀是内容哈希，只能比对前缀）
    $missingPrefix = @($RequiredPrefix | Where-Object { -not $hitPrefix.Contains($_) })
    if ($missingPrefix.Count -gt 0) {
        throw "pck 缺少导入产物（前缀）：`n  - $($missingPrefix -join "`n  - ")`n说明源资源在，但导入产物没进 pck。"
    }

    Write-Ok "关键资源全部命中（精确 $($RequiredExact.Count) 项 + 前缀 $($RequiredPrefix.Count) 项）"
    if ($idx.TailBytes -ne 0) {
        Write-Warn "文件表解析后有 $($idx.TailBytes) 字节尾数据（可能是格式扩展），非致命。"
    }

    # ── 步骤 5. 交付清单 ─────────────────────────────────────────────────
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

    Write-Host "构建完成" -ForegroundColor Green
    Write-Host "  $ModDir" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "构建失败：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
