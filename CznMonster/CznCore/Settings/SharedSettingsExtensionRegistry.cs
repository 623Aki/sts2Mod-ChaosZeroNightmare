using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Godot;

namespace CznCore.Settings;

internal static class SharedSettingsExtensionRegistry
{
	private sealed class CategoryBuilder(string id, int order, Func<string> label)
	{
		public string Id { get; } = id;

		public int Order { get; } = order;

		public Func<string> Label { get; } = label;

		public List<SettingsUiExtensionGroup> Groups { get; } = new List<SettingsUiExtensionGroup>();
	}

	public const string RegistryDataKey = "CznModConfig.SharedSettingsExtensions.v1";

	private const int CurrentApiVersion = 1;

	public static IReadOnlyList<SharedSettingsExtensionCategory> GetCategories()
	{
		if (!(AppDomain.CurrentDomain.GetData("CznModConfig.SharedSettingsExtensions.v1") is ConcurrentDictionary<string, object> collection))
		{
			return Array.Empty<SharedSettingsExtensionCategory>();
		}
		List<KeyValuePair<string, object>> list = new List<KeyValuePair<string, object>>(collection);
		list.Sort((KeyValuePair<string, object> left, KeyValuePair<string, object> right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));
		Dictionary<string, CategoryBuilder> dictionary = new Dictionary<string, CategoryBuilder>(StringComparer.Ordinal);
		foreach (var (providerId, rawDescriptor) in list)
		{
			if (TryParseGroup(providerId, rawDescriptor, out string categoryId, out int categoryOrder, out Func<string> categoryLabel, out SettingsUiExtensionGroup group))
			{
				if (!dictionary.TryGetValue(categoryId, out var value))
				{
					value = new CategoryBuilder(categoryId, categoryOrder, categoryLabel);
					dictionary.Add(categoryId, value);
				}
				value.Groups.Add(group);
			}
		}
		List<SharedSettingsExtensionCategory> list2 = new List<SharedSettingsExtensionCategory>(dictionary.Count);
		foreach (CategoryBuilder value2 in dictionary.Values)
		{
			value2.Groups.Sort((SettingsUiExtensionGroup left, SettingsUiExtensionGroup right) =>
			{
				int num = left.Order.CompareTo(right.Order);
				return (num == 0) ? string.Compare(left.ProviderId, right.ProviderId, StringComparison.Ordinal) : num;
			});
			list2.Add(new SharedSettingsExtensionCategory(value2.Id, value2.Order, value2.Label, value2.Groups.ToArray()));
		}
		list2.Sort((SharedSettingsExtensionCategory left, SharedSettingsExtensionCategory right) =>
		{
			int num = left.Order.CompareTo(right.Order);
			return (num == 0) ? string.Compare(left.Id, right.Id, StringComparison.Ordinal) : num;
		});
		return list2;
	}

	private static bool TryParseGroup(string providerId, object rawDescriptor, out string categoryId, out int categoryOrder, out Func<string> categoryLabel, out SettingsUiExtensionGroup group)
	{
		categoryId = string.Empty;
		categoryOrder = 0;
		categoryLabel = () => string.Empty;
		group = null;
		if (!(rawDescriptor is IReadOnlyDictionary<string, object> readOnlyDictionary) || !TryGet<int>(readOnlyDictionary, "api_version", out var value) || value != 1 || !TryGetText(readOnlyDictionary, "category_id", out categoryId) || IsReservedCategory(categoryId) || !TryGetTextReader(readOnlyDictionary, "category_label", categoryId, out categoryLabel) || !TryGetTextReader(readOnlyDictionary, "group_label", providerId, out Func<string> reader) || !readOnlyDictionary.TryGetValue("controls", out var value2) || !(value2 is object[] array))
		{
			Warn("Ignored invalid CZN Mod Config extension registration: " + providerId + ".");
			return false;
		}
		TryGet<int>(readOnlyDictionary, "category_order", out categoryOrder);
		TryGet<int>(readOnlyDictionary, "group_order", out var value3);
		TryGet<Action>(readOnlyDictionary, "reset", out var value4);
		List<SettingsUiExtensionControl> list = new List<SettingsUiExtensionControl>(array.Length);
		object[] array2 = array;
		foreach (object rawControl in array2)
		{
			if (TryParseControl(providerId, rawControl, out SettingsUiExtensionControl control))
			{
				list.Add(control);
			}
		}
		list.Sort((SettingsUiExtensionControl left, SettingsUiExtensionControl right) =>
		{
			int num2 = left.Order.CompareTo(right.Order);
			return (num2 == 0) ? string.Compare(left.Id, right.Id, StringComparison.Ordinal) : num2;
		});
		group = new SettingsUiExtensionGroup(providerId, categoryId, value3, reader, (value4 == null) ? null : SafeAction(providerId, "reset", value4), list.ToArray());
		return true;
	}

	private static bool TryParseControl(string providerId, object rawControl, out SettingsUiExtensionControl control)
	{
		control = null;
		if (!(rawControl is IReadOnlyDictionary<string, object> descriptor) || !TryGetText(descriptor, "type", out string value))
		{
			Warn("Ignored invalid CZN Mod Config extension control: " + providerId + ".");
			return false;
		}
		return value switch
		{
			"toggle" => TryParseToggle(providerId, descriptor, out control),
			"slider" => TryParseSlider(providerId, descriptor, out control),
			"choice" => TryParseChoice(providerId, descriptor, out control),
			"text" => TryParseText(providerId, descriptor, out control),
			_ => false,
		};
	}

	// 设计文档 §4.5 方案 B：text 控件。校验逻辑照抄 toggle。
	private static bool TryParseText(string providerId, IReadOnlyDictionary<string, object> descriptor, out SettingsUiExtensionControl text)
	{
		text = null;
		if (!TryGetText(descriptor, "id", out string value) || !TryGetTextReader(descriptor, "label", value, out Func<string> reader) || !TryGet<Func<string>>(descriptor, "get", out var value2) || !TryGet<Action<string>>(descriptor, "set", out var value3))
		{
			Warn("Ignored invalid CZN Mod Config extension control: " + providerId + ".");
			return false;
		}
		TryGet<int>(descriptor, "order", out var value4);
		TryGetTextReader(descriptor, "description", string.Empty, out Func<string> reader2);
		if (!TryGet<Func<bool>>(descriptor, "enabled", out var value5))
		{
			value5 = () => true;
		}
		text = new SettingsUiExtensionText(value, value4, reader, reader2, SafeStringReader(providerId, value, value2, string.Empty), SafeStringWriter(providerId, value, value3), SafeBoolReader(providerId, value + ".enabled", value5, fallback: false))
		{
			ParentId = ReadParentId(descriptor)
		};
		return true;
	}

	private static bool TryParseToggle(string providerId, IReadOnlyDictionary<string, object> descriptor, out SettingsUiExtensionControl toggle)
	{
		toggle = null;
		if (!TryGetText(descriptor, "id", out string value) || !TryGetTextReader(descriptor, "label", value, out Func<string> reader) || !TryGet<Func<bool>>(descriptor, "get", out var value2) || !TryGet<Action<bool>>(descriptor, "set", out var value3))
		{
			Warn("Ignored invalid CZN Mod Config extension control: " + providerId + ".");
			return false;
		}
		TryGet<int>(descriptor, "order", out var value4);
		TryGetTextReader(descriptor, "description", string.Empty, out Func<string> reader2);
		if (!TryGet<Func<bool>>(descriptor, "enabled", out var value5))
		{
			value5 = () => true;
		}
		toggle = new SettingsUiExtensionToggle(value, value4, reader, reader2, SafeBoolReader(providerId, value, value2, fallback: false), SafeBoolWriter(providerId, value, value3), SafeBoolReader(providerId, value + ".enabled", value5, fallback: false))
		{
			ParentId = ReadParentId(descriptor)
		};
		return true;
	}

	private static bool TryParseSlider(string providerId, IReadOnlyDictionary<string, object> descriptor, out SettingsUiExtensionControl slider)
	{
		slider = null;
		if (!TryGetText(descriptor, "id", out string value) || !TryGetTextReader(descriptor, "label", value, out Func<string> reader) || !TryGet<Func<double>>(descriptor, "get", out var value2) || !TryGet<Action<double>>(descriptor, "set", out var value3) || !TryGetNumber(descriptor, "min", out var value4) || !TryGetNumber(descriptor, "max", out var value5) || !TryGetNumber(descriptor, "step", out var value6) || value5 < value4 || value6 <= 0.0)
		{
			Warn("Ignored invalid CZN Mod Config extension slider: " + providerId + ".");
			return false;
		}
		TryGet<int>(descriptor, "order", out var value7);
		TryGetTextReader(descriptor, "description", string.Empty, out Func<string> reader2);
		TryGetTextReader(descriptor, "suffix", string.Empty, out Func<string> reader3);
		TryGetNumber(descriptor, "display_offset", out var value8);
		if (!TryGet<Func<bool>>(descriptor, "enabled", out var value9))
		{
			value9 = () => true;
		}
		slider = new SettingsUiExtensionSlider(value, value7, reader, reader2, SafeDoubleReader(providerId, value, value2, value4), SafeDoubleWriter(providerId, value, value3), SafeBoolReader(providerId, value + ".enabled", value9, fallback: false), value4, value5, value6, reader3, value8)
		{
			ParentId = ReadParentId(descriptor)
		};
		return true;
	}

	private static bool TryParseChoice(string providerId, IReadOnlyDictionary<string, object> descriptor, out SettingsUiExtensionControl choice)
	{
		choice = null;
		if (!TryGetText(descriptor, "id", out string value) || !TryGetTextReader(descriptor, "label", value, out Func<string> reader) || !TryGet<Func<int>>(descriptor, "get", out var value2) || !TryGet<Action<int>>(descriptor, "set", out var value3) || !descriptor.TryGetValue("options", out object value4) || !(value4 is object[] array))
		{
			Warn("Ignored invalid CZN Mod Config extension choice: " + providerId + ".");
			return false;
		}
		List<SettingsUiExtensionChoiceOption> list = new List<SettingsUiExtensionChoiceOption>(array.Length);
		object[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			if (array2[i] is IReadOnlyDictionary<string, object> descriptor2 && TryGet<int>(descriptor2, "id", out var value5) && TryGetTextReader(descriptor2, "label", value5.ToString(), out Func<string> reader2))
			{
				list.Add(new SettingsUiExtensionChoiceOption(value5, reader2));
			}
		}
		if (list.Count == 0)
		{
			Warn($"Ignored empty CZN Mod Config extension choice: {providerId}/{value}.");
			return false;
		}
		TryGet<int>(descriptor, "order", out var value6);
		TryGetTextReader(descriptor, "description", string.Empty, out Func<string> reader3);
		if (!TryGet<Func<bool>>(descriptor, "enabled", out var value7))
		{
			value7 = () => true;
		}
		choice = new SettingsUiExtensionChoice(value, value6, reader, reader3, SafeIntReader(providerId, value, value2, list[0].Id), SafeIntWriter(providerId, value, value3), SafeBoolReader(providerId, value + ".enabled", value7, fallback: false), list.ToArray())
		{
			ParentId = ReadParentId(descriptor)
		};
		return true;
	}

	private static string? ReadParentId(IReadOnlyDictionary<string, object> descriptor)
	{
		if (!TryGetText(descriptor, "parent_id", out string value))
		{
			return null;
		}
		return value;
	}

	private static bool TryGet<T>(IReadOnlyDictionary<string, object> descriptor, string key, out T value)
	{
		if (descriptor.TryGetValue(key, out object value2) && value2 is T val)
		{
			value = val;
			return true;
		}
		value = default;
		return false;
	}

	private static bool TryGetNumber(IReadOnlyDictionary<string, object> descriptor, string key, out double value)
	{
		if (!descriptor.TryGetValue(key, out object value2))
		{
			value = 0.0;
			return false;
		}
		if (!(value2 is double num))
		{
			if (!(value2 is float num2))
			{
				if (!(value2 is int num3))
				{
					if (value2 is long num4)
					{
						value = num4;
						return true;
					}
					value = 0.0;
					return false;
				}
				value = num3;
				return true;
			}
			value = num2;
			return true;
		}
		value = num;
		return true;
	}

	private static bool TryGetText(IReadOnlyDictionary<string, object> descriptor, string key, out string value)
	{
		if (TryGet<string>(descriptor, key, out value))
		{
			value = value.Trim();
			return !string.IsNullOrWhiteSpace(value);
		}
		return false;
	}

	private static bool TryGetTextReader(IReadOnlyDictionary<string, object> descriptor, string key, string fallback, out Func<string> reader)
	{
		if (TryGet<Func<string>>(descriptor, key, out var value))
		{
			reader = SafeTextReader(value, fallback);
			return true;
		}
		if (TryGet<string>(descriptor, key, out var text))
		{
			reader = () => (!string.IsNullOrWhiteSpace(text)) ? text : fallback;
			return true;
		}
		reader = () => fallback;
		return false;
	}

	private static bool IsReservedCategory(string categoryId)
	{
		if (!string.Equals(categoryId, "general", StringComparison.Ordinal))
		{
			return string.Equals(categoryId, "multiplayer", StringComparison.Ordinal);
		}
		return true;
	}

	private static Func<string> SafeTextReader(Func<string> reader, string fallback)
	{
		return () =>
		{
			try
			{
				string text = reader();
				return string.IsNullOrWhiteSpace(text) ? fallback : text;
			}
			catch
			{
				return fallback;
			}
		};
	}

	private static Func<bool> SafeBoolReader(string providerId, string operation, Func<bool> reader, bool fallback)
	{
		return () =>
		{
			try
			{
				return reader();
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
				return fallback;
			}
		};
	}

	private static Action<bool> SafeBoolWriter(string providerId, string operation, Action<bool> writer)
	{
		return (bool value) =>
		{
			try
			{
				writer(value);
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
			}
		};
	}

	// 设计文档 §4.5：text 控件的安全读写包装（照抄 bool 版）。
	private static Func<string> SafeStringReader(string providerId, string operation, Func<string> reader, string fallback)
	{
		return () =>
		{
			try
			{
				string text = reader();
				return text ?? fallback;
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
				return fallback;
			}
		};
	}

	private static Action<string> SafeStringWriter(string providerId, string operation, Action<string> writer)
	{
		return (string value) =>
		{
			try
			{
				writer(value);
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
			}
		};
	}

	private static Func<double> SafeDoubleReader(string providerId, string operation, Func<double> reader, double fallback)
	{
		return () =>
		{
			try
			{
				return reader();
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
				return fallback;
			}
		};
	}

	private static Action<double> SafeDoubleWriter(string providerId, string operation, Action<double> writer)
	{
		return (double value) =>
		{
			try
			{
				writer(value);
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
			}
		};
	}

	private static Func<int> SafeIntReader(string providerId, string operation, Func<int> reader, int fallback)
	{
		return () =>
		{
			try
			{
				return reader();
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
				return fallback;
			}
		};
	}

	private static Action<int> SafeIntWriter(string providerId, string operation, Action<int> writer)
	{
		return (int value) =>
		{
			try
			{
				writer(value);
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
			}
		};
	}

	private static Action SafeAction(string providerId, string operation, Action action)
	{
		return () =>
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				Warn($"CZN Mod Config extension {providerId}/{operation} failed: {ex.Message}");
			}
		};
	}

	private static void Warn(string message)
	{
		try
		{
			GD.PushWarning("[CznCore] " + message);
		}
		catch
		{
		}
	}
}
