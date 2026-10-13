namespace CznCore.Cards;

public readonly record struct CardMechanicDefinition(string Id, string LocalizationTable, string LocalizationEntryStem, string? CardTextArgumentName = null, string? IconPath = null)
{
	public static CardMechanicDefinition FromCardKeywordsTable(string id, string? cardTextArgumentName = null, string? iconPath = null)
	{
		return new CardMechanicDefinition(id, "card_keywords", id, cardTextArgumentName, iconPath);
	}
}
