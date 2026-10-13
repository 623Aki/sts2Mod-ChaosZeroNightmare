using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal sealed class CharacterVoicePlaybackGate
{
	private sealed class CategoryState
	{
		public ulong LastSuccessfulPlayMsec { get; set; }

		public string? LastPath { get; set; }
	}

	private readonly Dictionary<string, CategoryState> _categoryStates = new Dictionary<string, CategoryState>(StringComparer.OrdinalIgnoreCase);

	private ulong _lastSuccessfulPlayMsec;

	public bool TryPlay(string category, IReadOnlyList<string> paths, Func<string, bool> play, float categoryCooldownSeconds = 0f, float globalCooldownSeconds = 0f, float chance = 1f, bool avoidImmediateRepeat = true)
	{
		ArgumentNullException.ThrowIfNull(paths, "paths");
		ArgumentNullException.ThrowIfNull(play, "play");
		string category2 = NormalizeCategory(category);
		CategoryState state = GetState(category2);
		ulong ticksMsec = Time.GetTicksMsec();
		if (IsWithinCooldown(ticksMsec, _lastSuccessfulPlayMsec, globalCooldownSeconds) || IsWithinCooldown(ticksMsec, state.LastSuccessfulPlayMsec, categoryCooldownSeconds) || !PassesChance(chance))
		{
			return false;
		}
		string text = SelectPath(paths, state.LastPath, avoidImmediateRepeat);
		if (text == null || !play(text))
		{
			return false;
		}
		state.LastSuccessfulPlayMsec = ticksMsec;
		state.LastPath = text;
		_lastSuccessfulPlayMsec = ticksMsec;
		return true;
	}

	public bool TryPlayWithResult(string category, IReadOnlyList<string> paths, Func<string, AudioPlaybackResult> play, out AudioPlaybackResult result, float categoryCooldownSeconds = 0f, float globalCooldownSeconds = 0f, float chance = 1f, bool avoidImmediateRepeat = true)
	{
		ArgumentNullException.ThrowIfNull(paths, "paths");
		ArgumentNullException.ThrowIfNull(play, "play");
		result = AudioPlaybackResult.NotPlayed;
		string category2 = NormalizeCategory(category);
		CategoryState state = GetState(category2);
		ulong ticksMsec = Time.GetTicksMsec();
		if (IsWithinCooldown(ticksMsec, _lastSuccessfulPlayMsec, globalCooldownSeconds) || IsWithinCooldown(ticksMsec, state.LastSuccessfulPlayMsec, categoryCooldownSeconds) || !PassesChance(chance))
		{
			return false;
		}
		string text = SelectPath(paths, state.LastPath, avoidImmediateRepeat);
		if (text == null)
		{
			return false;
		}
		result = play(text);
		if (!result.Played)
		{
			result = AudioPlaybackResult.NotPlayed;
			return false;
		}
		state.LastSuccessfulPlayMsec = ticksMsec;
		state.LastPath = text;
		_lastSuccessfulPlayMsec = ticksMsec;
		return true;
	}

	public bool TryPlay(string category, string? path, Func<string, bool> play, float categoryCooldownSeconds = 0f, float globalCooldownSeconds = 0f, float chance = 1f)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		return TryPlay(category, new _003C_003Ez__ReadOnlySingleElementList<string>(path), play, categoryCooldownSeconds, globalCooldownSeconds, chance, avoidImmediateRepeat: false);
	}

	public bool TryPlayWithResult(string category, string? path, Func<string, AudioPlaybackResult> play, out AudioPlaybackResult result, float categoryCooldownSeconds = 0f, float globalCooldownSeconds = 0f, float chance = 1f)
	{
		result = AudioPlaybackResult.NotPlayed;
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		return TryPlayWithResult(category, new _003C_003Ez__ReadOnlySingleElementList<string>(path), play, out result, categoryCooldownSeconds, globalCooldownSeconds, chance, avoidImmediateRepeat: false);
	}

	private CategoryState GetState(string category)
	{
		if (!_categoryStates.TryGetValue(category, out CategoryState value))
		{
			value = new CategoryState();
			_categoryStates.Add(category, value);
		}
		return value;
	}

	private static string NormalizeCategory(string category)
	{
		if (!string.IsNullOrWhiteSpace(category))
		{
			return category;
		}
		return "default";
	}

	private static bool IsWithinCooldown(ulong nowMsec, ulong lastMsec, float cooldownSeconds)
	{
		if (cooldownSeconds <= 0f || lastMsec == 0L || nowMsec < lastMsec)
		{
			return false;
		}
		ulong num = Math.Max(1uL, (ulong)(cooldownSeconds * 1000f));
		return nowMsec - lastMsec < num;
	}

	private static bool PassesChance(float chance)
	{
		if (chance >= 1f)
		{
			return true;
		}
		if (chance <= 0f)
		{
			return false;
		}
		return Random.Shared.NextDouble() < (double)chance;
	}

	private static string? SelectPath(IReadOnlyList<string> paths, string? lastPath, bool avoidImmediateRepeat)
	{
		List<string> list = new List<string>();
		foreach (string path in paths)
		{
			if (!string.IsNullOrWhiteSpace(path))
			{
				list.Add(path);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		if (list.Count == 1 || !avoidImmediateRepeat || string.IsNullOrWhiteSpace(lastPath))
		{
			return list[Random.Shared.Next(list.Count)];
		}
		int num = Random.Shared.Next(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			string text = list[(num + i) % list.Count];
			if (!string.Equals(text, lastPath, StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
		}
		return list[num];
	}
}
