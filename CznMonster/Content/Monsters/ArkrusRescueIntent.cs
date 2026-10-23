using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 阿克鲁斯的「救援颂歌」意图：给救援链第 3 拍换一套自己的文案
/// （键 <c>ARKRUS_RESCUE.title / .description</c>，由本 mod 的 <c>intents</c> 表提供）。
///
/// <para><b>为什么不能直接用 <see cref="HealIntent"/></b>：原生治疗的悬浮说明只有一句
/// 「这个敌人将会[gold]回复[/gold]」——主语是阿克鲁斯自己，而实际效果是治疗<b>濒死的塞拉菲翁</b>
/// 并把它拉出濒死。玩家看到治疗图标会误判为「阿克鲁斯要自愈」，与杀序锁的核心抉择直接冲突。</para>
///
/// <para><b>为什么继承 <see cref="HealIntent"/> 而不是 <see cref="AbstractIntent"/></b>：
/// 继承后 <see cref="AbstractIntent.IntentType"/> 仍是 <see cref="IntentType.Heal"/>、图标与数值渲染逻辑全部
/// 复用原生；只需要覆写 <see cref="AbstractIntent.IntentPrefix"/> 换文案，并显式指定
/// <see cref="AbstractIntent.GetAnimation"/>（默认实现取 prefix 小写，自定义 prefix 不是内置键，
/// <c>NIntent</c> 取帧会越界——同 <c>AgnesExecuteIntent</c> 的坑）。</para>
/// </summary>
public sealed class ArkrusRescueIntent : HealIntent
{
	/// <inheritdoc />
	/// <remarks>本地化键前缀：<c>intents</c> 表下的 <c>ARKRUS_RESCUE.title / .description</c>。</remarks>
	protected override string IntentPrefix => "ARKRUS_RESCUE";

	/// <inheritdoc />
	/// <remarks>复用内置 <c>heal</c> 意图动画：<see cref="IntentAnimData"/> 只收录内置键。</remarks>
	public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
	{
		return IntentAnimData.heal;
	}
}
