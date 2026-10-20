using System;

namespace CznCore.Settings;

/// <summary>
/// 自由文本输入控件（设计文档 §4.5 方案 B 新增，"只删不改"的唯一例外）。
/// </summary>
internal sealed record SettingsUiExtensionText(string Id, int Order, Func<string> Label, Func<string> Description, Func<string> Read, Action<string> Write, Func<bool> Enabled) : SettingsUiExtensionControl(Id, Order, Label, Description, Enabled);
