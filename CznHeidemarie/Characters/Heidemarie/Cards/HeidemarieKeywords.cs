using System.Collections.Generic;
using CznCore.Cards;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// Heidemarie 自定义关键字的定义表。
///
/// <para>Id 即本地化表 <c>card_keywords</c> 的条目名（在
/// <c>CznHeidemarie/localization/zhs/card_keywords.json</c> 中登记
/// <c>&lt;Id&gt;.title</c> / <c>&lt;Id&gt;.description</c>）。</para>
///
/// <para>每个词条都带一个 <c>CardTextArgumentName</c>：卡面描述模板
/// （<c>localization/zhs/cards.json</c>）用 <c>{Link}</c> 取「连接。」、
/// <c>{LinkName}</c> 取「连接」，由
/// <see cref="HeidemarieCardMechanics.AddCardTextArguments"/> 在渲染时填值。
/// 这样卡面上的词条文本与悬停提示同源，且会跟着运行期状态变化
/// （「回收」的层数、有无）。</para>
///
/// <para>注意：原版 <c>CardKeyword</c> 枚举只含 8 个值，这些自定义关键字
/// <b>不改枚举</b>，只用 <see cref="CardMechanicDefinition"/> 做「卡面词条 + 悬停提示」的显示，
/// 行为则写在卡牌/Power 的钩子里。</para>
/// </summary>
internal static class HeidemarieKeywords
{
	/// <summary>感应：抽取该卡牌后触发。</summary>
	public static readonly CardMechanicDefinition Sense =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_SENSE", "Sense");

	/// <summary>连接：使用时丢弃手牌中其他所有「连接」牌。</summary>
	public static readonly CardMechanicDefinition Link =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_LINK", "Link");

	/// <summary>安息：非回合结束丢弃时发动；本回合不能移出墓地。</summary>
	public static readonly CardMechanicDefinition Rest =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_REST", "Rest");

	/// <summary>守墓：洗牌时不会移动至抽牌堆。</summary>
	public static readonly CardMechanicDefinition GraveKeep =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_GRAVE_KEEP", "GraveKeep");

	/// <summary>回收：移入墓地时返回手牌，结算后回收-1。</summary>
	public static readonly CardMechanicDefinition Recycle =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_RECYCLE", "Recycle");

	/// <summary>终结：使用此卡后立即结束本回合。</summary>
	public static readonly CardMechanicDefinition Terminate =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_TERMINATE", "Terminate");

	/// <summary>唯一：卡组内同名仅可持有 1 张。</summary>
	public static readonly CardMechanicDefinition Unique =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_UNIQUE", "Unique");

	/// <summary>焚烧：本牌被消耗时触发。</summary>
	public static readonly CardMechanicDefinition Burn =
		CardMechanicDefinition.FromCardKeywordsTable("HEIDEMARIE_KEYWORD_BURN", "Burn");

	/// <summary>
	/// 全部词条，供卡面描述模板做「占位参数预填」用。
	///
	/// <para>模板里引用到、但本牌当前没有的词条（例如还没被「凝锋」赋予「回收」的极光剑）
	/// 也必须有一个参数值，否则 SmartFormat 拿不到 <c>{Recycle}</c> 会报错/留字面量；
	/// 预填成空串即可让整段文本自然消失。</para>
	/// </summary>
	public static readonly IReadOnlyList<CardMechanicDefinition> All =
		new[] { Sense, Link, Rest, GraveKeep, Recycle, Terminate, Unique, Burn };
}
