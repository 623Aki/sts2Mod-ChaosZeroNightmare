using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 「临时力量」的统一基类 —— 本 mod 两处发放临时力量（遗物「市长铭牌」的希望 ≥ 100 档、
/// 卡牌「严阵以待」的下一回合档）共用它。
///
/// <para><b>为什么必须有一个子类而不能直接 new 基类</b>：引擎的
/// <see cref="TemporaryStrengthPower"/> 是<b>抽象</b>基类，它同时承担两件事 ——
/// 给出 X 点力量（<c>BeforeApplied</c> → <c>StrengthPower</c>），并在<b>本方回合结束时</b>
/// 把那 X 点力量收回去（<c>AfterSideTurnEnd</c>）。官方每个发放临时力量的东西
/// （如 <c>FlexPotion</c> → <c>FlexPotionPower</c>）都自己开一个子类，本 mod 照做。</para>
///
/// <para><b>为什么是「一个基类 + 两个来源子类」而不是一个类</b>：基类要求
/// <see cref="TemporaryStrengthPower.OriginModel"/> 指向「是谁给了我」，
/// 而 <c>PowerCmd.Apply</c> 只按类型创建能力、没有传入来源的入口，
/// 所以来源不同的两处只能各开一个子类（标题 / 悬停提示因此分别是
/// 「市长铭牌」与「严阵以待」）。共用的部分（图标、说明口径）全部收在本基类。</para>
///
/// <para><b>图标</b>：原版 <see cref="PowerModel"/> 自己不包含图标逻辑 ——
/// <c>PackedIconPath</c> 与 <c>BigIconPath</c> 都是非虚成员，按
/// <c>atlases/power_atlas.sprites/&lt;Id.Entry&gt;.tres</c> 与
/// <c>powers/&lt;Id.Entry&gt;.png</c> 拼路径，取不到就回落
/// <c>res://images/powers/missing_power.png</c>。本 mod 的能力 Id 是自定义的，
/// 于是两个子类会分别去找 <c>heidemarie_hope_strength_power.*</c> 与
/// <c>heidemarie_temporary_strength_power.*</c>，天然对不上。</para>
///
/// <para><b>解决办法</b>（不使用任何第三方库，见工程铁律 5）：由
/// <c>Registration/HeidemariePowerIconOverridePatch</c> 用 Harmony 改写
/// <c>get_PackedIconPath</c> / <c>get_ResolvedBigIconPath</c> 的返回值，
/// 只对 <see cref="HeidemarieTemporaryStrengthPowerBase"/> 的实例生效（其它能力原样返回，
/// 零侵入）。两个来源因此共用同一张
/// <c>res://images/powers/heidemarie_temporary_strength_power.png</c>（256×256 大图）。</para>
///
/// <para><b>为什么小图也用同一张</b>：引擎 <c>AtlasResourceLoader</c> 对
/// <c>power_atlas</c> 的 sprite 有磁盘回落 —— 命中不到图集时就读
/// <c>res://images/powers/&lt;sprite&gt;.png</c>，也就是本 mod 其它能力
/// （极光之光 / 极夜……）实际生效的那条路。这里显式指向同一文件，
/// 与全 mod 的表现保持一致，不引入第二种缩放口径。</para>
/// </summary>
public abstract class HeidemarieTemporaryStrengthPowerBase : TemporaryStrengthPower
{
	/// <summary>
	/// 本族能力共用的图标路径：大图 256×256，同时充当战斗内小图的回落图。
	///
	/// <para>工程里 <c>images/</c> 树位于 Godot 工程根（<c>res://images/...</c>），
	/// 不在 <c>res://CznHeidemarie/</c> 下 —— 写错前缀会让资源加载失败并被
	/// <c>AssetCache</c> 永久标记（见 <c>HeidemarieEnergyIconRedirect</c> 的教训）。</para>
	/// </summary>
	internal const string SharedIconPath = "res://images/powers/heidemarie_temporary_strength_power.png";

	/// <summary>
	/// 来源模型由两个子类各自声明（「市长铭牌」/「严阵以待」），
	/// 基类不指定，因此这里只需要把抽象成员继续往下传。
	/// </summary>
	public abstract override AbstractModel OriginModel { get; }
}
