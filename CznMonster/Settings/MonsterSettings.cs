using System;
using System.Globalization;
using Godot;

namespace CznMonster.Settings;

/// <summary>
/// CznMonster 设置存储（设计文档 §4.6）：
/// Godot ConfigFile，路径 <c>user://CznMonster/monster.cfg</c>，每次 set 立即落盘。
/// </summary>
internal static class MonsterSettings
{
	private const string DirPath = "user://CznModConfig/CznMonster";

	private const string FilePath = "user://CznModConfig/CznMonster/monster.cfg";

	private static readonly ConfigFile Config = new ConfigFile();

	private static bool _loaded;

	// ── 行动计数（默认关；范围默认"所有首领"）────────────────────────────
	private static bool _actionCountEnabled;

	private static int _actionCountScope = 1;

	// ── CZN 怪物（默认关；位置默认"随机"；血量默认"默认"）──────────────────
	private static bool _cznMonstersEnabled;

	private static bool _bossPosRandom = true;

	private static bool _bossPosSpecified;

	private static int _bossChapter = 1;

	// 指定首领：0=随机（默认），1..N=MonsterContent.BossEncounters 下标 +1
	private static int _bossSelect;

	private static bool _bossHpCustom;

	private static string _bossHpValueText = string.Empty;

	static MonsterSettings()
	{
		Load();
	}

	/// <summary>是否启用行动计数（总开关，可作用于原生怪）。</summary>
	public static bool ActionCountEnabled
	{
		get => _actionCountEnabled;
		set
		{
			_actionCountEnabled = value;
			Save();
		}
	}

	/// <summary>行动计数范围：1=所有首领，2=所有首领+精英，3=所有怪物。</summary>
	public static int ActionCountScope
	{
		get => _actionCountScope;
		set
		{
			_actionCountScope = Math.Clamp(value, 1, 3);
			Save();
		}
	}

	/// <summary>是否启用 CZN 怪物（默认关）。</summary>
	public static bool CznMonstersEnabled
	{
		get => _cznMonstersEnabled;
		set
		{
			_cznMonstersEnabled = value;
			Save();
		}
	}

	/// <summary>首领出现位置：随机（与"指定"互斥）。</summary>
	public static bool BossPosRandom
	{
		get => _bossPosRandom;
		set
		{
			_bossPosRandom = value;
			if (value)
			{
				_bossPosSpecified = false;
			}
			Save();
		}
	}

	/// <summary>首领出现位置：指定章节（与"随机"互斥）。</summary>
	public static bool BossPosSpecified
	{
		get => _bossPosSpecified;
		set
		{
			_bossPosSpecified = value;
			if (value)
			{
				_bossPosRandom = false;
			}
			Save();
		}
	}

	/// <summary>指定章节：1=第 1 章，2=第 2 章，3=第 3 章。</summary>
	public static int BossChapter
	{
		get => _bossChapter;
		set
		{
			_bossChapter = Math.Clamp(value, 1, 3);
			Save();
		}
	}

	/// <summary>
	/// 指定首领：0 = 随机（默认），1..N = MonsterContent.BossEncounters 的下标 +1。
	/// 只在「首领出现位置：指定章节」开启时生效；不再是 CZN 首领时按随机处理。
	/// </summary>
	public static int BossSelect
	{
		get => _bossSelect;
		set
		{
			_bossSelect = Math.Max(0, value);
			Save();
		}
	}

	/// <summary>首领血量：自定义开关（关=怪物原生血量）。</summary>
	public static bool BossHpCustom
	{
		get => _bossHpCustom;
		set
		{
			_bossHpCustom = value;
			Save();
		}
	}

	/// <summary>自定义血量文本（原始输入；非法输入不落盘，保持上次合法值）。</summary>
	public static string BossHpValueText
	{
		get => _bossHpValueText;
		set
		{
			_bossHpValueText = value;
			// §4.5：解析为整数并夹取 1–9999；非法输入不落盘，保持上次合法值
			if (ParsedHp == null)
			{
				_bossHpValueText = _lastValidText;
				return;
			}
			_lastValidText = _bossHpValueText;
			Save();
		}
	}

	private static string _lastValidText = string.Empty;

	/// <summary>纯解析（与开关无关）：整数 1–9999；空 / 非法返回 null。</summary>
	private static int? ParsedHp
	{
		get
		{
			if (string.IsNullOrWhiteSpace(_bossHpValueText))
			{
				return null;
			}
			if (!int.TryParse(_bossHpValueText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			{
				return null;
			}
			if (value < 1)
			{
				return 1;
			}
			if (value > 9999)
			{
				return 9999;
			}
			return value;
		}
	}

	/// <summary>自定义血量（开关打开时取解析值；开关关闭返回 null，用怪物原生血量）。</summary>
	public static int? CustomBossHp => _bossHpCustom ? (ParsedHp ?? DefaultHp) : null;

	/// <summary>
	/// 自定义血量的整数视图。共享设置面板的旧版 CznCore 只支持
	/// toggle / slider / choice，不支持 text，所以 UI 用 slider 绑定这个属性。
	/// </summary>
	public static int BossHpValue
	{
		get => ParsedHp ?? DefaultHp;
		set => BossHpValueText = value.ToString(CultureInfo.InvariantCulture);
	}

	private const int DefaultHp = 999;

	/// <summary>恢复默认值（设置面板 Reset 按钮）。</summary>
	public static void ResetToDefaults()
	{
		_actionCountEnabled = false;
		_actionCountScope = 1;
		_cznMonstersEnabled = false;
		_bossPosRandom = true;
		_bossPosSpecified = false;
		_bossChapter = 1;
		_bossSelect = 0;
		_bossHpCustom = false;
		_bossHpValueText = DefaultHp.ToString(CultureInfo.InvariantCulture);
		_lastValidText = _bossHpValueText;
		Save();
	}

	private static void Load()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		if (!Godot.FileAccess.FileExists(FilePath))
		{
			return;
		}
		if (Config.Load(FilePath) != Error.Ok)
		{
			return;
		}
		_actionCountEnabled = (bool)Config.GetValue("action_count", "enabled", false);
		_actionCountScope = Math.Clamp((int)Config.GetValue("action_count", "scope", 1), 1, 3);
		_cznMonstersEnabled = (bool)Config.GetValue("czn_monsters", "enabled", false);
		_bossPosRandom = (bool)Config.GetValue("czn_monsters", "boss_pos_random", true);
		_bossPosSpecified = (bool)Config.GetValue("czn_monsters", "boss_pos_specified", false);
		if (_bossPosRandom && _bossPosSpecified)
		{
			// 手改配置等异常情况：指定优先，保证二者互斥
			_bossPosRandom = false;
		}
		_bossChapter = Math.Clamp((int)Config.GetValue("czn_monsters", "boss_chapter", 1), 1, 3);
		_bossSelect = Math.Max(0, (int)Config.GetValue("czn_monsters", "boss_select", 0));
		_bossHpCustom = (bool)Config.GetValue("czn_monsters", "boss_hp_custom", false);
		_bossHpValueText = (string)Config.GetValue("czn_monsters", "boss_hp_value", DefaultHp.ToString(CultureInfo.InvariantCulture));
		if (string.IsNullOrWhiteSpace(_bossHpValueText))
		{
			_bossHpValueText = DefaultHp.ToString(CultureInfo.InvariantCulture);
		}
		_lastValidText = _bossHpValueText;
	}

	private static void Save()
	{
		Config.SetValue("action_count", "enabled", _actionCountEnabled);
		Config.SetValue("action_count", "scope", _actionCountScope);
		Config.SetValue("czn_monsters", "enabled", _cznMonstersEnabled);
		Config.SetValue("czn_monsters", "boss_pos_random", _bossPosRandom);
		Config.SetValue("czn_monsters", "boss_pos_specified", _bossPosSpecified);
		Config.SetValue("czn_monsters", "boss_chapter", _bossChapter);
		Config.SetValue("czn_monsters", "boss_select", _bossSelect);
		Config.SetValue("czn_monsters", "boss_hp_custom", _bossHpCustom);
		Config.SetValue("czn_monsters", "boss_hp_value", _bossHpValueText);
		if (DirAccess.MakeDirRecursiveAbsolute(DirPath) == Error.Ok)
		{
			Config.Save(FilePath);
		}
	}
}
