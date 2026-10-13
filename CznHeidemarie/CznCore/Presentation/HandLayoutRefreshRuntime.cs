using System;
using System.Reflection;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class HandLayoutRefreshRuntime
{
	private static readonly MethodInfo? RefreshLayoutMethod = typeof(NPlayerHand).GetMethod("RefreshLayout", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly string _pendingMeta;

	private readonly Action<string, int> _warn;

	public HandLayoutRefreshRuntime(string metaKeyPrefix, Action<string, int>? warn = null)
	{
		_pendingMeta = (string.IsNullOrWhiteSpace(metaKeyPrefix) ? "character_skin" : metaKeyPrefix.Trim()) + "_hand_layout_refresh_pending";
		_warn = warn ?? new Action<string, int>(NoopWarn);
	}

	public void Request(NPlayerHand? hand, string reason)
	{
		if (hand != null && GodotObject.IsInstanceValid(hand) && !hand.HasMeta(_pendingMeta))
		{
			hand.SetMeta(_pendingMeta, Variant.From<bool>(true));
			GodotScheduler.Deferred(() =>
			{
				Refresh(hand, reason);
			});
		}
	}

	private void Refresh(NPlayerHand hand, string reason)
	{
		try
		{
			if (GodotObject.IsInstanceValid(hand))
			{
				RefreshLayoutMethod?.Invoke(hand, null);
				hand.ForceRefreshCardIndices();
			}
		}
		catch (Exception ex)
		{
			_warn("Hand layout refresh failed: " + reason + " " + SkinLog.FormatException(ex), 2);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(hand) && hand.HasMeta(_pendingMeta))
			{
				hand.RemoveMeta(_pendingMeta);
			}
		}
	}

	private static void NoopWarn(string message, int priority)
	{
	}
}
