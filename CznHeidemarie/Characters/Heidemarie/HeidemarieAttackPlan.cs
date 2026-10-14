using CznHeidemarie.Integration;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// 一次攻击演出的编排。
///
/// <para>提炼自参考 mod 的 <c>CombatPresentationAttackPlan</c>（Nine 的 CznCore 框架）。
/// 它原本还带多段命中数组、语音提示、运镜逼近、目标遮挡策略等字段，
/// 那是为「一个框架驱动多个角色」准备的；本项目一个角色、一套骨架，
/// 只需保留真正影响手感的两个量：</para>
///
/// <list type="bullet">
///   <item><see cref="Trigger"/> —— 打哪个动画触发器；</item>
///   <item><see cref="HitDelaySeconds"/> —— 伤害在起手之后多久结算，
/// 由骨架实测时长推导（见 <see cref="HeidemarieAnimationSeconds"/>），不是估的。</item>
/// </list>
///
/// <para>这里没有播放速度：攻击链路走 <c>AttackCommand.WithAttackerAnim</c>，
/// 它只接受触发器名与等待时长，注入不了 timeScale。速度控制走技能卡那条路
/// （<see cref="HeidemarieAnimation.PlayTriggerAsync"/>）。</para>
/// </summary>
/// <param name="Trigger">角色 <c>GenerateAnimator</c> 里注册的触发器名。</param>
/// <param name="HitDelaySeconds">起手动画播完后、伤害结算前的等待秒数。</param>
internal sealed record HeidemarieAttackPlan(string Trigger, float HitDelaySeconds)
{
	/// <summary>普通打击：attack_ready（0.2s）→ attack_play1（0.6667s）。</summary>
	public static readonly HeidemarieAttackPlan Basic =
		new("Attack", HeidemarieAnimationSeconds.AttackReady);

	/// <summary>重斩：共用 attack_ready（0.2s），但走 attack_play2 纵劈分支。</summary>
	public static readonly HeidemarieAttackPlan Heavy =
		new("Attack2", HeidemarieAnimationSeconds.AttackReady);

	/// <summary>u1 技能攻击：u1_attack_ready（0.4667s）→ u1_attack_play（0.6667s）。</summary>
	public static readonly HeidemarieAttackPlan U1 =
		new("U1Attack", HeidemarieAnimationSeconds.U1AttackReady);

	/// <summary>u3 技能攻击：u3_attack_ready（0.4667s）→ u3_attack_play（0.9333s，全骨架最重的挥击）。</summary>
	public static readonly HeidemarieAttackPlan U3 =
		new("U3Attack", HeidemarieAnimationSeconds.U3AttackReady);

	/// <summary>
	/// ux 终结技：ux_attack 是单段演出（0.8333s），没有起手分段，
	/// 命中点取时间轴 40% 处 —— 那是挥砍动作越过身位的时刻。
	/// </summary>
	public static readonly HeidemarieAttackPlan Ux =
		new("XAttack", HeidemarieAnimationSeconds.UxAttack * 0.4f);

	/// <summary>
	/// ug 终极技：ug_attack 只有 0.3333 秒，是全骨架最短的攻击动作。
	/// 命中点取整段末尾（不再按 60% 截断）——等待时长太短时，后续战斗流程
	/// 会在动画播完前把它盖掉，观感上就像这一击「没有播 ug 动画」。
	/// </summary>
	public static readonly HeidemarieAttackPlan Ug =
		new("GAttack", HeidemarieAnimationSeconds.UgAttack);

	/// <summary>
	/// 取这次攻击实际要等的命中秒数 —— 「命中时机同步」设置项的接入点。
	///
	/// <para>开启（默认）：返回时间轴实测的 <see cref="HitDelaySeconds"/>，
	/// 伤害与下一张牌都等这一击的命中点，动作与结算对得上。</para>
	///
	/// <para>关闭：返回 0 —— 动画起手后立刻结算，战斗更快，
	/// 代价是伤害可能先于动画落下（面板上的原文就是这个意思）。</para>
	///
	/// <para>数值本身只在这里收口：卡牌一律写
	/// <c>WithAttackerAnim(plan.Trigger, HeidemarieAttackPlan.HitDelay(plan))</c>，
	/// 不要再各自读 <see cref="HitDelaySeconds"/>，否则设置项会漏掉一部分牌。</para>
	/// </summary>
	/// <param name="plan">本次攻击的演出编排。</param>
	public static float HitDelay(HeidemarieAttackPlan plan)
		=> HeidemarieSettings.CurrentCharacter.OfficialAttackTimingEnabled ? plan.HitDelaySeconds : 0f;
}
