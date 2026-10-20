using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
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
/// 不做节点池。战斗开始（且首领是 CZN 首领）时把 28 条采样一次性预热进缓存。</para>
/// </summary>
internal static class CznMonsterSfx
{
	/// <summary>占位事件路径前缀；<c>SfxCmd.Play</c> 补丁按它识别并接管。</summary>
	public const string PlaceholderPrefix = "event:/sfx/enemy/enemy_attacks/cznmonster/";

	/// <summary>本 mod 音频根目录（与 pck 内路径一致）。</summary>
	private const string AudioRoot = "res://CznMonster/audio/sfx/polemica/";

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

			// ── 军团兵蚁 1001009（3 条：enter / skill1 / skill2）──
			["POLEMICA_LEGION_SOLDIER"] = new(StringComparer.OrdinalIgnoreCase)
			{
				// 兵蚁只有 enter 采样；登场动画用 normal_summon，这里复用 enter 采样。
				["enter"] = new[] { "1001009/se_coorte_normal_enter.ogg" },
				["summon"] = new[] { "1001009/se_coorte_normal_enter.ogg" },
				["skill1"] = new[] { "1001009/se_coorte_normal_skill1.ogg" },   // 战吼突刺
				["skill2"] = new[] { "1001009/se_coorte_normal_skill2.ogg" },   // 撕咬
			},

			// ── 军团盾蚁 1001010（3 条：enter / skill1 / skill2）──
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

	/// <summary>把采样表里的 28 条 ogg 全部读进缓存（不进战斗也能安全调用）。</summary>
	public static void Prewarm()
	{
		foreach (Dictionary<string, string[]> byClip in Clips.Values)
		{
			foreach (string[] files in byClip.Values)
			{
				foreach (string file in files)
				{
					GetStream(AudioRoot + file);
				}
			}
		}
	}

	/// <summary>这个怪物是否由本类接管声音。</summary>
	public static bool IsOurs(MonsterModel? monster)
	{
		return monster != null && Clips.ContainsKey(monster.Id.Entry);
	}

	/// <summary>按怪物 + clip 名播放采样；怪物不在表里时静默跳过。</summary>
	public static void Play(MonsterModel monster, string clip, float volume = 1f)
	{
		Play(monster.Id.Entry, clip, volume);
	}

	/// <summary>按 entry + clip 名播放采样。</summary>
	public static void Play(string entry, string clip, float volume = 1f)
	{
		if (!Clips.TryGetValue(entry, out Dictionary<string, string[]>? byClip)
			|| !byClip.TryGetValue(clip, out string[]? files)
			|| files.Length == 0)
		{
			return;
		}

		string file = files.Length == 1 ? files[0] : files[Random.Shared.Next(files.Length)];
		PlayFile(AudioRoot + file, volume);
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
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return null;
		}

		// 挂到战斗 UI 下：离房即随房间释放，不需要额外清理。
		Node parent = (Node?)room.Ui ?? room;

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
