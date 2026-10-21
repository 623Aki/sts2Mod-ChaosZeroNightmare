using System.Threading.Tasks;
using CznMonster.Content.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Powers;

/// <summary>
/// 塞拉菲翁的「濒死不灭」（塞拉菲翁设计文档 §4.6 / §4.7 / T5–T9）：
/// 只要阿克鲁斯存活，塞拉菲翁不会死亡——生命降到 0 时被拦截至 1 点生命并陷入濒死
/// （<c>FADING</c>，垂死免疫生命流失）；复活由阿克鲁斯的救援颂歌触发，塞拉菲翁
/// 下一拍以复归拍（<c>REVIVAL</c>）回稳。
///
/// <para>实现参照原生 <see cref="T: MegaCrit.Sts2.Core.Models.Powers.Mocks.MockPreventDeathPower"/>：
/// <see cref="ShouldDie"/> 对 Owner 返回 false；<see cref="AfterDeath"/> 监听阿克鲁斯的死亡
/// ——阿克鲁斯死亡时，濒死的塞拉菲翁随之真死（force 击杀），健康的塞拉菲翁解开保护。
/// （游戏侧没有 <c>MonsterModel.OnAfterDeath</c> 钩子，Power 是唯一的 hook listener 入口。）</para>
///
/// <para>命名契约：标识符保留 <c>HalfDeath</c> 系列（对齐 CZN 原条件 <c>cdp_half_death_mon_amt</c>，
/// 文档 v1.1 裁定），中文表述统一「濒死」。</para>
/// </summary>
public sealed class SeraphionHalfDeathPower : PowerModel
{
	/// <inheritdoc />
	/// <remarks>
	/// 用 Buff：不会被 Artifact 挡掉（同 <see cref="PolemicaBroodPower"/> 的理由）——
	/// 这不是玩家的减益，拆迁它等于绕过死亡保护，超出设计意图。
	/// </remarks>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc />
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc />
	/// <remarks>被动的生命锚定，不播能力特效（自定义 Power 没有对应 vfx 资源）。</remarks>
	public override bool ShouldPlayVfx => false;

	/// <inheritdoc />
	/// <remarks>保护 / 免疫不随玩家数缩放。</remarks>
	public override bool ShouldScaleInMultiplayer => false;

	/// <inheritdoc />
	/// <remarks>
	/// Owner（塞拉菲翁）不死；其它生物照常（原判走 <c>true</c>，零侵入，同 Mock 写法反向）。
	/// </remarks>
	public override bool ShouldDie(Creature creature)
	{
		return creature != Owner;
	}

	/// <inheritdoc />
	/// <remarks>
	/// 濒死入场（文档 §4.6 / T5）：拉回 1 点生命 + 播濒死过渡（<c>normal_to_stance3</c> →
	/// <c>stance3_idle</c> 循环，无采样、静音侧写见 T15）+ 震屏 + 把行动切到 <c>FADING</c>。
	/// 已在濒死时幂等返回（同一轮多段伤害只做一次入场演出）。
	/// </remarks>
	public override async Task AfterPreventingDeath(Creature creature)
	{
		if (creature?.Monster is not Seraphion seraphion || seraphion.FadingActive)
		{
			return;
		}

		await CreatureCmd.Heal(creature, 1m);
		NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
		seraphion.EnterFading();

		// 濒死入场当场倒地：stance3_death（2.37s）→ 收在 stance3_idle（跪姿循环）；
		// 复归拍的 stance3_to_normal（起身）正好从跪姿起（用户裁定 2026-10-21：入场即倒地，不等下一拍）
		await CreatureCmd.TriggerAnim(creature, "EnterDying", 2.37f);

		// 清自身全部 buff/debuff（用户裁定 2026-10-21）；濒死不灭本体保留（保护机制在它身上）
		foreach (PowerModel power in creature.Powers.ToArray())
		{
			if (power is not SeraphionHalfDeathPower)
			{
				await PowerCmd.Remove(power);
			}
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// 垂死免疫（文档 §4.6 / T6）：濒死期间塞拉菲翁不承受任何生命流失（伤害演出照常）。
	/// 只在濒死后才生效——战斗开始到濒死之间必须可以被正常打到 0，否则死亡拦截永远不触发。
	/// </remarks>
	public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return target == Owner && Owner?.Monster is Seraphion { FadingActive: true } ? 0m : amount;
	}

	/// <inheritdoc />
	/// <remarks>
	/// 濒死的塞拉菲翁不可被指定为目标——任何卡牌（用户裁定 2026-10-21）；其它生物不受影响。
	/// 颂歌治疗后（FadingActive=false）恢复可指定。UI 选目标经
	/// <see cref="T: MegaCrit.Sts2.Core.Hooks.Hook.ShouldAllowTargeting"/> 逐模型询问（NTargetManager）。
	/// </remarks>
	public override bool ShouldAllowTargeting(Creature target)
	{
		return !(target == Owner && Owner?.Monster is Seraphion { FadingActive: true });
	}

	/// <inheritdoc />
	/// <remarks>
	/// 同上，覆盖 AoE / 全体效果：濒死的塞拉菲翁整体排除在受击列表外（伤害本来已归零，
	/// 排除后受击演出与卡牌预览更干净）。对其它生物恒放行。
	/// </remarks>
	public override bool ShouldAllowHitting(Creature creature)
	{
		return !(creature == Owner && Owner?.Monster is Seraphion { FadingActive: true });
	}

	/// <inheritdoc />
	/// <remarks>
	/// 伴生死亡（文档 §4.7 / T9 / T10）：<see cref="T: CznMonster.Content.Monsters.Arkrus"/> 死亡时——
	/// 濒死的塞拉菲翁随之真死（<c>force</c> 绕过本 Power 自己的拦截）；健康的塞拉菲翁失去保护，战斗继续，
	/// 下一拍按「无同伴」分支进入终局。
	/// </remarks>
	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (creature?.Monster is not Arkrus || Owner is not { IsDead: false } owner)
		{
			return;
		}

		if (Owner?.Monster is Seraphion { FadingActive: true })
		{
			await CreatureCmd.Kill(owner, force: true);
		}
		else
		{
			await PowerCmd.Remove<SeraphionHalfDeathPower>(owner);
		}
	}
}
