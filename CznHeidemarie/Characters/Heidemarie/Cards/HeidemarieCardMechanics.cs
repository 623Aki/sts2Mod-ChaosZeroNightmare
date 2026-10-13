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

	/// <summary>返回该卡牌应显示的全部自定义关键字词条（按稳定顺序）。</summary>
	public static IEnumerable<CardMechanicDefinition> For(CardModel card)
	{
		if (card is ISenseCard)
		{
			yield return HeidemarieKeywords.Sense;
		}

		if (card is ILinkCard)
		{
			yield return HeidemarieKeywords.Link;
		}

		if (card is IRestCard)
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
	/// 把自定义词条写进卡面描述模板的参数表。每个词条提供三种形态：
	/// <list type="bullet">
	///   <item><c>{Link}</c> —— 行内形态，文本形如「链接。」；</item>
	///   <item><c>{LinkLine}</c> —— 独立成行形态，文本自带前缀换行；</item>
	///   <item><c>{LinkName}</c> —— 纯词条名「链接」，用于「感应：…」这种后接说明的写法。</item>
	/// </list>
	///
	/// <para><b>为什么必须走模板参数</b>：卡面文本若把词条写死，随运行期出现/消失的词条
	/// （「凝锋」赋予的「回收」）就永远显示不出来 —— 模板参数是唯一能跟着
	/// <see cref="HeidemarieCard.RecycleRemaining"/> 这类状态走的通道（设计文档 §2.5）。</para>
	///
	/// <para><c>{XLine}</c> 存在的意义：模板里的换行是静态的，而词条可能整条不存在，
	/// 写成 <c>{Link}\n</c> 会在没有该词条时留下空行；换成 <c>{XLine}</c> 则整行随参数一起消失。</para>
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

		// 2) 本牌实际带的词条（「回收」带层数，单独处理，见第 3 步）。
		foreach (CardMechanicDefinition definition in For(card).Where(definition => definition.Id != HeidemarieKeywords.Recycle.Id))
		{
			string name = definition.CardTextArgumentName!;
			string text = CardMechanicPresentation.GetCardText(definition);
			description.Add(name, text);
			description.Add(name + CardMechanicPresentation.NameArgumentSuffix, CardMechanicPresentation.GetCardName(definition));
			description.Add(name + LineArgumentSuffix, PrefixNewline(text));
		}

		// 3) 「回收」带层数：1 层显示「回收」，N>1 显示「回收N」（设计文档 §2.5）。
		//    层数每次回手 -1，是运行期状态，所以文本只能在这里现算。
		if (card is HeidemarieCard holder && holder.RecycleRemaining > 0)
		{
			string name = CardMechanicPresentation.GetCardName(HeidemarieKeywords.Recycle);
			if (holder.RecycleRemaining > 1)
			{
				name += holder.RecycleRemaining;
			}

			string text = CardMechanicPresentation.FormatCardText(name);
			string argumentName = HeidemarieKeywords.Recycle.CardTextArgumentName!;
			description.Add(argumentName, text);
			description.Add(argumentName + CardMechanicPresentation.NameArgumentSuffix, name);
			description.Add(argumentName + LineArgumentSuffix, PrefixNewline(text));
		}
	}

	/// <summary>把行内词条文本转成「独立成行」形态（前缀换行）。</summary>
	private static string PrefixNewline(string cardText) => "\n" + cardText;
}
