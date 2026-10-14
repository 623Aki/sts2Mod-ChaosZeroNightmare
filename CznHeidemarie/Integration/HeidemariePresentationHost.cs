using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznHeidemarie.Integration;

/// <summary>
/// 海德玛丽的「演出上下文」宿主。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.Presentation.NinePresentationHost</c>：
/// 把「设置 → 演出策略对象」的构造集中在一处，供全屏演出、特写立绘、音效语音等
/// 接入层共用，避免每个接入层各自 new 一份、各自解读设置。</para>
///
/// <para>与参考 mod 的关键差别：本工程<b>没有</b>走
/// <c>CharacterSkinModBootstrap.Init</c>（那条路要求提供一个完整的
/// <c>CharacterSkinProfile</c>，本工程是手写的轻量接入，见 <see cref="HeidemarieIntegration"/>），
/// 所以这里只构造真正被消费的几个策略对象，不伪造 profile。</para>
/// </summary>
internal static class HeidemariePresentationHost
{
	/// <summary>
	/// 本角色真正做了的特性开关。
	///
	/// <para><see cref="CharacterSkinPresentationGates"/> 里那些「有素材才有意义」的
	/// 分支（败北 CG、战斗待机特效、战斗入场动作…）都先看这里的声明：
	/// 声明为 <c>false</c> 时对应设置项即使开着也不会生效。</para>
	/// </summary>
	internal static readonly CharacterSkinFeatureSupportProfile Features = new(
		CombatBodyScalePatch: false,
		CombatPositionOffsetPatch: false,
		CombatVoiceHooks: true,
		CombatEntryAnimation: true,
		CombatIdleEffects: true,
		BattleReadyOverlay: true,
		DeathCgOverlay: true,
		// 本骨架没有近战逼近位移，设置面板也不展示这一项
		MeleeApproachMovement: false,
		OfficialAttackTiming: true);

	/// <summary>设置门：亮度 / 粒子 / 特效 / 败北 CG / 攻击全屏时机等判定都走它。</summary>
	internal static CharacterSkinPresentationGates Gates { get; } =
		new(Features, HeidemarieSettings.CurrentCharacter);

	/// <summary>观众策略：本地玩家 / 队友 / 不支持，音效与全屏演出共用同一判定。</summary>
	internal static CombatPresentationAudiencePolicy Audience { get; } =
		new(HeidemarieSettings.CurrentCharacter, IsHeidemarie);

	/// <summary>攻击指令反射读取器：致死预判（攻击牌全屏演出「可击杀时」）用。</summary>
	internal static CombatAttackEffectTargetResolver AttackTargets { get; } = new();

	/// <summary>是不是海德玛丽（不论本地还是队友）。</summary>
	internal static bool IsHeidemarie(Player? player) => player?.Character is Heidemarie;

	/// <summary>是不是本地玩家操作的海德玛丽。</summary>
	internal static bool IsLocalHeidemarie(Player? player) => IsHeidemarie(player) && LocalContext.IsMe(player);
}
