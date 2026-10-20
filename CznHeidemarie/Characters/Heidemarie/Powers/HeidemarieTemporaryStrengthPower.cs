using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 「严阵以待」给的<b>临时力量</b>容器（官方 <see cref="TemporaryStrengthPower"/> 的角色化实现）。
///
/// <para>官方约定：临时力量不是独立数值，而是「给 <see cref="StrengthPower"/> 加一笔，
/// 那一回合结束时再扣回去」。因此本类只需声明<b>来源牌</b>
/// （<see cref="TemporaryStrengthPower.OriginModel"/>，决定卡面标题与悬停提示），
/// 加减逻辑、回合末回收、悬停提示全部沿用基类。</para>
///
/// <para><b>生命周期</b>：由 <see cref="HeidemarieStandReadyPower"/> 在「下一回合开始时」施加，
/// 于是基类会在<strong>那一回合结束时</strong>把它扣掉 —— 正好是「下一回合的临时力量」。</para>
///
/// <para><b>与「市长铭牌」那条的关系</b>：两处发放逻辑、图标全部共用
/// <see cref="HeidemarieTemporaryStrengthPowerBase"/>；本类只提供「来源 = 严阵以待」，
/// 因此战斗界面里这条 buff 的标题与悬停提示显示的是这张牌。</para>
/// </summary>
public sealed class HeidemarieTemporaryStrengthPower : HeidemarieTemporaryStrengthPowerBase
{
	/// <inheritdoc />
	/// <remarks>指向「严阵以待」这张牌：卡面标题会显示成牌名，而不是一个无名能力。</remarks>
	public override AbstractModel OriginModel => ModelDb.Card<Cards.HeidemarieStandReady>();
}
