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

	/// <summary>格挡类技能语音（防御语音公共入口 <see cref="PlayDefenseVoice"/> 的池子）。</summary>
	private static readonly string[] DefenseVoices = { VoicePath("defense_01"), VoicePath("defense_02") };

	/// <summary>能力牌 / 增益语音。</summary>
	private static readonly string[] BuffVoices = { VoicePath("buff_01"), VoicePath("buff_02") };

	/// <summary>其他技能语音。</summary>
	private static readonly string[] SkillVoices = { VoicePath("skill_01"), VoicePath("skill_02") };

	/// <summary>
	/// ux 全屏演出（剑之乐园 / 破军路线）语音，随机取一条。
	///
	/// <para>只在舞台真正接管画面时使用（<see cref="PlayCinematicSfx"/>）；
	/// 关掉全屏演出后回退档放的是 u3，见 <see cref="FallbackAudioTrigger"/>。</para>
	/// </summary>
	private static readonly string[] UxVoices = { VoicePath("ux"), VoicePath("ux_02") };

	/// <summary>
	/// 选角语音（选人界面点选本角色时，随机取一条）。
	///
	/// <para><c>team_join_01</c> 是原卡厄思的入队语音；<c>lobby_enter_01</c> /
	/// <c>manage_enter_01</c> 是进大厅 / 进管理界面的语音，本次一并并入池子。</para>
	/// </summary>
	private static readonly string[] SelectVoices =
	{
		VoicePath("team_join_01"),
		VoicePath("lobby_enter_01"),
		VoicePath("manage_enter_01"),
	};

	/// <summary>
	/// 动作名 → 音效文件；缺项表示这段动作没有专属音效。
	///
	/// <para><c>XAttack</c> / <c>GAttack</c> 是全屏演出专用动作：舞台接管时整段让位给
	/// <see cref="PlayCinematicSfx"/>；没有舞台时由 <see cref="OnTriggerAnim"/> 经
	/// <see cref="FallbackAudioTrigger"/> 改查 <c>U3Attack</c> 的条目（回退特效组是 u3）。</para>
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

	/// <summary>
	/// 动作名 → 固定语音（没有舞台接管时的单体档台词）。
	///
	/// <para>全屏演出两条路线（ug / ux）不在这里查表，由
	/// <see cref="PlayCinematicSfx"/> 按路线选段；<c>XAttack</c> / <c>GAttack</c> 没有舞台时
	/// 经 <see cref="FallbackAudioTrigger"/> 回退到 <c>U3Attack</c> 的 u3 台词。</para>
	/// </summary>
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

	/// <summary>
	/// 最近一次本角色攻击指令的来源牌（由 <c>Hook.ModifyAttackHitCount</c> 的补丁记账，
	/// 紧邻该指令的 <c>CreatureCmd.TriggerAnim</c> 之前；每次出牌开始时清空）。
	/// </summary>
	private static CardModel? _pendingAttackCard;

	/// <summary>
	/// 本次出牌的主角牌（<c>CardPlayCommittedEvent</c> 时记录，只记本地海德玛丽）。
	///
	/// <para><b>为什么还要这一份</b>：语音去重优先用 <see cref="_pendingAttackCard"/>，
	/// 但它依赖攻击补丁先跑；万一那条链路没走到（补丁提前返回、非攻击指令等），
	/// 去重会退化成「每段各响一句」。这份按「本次出牌的牌」兜底，
	/// 保证剑之乐园这种逐敌循环一次出牌最多一句台词。</para>
	/// </summary>
	private static CardModel? _currentPlayCard;

	/// <summary>
	/// 本次出牌已播过语音的「牌 Id + 触发器」键。
	/// AoE 逐敌攻击（如「剑之乐园」对 3 个敌人发 3 条 AttackCommand）会走到
	/// 3 次 <c>CreatureCmd.TriggerAnim</c>，语音按此去重，保证一次出牌只响一句；
	/// 挥击音效不去重 —— 原生演出下每一击都是真实可见的挥击，按段数响。
	/// </summary>
	private static readonly HashSet<string> VoicedAttackKeys = new(StringComparer.Ordinal);



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
	///
	/// <para><b>AoE 攻击的去重口径</b>（2026-10-16，2026-10-19 修正）：逐敌攻击会为每个敌人
	/// 各走一次本方法，因此 ——</para>
	/// <list type="bullet">
	///   <item>挥击音效：舞台接管时整段让位给演出（<see cref="PlayCinematicSfx"/>
	///   在 epoch 零点播路线音效，一次）；没有舞台时按攻击段数播，
	///   因为每一击都是真实可见的挥击；</item>
	///   <item>语音：舞台接管时让位（由 <see cref="PlayCinematicSfx"/> 发，
	///   整段演出只响一句），但「已响过」的记账**在让位之前**就要完成 ——
	///   否则演出收尾后剩下的攻击段会把 u3 台词再补播一遍（剑之乐园的老问题）；
	///   没有舞台时按「牌 + 触发器」去重，一次出牌只响一句
	///   （见 <see cref="NoteAttackCard"/> / <see cref="IsDuplicateAttackVoice"/>）。</item>
	/// </list>
	///
	/// <para><b>没有舞台时按「回退档」选音</b>：<c>GAttack</c> / <c>XAttack</c> 只属于
	/// 全屏演出动作，它们的单体回退特效组用的是「顶击_u3_完整 / 顶击_u3_核心」，
	/// 所以音效与语音一并回退到 u3 档（见 <see cref="FallbackAudioTrigger"/>）。
	/// 原实现把这两个触发器整段静音，导致「解放极光」关掉全屏演出后只剩特效没有人声。</para>
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

			// 全屏演出接管时，演出的音效 / 语音全部让位给舞台：舞台在
			// presentation_started（演出 epoch 零点）播路线音效并发一句语音，
			// 与动画严格同起、取消即停；这里再播就双响，且 AoE 逐敌攻击会把
			// 同一句台词按敌人数重复。没有舞台（设置关掉 / 致死预判不通过 /
			// 挂载失败降级）时没有舞台接盘，仍走本路播放。
			bool stagePresents = HeidemarieCinematicRuntime.IsStagePresenting(player, triggerName);

			// 语音去重键「先记账再分流」：舞台接管的那些段同样要占键。
			// 「剑之乐园」是逐敌循环，前几段被舞台吞掉、若不占键，等 stage_finished
			// 之后剩下的段就会把 u3 台词再补播一遍 —— 听感上就是同一句话重复。
			bool alreadyVoiced = IsDuplicateAttackVoice(triggerName);

			if (stagePresents)
			{
				// 音效与语音都归演出路径（PlayCinematicSfx 在 presentation_started 发）。
				return;
			}

			// 没有舞台接管 → 按单体攻击档播：全屏专属动作回退成 u3。
			string audioTrigger = FallbackAudioTrigger(triggerName);

			if (HeidemariePresentationHost.Audience.Allows(player, CombatPresentationChannel.CombatSfx)
				&& TriggerSfx.TryGetValue(audioTrigger, out string? sfxPath))
			{
				Sfx.TryPlay(sfxPath);
			}

			// 语音比音效「贵」，且一次出牌只该有一句：AoE 逐敌攻击会为每个敌人
			// 各走一次 TriggerAnim，这里按「牌 + 触发器」去重，只有第一段出声。
			if (alreadyVoiced)
			{
				return;
			}

			if (TriggerVoice.TryGetValue(audioTrigger, out string? voicePath))
			{
				Voice.PlayFor(player, voicePath);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("战斗音频播放失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 记账一条攻击指令的来源牌（由 <c>Hook.ModifyAttackHitCount</c> 的补丁调用，
	/// 时序上紧邻该指令的 <c>CreatureCmd.TriggerAnim</c>）。
	///
	/// <para>同一张牌的连续多条攻击指令（AoE 逐敌循环）不会清账，
	/// 语音去重因此覆盖整次出牌；换牌 / 新出牌时账自动重开
	///（另在 <see cref="PlayCardVoice"/> 的 CardPlayCommitted 入口清一次）。</para>
	///
	/// <para><b>为什么比 Id 而不比实例</b>：牌堆间移动、指令装配都可能带上同 Id 的
	/// 不同实例（<c>CardModel.Id</c> 在克隆后保持不变）。按实例比较会把每一次攻击
	/// 指令都当「换牌」清账，AoE 逐敌循环于是每段各响一句 —— 剑之乐园的 u3 语音
	/// 重复正是这条链。改按 Id 比较后，一次出牌的所有攻击指令共用同一份记账。</para>
	/// </summary>
	/// <param name="card">攻击指令的来源牌。</param>
	internal static void NoteAttackCard(CardModel card)
	{
		if (_pendingAttackCard is null || _pendingAttackCard.Id != card.Id)
		{
			VoicedAttackKeys.Clear();
		}

		_pendingAttackCard = card;
	}

	/// <summary>
	/// 这次触发器语音是否已经在本次出牌里播过（按「牌 Id + 触发器」判定），
	/// 并顺手把本次的键记账（调用即视为「本次要由调用方负责发声或已由演出发声」）。
	/// 没有记账（敌人动作、非攻击链路、来源牌缺失）时不去重。
	/// </summary>
	/// <param name="triggerName">动作触发器名。</param>
	/// <returns><c>true</c> 表示已播过，调用方应当静音。</returns>
	private static bool IsDuplicateAttackVoice(string triggerName)
	{
		// 优先用攻击指令记账的那张牌；它缺账时退到「本次出牌的牌」。
		CardModel? card = _pendingAttackCard ?? _currentPlayCard;
		if (card is null)
		{
			return false;
		}

		// HashSet.Add 已存在时返回 false —— 取反即「是否重复」。
		return !VoicedAttackKeys.Add(card.Id + "|" + triggerName);
	}

	/// <summary>
	/// 该动作在没有全屏舞台接管时，实际要按哪个单体攻击档放音。
	///
	/// <para><c>GAttack</c>（解放极光）/ <c>XAttack</c>（ux 预留档）只有全屏演出才会用，
	/// 单体回退特效组分别是「顶击_u3_完整」/「顶击_u3_核心」
	/// （见 <c>heidemarie_moves.json</c> 的 <c>AttackUg</c> / <c>AttackUx</c>），
	/// 所以音效与语音统一回退成 u3 —— 两张大招关掉全屏演出时都放
	/// <c>se_30093_u3_attack_play</c> + <c>vo_30093_u3</c>。</para>
	/// </summary>
	/// <param name="triggerName">原始动作触发器名。</param>
	/// <returns>用于查 <see cref="TriggerSfx"/> / <see cref="TriggerVoice"/> 的键。</returns>
	private static string FallbackAudioTrigger(string triggerName)
	{
		return triggerName is "GAttack" or "XAttack" ? "U3Attack" : triggerName;
	}

	/// <summary>
	/// 全屏演出的专属音效：由舞台在 <c>presentation_started</c>（演出 epoch 零点）
	/// 调用，与动画严格同起；音效长于画面时尾部自然放完，不被舞台寿命截断
	/// （攻击尾巴只等 <c>stage_finished</c>，不等音效）。
	///
	/// <para>与 <see cref="OnTriggerAnim"/> 的音效路二选一，由
	/// <see cref="HeidemarieCinematicRuntime.IsStagePresenting"/> 保证恰好一次：
	/// 舞台接管走本路，降级回原生演出走 TriggerAnim 老路。</para>
	///
	/// <para><b>语音也在这里发</b>（2026-10-16）：整段演出只响一句台词。
	/// AoE 逐敌攻击会多次走到 TriggerAnim，那时舞台已接管、语音全部让位，
	/// 若不在这里补一句，大招就只剩音效没有台词；而放在 TriggerAnim 那条路上
	/// 又会按敌人数重复。</para>
	///
	/// <para><b>路线 → 台词</b>（2026-10-19）：ug（解放极光）固定 <c>vo_30093_ug</c>；
	/// ux（剑之乐园）在 <c>vo_30093_ux</c> / <c>vo_30093_ux_02</c> 里随机取一条 ——
	/// 演出音效已经是 <c>ux_attack</c>，台词也必须是 ux 那套，不能再用 u3
	/// （u3 是没有舞台时的回退档台词，见 <see cref="FallbackAudioTrigger"/>）。</para>
	///
	/// <param name="player">发起演出的玩家。</param>
	/// <param name="route">演出路线（ug / ux，见 <see cref="HeidemarieCinematicRuntime"/> 的路线常量）。</param>
	internal static void PlayCinematicSfx(Player? player, string route)
	{
		try
		{
			if (player is null || !HeidemariePresentationHost.IsHeidemarie(player))
			{
				return;
			}

			// 演出没开就没人调这里（舞台本身不挂），保留判定做双保险。
			if (!CinematicPresentationEnabled(player))
			{
				return;
			}

			if (!HeidemariePresentationHost.Audience.Allows(player, CombatPresentationChannel.CombatSfx))
			{
				return;
			}

			// 路线 → 演出音效：ug（解放极光）/ ux（剑之乐园）。
			// 与 TriggerSfx 里 GAttack→ug_attack、XAttack→ux_attack 同一批素材；
			// ux 改挂「剑之乐园」后由舞台按路线取，不再依赖触发器名。
			string? sfxPath = route switch
			{
				HeidemarieCinematicRuntime.RouteUg => SfxPath("ug_attack"),
				HeidemarieCinematicRuntime.RouteUx => SfxPath("ux_attack"),
				_ => null,
			};
			if (sfxPath is null)
			{
				return;
			}

			Sfx.TryPlay(sfxPath);

			// 语音与音效同起，整段演出只此一句（见方法文档）。
			// ug 固定一条；ux 是「破军」路线，在两条 ux 台词里随机。
			switch (route)
			{
				case HeidemarieCinematicRuntime.RouteUg:
					Voice.PlayFor(player, VoicePath("ug"));
					break;
				case HeidemarieCinematicRuntime.RouteUx:
					Voice.PlayRandomFor(player, UxVoices);
					break;
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("全屏演出音效播放失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 停掉全部战斗音效 —— 全屏演出被取消 / 打断时由演出运行时调用，
	/// 避免演出没了声音还在响。自然播完不停（尾部放完是设计行为）。
	/// </summary>
	internal static void StopSfx()
	{
		try
		{
			Sfx.Stop();
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("战斗音效停止失败：" + ex.Message);
		}
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

	/// <summary>
	/// 防御语音公共入口：打「防御」与任何加格挡的牌都从这里随机播一条
	/// （<c>vo_30093_defense_01</c> / <c>vo_30093_defense_02</c>）。
	///
	/// <para>出牌链路由 <see cref="PlayCardVoice"/> 按 <c>GainsBlock</c> 分流进来；
	/// 后续想补防御语音的入口（如加格挡的遗物 / 能力）也直接调本方法，
	/// 不要各自 <c>Voice.PlayRandomFor</c>，保证音量 / 开关 / 间隔 / 本地判定一致。</para>
	/// </summary>
	/// <param name="player">正在获得格挡的玩家。</param>
	internal static void PlayDefenseVoice(Player? player)
	{
		try
		{
			if (player is null || !HeidemariePresentationHost.IsLocalHeidemarie(player))
			{
				return;
			}

			Voice.PlayRandomFor(player, DefenseVoices);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("防御语音播放失败：" + ex.Message);
		}
	}

	/// <summary>按卡牌类型选出牌语音。</summary>
	private static void PlayCardVoice(CardModel? card)
	{
		// 新出牌开始：清掉上一条攻击指令的记账，语音去重按「一次出牌」重新计。
		// （放在本地判定之前 —— 记账与清账对所有出牌都要发生。）
		_pendingAttackCard = null;
		_currentPlayCard = null;
		VoicedAttackKeys.Clear();

		try
		{
			Player? player = card?.Owner;
			if (card is null || !HeidemariePresentationHost.IsLocalHeidemarie(player))
			{
				return;
			}

			// 兜底记账：本次出牌的牌（攻击指令记账缺失时，语音去重仍能按它收敛）。
			_currentPlayCard = card;

			// 终结技的语音由各自的攻击动作名播出（解放极光 GAttack / 剑之乐园
			// U3Attack / 绝响 XAttack），出牌时再播一条就会叠成双重语音，
			// 这里一律跳过。
			// 2026-10-16：补「剑之乐园」—— 10-15 把 ux 演出换挂到它身上后
			// 漏了这里，导致出牌瞬间多响一条 attack_01~05，随后 U3Attack
			// 再响 u3。
			if (card is HeidemarieLiberatedAurora or HeidemarieSwordParadise or HeidemarieFinalEcho)
			{
				return;
			}

			// 防御 / 加格挡的牌：走防御语音公共入口，与牌型无关
			if (card.GainsBlock)
			{
				PlayDefenseVoice(player);
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
					Voice.PlayRandomFor(player, SkillVoices);
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
			// 两条全屏演出的专属音效一并预热：它们由舞台在 presentation_started
			// 起播（见 PlayCinematicSfx），第一次放大招才现加载会砸在演出第一帧上。
			Sfx.Prewarm(SfxPath("ug_attack"));
			Sfx.Prewarm(SfxPath("ux_attack"));

			List<string> voicePaths = new();
			voicePaths.AddRange(AttackVoices);
			voicePaths.AddRange(SkillVoices);
			voicePaths.AddRange(DefenseVoices);
			voicePaths.AddRange(BuffVoices);
			voicePaths.Add(VoicePath("hit_01"));
			// 技能攻击 / 全屏演出的固定台词：u1~u4 是动作档台词，
			// ug 是解放极光的演出台词，u3 是两张大招关掉全屏后的回退台词。
			voicePaths.Add(VoicePath("u1"));
			voicePaths.Add(VoicePath("u2"));
			voicePaths.Add(VoicePath("u3"));
			voicePaths.Add(VoicePath("u4"));
			// 剑之乐园的演出台词是随机两条，预报热时两条都要进缓存。
			voicePaths.AddRange(UxVoices);
			voicePaths.Add(VoicePath("ug"));
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
