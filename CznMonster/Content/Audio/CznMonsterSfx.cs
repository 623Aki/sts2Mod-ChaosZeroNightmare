using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznMonster.Content.Audio;

/// <summary>
/// 波勒米卡战团的音效播放器（设计文档 §1.2.1 ① / §5.4）。
///
/// <para><b>为什么不用 <c>event:/...</c></b>：CZN 的 6 个 <c>monster_*.bank</c> 里是
/// FSB5 采样，bank 内的 <c>se_*</c> 只是采样名，不是 FMOD 事件路径，直接填进
/// <c>AttackSfx</c> / <c>DeathSfx</c> 只会 404。所以本类直接加载
/// <c>res://CznMonster/audio/sfx/polemica/**/*.ogg</c>，用 <see cref="AudioStreamPlayer"/>
/// 走 <c>SFX</c> 总线播放。</para>
///
/// <para><b>占位事件路径的作用</b>：<see cref="MonsterModel.AttackSfx"/> /
/// <see cref="MonsterModel.DeathSfx"/> / <see cref="MonsterModel.HurtSfx"/> 只能返回字符串，
/// 无法返回「播放这个 ogg」。于是这些属性统一返回
/// <c>{@link PlaceholderPrefix}{entry}/{clip}</c>，再由
/// <c>Registration.CznMonsterSfxPatch</c> 里的 <c>SfxCmd.Play</c> 前缀补丁
/// 解析成真实采样并吞掉原调用 —— 既不会去解析不存在的 FMOD 事件，也保持零侵入。</para>
///
/// <para><b>播放器生命周期</b>：跟着 <see cref="NCombatRoom"/> 走，离房即失效；
/// 不做节点池。战斗开始（且首领是 CZN 首领）时把采样表里的全部采样一次性预热进缓存。</para>
/// </summary>
internal static class CznMonsterSfx
{
	/// <summary>占位事件路径前缀；<c>SfxCmd.Play</c> 补丁按它识别并接管。</summary>
	public const string PlaceholderPrefix = "event:/sfx/enemy/enemy_attacks/cznmonster/";

	/// <summary>本 mod 的波勒米卡组音频根目录（与 pck 内路径一致）。</summary>
	private const string AudioRoot = "res://CznMonster/audio/sfx/polemica/";

	/// <summary>塞拉菲翁／阿克鲁斯（Diallos 双怪组）音频根目录。</summary>
	private const string DiallosRoot = "res://CznMonster/audio/sfx/diallos/";

	/// <summary>阿涅丝（双形态共用）音频根目录。</summary>
	private const string AgnesRoot = "res://CznMonster/audio/sfx/agnes/";

	/// <summary>游戏自带的音效总线名。</summary>
	private const string SfxBus = "SFX";

	/// <summary>同时可用的播放器数量（首领 + 多只召唤物可能同帧出声）。</summary>
	private const int MaxPlayers = 4;

	/// <summary>
	/// 采样表：<c>entry → clip → ogg 相对路径</c>。
	/// 路径相对 <see cref="AudioRoot"/>，clip 名同时是动画 clip 的关键片段，
	/// 便于「动画名 → 音效」一眼对齐。
	/// </summary>
	private static readonly Dictionary<string, Dictionary<string, string[]>> Clips =
		new(StringComparer.OrdinalIgnoreCase)
		{
			// ── 波勒米卡 1001013（bank 共 10 条：enter / death / fatal / skill1_3 / skill2_3 /
			//    skill3 / skill4 + 3 条 groan）──
			["POLEMICA"] = new(StringComparer.OrdinalIgnoreCase)
			{
				// normal_enter：战斗开始的登场动画
				["enter"] = new[] { "1001013/se_corpsleader_formica_normal_enter.ogg" },
				// normal_skill1_3：AttackHeavy（m_attack_02_1，200% 伤害）
				["skill1_3"] = new[] { "1001013/se_corpsleader_formica_normal_skill1_3.ogg" },
				// normal_skill2_3：Summon（buff_1_1，召唤/强化）
				["skill2_3"] = new[] { "1001013/se_corpsleader_formica_normal_skill2_3.ogg" },
				// normal_skill3：Shell（buff_2_1，护盾）
				["skill3"] = new[] { "1001013/se_corpsleader_formica_normal_skill3.ogg" },
				// normal_skill4：AttackFast（m_attack_01_1，160% 伤害）
				["skill4"] = new[] { "1001013/se_corpsleader_formica_normal_skill4.ogg" },
				// normal_fatal：处决自家召唤物
				["fatal"] = new[] { "1001013/se_1001013_normal_fatal.ogg" },
				// normal_death：本体死亡
				["death"] = new[] { "1001013/se_1001013_normal_death.ogg" },
				// 受击呻吟：3 条随机，对应 HurtSfx
				["hurt"] = new[]
				{
					"1001013/se_1001013_groan_1.ogg",
					"1001013/se_1001013_groan_2.ogg",
					"1001013/se_1001013_groan_3.ogg",
				},
			},

			// ── 不屈的库勒提 1001009（3 条：enter / skill1 / skill2）──
			["POLEMICA_LEGION_SOLDIER"] = new(StringComparer.OrdinalIgnoreCase)
			{
				// 不屈的库勒提只有 enter 采样；登场动画用 normal_summon，这里复用 enter 采样。
				["enter"] = new[] { "1001009/se_coorte_normal_enter.ogg" },
				["summon"] = new[] { "1001009/se_coorte_normal_enter.ogg" },
				["skill1"] = new[] { "1001009/se_coorte_normal_skill1.ogg" },   // 战吼突刺
				["skill2"] = new[] { "1001009/se_coorte_normal_skill2.ogg" },   // 撕咬
			},

			// ── 装甲库勒提 1001010（3 条：enter / skill1 / skill2）──
			["POLEMICA_LEGION_SHIELD"] = new(StringComparer.OrdinalIgnoreCase)
			{
				["enter"] = new[] { "1001010/se_armored_coorte_normal_enter.ogg" },
				["summon"] = new[] { "1001010/se_armored_coorte_normal_enter.ogg" },
				["skill1"] = new[] { "1001010/se_armored_coorte_normal_skill1.ogg" },   // 撞角
				["skill2"] = new[] { "1001010/se_armored_coorte_normal_skill2.ogg" },   // 掩护
			},

			// ── 岩壳虫 1001019（4 条：enter / skill1 / skill2 / fatal）──
			["POLEMICA_ROCK_SHELL_INSECT"] = new(StringComparer.OrdinalIgnoreCase)
			{
				["enter"] = new[] { "1001019/se_shell_insect_normal_enter.ogg" },
				["skill1"] = new[] { "1001019/se_shell_insect_normal_skill1.ogg" },   // 碎岩重击 / 岩壳防御
				["skill2"] = new[] { "1001019/se_shell_insect_normal_skill2.ogg" },   // 岩壳硬化
				["fatal"] = new[] { "1001019/se_1001019_normal_fatal.ogg" },
			},

			// ── 翼壳虫 1001018（4 条：enter / skill1 / skill2 / fatal）──
			["POLEMICA_WING_SHELL_INSECT"] = new(StringComparer.OrdinalIgnoreCase)
			{
				["enter"] = new[] { "1001018/se_shell_insect_normal_enter.ogg" },
				["skill1"] = new[] { "1001018/se_shell_insect_normal_skill1.ogg" },   // 翼击
				["skill2"] = new[] { "1001018/se_shell_insect_normal_skill2.ogg" },   // 翼壳防御
				["fatal"] = new[] { "1001018/se_1001018_normal_fatal.ogg" },
			},

			// ── 壳虫 1001004（4 条：enter / skill1 / skill2 / fatal）──
			["POLEMICA_SHELL_INSECT"] = new(StringComparer.OrdinalIgnoreCase)
			{
				["enter"] = new[] { "1001004/se_shell_insect_normal_enter.ogg" },
				["skill1"] = new[] { "1001004/se_shell_insect_normal_skill1.ogg" },   // 甲壳撕咬
				["skill2"] = new[] { "1001004/se_shell_insect_normal_skill2.ogg" },   // 甲壳防御
				["fatal"] = new[] { "1001004/se_1001004_normal_fatal.ogg" },
			},

			// ── 塞拉菲翁 1003017（bank 24 条；本表只挂用到的 13 条，采样名以 bank 导出实测为准）──
			["SERAPHION"] = new(StringComparer.OrdinalIgnoreCase)
			{
				// se_seraphion_normal_enter：登场
				["enter"] = new[] { "1003017/se_seraphion_normal_enter.ogg" },
				// se_seraphion_normal_attack_play1：双牙撕咬（normal_attack_play1）
				["bite"] = new[] { "1003017/se_seraphion_normal_attack_play1.ogg" },
				// se_seraphion_normal_attack_play2：终焉回旋（normal_attack_play2）
				["cyclone"] = new[] { "1003017/se_seraphion_normal_attack_play2.ogg" },
				// se_seraphion_stance2_buff_1：光耀增幅（stance2_buff_1）
				["finalebuff"] = new[] { "1003017/se_seraphion_stance2_buff_1.ogg" },
				// se_seraphion_stance2_attack_play1：终焉四连（stance2_attack_play1）
				["finalquad"] = new[] { "1003017/se_seraphion_stance2_attack_play1.ogg" },
				// se_seraphion_stance3_to_normal：复归拍 ① 起身（无濒死入场采样：bank 无 normal_to_stance3，T15 静音）
				["revival"] = new[] { "1003017/se_seraphion_stance3_to_normal.ogg" },
				// se_seraphion_normal_to_stance1：复归拍 ② 切持盾形态
				["toshield"] = new[] { "1003017/se_seraphion_normal_to_stance1.ogg" },
				// se_seraphion_stance1_attack_play1：回稳三连（stance1_attack_play1）
				["shieldstance"] = new[] { "1003017/se_seraphion_stance1_attack_play1.ogg" },
				// se_seraphion_stance1_to_normal：退出持盾形态回 P1
				["tonormal"] = new[] { "1003017/se_seraphion_stance1_to_normal.ogg" },
				// se_1003017_normal_death：普通死亡
				["death"] = new[] { "1003017/se_1003017_normal_death.ogg" },
				// se_1003017_stance3_death：濒死形态被伴生真死（T9）
				["death_fading"] = new[] { "1003017/se_1003017_stance3_death.ogg" },
				// se_1003017_normal_hit：受击
				["hurt"] = new[] { "1003017/se_1003017_normal_hit.ogg" },
				// se_1003017_normal_fatal：处决（占位保留，本组当前未使用全屏处决）
				["fatal"] = new[] { "1003017/se_1003017_normal_fatal.ogg" },
			},

			// ── 阿克鲁斯 1003016（bank 12 条；用 11 条；无受击采样，T15 不发声）──
			["ARKRUS"] = new(StringComparer.OrdinalIgnoreCase)
			{
				// se_arkrus_normal_enter：登场
				["enter"] = new[] { "1003016/se_arkrus_normal_enter.ogg" },
				// se_arkrus_normal_buff_1：庇护（normal_buff_1）
				["ward"] = new[] { "1003016/se_arkrus_normal_buff_1.ogg" },
				// se_arkrus_normal_attack_play1：守护突进（normal_attack_play1）
				["assault"] = new[] { "1003016/se_arkrus_normal_attack_play1.ogg" },
				// se_arkrus_normal_to_stance1：入救援形态
				["torescue"] = new[] { "1003016/se_arkrus_normal_to_stance1.ogg" },
				// se_arkrus_stance1_m_attack_01：救援突刺
				["rescuestrike"] = new[] { "1003016/se_arkrus_stance1_m_attack_01.ogg" },
				// se_arkrus_stance1_debuff_1：救援咒缚
				["rescuecurse"] = new[] { "1003016/se_arkrus_stance1_debuff_1.ogg" },
				// se_arkrus_stance1_unique_1：救援颂歌
				["rescuesong"] = new[] { "1003016/se_arkrus_stance1_unique_1.ogg" },
				// se_arkrus_stance1_to_normal：退出救援形态
				["toguard"] = new[] { "1003016/se_arkrus_stance1_to_normal.ogg" },
				// se_1003016_normal_death：普通死亡
				["death"] = new[] { "1003016/se_1003016_normal_death.ogg" },
				// se_1003016_stance1_death：救援形态死亡
				["death_rescue"] = new[] { "1003016/se_1003016_stance1_death.ogg" },
				// se_1003016_normal_fatal：处决（占位保留）
				["fatal"] = new[] { "1003016/se_1003016_normal_fatal.ogg" },
			},

			// ── 阿涅丝（3000504 + 3000503 双形态；SFX bank 两底共 14 条，用 10 条；无受击采样，T15 不发声）──
			["AGNES"] = new(StringComparer.OrdinalIgnoreCase)
			{
				// se_3000504_normal_enter：登场
				["enter"] = new[] { "3000504/se_3000504_normal_enter.ogg" },
				// se_3000504_normal_attack_play2：集约期连击 / 重击（normal_attack 组共用动作）
				["combo"] = new[] { "3000504/se_3000504_normal_attack_play2.ogg" },
				// se_3000504_normal_buff_1：集约期增益（normal_buff_1）
				["buff"] = new[] { "3000504/se_3000504_normal_buff_1.ogg" },
				// se_3000503_stance1_run：变身收尾（无 transform 专属采样，借 move 鼓点）
				["transform"] = new[] { "3000503/se_3000503_stance1_run.ogg" },
				// se_3000503_stance1_m_attack_01：Form B 重击（stance1_m_attack_01）
				["b_heavy"] = new[] { "3000503/se_3000503_stance1_m_attack_01.ogg" },
				// se_3000503_stance1_attack_play1：Form B 三连（stance1_attack 循环组）
				["b_triple"] = new[] { "3000503/se_3000503_stance1_attack_play1.ogg" },
				// se_3000503_stance1_s_attack_01：Form B 斩击（stance1_s_attack_01）
				["b_slash"] = new[] { "3000503/se_3000503_stance1_s_attack_01.ogg" },
				// se_3000503_stance1_debuff_1：Form B 压制（stance1_debuff_1）
				["debuff"] = new[] { "3000503/se_3000503_stance1_debuff_1.ogg" },
				// se_3000503_stance1_death：本体死亡（Form A 无死亡采样，DeathSfx 判 _formB）
				["death"] = new[] { "3000503/se_3000503_stance1_death.ogg" },
				// se_3000503_stance1_fatal：处决（占位保留，本组当前未使用）
				["fatal"] = new[] { "3000503/se_3000503_stance1_fatal.ogg" },
			},
		};

	/// <summary>已加载的采样缓存（key = 完整 res:// 路径）。</summary>
	private static readonly Dictionary<string, AudioStream> Cache =
		new(StringComparer.OrdinalIgnoreCase);

	/// <summary>缺失资源只提示一次，避免每回合刷屏。</summary>
	private static readonly HashSet<string> Warned = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>当前挂在哪个父节点下；房间切换时重新挂载。</summary>
	private static readonly List<AudioStreamPlayer> Players = new();

	private static Node? _attachedTo;

	private static int _nextPlayer;

	/// <summary>
	/// 订阅战斗生命周期（<see cref="MainFile.Initialize"/> 调用）。
	/// 只有本 mod 首领在场时才预热，避免每次进战斗都白读 28 个文件。
	/// </summary>
	internal static void Initialize()
	{
		CombatManager.Instance.CombatSetUp += OnCombatSetUp;
	}

	private static void OnCombatSetUp(CombatState state)
	{
		try
		{
			if (state.Encounter == null || !MonsterContent.IsCznBoss(state.Encounter.Id.Entry))
			{
				return;
			}
			Prewarm();
		}
		catch (Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 音效预热跳过：{ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>把采样表里的全部 ogg 读进缓存（不进战斗也能安全调用）。</summary>
	public static void Prewarm()
	{
		foreach (KeyValuePair<string, Dictionary<string, string[]>> group in Clips)
		{
			foreach (string[] files in group.Value.Values)
			{
				foreach (string file in files)
				{
					GetStream(RootFor(group.Key) + file);
				}
			}
		}
	}

	/// <summary>
	/// entry → 采样表分组：图鉴展示用的第二形态（<c>AGNES_FINAL</c>）复用阿涅丝的
	/// <c>AGNES</c> 组（同骨架家族、同 bank），其余 entry 即分组名。
	/// </summary>
	private static string GroupOf(string entry)
	{
		return entry.Equals("AGNES_FINAL", StringComparison.OrdinalIgnoreCase) ? "AGNES" : entry;
	}

	/// <summary>按 entry 选音频根：塞拉菲翁／阿克鲁斯走 Diallos 组，阿涅丝走 Agnes 组，其余走波勒米卡组。</summary>
	private static string RootFor(string entry)
	{
		if (entry.Equals("AGNES", StringComparison.OrdinalIgnoreCase))
		{
			return AgnesRoot;
		}
		return entry.Equals("SERAPHION", StringComparison.OrdinalIgnoreCase)
			|| entry.Equals("ARKRUS", StringComparison.OrdinalIgnoreCase)
			? DiallosRoot
			: AudioRoot;
	}

	/// <summary>这个怪物是否由本类接管声音。</summary>
	public static bool IsOurs(MonsterModel? monster)
	{
		return monster != null && Clips.ContainsKey(GroupOf(monster.Id.Entry));
	}

	/// <summary>按怪物 + clip 名播放采样；怪物不在表里时静默跳过。</summary>
	public static void Play(MonsterModel monster, string clip, float volume = 1f)
	{
		Play(monster.Id.Entry, clip, volume);
	}

	/// <summary>按 entry + clip 名播放采样。</summary>
	public static void Play(string entry, string clip, float volume = 1f)
	{
		entry = GroupOf(entry);
		if (!Clips.TryGetValue(entry, out Dictionary<string, string[]>? byClip)
			|| !byClip.TryGetValue(clip, out string[]? files)
			|| files.Length == 0)
		{
			return;
		}

		string file = files.Length == 1 ? files[0] : files[Random.Shared.Next(files.Length)];
		PlayFile(RootFor(entry) + file, volume);
	}

	/// <summary>播放一个绝对 <c>res://</c> 路径的采样。</summary>
	public static void PlayFile(string resPath, float volume = 1f)
	{
		AudioStream? stream = GetStream(resPath);
		if (stream == null)
		{
			return;
		}

		AudioStreamPlayer? player = EnsurePlayer();
		if (player == null)
		{
			return;
		}

		player.Stop();
		player.Stream = stream;
		player.VolumeLinear = Math.Max(0f, volume);
		player.Bus = new StringName(SfxBus);
		player.Play();
	}

	/// <summary>
	/// <c>SfxCmd.Play</c> 补丁入口：命中占位前缀就接管并返回 <c>true</c>；
	/// 其余路径返回 <c>false</c>，交回原实现（别的角色/怪物零影响）。
	/// </summary>
	public static bool TryPlayPlaceholder(string? eventPath, float volume)
	{
		if (string.IsNullOrEmpty(eventPath)
			|| !eventPath.StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string rest = eventPath.Substring(PlaceholderPrefix.Length);
		int slash = rest.LastIndexOf('/');
		if (slash > 0 && slash < rest.Length - 1)
		{
			Play(rest.Substring(0, slash), rest.Substring(slash + 1), volume);
		}

		return true;
	}

	/// <summary>拼一个占位事件路径（供各怪物属性使用）。</summary>
	public static string Placeholder(MonsterModel monster, string clip)
	{
		return PlaceholderPrefix + monster.Id.Entry + "/" + clip;
	}

	// ── 播放器管理 ──

	private static AudioStream? GetStream(string resPath)
	{
		if (Cache.TryGetValue(resPath, out AudioStream? cached))
		{
			if (GodotObject.IsInstanceValid(cached))
			{
				return cached;
			}
			Cache.Remove(resPath);
		}

		try
		{
			if (!ResourceLoader.Exists(resPath))
			{
				WarnOnce(resPath, "资源不存在");
				return null;
			}
			AudioStream? stream = ResourceLoader.Load<AudioStream>(resPath);
			if (stream == null)
			{
				WarnOnce(resPath, "加载失败");
				return null;
			}
			Cache[resPath] = stream;
			return stream;
		}
		catch (Exception ex)
		{
			WarnOnce(resPath, "加载异常 " + ex.GetType().Name);
			return null;
		}
	}

	private static AudioStreamPlayer? EnsurePlayer()
	{
		// 战斗内挂到战斗 UI 下（离房即随房间释放，不需要额外清理）；
		// 图鉴里没有战斗房间（NCombatRoom.Instance 为 null），退回挂到 NGame 根上，
		// 否则图鉴的招式按钮会「动画播了、没有任何声音」。
		Node? parent = NCombatRoom.Instance is { } room
			? ((Node?)room.Ui ?? room)
			: NGame.Instance;
		if (parent == null)
		{
			return null;
		}

		for (int i = Players.Count - 1; i >= 0; i--)
		{
			if (!GodotObject.IsInstanceValid(Players[i]))
			{
				Players.RemoveAt(i);
			}
		}
		if (Players.Count == 0)
		{
			_nextPlayer = 0;
		}
		else if (_nextPlayer >= Players.Count)
		{
			_nextPlayer = 0;
		}

		if (!ReferenceEquals(_attachedTo, parent))
		{
			foreach (AudioStreamPlayer existing in Players)
			{
				existing.GetParent()?.RemoveChild(existing);
				parent.AddChild(existing, forceReadableName: false, Node.InternalMode.Disabled);
			}
			_attachedTo = parent;
		}

		foreach (AudioStreamPlayer existing in Players)
		{
			if (!existing.Playing)
			{
				return existing;
			}
		}

		if (Players.Count < MaxPlayers)
		{
			AudioStreamPlayer created = new()
			{
				Name = "CznMonsterSfx" + Players.Count,
				Bus = new StringName(SfxBus),
			};
			parent.AddChild(created, forceReadableName: false, Node.InternalMode.Disabled);
			Players.Add(created);
			_attachedTo = parent;
			return created;
		}

		_nextPlayer = (_nextPlayer + 1) % Players.Count;
		AudioStreamPlayer reused = Players[_nextPlayer];
		reused.Stop();
		return reused;
	}

	private static void WarnOnce(string resPath, string reason)
	{
		if (Warned.Add(resPath))
		{
			MainFile.Log.Warn($"CznMonster 音效{reason}：{resPath}");
		}
	}
}
