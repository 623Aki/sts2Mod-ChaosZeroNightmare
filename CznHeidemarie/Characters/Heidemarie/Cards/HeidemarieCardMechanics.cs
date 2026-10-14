using System.Collections.Generic;
using System.Linq;
using CznCore.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「卡牌 → 它带哪些自定义关键字」的集中映射。
///
/// <para>做法同参考 mod 的 <c>TipheraCardMechanics.For</c> / <c>NineCardMechanics.For</c>：
/// 按卡牌实现的接口（= 关键字的行为载体）推导出要显示的词条。
/// 两个消费方同源：</para>
/// <list type="bullet">
///   <item>悬停提示 —— <see cref="HeidemarieCard"/><c>.ExtraHoverTips</c>；</item>
///   <item>卡面词条文本 —— <see cref="AddCardTextArguments"/> 填模板参数。</item>
/// </list>
/// </summary>
internal static class HeidemarieCardMechanics
{
	/// <summary>「独立成行」形态参数的尾缀，模板里写作 <c>{LinkLine}</c>。</summary>
	private const string LineArgumentSuffix = "Line";

	/// <summary>
	/// 「关键字整块」公共占位符：模板统一写 <c>{KwBlock}</c>，不必逐词条列举。
	///
	/// <para>值为本牌当前生效的「独立成行词条」（连接 / 安息 / 守墓 / 回收 / 终结 / 唯一）
	/// 拼成的一整块，<b>末尾自带换行</b>；一个词条都没有时整块（含换行）一起消失，
	/// 因此模板可以无条件把它放在最前面，且不会留下空行。</para>
	///
	/// <para>这样「运行期临时赋予」的词条也能随 <see cref="For"/> 每次渲染现算并显示 ——
	/// 例如缀星 / 微光 / 冥渡 / 撷光 / 引锋召刃 / 万众英雄 给的「连接」、
	/// 北极圈升级后的「安息」，都不需要再在 cards.json 里为每张牌单独写 <c>{Link}</c>。</para>
	///
	/// <para>「感应」「焚烧」<b>不进整块</b> —— 它们永远配
	/// <c>{SenseName}：卡面专属说明</c> / <c>{BurnName}：…</c> 使用，仍走各自参数。</para>
	/// </summary>
	public const string KeywordBlockArgumentName = "KwBlock";

	/// <summary>返回该卡牌应显示的全部自定义关键字词条（按稳定顺序）。</summary>
	public static IEnumerable<CardMechanicDefinition> For(CardModel card)
	{
		if (card is ISenseCard)
		{
			yield return HeidemarieKeywords.Sense;
		}

		// 「连接」「安息」都可能是**运行期临时赋予**的，因此以基类的复合判定为准
		// （静态接口 ILinkCard / IRestCard 或运行期标记任一成立）。
		if (card is HeidemarieCard linkHolder && linkHolder.IsLinked)
		{
			yield return HeidemarieKeywords.Link;
		}

		if (card is HeidemarieCard restHolder && restHolder.IsRestCard)
		{
			yield return HeidemarieKeywords.Rest;
		}

		if (card is IGraveKeepCard)
		{
			yield return HeidemarieKeywords.GraveKeep;
		}

		// 「回收」是带层数的关键字：默认 1 层；层数归零后不再显示。
		// 固有回收（凝聚极光）读 CanonicalRecycle；临时赋予（凝锋给的极光剑）读运行期字段。
		if (card is HeidemarieCard holder && holder.RecycleRemaining > 0)
		{
			yield return HeidemarieKeywords.Recycle;
		}

		if (card is ITerminateCard)
		{
			yield return HeidemarieKeywords.Terminate;
		}

		if (card is IUniqueCard)
		{
			yield return HeidemarieKeywords.Unique;
		}

		if (card is IBurnCard)
		{
			yield return HeidemarieKeywords.Burn;
		}
	}

	/// <summary>
	/// 把自定义词条写进卡面描述模板的参数表。
	///
	/// <para><b>首选写法</b>：模板最前面写一个 <c>{KwBlock}</c>（见 <see cref="KeywordBlockArgumentName"/>）——
	/// 它把本牌当前生效的全部「独立成行词条」一次性拼好，末尾自带换行；无词条时整块消失。
	/// 这样运行期临时赋予的连接 / 安息会随 <see cref="For"/> 自动显示，且新增关键字不必改模板。</para>
	///
	/// <para><b>兼容写法</b>：仍提供逐词条的三种形态，供「词条名 + 卡面专属说明」这类写法使用：
	/// <list type="bullet">
	///   <item><c>{Link}</c> —— 行内形态，文本形如「连接。」；</item>
	///   <item><c>{LinkLine}</c> —— 独立成行形态，文本自带前缀换行；</item>
	///   <item><c>{LinkName}</c> —— 纯词条名「连接」，用于「感应：…」这种后接说明的写法。</item>
	/// </list>
	/// </para>
	///
	/// <para><b>为什么必须走模板参数</b>：卡面文本若把词条写死，随运行期出现/消失的词条
	/// （「凝锋」赋予的「回收」）就永远显示不出来 —— 模板参数是唯一能跟着
	/// <see cref="HeidemarieCard.RecycleRemaining"/> 这类状态走的通道（设计文档 §2.5）。</para>
	///
	/// <para><c>{XLine}</c> 存在的意义：模板里的换行是静态的，而词条可能整条不存在，
	/// 写成 <c>{Link}\n</c> 会在没有该词条时留下空行；换成 <c>{XLine}</c> 则整行随参数一起消失。
	/// <c>{KwBlock}</c> 用的就是同一思路，只是把「整行」升级成了「整块」。</para>
	/// </summary>
	/// <param name="description">卡面描述模板，由 <c>CardModel.AddExtraArgsToDescription</c> 传入。</param>
	/// <param name="card">当前卡牌实例。</param>
	public static void AddCardTextArguments(LocString description, CardModel card)
	{
		// 1) 先把全部词条的三种形态填成空串：模板引用了但本牌当前没有的词条必须渲染成空，
		//    否则 SmartFormat 取不到 {Recycle} 会报错或把占位符原样留在卡面上。
		foreach (CardMechanicDefinition definition in HeidemarieKeywords.All)
		{
			string? name = definition.CardTextArgumentName;
			if (string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			description.Add(name, string.Empty);
			description.Add(name + CardMechanicPresentation.NameArgumentSuffix, string.Empty);
			description.Add(name + LineArgumentSuffix, string.Empty);
		}

		// 2) 关键字整块：模板只需写一个 {KwBlock}，值在每次渲染时按 For(card) 现算，
		//    于是「运行期临时赋予」的连接 / 安息也能显示，无需逐张牌维护占位符。
		description.Add(KeywordBlockArgumentName, BuildKeywordBlock(card));

		// 3) 本牌实际带的词条（「回收」带层数，单独处理，见第 4 步）。
		foreach (CardMechanicDefinition definition in For(card).Where(definition => definition.Id != HeidemarieKeywords.Recycle.Id))
		{
			string name = definition.CardTextArgumentName!;
			string text = CardMechanicPresentation.GetCardText(definition);
			description.Add(name, text);
			description.Add(name + CardMechanicPresentation.NameArgumentSuffix, CardMechanicPresentation.GetCardName(definition));
			description.Add(name + LineArgumentSuffix, PrefixNewline(text));
		}

		// 4) 「回收」带层数：1 层显示「回收」，N>1 显示「回收N」（设计文档 §2.5）。
		//    层数每次回手 -1，是运行期状态，所以文本只能在这里现算。
		//    先把层数并进**纯文本名**，再整体上色，避免「回收」与层数被金色标签拆开。
		if (BuildRecyclePlainName(card) is { } plainName)
		{
			string text = CardMechanicPresentation.FormatCardText(plainName);
			string name = CardMechanicPresentation.ColorizeCardName(plainName);
			string argumentName = HeidemarieKeywords.Recycle.CardTextArgumentName!;
			description.Add(argumentName, text);
			description.Add(argumentName + CardMechanicPresentation.NameArgumentSuffix, name);
			description.Add(argumentName + LineArgumentSuffix, PrefixNewline(text));
		}
	}

	/// <summary>
	/// 生成 <c>{KwBlock}</c> 的值：把本牌当前生效的「独立成行词条」按 <see cref="For"/> 的稳定顺序
	/// 拼成一整块（<b>末尾带换行</b>）。
	///
	/// <para>一个词条都没有时返回空串 —— 于是整块连同那个换行一起消失，模板最前面的
	/// <c>{KwBlock}</c> 不会留下空行。</para>
	/// </summary>
	/// <param name="card">当前卡牌实例。</param>
	private static string BuildKeywordBlock(CardModel card)
	{
		List<string> lines = new List<string>();

		foreach (CardMechanicDefinition definition in For(card))
		{
			// 「感应」「焚烧」永远配卡面专属说明（{SenseName}：… / {BurnName}：…），不进整块。
			if (definition.Id == HeidemarieKeywords.Sense.Id || definition.Id == HeidemarieKeywords.Burn.Id)
			{
				continue;
			}

			// 「回收」带层数（1 层「回收」、N>1「回收N」），文本必须现算。
			if (definition.Id == HeidemarieKeywords.Recycle.Id)
			{
				if (BuildRecyclePlainName(card) is { } recyclePlainName)
				{
					lines.Add(CardMechanicPresentation.FormatCardText(recyclePlainName));
				}

				continue;
			}

			lines.Add(CardMechanicPresentation.GetCardText(definition));
		}

		return lines.Count == 0 ? string.Empty : string.Join('\n', lines) + "\n";
	}

	/// <summary>
	/// 取本牌「回收」词条的**纯文本名**（已并进层数、未上色）；
	/// 不带回收（层数 ≤ 0）时返回 <c>null</c>。
	/// </summary>
	/// <param name="card">当前卡牌实例。</param>
	private static string? BuildRecyclePlainName(CardModel card)
	{
		if (card is not HeidemarieCard holder || holder.RecycleRemaining <= 0)
		{
			return null;
		}

		string plainName = CardMechanicPresentation.GetPlainCardName(HeidemarieKeywords.Recycle);
		return holder.RecycleRemaining > 1 ? plainName + holder.RecycleRemaining : plainName;
	}

	/// <summary>把行内词条文本转成「独立成行」形态（前缀换行）。</summary>
	private static string PrefixNewline(string cardText) => "\n" + cardText;
}
