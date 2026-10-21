using System;
using CznMonster.Content.Audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 塞拉菲翁 ↔ 阿克鲁斯之间「濒死体制」的跨模块读写快门（塞拉菲翁设计文档 §7.2）。
///
/// <para>两个模型之间不直接持有引用，全部经 <see cref="CombatState"/> 派生；
/// 濒死状态的变更（救援拉起）只从这里的写入点走，保证单一事实来源。
/// 代码标识符保留 <c>HalfDeath</c> 系列，对齐 CZN 原条件名 <c>cdp_half_death_mon_amt</c> 便于溯源；
/// 复归拍新标识符用 <c>Revival*</c>（文档 v1.1 命名契约）。</para>
/// </summary>
internal static class SeraphionHalfState
{
	/// <summary>在敌方阵容里找塞拉菲翁；战斗未开始 / 不在场时返回 null。</summary>
	public static Seraphion? FindSeraphion(ICombatState? state)
	{
		return state?.Enemies
			.Select(c => c.Monster)
			.OfType<Seraphion>()
			.FirstOrDefault();
	}

	/// <summary>找「在场且存活」的塞拉菲翁生物实体（治疗 / 击杀目标用）。</summary>
	public static Creature? FindSeraphionCreature(ICombatState? state)
	{
		return state?.Enemies
			.FirstOrDefault(c => c.IsAlive && c.Monster is Seraphion);
	}

	/// <summary>
	/// 塞拉菲翁当前是否处于濒死（v1.2 裁定：读濒死演出状态 <see cref="Seraphion.FadingActive"/>，
	/// **不读**濒死不灭 Power 在场——那张 Power 是开场挂载、整场保留的被动，不能当濒死信号）。
	/// 对齐 CZN 原条件 <c>cdp_half_death_mon_amt_eq01</c>（场上濒死怪==1）：
	/// 本遭遇只有塞拉菲翁能濒死，数量 1 ⟺ 它濒死中。
	/// </summary>
	public static bool IsHalfDeath(ICombatState? state)
	{
		return FindSeraphion(state)?.FadingActive ?? false;
	}

	/// <summary>
	/// 濒死发生的事件触发（<see cref="Seraphion.EnterFading"/> 唯一调用方）：
	/// 通知阿克鲁斯当场改写下一次行动为救援突刺（<see cref="Arkrus.ForceRescueMove"/>），
	/// 救援不滞后一拍——对齐 CZN 原版「条件每拍当场判」的体感。
	/// </summary>
	public static void NotifyArkrusRescue(ICombatState? state)
	{
		state?.Enemies
			.Select(c => c.Monster)
			.OfType<Arkrus>()
			.FirstOrDefault(m => m.Creature.IsAlive)
			?.ForceRescueMove();
	}

	/// <summary>
	/// 阿克鲁斯的「救援颂歌」落点（文档 §4.7 / 验收 T8，v1.2 裁定）：
	/// 治疗 50% 最大生命 → 濒死状态就地解除（原数据 cur_hp≥1 → P1，濒死怪数量当场归 0；
	/// 不清位的话阿克鲁斯下一掷还会再出一条救援链）→ 置复归拍标志，
	/// 塞拉菲翁下一拍走 <c>REVIVAL</c> 复归拍（视觉起身演出保留）。
	/// **不再卸载濒死不灭 Power**（v1.2：濒死保护可循环，对齐 CZN 原数据 P4 可反复进入；
	/// 复第二次濒死照常被拦截。Power 的唯一卸载点剩「阿克鲁斯死亡且塞拉菲翁健康」）。
	/// </summary>
	public static async Task BeginRevivalAsync(Creature seraphionCreature)
	{
		decimal heal = (decimal)Math.Round((double)seraphionCreature.MaxHp * 0.5);
		await CreatureCmd.Heal(seraphionCreature, heal);
		if (seraphionCreature.Monster is Seraphion seraphion)
		{
			seraphion.ExitFading();

			// 起身当场播（v1.2 用户裁定：复活即起身；原数据「cur_hp≥1 → P1」同拍口径），
			// 从跪姿（stance3_death 收尾的 stance3_idle）接到 normal 待机；复归拍从切持盾起
			CznMonsterSfx.Play(seraphion, "revival");
			await CreatureCmd.TriggerAnim(seraphionCreature, "Revival", 1.00f);

			// 复归拍标志 + 当场把下一次行动改为 REVIVAL（意图立刻显示 Attack+Defend）
			seraphion.RequestRevival();
		}
	}
}
