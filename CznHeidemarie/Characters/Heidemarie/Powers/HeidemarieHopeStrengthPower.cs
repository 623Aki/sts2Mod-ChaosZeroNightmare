using CznHeidemarie.Characters.Heidemarie.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 「市长铭牌」在<b>希望 ≥ 100 层</b>档位给出的「临时力量」载体。
///
/// <para><b>为什么必须自己开一个类</b>：引擎里「临时力量」不是一个能直接 new 的能力 ——
/// <see cref="TemporaryStrengthPower"/> 是<b>抽象</b>基类，它同时承担两件事：
/// 给出 X 点力量（`BeforeApplied` → `StrengthPower`），并在<b>本方回合结束时</b>
/// 把那 X 点力量收回去（`AfterSideTurnEnd`）。官方每个发放临时力量的东西
/// （如 `FlexPotion` → `FlexPotionPower`）都自己开一个子类，本 mod 照做。</para>
///
/// <para><b>OriginModel 是必填的</b>：基类的 <c>Title</c> / 悬停提示会去问
/// 「是谁给了我」，从而在战斗界面把这条 buff 显示成来源的名字
/// （这里 = 遗物「市长铭牌」）。标题 / 描述都复用引擎自带的
/// <c>TEMPORARY_STRENGTH_POWER.*</c> 本地化键，<b>不需要新增文本</b>。</para>
///
/// <para>叠加方式为 <see cref="MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter"/>：
/// 连着几个回合都停在 100~199 档时，力量逐回合累加到本回合结束。</para>
/// </summary>
public sealed class HeidemarieHopeStrengthPower : TemporaryStrengthPower
{
	/// <inheritdoc/>
	/// <remarks>指回发放它的遗物；引擎用它决定这条 buff 的标题与悬停提示。</remarks>
	public override AbstractModel OriginModel => ModelDb.Relic<HeidemarieMayorBadge>();
}
