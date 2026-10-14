using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「万剑诀」：2 费技能（公共牌）。
///
/// <para>关键字<b>连接</b>。打出前手牌中每有 1 张带「连接」的牌（含自身），
/// 就在手牌生成 1 张极光剑。</para>
/// </summary>
public sealed class HeidemarieMirrorBlades : HeidemarieCard, ILinkCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>2 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieMirrorBlades()
		: base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 「打出前」连接数由 HeidemarieCard.BeforeCardPlayed 在丢弃其他连接牌之前记录；
		// 连带丢弃也已在同一处完成，因此这里拿到的数量不会把刚生成的极光剑算进去。
		// 卡面写了「打出前」→ 含自身（口径见 ResolveLinkedCountBeforePlay）。
		int linked = ResolveLinkedCountBeforePlay(includeSelf: true);

		await HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, linked, PileType.Hand);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
