using System;
using System.Collections.Generic;
using CznCore.Godot;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Integration;

/// <summary>
/// 战斗音效与角色语音的接入层 —— 把
/// <c>特效音效 / 特效音量 / 角色语音 / 语音音量 / 角色语音间隔</c>
/// 五个设置项真正接到音频上。
///
/// <para>素材来源：卡厄思梦境素材区 <c>CNZ_Source/czn30093/sound/*.bank</c>
/// （FMOD 事件库，内嵌 FSB5 Vorbis 采样）。音频由 <c>tools/extract_fmod_bank.py</c>
/// 导出为 <c>.ogg</c>，落在 <c>res://CznHeidemarie/audio/{sfx,voice}/</c>。</para>
///
/// <para>两个播放器都直接用 CznCore 的现成运行时，因此音量、开关、语音间隔、
/// 队友门（<see cref="CombatPresentationAudiencePolicy"/>）全是框架行为；
/// 本类只负责「什么时候放哪一段」。</para>
///
/// <para>触发点两处：<c>CreatureCmd.TriggerAnim</c> 的补丁（骨架动作名 → 音效 / 语音），
/// 以及 <c>SkinEventHub</c> 的生命周期事件（出牌、胜利、阵亡、休息点）。</para>
/// </summary>
internal static class HeidemarieCombatAudio
{
	/// <summary>音效目录。</summary>
	private const string SfxRoot = "res://CznHeidemarie/audio/sfx/";

	/// <summary>语音目录。</summary>
	private const string VoiceRoot = "res://CznHeidemarie/audio/voice/";

	/// <summary>入场语音延迟（秒）：等入场动作与本体显示都到位再出声。</summary>
	private const float EntryVoiceDelaySeconds = 0.6f;

	/// <summary>普通攻击语音（随机取一条）。</summary>
	private static readonly string[] AttackVoices =
	{
		VoicePath("attack_01"), VoicePath("attack_02"), VoicePath("attack_03"),
		VoicePath("attack_04"), VoicePath("attack_05"),
	};

	/// <summary>格挡类技能语音。</summary>
	private static readonly string[] DefenseVoices = { VoicePath("defense_01"), VoicePath("defense_02") };

	/// <summary>能力牌 / 增益语音。</summary>
	private static readonly string[] BuffVoices = { VoicePath("buff_01"), VoicePath("buff_02") };

	/// <summary>其他技能语音。</summary>
	private static readonly string[] SkillVoices = { VoicePath("skill_01"), VoicePath("skill_02") };

	/// <summary>终结技 ug（解放极光）语音。</summary>
	private static readonly string[] UgVoices = { VoicePath("ug"), VoicePath("skill_special_01"), VoicePath("skill_cutin_01") };

	/// <summary>终结技 ux（绝响）语音。</summary>
	private static readonly string[] UxVoices = { VoicePath("ux"), VoicePath("ux_02") };

	/// <summary>选角语音（选人界面点选本角色时）。</summary>
	private static readonly string[] SelectVoices = { VoicePath("team_join_01") };

	/// <summary>
	/// 动作名 → 音效文件；缺项表示这段动作没有专属音效。
	///
	/// <para><c>XAttack</c> / <c>GAttack</c> 两条属于全屏演出（绝响 / 解放极光），
	/// 播放前还要过「全屏演出」门，见 <see cref="OnTriggerAnim"/>。</para>
	/// </summary>
	private static readonly Dictionary<string, string> TriggerSfx = new(StringComparer.Ordinal)
	{
		["Attack"] = SfxPath("attack_play1"),
		["Attack2"] = SfxPath("attack_play2"),
		["U1Attack"] = SfxPath("u1_attack_play"),
		["U3Attack"] = SfxPath("u3_attack_play"),
		["XAttack"] = SfxPath("ux_attack"),
		["GAttack"] = SfxPath("ug_attack"),
		["Cure"] = SfxPath("buff_ready"),
		["U2Buff"] = SfxPath("u2_buff_ready"),
		["U4Buff"] = SfxPath("u4_buff_ready"),
		["Dead"] = SfxPath("collapse"),
	};

	/// <summary>动作名 → 固定语音；随机多段的走 <see cref="UgVoices"/> / <see cref="UxVoices"/>。</summary>
	private static readonly Dictionary<string, string> TriggerVoice = new(StringComparer.Ordinal)
	{
		["U1Attack"] = VoicePath("u1"),
		["U3Attack"] = VoicePath("u3"),
		["U2Buff"] = VoicePath("u2"),
		["U4Buff"] = VoicePath("u4"),
		["Hit"] = VoicePath("hit_01"),
		["Dead"] = VoicePath("death_01"),
		["Entry"] = VoicePath("stage_enter_01"),
	};

	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>战斗音效播放器（音量与开关由设置控制）。</summary>
	internal static CharacterSkinSfxPlayerRuntime Sfx { get; } =
		new(HeidemarieSettings.CurrentCharacter, "HeidemarieCombatSfx");

	/// <summary>角色语音播放器（音量、开关、间隔由设置控制）。</summary>
	internal static CharacterSkinVoicePlayerRuntime Voice { get; } =
		new(HeidemarieSettings.CurrentCharacter, "HeidemarieCombatVoice");

	/// <summary>
	/// 订阅生命周期事件。由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	internal static void Initialize()
	{
		// 出牌语音：按牌的类型选段（两张终结技交给攻击动作名那条路，避免两段语音叠在一起）
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 出牌语音", (CardPlayCommittedEvent e) => PlayCardVoice(e.Card)));

		// 进战斗时预热：第一次出牌才现加载会卡半帧
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 音频预热", (CombatStartingEvent e) => Prewarm(e.CombatState)));

		// 战斗入场语音：入场动作是 HeidemarieCombatEntryRuntime 直接调
		// NCreature.SetAnimationTrigger("Entry") 播的，不走 CreatureCmd.TriggerAnim，
		// 所以这里按事件补一句，并留一点延迟等本体现身。
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 入场语音", (CombatStartingEvent e) => PlayEntryVoice(e.CombatState)));

		// 胜利 / 阵亡 / 休息点：各一句
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 胜利语音", (PlayerCombatVictoryEvent e) =>
		{
			if (HeidemariePresentationHost.IsLocalHeidemarie(e.Player))
			{
				Voice.PlayFor(e.Player, VoicePath("stage_success_01"));
			}
		}));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 阵亡语音", (PlayerDeathResolvedEvent e) =>
		{
			if (HeidemariePresentationHost.IsLocalHeidemarie(e.Player))
			{
				Voice.Stop();
				Voice.PlayFor(e.Player, VoicePath("lose_01"));
			}
		}));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 休息点语音", (RestSiteHealedEvent e) =>
		{
			if (HeidemariePresentationHost.IsLocalHeidemarie(e.Player))
			{
				Voice.PlayFor(e.Player, VoicePath("camp_01"));
			}
		}));
	}

	/// <summary>
	/// 骨架动作触发时播对应的音效与语音（由 <c>CreatureCmd.TriggerAnim</c> 的补丁调用）。
	///
	/// <para>只对海德玛丽生效；音效还要过观众门（队友演出设置）。</para>
	/// </summary>
	/// <param name="creature">正在播动作的单位。</param>
	/// <param name="triggerName">动作触发器名。</param>
	internal static void OnTriggerAnim(Creature creature, string triggerName)
	{
		try
		{
			Player? player = creature.Player;
			if (!HeidemariePresentationHost.IsHeidemarie(player))
			{
				return;
			}

			// 全屏演出牌的专属声音归演出所有：绝响（XAttack / ux）与
			// 解放极光（GAttack / ug）的音效与语音，在参考 mod 里由演出驱动，
			// 关掉「全屏演出」就整段不播。这里按同一道门控，
			// 保证「关演出 = 对应声音同步关闭」。
			if (IsCinematicTrigger(triggerName) && !CinematicPresentationEnabled(player))
			{
				return;
			}

			if (HeidemariePresentationHost.Audience.Allows(player, CombatPresentationChannel.CombatSfx)
				&& TriggerSfx.TryGetValue(triggerName, out string? sfxPath))
			{
				Sfx.TryPlay(sfxPath);
			}

			// 语音比音效「贵」（有间隔限制），所以只在真正有台词的时机调
			if (triggerName == "XAttack")
			{
				Voice.PlayRandomFor(player, UxVoices);
			}
			else if (triggerName == "GAttack")
			{
				Voice.PlayRandomFor(player, UgVoices);
			}
			else if (TriggerVoice.TryGetValue(triggerName, out string? voicePath))
			{
				Voice.PlayFor(player, voicePath);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("战斗音频播放失败：" + ex.Message);
		}
	}

	/// <summary>这个动作触发器是不是全屏演出牌专用的（绝响 <c>XAttack</c> / 解放极光 <c>GAttack</c>）。</summary>
	private static bool IsCinematicTrigger(string triggerName)
	{
		return triggerName is "XAttack" or "GAttack";
	}

	/// <summary>
	/// 全屏演出是否对该玩家生效 —— 与参考 mod 的判定同源
	/// （<c>EffectiveEffectsEnabled &amp;&amp; Allows(CutIn)</c>）。
	///
	/// <para>本地玩家看「你的全屏角色演出」（<c>ShowCardPlayPresentation</c>），
	/// 队友看「队友全屏演出」；特效总开关关掉时一律不生效。
	/// 演出牌的专属声音只有在这道门打开时才播。</para>
	/// </summary>
	private static bool CinematicPresentationEnabled(Player? player)
	{
		return HeidemariePresentationHost.Gates.EffectsEnabled
			&& HeidemariePresentationHost.Audience.Allows(player, CombatPresentationChannel.CutIn);
	}

	/// <summary>
	/// 玩家死亡瞬间的战斗音效。
	///
	/// <para><b>为什么单独开一个入口</b>：<c>CreatureCmd.TriggerAnim</c> 只对
	/// <c>Attack</c> / <c>Cast</c> / <c>PowerUp</c> 播角色音效，死亡走的是
	/// <c>NCreature.StartDeathAnim</c> → <c>SfxCmd.PlayDeath</c>，
	/// <see cref="OnTriggerAnim"/> 收不到 <c>"Dead"</c>。因此由
	/// <c>Registration/HeidemariePlayerDeathSfxPatch</c> 在拦下占位事件时调用这里。</para>
	///
	/// <para>只补音效通道：战败语音已由
	/// <see cref="PlayerDeathResolvedEvent"/> 那条订阅负责（<c>lose_01</c>），
	/// 时机在死亡动画结算之后，两条不重叠。</para>
	/// </summary>
	/// <param name="player">正在播放死亡动画的玩家。</param>
	internal static void OnPlayerDeath(Player? player)
	{
		try
		{
			if (!HeidemariePresentationHost.IsHeidemarie(player))
			{
				return;
			}

			if (HeidemariePresentationHost.Audience.Allows(player, CombatPresentationChannel.CombatSfx)
				&& TriggerSfx.TryGetValue("Dead", out string? sfxPath))
			{
				Sfx.TryPlay(sfxPath);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("死亡音效播放失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 选角语音：在选人界面点选本角色时播一句。
	///
	/// <para>由占位音效补丁（<c>Registration/HeidemarieCombatPresentationPatch.cs</c> 的
	/// <c>SfxCmd.Play</c> 前缀）在拦下 <c>CharacterSelectSfx</c> 的占位事件时调用。
	/// 那条链路上拿不到 <c>Player</c> 实例，所以走无参重载 —— 它只认「角色语音」开关与
	/// 语音音量 / 间隔，不需要玩家上下文。</para>
	/// </summary>
	internal static void PlayCharacterSelectVoice()
	{
		try
		{
			Voice.PlayRandom(SelectVoices);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("选角语音播放失败：" + ex.Message);
		}
	}

	/// <summary>立即停掉全部语音（设置里关掉「角色语音」时调用）。</summary>
	internal static void StopVoice()
	{
		try
		{
			Voice.Stop();
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("停止语音失败：" + ex.Message);
		}
	}

	/// <summary>清理音效缓存（设置里关掉「特效音效」时调用）。</summary>
	internal static void TrimSfx()
	{
		try
		{
			Sfx.TrimCache(stopPlayback: true);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("清理音效缓存失败：" + ex.Message);
		}
	}

	/// <summary>按卡牌类型选出牌语音。</summary>
	private static void PlayCardVoice(CardModel? card)
	{
		try
		{
			Player? player = card?.Owner;
			if (card is null || !HeidemariePresentationHost.IsLocalHeidemarie(player))
			{
				return;
			}

			// 走全屏演出的两张牌由各自的动作名（GAttack / XAttack）出语音
			if (card is HeidemarieLiberatedAurora or HeidemarieFinalEcho)
			{
				return;
			}

			switch (card.Type)
			{
				case CardType.Attack:
					Voice.PlayRandomFor(player, AttackVoices);
					break;
				case CardType.Power:
					Voice.PlayRandomFor(player, BuffVoices);
					break;
				case CardType.Skill:
					Voice.PlayRandomFor(player, card.GainsBlock ? DefenseVoices : SkillVoices);
					break;
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("出牌语音播放失败：" + ex.Message);
		}
	}

	/// <summary>战斗入场语音（进战斗后稍等本体现身再出声）。</summary>
	private static void PlayEntryVoice(ICombatState? combatState)
	{
		Player? player = FindLocalHeidemarie(combatState);
		if (player is null)
		{
			return;
		}

		GodotScheduler.Delay(EntryVoiceDelaySeconds, () => Voice.PlayFor(player, VoicePath("stage_enter_01")));
	}

	/// <summary>进战斗时把本次会用到的音频预热进缓存。</summary>
	private static void Prewarm(ICombatState? combatState)
	{
		if (combatState is null || FindLocalHeidemarie(combatState) is null)
		{
			return;
		}

		try
		{
			Sfx.Prewarm(SfxPath("attack_play1"));
			Sfx.Prewarm(SfxPath("attack_play2"));
			Sfx.Prewarm(SfxPath("u1_attack_play"));
			Sfx.Prewarm(SfxPath("u3_attack_play"));
			Sfx.Prewarm(SfxPath("buff_ready"));

			List<string> voicePaths = new();
			voicePaths.AddRange(AttackVoices);
			voicePaths.AddRange(SkillVoices);
			voicePaths.AddRange(DefenseVoices);
			voicePaths.AddRange(BuffVoices);
			voicePaths.Add(VoicePath("hit_01"));
			Voice.Prewarm(voicePaths);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("音频预热失败：" + ex.Message);
		}
	}

	/// <summary>找本场战斗里本地玩家的海德玛丽；没有就是 null。</summary>
	private static Player? FindLocalHeidemarie(ICombatState? combatState)
	{
		if (combatState is null)
		{
			return null;
		}

		foreach (Player player in combatState.Players)
		{
			if (HeidemariePresentationHost.IsLocalHeidemarie(player))
			{
				return player;
			}
		}

		return null;
	}

	/// <summary>拼一条音效路径。</summary>
	private static string SfxPath(string stem) => SfxRoot + "se_30093_" + stem + ".ogg";

	/// <summary>拼一条语音路径。</summary>
	private static string VoicePath(string stem) => VoiceRoot + "vo_30093_" + stem + ".ogg";
}
