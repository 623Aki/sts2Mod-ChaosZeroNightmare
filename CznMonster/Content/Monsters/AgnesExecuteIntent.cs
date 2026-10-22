using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 阿涅丝的「处决」意图（v0.7 新增）：图标沿用本体的 <c>intent_death_blow</c>，
/// 标题与描述由本 mod 的 <c>intents</c> 表提供（<c>AGNES_EXECUTE.title / .description</c>）。
///
/// <para><b>为什么不用引擎自带的 <see cref="DeathBlowIntent"/></b>：它的文案是
/// 「濒死一击 / 这个生物试图拉你一起陪葬，会在被摧毁前攻击你」，语义是临终反扑；
/// 阿涅丝的处决是每 4 拍一次的常规高潮，需要自己的名字与说明。</para>
///
/// <para><b>伤害走委托实时取值</b>：<c>基础 × (1 + 共鸣层数 + 2×场上碎片数)</c>——
/// 三个入参全是玩家可见的量（Power 层数 + 场上碎片数），所以图标数字与
/// <c>Agnes.ExecuteMove</c> 的实伤始终同源，不存在「显示一套、打另一套」。</para>
/// </summary>
public sealed class AgnesExecuteIntent : SingleAttackIntent
{
	/// <param name="damageCalc">伤害委托（每次刷新/取标签时现算，见 <c>Agnes.ExecuteDamage</c>）。</param>
	public AgnesExecuteIntent(Func<decimal> damageCalc)
		: base(damageCalc)
	{
	}

	/// <inheritdoc />
	/// <remarks>本地化键前缀：<c>intents</c> 表下的 <c>AGNES_EXECUTE.title / .description</c>。</remarks>
	protected override string IntentPrefix => "AGNES_EXECUTE";

	/// <inheritdoc />
	/// <remarks>复用本体的处决图标，不新增美术资源。</remarks>
	protected override string SpritePath => "atlases/intent_atlas.sprites/intent_death_blow.tres";

	/// <inheritdoc />
	public override IntentType IntentType => IntentType.DeathBlow;

	/// <inheritdoc />
	/// <remarks>固定用 <see cref="SpritePath"/>（父类按总伤害挑 attack_1~5 的图标，不适用于处决）。</remarks>
	public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
	{
		return PreloadManager.Cache.GetTexture2D(ImageHelper.GetImagePath(SpritePath));
	}

	/// <inheritdoc />
	/// <remarks>
	/// 复用内置 <c>death_blow</c> 的意图动画数据：<see cref="IntentAnimData"/> 只收录内置键，
	/// 若返回自定义键（如 <c>agnes_execute</c>），<c>NIntent.UpdateVisuals</c> 取帧会越界。
	/// </remarks>
	public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
	{
		return IntentAnimData.deathBlow;
	}
}
