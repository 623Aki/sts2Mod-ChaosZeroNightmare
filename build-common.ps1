<#
.SYNOPSIS
    CznHeidemarie / CznMonster 构建脚本共用的底层工具。

.DESCRIPTION
    两个 mod 的 build.ps1 需要同一批「容易写错、写错了很难查」的能力：

      · 按「产物就绪」而不是「进程退出」判定 Godot 是否干完活
        （Godot 写完 pck 后经常不退出，用 & 调用会永久阻塞）
      · 流式解析 pck 文件表（只读文件头 + 索引区，绝不整包读入内存）
      · 只关闭真正会锁住 bin\windows\*.dll / mods\*.dll 的进程
      · 校验导出预设、.import 导入状态
      · 统一输出格式与交付物清单

    这些逻辑原先在两个 build.ps1 里各复制一份，改一处很容易忘另一处。
    现在集中在这里，由 build.ps1 用 dot-source 引入。本文件只定义函数，没有副作用。

.NOTES
    必须是 UTF-8 with BOM：Windows PowerShell 5.1 会把「无 BOM 的 .ps1」
    按 ANSI 解码，文件里的中文会变乱码甚至造成语法错误。
    本文件不依赖任何脚本级变量，所有路径都通过参数传入。
#>
#Requires -Version 5.1

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

function Write-Fail([string]$Text) {
    Write-Host "  [XX] $Text" -ForegroundColor Red
}

<#
.SYNOPSIS
    把字节数格式化成人类可读的字符串（B / KB / MB / GB）。
#>
function Format-Size([long]$Bytes) {
    if ($Bytes -ge 1GB) { return ('{0:N2} GB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N2} MB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N1} KB' -f ($Bytes / 1KB)) }
    return ('{0:N0} B' -f $Bytes)
}

<#
.SYNOPSIS
    列出 mods 目录里的交付物（文件名 / 大小 / 时间）。

.DESCRIPTION
    两个脚本的交付物清单格式必须一致，否则一眼看不出哪次构建换了文件。
#>
function Show-Deliverables {
    param([Parameter(Mandatory)][string]$Directory)

    if (-not (Test-Path $Directory)) {
        Write-Warn "目录不存在：$Directory"
        return
    }

    Get-ChildItem $Directory |
        Sort-Object Name |
        Select-Object Name,
            @{ Name = '大小'; Expression = { Format-Size ([long]$_.Length) } },
            LastWriteTime |
        Format-Table -AutoSize
}

# ══════════════════════════════════════════════════════════════════════════
# Godot CLI 包装
#
# 为什么不能用 `& $GodotExe @Arguments 2>&1`：
#   实测 --export-pack 时 Godot 内部还会跑一遍 dotnet publish（本项目在 Godot 里
#   这一步经常会失败，耗时 15 秒 ~ 2 分钟，报 "Failed to build project"），pck 是在
#   这之后才写出的；而 pck 写完后 Godot 进程往往卡在收尾不再退出（实测只剩
#   1 个线程、CPU 恒为 0）。调用运算符 & 会一直阻塞在这一行、永不返回 ——
#   卡点在导出之后，看起来就像「卡在校验 pck」，其实自检根本没开始跑。
#
#   ⚠ 不能用「CPU 长时间不涨」判假死：Godot 等子进程 dotnet 干活时自己就是 0 CPU，
#     那样会在 pck 还没写出时就把导出杀掉。正确判据是产物本身：
#       · 给了 -ArtifactPath：等「文件出现 + 大小稳定」，即认定导出完成；
#       · 没给（如 --import）：只看硬超时，宁慢不误杀。
#   判定完成后给进程一点自然退出的时间（正常 1~2 秒就退），仍不退就强杀；
#   产物到底完不完整，一律交给调用方的自检验证。
# ══════════════════════════════════════════════════════════════════════════

function Invoke-GodotTimed {
    param(
        [Parameter(Mandatory)][string]$GodotExe,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$LogDir,
        [Parameter(Mandatory)][string]$LogName,     # 日志文件名前缀
        [int]$TimeoutSec = 900,                     # 硬超时，兜底防止无限等待
        [string]$ArtifactPath,                      # 期望产物；给出后按「产物就绪」判定
        [long]$ArtifactMinBytes = 0,                # 产物至少多大才算写完
        [int]$ArtifactStableSec = 4,                # 产物大小稳定多久才算写完
        [int]$ExitGraceSec = 10                     # 产物就绪后等进程自然退出的秒数
    )

    # 日志放 .godot\build\ —— 实测导出器不递归扫 .godot（工程里 .godot/editor 存在，
    # 但 pck 里 0 条），所以放这里不会被误打进 pck。
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    $outLog = Join-Path $LogDir "$LogName.out.log"
    $errLog = Join-Path $LogDir "$LogName.err.log"
    Remove-Item $outLog, $errLog -Force -ErrorAction SilentlyContinue

    # 含空格的参数（预设名 "Windows Desktop"、含空格的 pck 路径）必须自己加引号：
    # Start-Process 的 -ArgumentList 只做空格拼接，不像调用运算符 & 会自动加引号。
    $argString = ($Arguments | ForEach-Object {
            if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
        }) -join ' '

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath $GodotExe -ArgumentList $argString -NoNewWindow -PassThru `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog

    $status = 'exited'
    $lastArtifactLen = -1
    $lastArtifactChange = Get-Date

    while (-not $proc.HasExited) {
        Start-Sleep -Milliseconds 500

        if ($ArtifactPath) {
            if (Test-Path $ArtifactPath) {
                $len = [long](Get-Item $ArtifactPath).Length
                if ($len -ne $lastArtifactLen) {
                    $lastArtifactLen = $len
                    $lastArtifactChange = Get-Date
                }
                elseif ($len -ge $ArtifactMinBytes -and
                    ((Get-Date) - $lastArtifactChange).TotalSeconds -ge $ArtifactStableSec) {
                    $status = 'artifact-ready'
                    break
                }
            }
            else {
                $lastArtifactLen = -1
            }
        }

        if ($sw.Elapsed.TotalSeconds -ge $TimeoutSec) {
            $status = 'timeout-killed'
            break
        }
    }

    $exitCode = $null
    if ($status -eq 'artifact-ready') {
        if ($proc.WaitForExit($ExitGraceSec * 1000)) {
            $status = 'exited'
            $exitCode = $proc.ExitCode
        }
        else {
            $status = 'artifact-killed'
        }
    }
    elseif ($status -eq 'exited') {
        $exitCode = $proc.ExitCode
    }

    if ($status -in @('artifact-killed', 'timeout-killed')) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        try { [void]$proc.WaitForExit(5000) } catch { }
    }
    $sw.Stop()

    $artifactSize = $null
    if ($ArtifactPath -and (Test-Path $ArtifactPath)) {
        $artifactSize = [long](Get-Item $ArtifactPath).Length
    }

    # 进程刚被杀掉时句柄可能还没释放，读日志失败一次不算错误
    $log = New-Object 'System.Collections.Generic.List[string]'
    foreach ($f in @($outLog, $errLog)) {
        if (-not (Test-Path $f)) { continue }
        $lines = @(Get-Content -LiteralPath $f -Encoding UTF8 -ErrorAction SilentlyContinue)
        if ($lines.Count -eq 0 -and (Get-Item $f).Length -gt 0) {
            Start-Sleep -Milliseconds 300
            $lines = @(Get-Content -LiteralPath $f -Encoding UTF8 -ErrorAction SilentlyContinue)
        }
        foreach ($line in $lines) { [void]$log.Add([string]$line) }
    }

    return [pscustomobject]@{
        Status        = $status        # exited / artifact-ready / artifact-killed / timeout-killed
        ExitCode      = $exitCode      # 仅供参考：PS 5.1 读 Start-Process 的 ExitCode 常为 $null
        Log           = $log
        LogDir        = $LogDir
        ArtifactBytes = $artifactSize
        ElapsedSec    = [int]$sw.Elapsed.TotalSeconds
        OutLog        = $outLog
        ErrLog        = $errLog
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
    （实测约 150 KB）。绝不把整个 pck（100 MB 级）读进内存。

    条目名可能带尾部 NUL 填充（实测 2031 条里 1503 条带），统一 TrimEnd 掉，
    否则 EndsWith('.spskel') 之类的判断会全部落空。

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
    $set = New-Object 'System.Collections.Generic.HashSet[string]'

    # 位置相对文件表缓冲区起点：4=count 之后，每条 = u32 nameLen + name + 36 字节定长字段
    $pos = 4
    for ($i = 0; $i -lt $count; $i++) {
        if ($pos + 4 -gt $dir.Length) { throw "pck 文件表在解析到第 $i 条时越界：$Path" }

        $nameLen = [int][BitConverter]::ToUInt32($dir, $pos)
        $pos += 4
        if ($pos + $nameLen + 36 -gt $dir.Length) { throw "pck 第 $i 条条目越界：$Path" }

        # 条目名可能带尾部 NUL 填充，统一裁掉
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
    一次遍历统计 pck 内的导入产物数量，并检查关键资源是否命中。

.DESCRIPTION
    pck 里含数百个 .cs 源码，任何出现在 C# 字符串常量里的资源路径都会让
    「全文 Contains」误判为存在 —— 所以必须比对文件表里的真实条目名。

    计数与前缀命中一次遍历完成：旧实现用 Where-Object 管道把 2000+ 个名字
    反复过滤，每遍都要走脚本块，是校验阶段的主要耗时点。

.PARAMETER Index
    Read-PckIndex 的返回值。

.PARAMETER RequiredExact
    必须以完整路径存在的条目。

.PARAMETER RequiredPrefix
    必须以该前缀开头的条目（导入产物的后缀是内容哈希，只能比前缀）。
#>
function Test-PckContent {
    param(
        [Parameter(Mandatory)]$Index,
        [string[]]$RequiredExact = @(),
        [string[]]$RequiredPrefix = @()
    )

    $spskel = 0
    $spatlas = 0
    $ctex = 0
    $hitPrefix = New-Object 'System.Collections.Generic.HashSet[string]'

    foreach ($name in $Index.Names) {
        if ($name.EndsWith('.spskel')) { $spskel++ }
        elseif ($name.EndsWith('.spatlas')) { $spatlas++ }
        elseif ($name.EndsWith('.ctex')) { $ctex++ }

        foreach ($prefix in $RequiredPrefix) {
            if ($name.StartsWith($prefix)) { [void]$hitPrefix.Add($prefix); break }
        }
    }

    $missingExact = @($RequiredExact | Where-Object { -not $Index.NameSet.Contains($_) })
    $missingPrefix = @($RequiredPrefix | Where-Object { -not $hitPrefix.Contains($_) })

    return [pscustomobject]@{
        Spskel        = $spskel
        Spatlas       = $spatlas
        Ctex          = $ctex
        HitPrefixes   = $hitPrefix
        MissingExact  = $missingExact
        MissingPrefix = $missingPrefix
    }
}

# ══════════════════════════════════════════════════════════════════════════
# 工程前置检查
# ══════════════════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    检查 .gitignore 或 bin/.gdignore 是否会把 bin/ 目录排除掉。

.DESCRIPTION
    bin/ 是 spine-godot 扩展本体（bin/spine_godot_extension.gdextension +
    bin/windows/*.dll）。bin/ 一旦被排除，spine 导入器就不注册，
    .skel/.atlas 连同 .spskel/.spatlas 产物会整体从 pck 丢失 —— 这是历史上最大的故障。

.OUTPUTS
    被排除时返回原因字符串，否则返回 $null。
#>
function Test-BinExcluded {
    param(
        [Parameter(Mandatory)][string]$ProjectDir
    )

    # 能命中 bin 目录的常见写法
    $patterns = @(
        'bin', 'bin/', '/bin', '/bin/',
        'bin/*', '/bin/*',
        'bin/**', '/bin/**',
        '**/bin', '**/bin/', '**/bin/**'
    )

    $gitignorePath = Join-Path $ProjectDir '.gitignore'
    if (Test-Path $gitignorePath) {
        $lines = @(Get-Content -Path $gitignorePath -ErrorAction SilentlyContinue)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = "$($lines[$i])".Trim()
            if ($line -eq '' -or $line.StartsWith('#')) { continue }
            if ($patterns -contains $line) {
                return ".gitignore 第 $($i + 1) 行「$line」会排除 bin/"
            }
        }
    }

    # bin/.gdignore 是同一机制：让 Godot 的资源扫描整体跳过 bin/
    if (Test-Path (Join-Path $ProjectDir 'bin\.gdignore')) {
        return 'bin/.gdignore 存在，会让 Godot 资源扫描跳过 bin/'
    }

    return $null
}

<#
.SYNOPSIS
    检查所有 .import 的导入状态是否有效。

.DESCRIPTION
    .import 里 valid=false 意味着「源图在、贴图没导入」。Godot 会把这种 .import
    照样打进 pck，但 ResourceLoader.Exists 判 false，卡面 / 图标会静默回落到占位图。

    必须放在 Godot --import **之后** 调用：--import 本身会把 valid=false 的资源
    重新导入，提前拦截反而会把「重跑一次就好」的情况误判成失败。

.OUTPUTS
    检查过的 .import 文件数量。
#>
function Assert-ValidImports {
    param([Parameter(Mandatory)][string]$ProjectDir)

    $invalid = New-Object 'System.Collections.Generic.List[string]'
    $checked = 0

    $files = @(
        Get-ChildItem -Path $ProjectDir -Recurse -Filter '*.import' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|\.godot|\.vs|\.nuget-feed|\.nuget-packages|\.stage_out)\\' }
    )

    foreach ($f in $files) {
        try { $text = [IO.File]::ReadAllText($f.FullName) } catch { continue }
        $checked++
        if ($text -match 'valid\s*=\s*false') {
            [void]$invalid.Add($f.FullName.Substring($ProjectDir.Length).TrimStart('\'))
        }
    }

    if ($invalid.Count -gt 0) {
        throw ("有 {0} 个资源导入状态无效（.import 里 valid=false）：`n  - {1}`n" -f $invalid.Count, ($invalid -join "`n  - ")) +
        "这些资源会静默回落到占位图。修复：删掉对应 .import 后重跑 Godot --import，或核对 .godot/imported 里的 .ctex 是否存在。"
    }

    return $checked
}

<#
.SYNOPSIS
    读取 export_presets.cfg 中指定预设的关键开关。

.DESCRIPTION
    不能直接对整个文件做正则：同一份 cfg 里可能有多个预设，别的预设的
    export_path 会把当前预设的值盖掉。这里按 [preset.N] 分段，只取 name 匹配的那段。

    export_path 写法可能是 / 或 \，比较前统一成 \ 并去掉尾部斜杠。
#>
function Read-ExportPreset {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$PresetName
    )

    $result = [pscustomobject]@{
        Found        = $false
        Name         = $PresetName
        ExportFilter = $null
        ExcludeFilter = $null
        ExportPath   = $null
    }

    $inTarget = $false
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        $t = $line.Trim()

        if ($t -match '^\[preset\.\d+\]$') {
            $inTarget = $false
            continue
        }
        if ($t -match '^name\s*=\s*"(.*)"$') {
            $inTarget = ($Matches[1] -eq $PresetName)
            if ($inTarget) { $result.Found = $true }
            continue
        }
        if (-not $inTarget) { continue }

        if ($t -match '^export_filter\s*=\s*"(.*)"$') { $result.ExportFilter = $Matches[1]; continue }
        if ($t -match '^exclude_filter\s*=\s*"(.*)"$') { $result.ExcludeFilter = $Matches[1]; continue }
        if ($t -match '^export_path\s*=\s*"(.*)"$') { $result.ExportPath = $Matches[1]; continue }
    }

    return $result
}

<#
.SYNOPSIS
    校验导出预设：预设存在、export_filter=all_resources、export_path 指向脚本的 pck。
.DESCRIPTION
    导出开关一旦被改坏，pck 内容会静默缺斤少两，必须提前拦。
#>
function Assert-ExportPreset {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$PresetName,
        [Parameter(Mandatory)][string]$ExpectedPckPath,
        [Parameter(Mandatory)][string]$ManifestName
    )

    if (-not (Test-Path $Path)) {
        throw "缺少导出预设：$Path"
    }

    $preset = Read-ExportPreset -Path $Path -PresetName $PresetName
    if (-not $preset.Found) {
        throw "export_presets.cfg 里找不到名为「$PresetName」的预设。"
    }
    if ($preset.ExportFilter -ne 'all_resources') {
        throw "导出预设「$PresetName」的 export_filter 是 '$($preset.ExportFilter)'，不是 all_resources，导出会漏资源。"
    }
    if ($preset.ExcludeFilter -ne $ManifestName) {
        Write-Warn "导出预设的 exclude_filter 是 '$($preset.ExcludeFilter)'（预期 '$ManifestName'），请确认是否有意为之。"
    }

    $actual = ConvertTo-NormalizedPath $preset.ExportPath
    $expected = ConvertTo-NormalizedPath $ExpectedPckPath
    if ($actual -ne $expected) {
        throw "export_presets.cfg 的 export_path 与脚本的 `$PckOut 不一致：`n  预设 $($preset.ExportPath)`n  脚本 $ExpectedPckPath"
    }

    return $preset
}

<#
.SYNOPSIS
    把路径统一成「反斜杠 + 无尾斜杠」，用于比较。
#>
function ConvertTo-NormalizedPath([string]$Path) {
    if (-not $Path) { return '' }
    return ($Path -replace '/', '\').TrimEnd('\')
}

# ══════════════════════════════════════════════════════════════════════════
# 进程占用
# ══════════════════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    判断文件是否被其它进程独占（无法以读写方式打开）。

.DESCRIPTION
    进程还在不代表文件被锁 —— 别的 Godot 工程、别的用户会话都会留下进程，
    但不会锁本工程的 bin\windows\*.dll。只有真被锁住才会让扩展加载失败、
    骨架整体从 pck 消失，所以这里直接测文件，不看进程数量。
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

<#
.SYNOPSIS
    关闭会锁住本工程文件的进程，然后确认这些文件已经解锁。

.DESCRIPTION
    需要关闭的只有两类：
      · 运行中的游戏：会锁住 mods\<mod>.dll，让 post-build 复制报 MSB3021
      · 打开了本工程的 Godot：会锁住 bin\windows\*.dll，扩展加载失败会让骨架整体从 pck 消失

    以前是无差别杀掉所有 Godot* 进程，会误伤别的工程 / 别人的编辑器。
    现在只关命令行里带本工程路径的 Godot；识别不到也不要紧 —— 真正的判据是
    下面的文件锁检测，锁住了就报清楚的错，让用户手动关。

.PARAMETER LockPaths
    必须保证没有被独占的文件（bin\windows\*.dll、mods\<mod>.dll）。

.OUTPUTS
    被结束的进程数量。
#>
function Stop-BlockingProcesses {
    param(
        [Parameter(Mandatory)][string]$ProjectDir,
        [string[]]$LockPaths = @(),
        [int]$WaitSeconds = 2
    )

    $targets = New-Object 'System.Collections.Generic.List[object]'

    # 游戏进程总是要关：它锁的是 mods 目录里的 dll
    foreach ($p in @(Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue)) {
        if ($p) { [void]$targets.Add($p) }
    }

    # 只关「命令行里带本工程路径」的 Godot，别把别的工程一起杀了
    try {
        $needle = $ProjectDir.TrimEnd('\')
        foreach ($cp in @(Get-CimInstance -ClassName Win32_Process -Filter "Name LIKE 'Godot%'" -ErrorAction Stop)) {
            $cmd = [string]$cp.CommandLine
            if ($cmd -and $cmd.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $p = Get-Process -Id ([int]$cp.ProcessId) -ErrorAction SilentlyContinue
                if ($p) { [void]$targets.Add($p) }
            }
        }
    }
    catch {
        Write-Note '无法枚举 Godot 命令行，跳过自动关闭；直接检测文件占用'
    }

    $unique = @($targets | Sort-Object -Property Id -Unique)
    if ($unique.Count -gt 0) {
        $names = ($unique | Select-Object -ExpandProperty ProcessName -Unique) -join ', '
        Write-Warn "结束 $($unique.Count) 个占用进程（$names）"
        $unique | Stop-Process -Force -ErrorAction SilentlyContinue
    }

    $locked = @($LockPaths | Where-Object { $_ -and (Test-FileLocked -Path $_) })
    if ($unique.Count -gt 0 -and $locked.Count -gt 0) {
        # 刚被杀掉的进程释放句柄需要一点时间，别立刻判失败
        $deadline = (Get-Date).AddSeconds([Math]::Max($WaitSeconds, 1) + 3)
        while ($locked.Count -gt 0 -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
            $locked = @($LockPaths | Where-Object { $_ -and (Test-FileLocked -Path $_) })
        }
    }

    if ($locked.Count -gt 0) {
        $alive = @(Get-Process -Name 'Godot*', 'SlayTheSpire2' -ErrorAction SilentlyContinue)
        $aliveNames = if ($alive.Count -gt 0) { ($alive | Select-Object -ExpandProperty ProcessName -Unique) -join ', ' } else { '（无）' }
        throw ("以下文件仍被占用，Godot 无法加载 spine 扩展，或无法覆盖 mods 里的 dll：`n  - " +
            ($locked -join "`n  - ") +
            "`n仍存活的相关进程：$aliveNames`n请手动关闭这些进程（可能需管理员权限），或重启后再试。")
    }

    return $unique.Count
}

# ══════════════════════════════════════════════════════════════════════════
# 编译
# ══════════════════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    执行 dotnet build，失败时抛异常。
.PARAMETER Rebuild
    传 -t:Rebuild 强制完整重编译（改了 .cs 但 DLL 时间没变时使用）。
#>
function Invoke-DotnetBuild {
    param(
        [Parameter(Mandatory)][string]$ProjectDir,
        [Parameter(Mandatory)][string]$ProjectFile,
        [string]$Configuration = 'Debug',
        [switch]$Rebuild
    )

    Push-Location $ProjectDir
    try {
        $buildArgs = @($ProjectFile, '-c', $Configuration, '-v', 'minimal', '--nologo')
        if ($Rebuild) { $buildArgs += '-t:Rebuild' }
        & dotnet build @buildArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build 失败（退出码 $LASTEXITCODE）。"
        }
    }
    finally {
        Pop-Location
    }
}

<#
.SYNOPSIS
    找出工程内最新的 .cs 源文件（跳过 bin/obj/.godot 等生成目录）。
.DESCRIPTION
    用于判断 mods 里的 dll 是否漏掉了某次编译：dotnet build 是增量的，
    源码没改时输出 dll 的时间戳保持上一次的值，所以不能用「dll 比本次构建开始时间新」判断。
#>
function Get-NewestSourceFile {
    param([Parameter(Mandatory)][string]$ProjectDir)

    Get-ChildItem -Path $ProjectDir -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|\.godot|\.vs|\.nuget-feed|\.nuget-packages|\.stage_out)\\' } |
        Sort-Object -Property LastWriteTime -Descending |
        Select-Object -First 1
}

# ══════════════════════════════════════════════════════════════════════════
# 导入产物
# ══════════════════════════════════════════════════════════════════════════

function Get-ImportArtifactCounts {
    param([Parameter(Mandatory)][string]$ImportedDir)

    if (-not (Test-Path $ImportedDir)) {
        return [pscustomobject]@{ Spskel = 0; Spatlas = 0; Ctex = 0 }
    }

    return [pscustomobject]@{
        Spskel  = @(Get-ChildItem $ImportedDir -Filter '*.spskel' -File -ErrorAction SilentlyContinue).Count
        Spatlas = @(Get-ChildItem $ImportedDir -Filter '*.spatlas' -File -ErrorAction SilentlyContinue).Count
        Ctex    = @(Get-ChildItem $ImportedDir -Filter '*.ctex' -File -ErrorAction SilentlyContinue).Count
    }
}

<#
.SYNOPSIS
    断言 .godot/imported 里已经有 spine 导入产物。
.DESCRIPTION
    .spskel/.spatlas 为 0 说明 spine 扩展没生效，继续导出必然丢骨架。
#>
function Assert-ImportArtifacts {
    param([Parameter(Mandatory)][string]$ImportedDir)

    $counts = Get-ImportArtifactCounts -ImportedDir $ImportedDir
    if ($counts.Spskel -eq 0 -or $counts.Spatlas -eq 0) {
        throw ("缺少 spine 导入产物（.spskel {0} 个 / .spatlas {1} 个），说明扩展没生效，导出后必然丢骨架。`n" -f $counts.Spskel, $counts.Spatlas) +
        '请确认 bin\windows\*.dll 没有被 Godot 占用后重试。'
    }
    return $counts
}
