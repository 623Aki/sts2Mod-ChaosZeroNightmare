using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「安息」的锁墓判定与「强制绕过」开关。
///
/// <para><b>语义（已确认的设计口径）</b>：</para>
/// <list type="number">
///   <item>锁的持续时间是「本回合」—— 由卡牌自己在
///   <see cref="HeidemarieCard.BeforeHandDraw"/>（下个自己回合的起手抽牌之前）清除，
///   与出牌方回合对齐，且不会挡住起手抽牌触发的自动洗牌；</item>
///   <item>本回合内该牌不能被移出弃牌堆：洗牌（<c>CardPileCmd.Shuffle</c>）、
///   任意「从墓地拿牌」效果（撷光、原版取墓牌等）都要拦；</item>
///   <item><b>例外</b>：「坠星」按设计故意连安息锁一起绕过（它连守墓都绕），
///   调用方用 <see cref="Bypass"/> 开一个作用域即可；</item>
///   <item>牌一旦以任何方式回到手牌，再被丢弃时<strong>会再次发动</strong>安息 ——
///   因此 <see cref="HeidemarieCard.AfterCardDiscarded"/> 不做「已锁则不触发」的抑制。</item>
/// </list>
///
/// <para><b>为什么需要额外拦截层</b>：引擎只提供事后的
/// <c>AfterCardChangedPiles</c>，没有可取消的移堆钩子；<c>CardPileCmd.Add</c> 也不认识
/// 自定义状态。因此本类只负责「判定 + 绕过」，
/// 实际拦截点分别落在：
/// <see cref="HeidemarieCard.ModifyShuffleOrder"/>（洗牌）、
/// 各取墓牌自己的候选列表（如 <see cref="HeidemarieRecover"/>），
/// 以及 <c>Registration/HeidemarieRestLockPatch</c>（
/// <c>CardPileCmd.Add</c> 的统一兜底）。</para>
/// </summary>
internal static class HeidemarieRestLock
{
	/// <summary>
	/// 「强制绕过」的嵌套深度，<b>按玩家</b>区分。
	///
	/// <para>不写成全局开关：绕过作用域会跨 <c>await</c> 存活，
	/// 联机时另一名玩家的结算可能在等待点插入 —— 按玩家隔离后，
	/// 一个人的「坠星」不会顺手解开别人的墓地锁。</para>
	/// </summary>
	private static readonly Dictionary<ulong, int> BypassDepthByOwnerNetId = new();

	/// <summary>
	/// 这张牌此刻是否被「安息」锁在墓地：
	/// 带锁标记 <b>且</b> 当前确实位于弃牌堆。
	///
	/// <para>必须同时判堆位 —— 只有「还在墓地」才谈得上「不能被移出墓地」；
	/// 坠星把牌强推到抽牌堆之后，锁标记虽然还在（要留到本回合结束），
	/// 但牌已经不在墓地，不应再挡它的正常移动。</para>
	/// </summary>
	internal static bool IsLocked(CardModel? card)
		=> card is HeidemarieCard holder
			&& holder.RestLockedThisTurn
			&& card.Pile?.Type == PileType.Discard;

	/// <summary>该玩家当前是否处于「忽略安息锁」的作用域内。</summary>
	internal static bool IsBypassedFor(Player? player)
		=> player != null && BypassDepthByOwnerNetId.ContainsKey(player.NetId);

	/// <summary>
	/// 为 <paramref name="player"/> 开一个「忽略安息锁」的作用域
	/// （供「坠星」这类设计上强制绕过墓地锁的牌使用）。
	/// 用法：<c>using (HeidemarieRestLock.Bypass(Owner)) { ... }</c>。
	/// </summary>
	internal static IDisposable Bypass(Player player) => new BypassScope(player.NetId);

	private sealed class BypassScope : IDisposable
	{
		private readonly ulong _netId;
		private bool _disposed;

		internal BypassScope(ulong netId)
		{
			_netId = netId;
			BypassDepthByOwnerNetId.TryGetValue(netId, out int depth);
			BypassDepthByOwnerNetId[netId] = depth + 1;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			if (!BypassDepthByOwnerNetId.TryGetValue(_netId, out int depth))
			{
				return;
			}

			if (depth <= 1)
			{
				BypassDepthByOwnerNetId.Remove(_netId);
			}
			else
			{
				BypassDepthByOwnerNetId[_netId] = depth - 1;
			}
		}
	}
}
