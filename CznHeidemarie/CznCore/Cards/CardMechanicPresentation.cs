using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace CznCore.Cards;

public static class CardMechanicPresentation
{
	public const string NameArgumentSuffix = "Name";

	public static IEnumerable<IHoverTip> CreateHoverTips(IEnumerable<CardMechanicDefinition> definitions)
	{
		ArgumentNullException.ThrowIfNull(definitions, "definitions");
		HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (CardMechanicDefinition definition in definitions)
		{
			Validate(definition);
			if (seenIds.Add(definition.Id))
			{
				yield return CreateHoverTip(definition);
			}
		}
	}

	public static IHoverTip CreateHoverTip(CardMechanicDefinition definition)
	{
		Validate(definition);
		LocString title = Title(definition);
		LocString locString = Description(definition);
		locString.Add("energyPrefix", string.Empty);
		Texture2D icon = null;
		if (!string.IsNullOrWhiteSpace(definition.IconPath) && ResourceLoader.Exists(definition.IconPath))
		{
			icon = ResourceLoader.Load<Texture2D>(definition.IconPath, null, ResourceLoader.CacheMode.Reuse);
		}
		HoverTip hoverTip = new HoverTip(title, locString, icon);
		hoverTip.Id = definition.Id;
		return hoverTip;
	}

	public static void AddCardTextArguments(LocString cardDescription, IEnumerable<CardMechanicDefinition> definitions)
	{
		ArgumentNullException.ThrowIfNull(cardDescription, "cardDescription");
		ArgumentNullException.ThrowIfNull(definitions, "definitions");
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		foreach (CardMechanicDefinition definition in definitions)
		{
			Validate(definition);
			if (hashSet.Add(definition.Id) && !string.IsNullOrWhiteSpace(definition.CardTextArgumentName))
			{
				if (!hashSet2.Add(definition.CardTextArgumentName))
				{
					throw new InvalidOperationException("Card mechanic argument '" + definition.CardTextArgumentName + "' is assigned more than once.");
				}
				cardDescription.Add(definition.CardTextArgumentName, GetCardText(definition));
				cardDescription.Add(definition.CardTextArgumentName + "Name", GetCardName(definition));
			}
		}
	}

	/// <summary>
	/// 取词条的**卡面名**：带金色标记、不含句号（形如 <c>[gold]连接[/gold]</c>），
	/// 供模板里的 <c>{XName}</c> 使用（如「感应：…」这种后接说明的写法）。
	///
	/// <para>带色是为了与行内形态 <c>{X}</c>（<see cref="GetCardText"/>）观感一致 ——
	/// 卡面上的关键字名统一是金色。</para>
	/// </summary>
	public static string GetCardName(CardMechanicDefinition definition)
	{
		Validate(definition);
		return ColorizeCardName(GetPlainCardName(definition));
	}

	/// <summary>
	/// 取词条的**纯文本名**（不含金色标记与句号）。
	///
	/// <para>只有需要自行拼接（如把层数并进名字再整体上色）时才用它；
	/// 直接展示请用 <see cref="GetCardName"/>。</para>
	/// </summary>
	public static string GetPlainCardName(CardMechanicDefinition definition)
	{
		Validate(definition);
		return Title(definition).GetFormattedText();
	}

	public static string GetCardText(CardMechanicDefinition definition)
	{
		Validate(definition);
		return FormatCardText(GetPlainCardName(definition));
	}

	/// <summary>把词条名包成卡面用的金色标记（不含句号）。</summary>
	public static string ColorizeCardName(string mechanicName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mechanicName, "mechanicName");
		return "[gold]" + mechanicName + "[/gold]";
	}

	/// <summary>
	/// 用给定的词条名生成卡面词条文本（形如 <c>[gold]连接[/gold]。</c>）。
	///
	/// <para>与 <see cref="GetCardText"/> 的唯一区别是词条名由调用方给出 ——
	/// 带层数的词条（「回收2」）要复用同一套格式，而层数是运行期状态，
	/// 定义里那个固定的 title 不够用。传入的应是**纯文本名**（未经 <see cref="ColorizeCardName"/>）。</para>
	/// </summary>
	public static string FormatCardText(string mechanicName)
	{
		string rawText = new LocString("card_keywords", "PERIOD").GetRawText();
		return ColorizeCardName(mechanicName) + rawText;
	}

	private static LocString Title(CardMechanicDefinition definition)
	{
		return new LocString(definition.LocalizationTable, definition.LocalizationEntryStem + ".title");
	}

	private static LocString Description(CardMechanicDefinition definition)
	{
		return new LocString(definition.LocalizationTable, definition.LocalizationEntryStem + ".description");
	}

	private static void Validate(CardMechanicDefinition definition)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id, "definition.Id");
		ArgumentException.ThrowIfNullOrWhiteSpace(definition.LocalizationTable, "definition.LocalizationTable");
		ArgumentException.ThrowIfNullOrWhiteSpace(definition.LocalizationEntryStem, "definition.LocalizationEntryStem");
	}
}
