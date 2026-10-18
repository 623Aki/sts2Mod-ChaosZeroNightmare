using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// Heidemarie 的「防御」：1 费技能，获得 5 点格挡，升级后 8 点。
/// </summary>
public sealed class HeidemarieDefend : HeidemarieCard
{
	/// <inheritdoc/>
	/// <remarks>专属卡面上线前，临时复用战士防御的立绘。</remarks>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	/// <remarks>防御牌必须声明 <see cref="CardModel.GainsBlock"/>，否则无法被相关效果识别。</remarks>
	public override bool GainsBlock => true;

	/// <inheritdoc/>
	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new BlockVar(5m, ValueProp.Move),
	};

	/// <summary>1 费、技能、基础牌、目标为自身。</summary>
	public HeidemarieDefend()
		: base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 防御动作在骨架里是 <c>defense_ready → defense_play</c> 两段，用动作序列一次排完
	/// （做法同参考 mod 的防御路线）。任一动画缺失时降级回状态机的 <c>Defend</c> 触发器，
	/// 保证换成别的骨架也还有演出。技能卡的角色动画都放在这个「出牌演出」钩子里，
	/// 先于 <see cref="OnPlay"/> 的数值结算发生。
	/// </remarks>
	public override async Task OnEnqueuePlayVfx(Creature? target)
	{
		if (!HeidemarieAnimation.PlaySequence(Owner.Creature, "defense_ready", "defense_play"))
		{
			await HeidemarieAnimation.PlayTriggerAsync(
				Owner.Creature,
				"Defend",
				HeidemarieAnimationSeconds.DefenseReady);
		}
	}

	/// <inheritdoc/>
	/// <summary>演出档位：防御（defense_ready → defense_play）。当前档位表里没有配特效组。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.Defend;

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Block.UpgradeValueBy(3m);
	}
}
