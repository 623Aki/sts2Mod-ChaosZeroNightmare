using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using CznHeidemarie.Integration;

namespace CznHeidemarie.Characters.Heidemarie.Presentation;

/// <summary>
/// 一个档位的完整演出配方：身体动作 + 特效组。
///
/// <para>这是「档位」的唯一真值来源。原先同一个档位散在三处（
/// <c>HeidemarieCard.SkillBodyTier</c> 的动作序列、<c>HeidemarieAttackPlan</c> 的触发器与命中秒数、
/// <c>heidemarie_moves.json</c> 的特效组键），改一处漏两处就会动作与特效错位；
/// 现在卡牌只声明档位，其余全从本表取。</para>
/// </summary>
/// <param name="Tier">档位。</param>
/// <param name="BodySequence">技能类动作链（逐段播放）；攻击类为 <c>null</c>（走 AttackCommand）。</param>
/// <param name="Attack">攻击类的触发器 + 命中延迟；技能类为 <c>null</c>。</param>
/// <param name="GroupName">默认特效组名（自组装组库 <c>heidemarie_effect_compose.json</c> 的键）。
/// 空串表示该档位暂无特效组；实际用哪套由 <c>heidemarie_moves.json</c> 的档位条目决定，
/// 这里只作文档与一致性对照用。</param>
/// <param name="CinematicRoute">全屏演出路线（<c>HeidemarieCinematicRuntime.RouteUg/RouteUx</c>）；
/// <c>null</c> 表示不走全屏。**注意这是独立维度**：身体动作档位与全屏路线不是一回事
/// （剑之乐园身体走顶击档、全屏走 ux），不一致的走 <see cref="CardRouteOverrides"/>。</param>
internal sealed record HeidemariePresentationPlan(
	HeidemarieEffectTier Tier,
	string[]? BodySequence,
	HeidemarieAttackPlan? Attack,
	string GroupName,
	string? CinematicRoute = null);

/// <summary>档位表：档位 → 动作链 / 攻击计划 / 默认特效组。</summary>
internal static class HeidemariePresentationCatalog
{
	/// <summary>全部档位的配方。</summary>
	private static readonly Dictionary<HeidemarieEffectTier, HeidemariePresentationPlan> Table = new()
	{
		// ── 攻击档：走 AttackCommand.WithAttackerAnim，C# 只给触发器与命中延迟 ──
		[HeidemarieEffectTier.AttackLight] = new(HeidemarieEffectTier.AttackLight, null, HeidemarieAttackPlan.Basic, "轻击_横挥"),
		[HeidemarieEffectTier.AttackMid] = new(HeidemarieEffectTier.AttackMid, null, HeidemarieAttackPlan.Heavy, "中击_横挥"),
		[HeidemarieEffectTier.AttackHeavy] = new(HeidemarieEffectTier.AttackHeavy, null, HeidemarieAttackPlan.U1, "重击_u1"),
		[HeidemarieEffectTier.AttackTop] = new(HeidemarieEffectTier.AttackTop, null, HeidemarieAttackPlan.U3, "顶击_u3_完整"),
		// 全屏演出关掉时的回退档：ug 单目标用完整版，ux 预留（可能被敌人数放大）用核心版
		[HeidemarieEffectTier.AttackUg] = new(HeidemarieEffectTier.AttackUg, null, HeidemarieAttackPlan.Ug, "顶击_u3_完整", HeidemarieCinematicRuntime.RouteUg),
		[HeidemarieEffectTier.AttackUx] = new(HeidemarieEffectTier.AttackUx, null, HeidemarieAttackPlan.Ux, "顶击_u3_核心", HeidemarieCinematicRuntime.RouteUx),

		// ── 技能 / 能力档：走 OnEnqueuePlayVfx 逐段播放 ──
		[HeidemarieEffectTier.Cast] = new(HeidemarieEffectTier.Cast, new[] { "buff_ready", "buff_play" }, null, "施法_聚气"),
		[HeidemarieEffectTier.Guard] = new(HeidemarieEffectTier.Guard, new[] { "u2_buff_ready", "u2_buff_play" }, null, "结阵_u2"),
		[HeidemarieEffectTier.Charge] = new(HeidemarieEffectTier.Charge, new[] { "u4_buff_ready", "u4_buff_play" }, null, "蓄势_u4"),
		[HeidemarieEffectTier.Defend] = new(HeidemarieEffectTier.Defend, new[] { "defense_ready", "defense_play" }, null, ""),
		[HeidemarieEffectTier.Cure] = new(HeidemarieEffectTier.Cure, new[] { "cure_ready", "cure_play" }, null, ""),
		[HeidemarieEffectTier.Debuff] = new(HeidemarieEffectTier.Debuff, new[] { "debuff_ready", "debuff_play" }, null, ""),
	};

	/// <summary>
	/// 卡级全屏路线例外：身体动作档位与全屏路线**不是同一个维度**，少数卡两者不一致时在这里显式登记。
	/// </summary>
	/// <remarks>
	/// 破军剑舞：身体动作是顶击档（u3_attack_ready → u3_attack_play，与「顶击_u3_完整」的两段式特效配套），
	/// 但全屏演出走 ux 路线（它的 ug/ux 时间轴是另一套）。若把 RouteUx 挂到 AttackTop 档位上，
	/// 以后所有顶击卡都会被全屏接管，所以用例外表按卡登记。
	/// </remarks>
	private static readonly Dictionary<string, string> CardRouteOverrides = new(StringComparer.Ordinal)
	{
		[nameof(HeidemarieSwordParadise)] = HeidemarieCinematicRuntime.RouteUx,
	};

	/// <summary>取某张牌的演出配方。档位没登记时回退轻击档（不炸演出）。</summary>
	internal static HeidemariePresentationPlan Resolve(HeidemarieCard card)
	{
		return Table.TryGetValue(card.EffectiveTier, out HeidemariePresentationPlan? plan)
			? plan
			: Table[HeidemarieEffectTier.AttackLight];
	}

	/// <summary>
	/// 本牌的演出键（= <c>heidemarie_moves.json</c> 的 <c>effects</c> 键）。
	/// 带变体时是「档位/变体」，如 <c>AttackLight/red</c>。
	/// </summary>
	internal static string ResolveKey(HeidemarieCard card)
	{
		HeidemarieEffectTier tier = card.EffectiveTier;
		string? variant = card.EffectiveVariant;
		return variant is { Length: > 0 } ? $"{tier}/{variant}" : tier.ToString();
	}

	/// <summary>攻击档的触发器 + 命中延迟；技能档返回 <c>null</c>。</summary>
	internal static HeidemarieAttackPlan? AttackPlanOf(HeidemarieCard card) => Resolve(card).Attack;

	/// <summary>该档位默认的特效组名（空串 = 暂无）。</summary>
	internal static string GroupNameOf(HeidemarieEffectTier tier)
	{
		return Table.TryGetValue(tier, out HeidemariePresentationPlan? plan) ? plan.GroupName : "";
	}

	/// <summary>
	/// 本牌的全屏演出路线（<c>null</c> = 不走全屏）。
	/// 查表顺序：卡级例外 → 档位默认。
	/// </summary>
	internal static string? CinematicRouteOf(HeidemarieCard card)
	{
		string cardName = card.GetType().Name;
		if (CardRouteOverrides.TryGetValue(cardName, out string? overridden))
		{
			return overridden;
		}

		return Resolve(card).CinematicRoute is { Length: > 0 } route ? route : null;
	}
}
