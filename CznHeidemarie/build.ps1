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
      3. Godot --export-pack ── 先导出到 <mod>.pck.new，正式 pck 不动
      4. 自检 ── 流式解析 pck 真实文件索引（只读头部 + 索引区，不整包读入）
                + 体积区间 + 规模下限 + 关键资源命中
      5. 自检通过才把 .new 换成正式 pck ── 导出失败 / 进程被杀时，
         上一版 pck 原封不动，不会出现「有 dll 没 pck」的坏状态

    为什么 Godot 调用要按「产物就绪」判定，而不是直接等进程结束：
    Godot 导出 pck 时内部还会跑一遍 dotnet publish（本项目这一步在 Godot 里会失败，
    耗时 15 秒 ~ 2 分钟），pck 是在它之后才写出的；而 pck 写完后 Godot 进程往往
    卡在收尾不再退出（实测只剩 1 个线程、CPU 恒为 0）。若直接用调用运算符 &，
    脚本会永久阻塞在那一行 —— 卡点在导出之后，很容易被误判成「卡在校验 pck」。
    现在改为等「pck 已写出且大小稳定」即认定导出完成，再给进程一点自然退出的时间，
    仍不退就强杀；pck 完不完整一律以自检结果为准。

    自检为什么必须解析索引而不是搜文本：
    pck 里打进了 518 个 .cs 源码，任何出现在 C# 字符串常量里的资源路径
    都会让「全文 Contains」误判为存在。只有读文件表才能得到真值。

.PARAMETER Configuration
    构建配置，默认 Debug，可传 Release。

.PARAMETER SkipBuild
    跳过 dotnet build，只重导 pck。会校验 dll 已存在，但无法保证它是最新的。

.PARAMETER SkipImport
    跳过 Godot --import，确定资源没变时可用（省时间）。

.PARAMETER Rebuild
    强制完整重编译（dotnet build -t:Rebuild）。
    dotnet build 是增量的：改了 .cs 但中间 DLL 时间没变时（少见，通常是时钟/缓存异常），
    用它与 CznMonster 的行为保持一致。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1

.EXAMPLE
    # 只重导一次 pck，不重新编译
    .\build.ps1 -SkipBuild

.NOTES
    Godot 调用判定、pck 文件表解析、进程占用检查等通用逻辑在
    工作区根的 build-common.ps1；本脚本只保留角色专属的资源清单与流程。
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$SkipImport,
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
# 导出目标：先落在工程内的暂存目录（必须以 .pck 结尾，Godot 会校验扩展名），
# 自检通过后才移到 mods。失败/被杀时正式 pck 原封不动。
$PckStageDir = Join-Path $ProjectDir '.godot\build\pack'
$PckNew      = Join-Path $PckStageDir "$ModName.pck"
$DllPath     = Join-Path $ModDir "$ModName.dll"
$JsonPath    = Join-Path $ModDir "$ModName.json"
$ImportedDir = Join-Path $ProjectDir '.godot\imported'
$ExtListPath = Join-Path $ProjectDir '.godot\extension_list.cfg'
$GdextPath   = Join-Path $ProjectDir 'bin\spine_godot_extension.gdextension'
$PresetPath  = Join-Path $ProjectDir 'export_presets.cfg'
$GodotLogDir = Join-Path $ProjectDir '.godot\build'

# ──────────────────────────────────────────────────────────────────────────
# 必需资源清单（精确路径）
#
# 取值依据：当前 pck 的真实文件表（2031 条）逐条比对得到，不是猜的。
# 注意三个反直觉的事实：
#   1. 源资源本体（.skel/.atlas/.png/.ogg/.webp）不进 pck，
#      进 pck 的是它们的 .import 索引文件。所以这里校验的是 .import。
#   2. 扩展只打进了描述文件 bin/spine_godot_extension.gdextension，
#      扩展的 DLL（bin/windows/*.dll）从来不在 pck 里 —— 运行期由游戏本体提供。
#   3. .gd 脚本不进 pck 本体：script_export_mode=2 下导出器把它们编译成
#      .gdc 字节码，并额外生成 .gd.remap 索引；场景 tscn 里的引用已重映射。
#      所以校验 GDScript 演出场景时锁的是 .gdc。
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
    'CznHeidemarie/localization/zhs/powers.json',
    'CznHeidemarie/localization/zhs/relics.json',
    'CznHeidemarie/localization/zhs/settings.json',
    'CznHeidemarie/localization/zhs/ancients.json',

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
    'CznHeidemarie/images/defeat/collapse_30093_01.png.import',
    'CznHeidemarie/images/defeat/collapse_30093_02.png.import',

    # 败北 CG（Tiphera 类型 gloom 演出）：官方共享 gloom Spine FX 六件套的
    # 入口（skel.import + _skel_data.tres；atlas/png .import 与导入产物由
    # RequiredPrefix 覆盖）+ cha_node 12 槽时间线 + PMA 双材质 +
    # GDScript 演出场景（编译态 .gdc，见文件头说明 3）
    'CznHeidemarie/data/screen_fx_collapse_gloom_cha_node.json',
    'CznHeidemarie/scenes/defeat_gloom_overlay.tscn',
    'CznHeidemarie/scenes/defeat_gloom_overlay.gdc',
    'CznHeidemarie/scenes/defeat_cg_framing.gdc',
    'CznHeidemarie/materials/heidemarie_gloom_pma_alpha.tres',
    'CznHeidemarie/materials/heidemarie_gloom_pma_additive.tres',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_bg1/screen_fx_collapse_gloom_sin_bg1.skel.import',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_bg1/screen_fx_collapse_gloom_sin_bg1_skel_data.tres',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_bg2/screen_fx_collapse_gloom_sin_bg2.skel.import',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_bg2/screen_fx_collapse_gloom_sin_bg2_skel_data.tres',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_bg3/screen_fx_collapse_gloom_sin_bg3.skel.import',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_bg3/screen_fx_collapse_gloom_sin_bg3_skel_data.tres',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_glass/screen_fx_collapse_gloom_sin_glass.skel.import',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_glass/screen_fx_collapse_gloom_sin_glass_skel_data.tres',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_lighting/screen_fx_collapse_gloom_sin_lighting.skel.import',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_lighting/screen_fx_collapse_gloom_sin_lighting_skel_data.tres',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_screen_refl/screen_fx_collapse_gloom_sin_screen_refl.skel.import',
    'CznHeidemarie/animations/effects/collapse_gloom/screen_fx_collapse_gloom_sin_screen_refl/screen_fx_collapse_gloom_sin_screen_refl_skel_data.tres',

    # 卡面：卡框材质 + 「解放极光」的全屏卡原画（静态兜底）
    'materials/cards/frames/card_frame_heidemarie_mat.tres',
    'CznHeidemarie/images/card_art/heidemarie_liberated_aurora.png.import',

    # 动态卡面：Spine 三件套 + 骨架数据 + 渲染器场景
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora.skel.import',
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora.atlas.import',
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora.png.import',
    'CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora_skel_data.tres',
    'CznHeidemarie/scenes/cards/heidemarie_card_art.tscn',

    # 「终焉」动态卡面（素材区 card/unique_30093_01，参考「解放极光」）
    'CznHeidemarie/animations/cards/heidemarie_card_terminus.skel.import',
    'CznHeidemarie/animations/cards/heidemarie_card_terminus.atlas.import',
    'CznHeidemarie/animations/cards/heidemarie_card_terminus.png.import',
    'CznHeidemarie/animations/cards/heidemarie_card_terminus_skel_data.tres',

    # 「剑之乐园」动态卡面（三模型组装）：ux 插画三件套的骨架数据。
    # 骨架 / 图集 / 贴图本体已由 RequiredPrefix 与数量下限覆盖，这里只锁定入口文件。
    'CznHeidemarie/animations/effects/heidemarie_30093_ux_illust_1_skel_data.tres',
    'CznHeidemarie/animations/effects/heidemarie_30093_ux_illust_2_skel_data.tres',
    'CznHeidemarie/animations/effects/heidemarie_30093_ux_illust_3_skel_data.tres',

    # 初始遗物「市长铭牌」图标：大图 256 / 小图 85 / 描边 85
    #
    # ⚠️ 这三张挂的是 res://images/relics/（**工程根**的 images），不是 res://CznHeidemarie/images/。
    #    理由见 HeidemarieMayorBadge.cs：图标走 ImageHelper.GetImagePath("relics/…")，
    #    它是引擎约定的「角色素材图」目录，前缀由引擎给，不能自己加 CznHeidemarie/。
    #    2026-10-15 修正：此处原先写成 CznHeidemarie/images/relics/…，导致每次构建
    #    自检都在这里误报缺件（资源其实一直在）。
    'images/relics/heidemarie_mayor_badge.png.import',
    'images/relics/heidemarie_mayor_badge_small.png.import',
    'images/relics/heidemarie_mayor_badge_outline.png.import',

    # 可获取遗物「和平奖章」图标：大图 256 / 小图 85 / 描边 85（同市长铭牌三件套）
    'images/relics/heidemarie_peace_medal.png.import',
    'images/relics/heidemarie_peace_medal_small.png.import',
    'images/relics/heidemarie_peace_medal_outline.png.import',

    # 能力图标：每个能力两张 —— 256 大图（PowerModel.BigIconPath → images/powers/）
    # 与 64 小图（PowerModel.PackedIconPath → power_atlas 的 .tres 指向 images/packed/powers/）。
    # 同 relics：挂工程根 res://images/，前缀由引擎给，不能自己加 CznHeidemarie/。
    'images/powers/heidemarie_aurora_light_power.png.import',
    'images/powers/heidemarie_daylight_power.png.import',
    'images/powers/heidemarie_night_power.png.import',
    'images/powers/heidemarie_north_polar_power.png.import',
    'images/powers/heidemarie_wall_of_swords_power.png.import',
    'images/powers/heidemarie_old_oath_power.png.import',
    'images/powers/heidemarie_hope_power.png.import',
    'images/packed/powers/heidemarie_aurora_light_power.png.import',
    'images/packed/powers/heidemarie_daylight_power.png.import',
    'images/packed/powers/heidemarie_night_power.png.import',
    'images/packed/powers/heidemarie_north_polar_power.png.import',
    'images/packed/powers/heidemarie_wall_of_swords_power.png.import',
    'images/packed/powers/heidemarie_old_oath_power.png.import',
    'images/packed/powers/heidemarie_hope_power.png.import',
    'images/atlases/power_atlas.sprites/heidemarie_aurora_light_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_daylight_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_night_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_north_polar_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_wall_of_swords_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_old_oath_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_hope_power.tres',

    # 第二轮补齐的三个能力图标：无脸女 / 情绪爆发 / 严阵以待（各 256 大图 + 64 小图 + 图集）
    'images/powers/heidemarie_faceless_woman_power.png.import',
    'images/powers/heidemarie_emotional_outburst_power.png.import',
    'images/powers/heidemarie_stand_ready_power.png.import',
    'images/packed/powers/heidemarie_faceless_woman_power.png.import',
    'images/packed/powers/heidemarie_emotional_outburst_power.png.import',
    'images/packed/powers/heidemarie_stand_ready_power.png.import',
    'images/atlases/power_atlas.sprites/heidemarie_faceless_woman_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_emotional_outburst_power.tres',
    'images/atlases/power_atlas.sprites/heidemarie_stand_ready_power.tres',

    # 临时力量载体（TemporaryStrengthPower 子类）的 256/64/图集
    # 两个发放来源（市长铭牌 / 严阵以待）共用同一张图标，见
    # Characters/Heidemarie/Powers/HeidemarieTemporaryStrengthPowerBase.cs
    # 与 Registration/HeidemariePowerIconOverridePatch.cs
    'images/powers/heidemarie_temporary_strength_power.png.import',
    'images/packed/powers/heidemarie_temporary_strength_power.png.import',
    'images/atlases/power_atlas.sprites/heidemarie_temporary_strength_power.tres'
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
    '.godot/imported/collapse_30093_01.png-',        # 败北 CG .ctex
    '.godot/imported/collapse_30093_02.png-',        # 败北 CG .ctex
    # 败北 CG gloom 演出：六套官方 Spine FX 的导入产物（各 .spskel/.spatlas/.ctex）
    '.godot/imported/screen_fx_collapse_gloom_sin_bg1.skel-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg1.atlas-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg1.png-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg2.skel-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg2.atlas-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg2.png-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg3.skel-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg3.atlas-',
    '.godot/imported/screen_fx_collapse_gloom_sin_bg3.png-',
    '.godot/imported/screen_fx_collapse_gloom_sin_glass.skel-',
    '.godot/imported/screen_fx_collapse_gloom_sin_glass.atlas-',
    '.godot/imported/screen_fx_collapse_gloom_sin_glass.png-',
    '.godot/imported/screen_fx_collapse_gloom_sin_lighting.skel-',
    '.godot/imported/screen_fx_collapse_gloom_sin_lighting.atlas-',
    '.godot/imported/screen_fx_collapse_gloom_sin_lighting.png-',
    '.godot/imported/screen_fx_collapse_gloom_sin_screen_refl.skel-',
    '.godot/imported/screen_fx_collapse_gloom_sin_screen_refl.atlas-',
    '.godot/imported/screen_fx_collapse_gloom_sin_screen_refl.png-',
    '.godot/imported/heidemarie_liberated_aurora.png-', # 全屏卡原画 .ctex
    '.godot/imported/heidemarie_card_liberated_aurora.skel-',  # 卡面骨架 .spskel
    '.godot/imported/heidemarie_card_liberated_aurora.atlas-', # 卡面图集 .spatlas
    '.godot/imported/heidemarie_card_liberated_aurora.png-',   # 卡面图集贴图 .ctex
    '.godot/imported/heidemarie_card_terminus.skel-',          # 终焉卡面骨架 .spskel
    '.godot/imported/heidemarie_card_terminus.atlas-',         # 终焉卡面图集 .spatlas
    '.godot/imported/heidemarie_card_terminus.png-'            # 终焉卡面图集贴图 .ctex
)

# ──────────────────────────────────────────────────────────────────────────
# 规模下限 / 体积区间
#
# 实测当前 pck：160,650,796 字节（约 153.2 MB）、2031 条文件表条目、
#   .spskel 160 / .spatlas 161 / .ctex 270。
# 骨架丢失的故障态是 pck 缩到 0.53 MB、上述导入产物全灭。
# 下面留了余量：既能拦住故障态，又允许正常增删几个动画。
# ──────────────────────────────────────────────────────────────────────────
$MinEntryCount  = 1500
$MinSpskelCount = 140
$MinSpatlasCount = 140
$MinCtexCount   = 180
$MinPckBytes    = 40MB      # 故障态约 0.5 MB，正常约 153 MB
$MaxPckBytes    = 400MB     # 防止误把别的大文件导进来

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
    $binExcluded = Test-BinExcluded -ProjectDir $ProjectDir
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

    # 导出预设：导出开关一旦被改坏，pck 内容会静默缺斤少两，这里提前拦。
    # 解析按 [preset.N] 分段，避免同一份 cfg 里别的预设把值盖掉。
    $null = Assert-ExportPreset -Path $PresetPath -PresetName $PresetName `
        -ExpectedPckPath $PckOut -ManifestName "$ModName.json"
    Write-Ok "导出预设「$PresetName」开关正常"

    # 关掉会锁住本工程文件的进程，并确认文件已解锁：
    #   - 打开了本工程的 Godot 会锁住 bin\windows\*.dll，扩展加载失败会让骨架整体从 pck 消失
    #   - 运行中的游戏会锁住 mods 目录的 <mod>.dll，让 post-build 复制报 MSB3021
    # 只关本工程的 Godot + 游戏；别的工程 / 别人的编辑器不再被误伤。
    $spineDlls = @(Get-ChildItem (Join-Path $ProjectDir 'bin\windows') -Filter '*.dll' -ErrorAction SilentlyContinue)
    $lockTargets = @($DllPath) + @($spineDlls | Select-Object -ExpandProperty FullName)
    $null = Stop-BlockingProcesses -ProjectDir $ProjectDir -LockPaths $lockTargets
    Write-Ok 'spine 扩展与 mods 里的 dll 均未被占用'

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
        $buildLabel = if ($Rebuild) { "$Configuration，Rebuild" } else { $Configuration }
        Write-Step "编译 C#（$buildLabel）"
        Invoke-DotnetBuild -ProjectDir $ProjectDir -ProjectFile "$ModName.csproj" `
            -Configuration $Configuration -Rebuild:$Rebuild
        Write-Ok '编译成功'
    }

    # 编译产物断言：dll/json 必须在 mods 目录，且 dll 必须是本次编译的产物
    if (-not (Test-Path $DllPath)) {
        throw "mods 目录里没有 dll：$DllPath`n（csproj 的 post-build 会把 dll 复制过去；-SkipBuild 时请先完整构建一次）"
    }
    if (-not (Test-Path $JsonPath)) {
        throw "mods 目录里没有清单 json：$JsonPath`n（csproj 的 post-build 会复制它，请确认工程根有 $ModName.json）"
    }

    $dllItem = Get-Item $DllPath
    if ($SkipBuild) {
        Write-Note "dll 已存在（$($dllItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))，未校验是否为最新编译产物）"
    }
    else {
        # 不能用「dll 时间戳 ≥ 本次编译开始时间」判断 —— dotnet build 是增量的，
        # 源码没改时输出 dll 的时间戳保持上一次的值，post-build 的 Copy
        # SkipUnchangedFiles 也正因此不再复制，会把正常情况误判成复制失败。
        # 真正要保证的是「mods 里的 dll 不比源码旧」，即没有漏掉某次编译。
        $newestSource = Get-NewestSourceFile -ProjectDir $ProjectDir
        if ($newestSource -and $dllItem.LastWriteTime -lt $newestSource.LastWriteTime.AddSeconds(-10)) {
            $msg = "mods 目录的 dll 比最新源码还旧，post-build 复制没生效：`n" +
            "  dll  $($dllItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))`n" +
            "  源码 $($newestSource.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))  $($newestSource.Name)`n" +
            "先重跑一次；仍然如此就加 -Rebuild 强制完整重编译。"
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

    # ── 步骤 2. 导入资源 ─────────────────────────────────────────────────
    if ($SkipImport) {
        Write-Step '跳过资源导入（-SkipImport）'
    }
    else {
        Write-Step '导入资源（Godot --import）'
        # --import 只关心 spine 扩展与 mod 资源相关的报错，
        # 别的地方（如 res://src/... 这类跨 pck 引用）是预期噪音，刷屏无用。
        $imp = Invoke-GodotTimed -GodotExe $GodotExe -LogDir $GodotLogDir `
            -Arguments @('--headless', '--path', $ProjectDir, '--import') `
            -LogName 'import' -TimeoutSec 900
        if ($imp.Status -ne 'exited') {
            Write-Warn "--import 以 $($imp.Status) 结束（耗时 $($imp.ElapsedSec) 秒），已强制收尾；导入产物是否齐全由下一步判定"
        }
        $imp.Log |
            Select-String -Pattern 'Cannot open GDExtension|Error loading extension|Failed loading resource: res://CznHeidemarie' |
            ForEach-Object { Write-Warn $_.ToString().Trim() }
    }

    # 静态资源导入状态：任何 .import 带 valid=false 都意味着「源图在、贴图没导入」。
    # Godot 会把这种 .import 照样打进 pck，但 ResourceLoader.Exists 判 false，
    # 卡面 / 图标就会静默回落到占位图 —— 2026-10-18 的微光 / 北极圈卡面不显示就是它。
    # 放在 --import 之后检查：--import 本身就会重新导入 valid=false 的资源，
    # 提前拦截会把「重跑一次就好」的情况误判成失败。
    $checkedImports = Assert-ValidImports -ProjectDir $ProjectDir
    Write-Ok "所有 .import 导入状态有效（检查 $checkedImports 个）"

    $importCounts = Assert-ImportArtifacts -ImportedDir $ImportedDir
    Write-Ok "导入产物就位：.spskel $($importCounts.Spskel) 个、.spatlas $($importCounts.Spatlas) 个"

    # ── 步骤 3. 导出 pck ─────────────────────────────────────────────────
    Write-Step '导出 pck'
    New-Item -ItemType Directory -Force -Path $ModDir | Out-Null

    # 先导出到工程内的暂存目录（.godot/build/pack，不会被扫进包），
    # 自检全部通过后才移到 mods。这样导出中途失败 / 进程被强杀时，
    # mods 里仍是上一版可用的 pck；那个目录也不会有半成品干扰。
    New-Item -ItemType Directory -Force -Path $PckStageDir | Out-Null
    Remove-Item $PckNew -Force -ErrorAction SilentlyContinue

    # 参数按「一个元素一个参数」传：预设名「Windows Desktop」含空格，
    # 引号由 Invoke-GodotTimed 内部统一补，不要在这里自己拼字符串。
    $exp = Invoke-GodotTimed -GodotExe $GodotExe -LogDir $GodotLogDir `
        -Arguments @('--headless', '--path', $ProjectDir, '--export-pack', $PresetName, $PckNew) `
        -LogName 'export' -ArtifactPath $PckNew -ArtifactMinBytes $MinPckBytes -TimeoutSec 600
    $exp.Log | Select-Object -Last 3 | ForEach-Object { Write-Note $_.ToString().Trim() }

    # Godot 会在导出时自己跑一遍 dotnet publish。这一步的产物不进 pck（预设
    # dotnet/embed_build_outputs=false，实测 pck 内 .dll 条目为 0），mod 的 dll 由
    # csproj 的 post-build 交付到 mods 目录，所以它成功与否都不影响 pck。
    # 它偶尔会因为源码瞬时状态失败（headless 下看不到 MSBuild 详情，细节在
    # %APPDATA%\Godot\mono\build_logs\<hash>_ExportRelease\msbuild_log.txt）。
    if ($exp.Log | Select-String -Pattern 'Failed to build project|completed with warnings' -Quiet) {
        Write-Note 'Godot 内部那次 dotnet publish 没成功，属已知现象，不影响 pck'
    }

    # 判定只认产物，不认退出码，原因有二：
    #   ① 本机 PS 5.1 读 Start-Process -PassThru 的 ExitCode 恒为 $null（实测），
    #      拿它比较必然误判成「导出失败」；
    #   ② Godot 写完 pck 后经常不退出，是我们按需强杀的，退出码本来就无意义。
    # pck 完不完整由下面的自检负责：magic / 格式版本 / 条目数 / 关键资源命中 / 体积区间。
    if (-not (Test-Path $PckNew)) {
        throw "Godot 导出没有产出 pck（结束状态 $($exp.Status)）。完整日志：$($exp.LogDir)"
    }
    if ($exp.Status -eq 'artifact-killed') {
        Write-Note "Godot 写完 pck 后没有自行退出，已强制结束（导出耗时 $($exp.ElapsedSec) 秒）"
    }
    elseif ($exp.Status -eq 'timeout-killed') {
        Write-Warn "Godot 导出超时被强制结束（耗时 $($exp.ElapsedSec) 秒），pck 是否完整交由自检判定"
    }
    Write-Ok "导出完成（耗时 $($exp.ElapsedSec) 秒），开始自检"

    # ── 步骤 4. 自检 ─────────────────────────────────────────────────────
    Write-Step '自检'

    $pckItem = Get-Item $PckNew
    $pckSize = [long]$pckItem.Length
    Write-Note ("pck 体积 {0:N2} MB" -f ($pckSize / 1MB))

    if ($pckSize -lt $MinPckBytes -or $pckSize -gt $MaxPckBytes) {
        throw ("pck 体积 {0:N2} MB 超出预期区间 {1:N0}–{2:N0} MB。`n太小通常是骨架丢失（故障态约 0.5 MB），排查顺序：`n  ① .gitignore 是否含 bin/ ② bin/.gdignore 是否存在 ③ bin/ 下扩展是否齐全" -f `
            ($pckSize / 1MB), ($MinPckBytes / 1MB), ($MaxPckBytes / 1MB))
    }

    # 解析真实文件表。pck 里含 518 个 .cs，全文搜文本会被源码里的路径常量骗到，
    # 所以必须读索引拿真值。Read-PckIndex 只读头部 + 索引区，不整包读入内存。
    $swVerify = [Diagnostics.Stopwatch]::StartNew()
    $idx = Read-PckIndex -Path $PckNew
    $swVerify.Stop()
    Write-Note "文件表解析成功：$($idx.Count) 条条目，格式 ver=$($idx.Version) pack=$($idx.PackVersion)（耗时 $($swVerify.ElapsedMilliseconds) ms）"

    if ($idx.Version -ne '4.5.1') {
        throw "pck 格式版本是 $($idx.Version)，不是游戏要求的 4.5.1 —— 多半是用了更高版本的 Godot 导出。"
    }

    # 4-1. 规模下限
    if ($idx.Count -lt $MinEntryCount) {
        throw "pck 只有 $($idx.Count) 条条目（下限 $MinEntryCount），资源疑似大面积缺失。"
    }

    # 计数与关键资源命中由 build-common 的 Test-PckContent 一次遍历完成
    $coverage = Test-PckContent -Index $idx -RequiredExact $RequiredExact -RequiredPrefix $RequiredPrefix

    $scale = ".spskel $($coverage.Spskel) / .spatlas $($coverage.Spatlas) / .ctex $($coverage.Ctex)"
    if ($coverage.Spskel -lt $MinSpskelCount -or $coverage.Spatlas -lt $MinSpatlasCount -or $coverage.Ctex -lt $MinCtexCount) {
        throw "pck 里导入产物数量不足（$scale），下限 .spskel $MinSpskelCount / .spatlas $MinSpatlasCount / .ctex $MinCtexCount。`n骨架多半丢了，见「打包踩坑与解决方案.md」第五节决策树。"
    }
    Write-Ok "pck 内导入产物数量正常（$scale）"

    # 4-2. 关键资源精确匹配
    if ($coverage.MissingExact.Count -gt 0) {
        throw "pck 缺少关键资源：`n  - $($coverage.MissingExact -join "`n  - ")"
    }

    # 4-3. 导入产物前缀匹配（后缀是内容哈希，只能比对前缀）
    if ($coverage.MissingPrefix.Count -gt 0) {
        throw "pck 缺少导入产物（前缀）：`n  - $($coverage.MissingPrefix -join "`n  - ")`n说明源资源在，但导入产物没进 pck。"
    }

    Write-Ok "关键资源全部命中（精确 $($RequiredExact.Count) 项 + 前缀 $($RequiredPrefix.Count) 项）"
    if ($idx.TailBytes -ne 0) {
        Write-Warn "文件表解析后有 $($idx.TailBytes) 字节尾数据（可能是格式扩展），非致命。"
    }

    # 自检通过才把新包换上去（同盘 Move 是重命名，瞬间完成）
    Move-Item $PckNew $PckOut -Force
    Write-Ok "pck 已更新：$PckOut（$(Format-Size ([long](Get-Item $PckOut).Length))）"

    # ── 步骤 5. 交付清单 ─────────────────────────────────────────────────
    Write-Step '交付物'
    Show-Deliverables -Directory $ModDir

    Write-Host "构建完成" -ForegroundColor Green
    Write-Host "  $ModDir" -ForegroundColor Green
    Write-Host ""
    exit 0
}
catch {
    # 正式 pck 全程没被碰过，这里只需清掉暂存区的半成品
    # （进程被强杀时 catch 不会执行，但残留文件下次导出前也会被清掉）
    Remove-Item $PckNew -Force -ErrorAction SilentlyContinue
    Write-Host ""
    Write-Host "构建失败：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  mods 里的上一版 pck 未被改动：$PckOut" -ForegroundColor Yellow
    exit 1
}