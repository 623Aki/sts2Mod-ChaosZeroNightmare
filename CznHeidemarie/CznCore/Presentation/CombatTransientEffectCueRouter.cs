using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class CombatTransientEffectCueRouter<TActor, TTarget> where TActor : class where TTarget : class
{
	private readonly ICombatPresentationProfile _profile;

	private readonly Func<bool> _isEnabled;

	private readonly Func<TActor?, bool> _isEnabledFor;

	private readonly Func<TActor, Node2D?> _findOwnerAnchor;

	private readonly Func<TTarget?, Node2D?> _findTargetAnchor;

	private readonly Func<TTarget?, Node2D?> _findTargetSurfaceAnchor;

	private readonly Func<Node2D?> _findScreenAnchor;

	private readonly Func<Node2D?> _findScreenBackAnchor;

	private readonly Action<TActor, Node2D, CombatPresentationEffectCue, int> _queue;

	private readonly Action<string, int> _warn;

	public CombatTransientEffectCueRouter(ICombatPresentationProfile profile, ICombatTransientEffectCueRouterHost<TActor, TTarget> host, Action<string, int> warn)
	{
		_profile = profile ?? throw new ArgumentNullException("profile");
		ArgumentNullException.ThrowIfNull(host, "host");
		_isEnabled = host.IsEnabled;
		_isEnabledFor = host.IsEnabledFor;
		_findOwnerAnchor = host.FindOwnerAnchor;
		_findTargetAnchor = host.FindTargetAnchor;
		_findTargetSurfaceAnchor = host.FindTargetSurfaceAnchor;
		_findScreenAnchor = host.FindScreenAnchor;
		_findScreenBackAnchor = host.FindScreenBackAnchor;
		_queue = host.Queue;
		_warn = warn ?? throw new ArgumentNullException("warn");
	}

	public void Play(TActor actor, TTarget? target, string animationName, int token)
	{
		if (target == null)
		{
			PlayForTargets(actor, null, animationName, token);
		}
		else
		{
			PlayForSingleTarget(actor, target, animationName, token);
		}
	}

	public void PlayForSingleTarget(TActor actor, TTarget target, string animationName, int token)
	{
		PlayForSingleTargetScoped(actor, target, animationName, CombatTransientEffectCueScope.All, token);
	}

	public void PlayOwnerCues(TActor actor, string animationName, int token)
	{
		PlayForSingleTargetScoped(actor, null, animationName, CombatTransientEffectCueScope.OwnerOnly, token);
	}

	public void PlayTargetCues(TActor actor, TTarget target, string animationName, int token)
	{
		PlayForSingleTargetScoped(actor, target, animationName, CombatTransientEffectCueScope.TargetOnly, token);
	}

	public void PlayTargetCues(TActor actor, TTarget target, string animationName, int token, Func<CombatPresentationEffectCue, bool> cueFilter)
	{
		PlayForSingleTargetScoped(actor, target, animationName, CombatTransientEffectCueScope.TargetOnly, token, cueFilter);
	}

	public void PlayCues(TActor actor, TTarget? target, string cueLabel, IReadOnlyList<CombatPresentationEffectCue> cues, int token)
	{
		if (_isEnabled() && _isEnabledFor(actor) && cues.Count != 0)
		{
			PlayForSingleTargetScoped(actor, target, cueLabel, cues, CombatTransientEffectCueScope.All, token);
		}
	}

	public void PlayTargetCues(TActor actor, TTarget target, string cueLabel, IReadOnlyList<CombatPresentationEffectCue> cues, int token)
	{
		if (_isEnabled() && _isEnabledFor(actor) && cues.Count != 0)
		{
			PlayForSingleTargetScoped(actor, target, cueLabel, cues, CombatTransientEffectCueScope.TargetOnly, token);
		}
	}

	private void PlayForSingleTargetScoped(TActor actor, TTarget? target, string animationName, CombatTransientEffectCueScope scope, int token, Func<CombatPresentationEffectCue, bool>? cueFilter = null)
	{
		if (_isEnabledFor(actor) && TryGetCues(animationName, out IReadOnlyList<CombatPresentationEffectCue> cues))
		{
			PlayForSingleTargetScoped(actor, target, animationName, cues, scope, token, cueFilter);
		}
	}

	private void PlayForSingleTargetScoped(TActor actor, TTarget? target, string cueLabel, IReadOnlyList<CombatPresentationEffectCue> cues, CombatTransientEffectCueScope scope, int token, Func<CombatPresentationEffectCue, bool>? cueFilter = null)
	{
		EnsureTargetUiDepth(target);
		Node2D node2D = null;
		Node2D node2D2 = null;
		Node2D node2D3 = null;
		Node2D node2D4 = null;
		Node2D node2D5 = null;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		foreach (CombatPresentationEffectCue cue in cues)
		{
			bool flag5 = IsTargetCueAnchor(cue.Anchor);
			bool flag6 = !flag5;
			if (flag6)
			{
				CombatPresentationEffectAnchor anchor = cue.Anchor;
				bool flag7 = (uint)(anchor - 2) <= 1u;
				flag6 = !flag7;
			}
			bool flag8 = flag6;
			if ((scope == CombatTransientEffectCueScope.OwnerOnly && !flag8) || (scope == CombatTransientEffectCueScope.TargetOnly && !flag5) || (cueFilter != null && !cueFilter(cue)))
			{
				continue;
			}
			if (IsScreenTargetAnchor(cue.Anchor))
			{
				Node2D node2D6;
				if (UsesTargetSurfaceProjection(cue.Anchor))
				{
					if (!flag2)
					{
						node2D3 = _findTargetSurfaceAnchor(target);
						flag2 = true;
					}
					node2D6 = node2D3;
				}
				else
				{
					if (!flag)
					{
						node2D2 = _findTargetAnchor(target);
						flag = true;
					}
					node2D6 = node2D2;
				}
				if (IsScreenTargetBackAnchor(cue.Anchor))
				{
					if (!flag4)
					{
						node2D5 = _findScreenBackAnchor();
						flag4 = true;
					}
					if (node2D6 != null && node2D5 != null)
					{
						_queue(actor, node2D5, CombatPresentationEffectCueProjection.ProjectToScreenTarget(cue, node2D6, node2D5, CombatPresentationEffectAnchor.ScreenBack), token);
						continue;
					}
					if (node2D6 == null)
					{
						WarnMissingTarget(cueLabel, cue);
					}
					if (node2D5 == null)
					{
						WarnMissingScreen(cueLabel, cue);
					}
					continue;
				}
				if (!flag3)
				{
					node2D4 = _findScreenAnchor();
					flag3 = true;
				}
				if (node2D6 != null && node2D4 != null)
				{
					_queue(actor, node2D4, CombatPresentationEffectCueProjection.ProjectToScreenTarget(cue, node2D6, node2D4), token);
					continue;
				}
				if (node2D6 == null)
				{
					WarnMissingTarget(cueLabel, cue);
				}
				if (node2D4 == null)
				{
					WarnMissingScreen(cueLabel, cue);
				}
			}
			else if (cue.Anchor == CombatPresentationEffectAnchor.Target)
			{
				if (!flag2)
				{
					node2D3 = _findTargetSurfaceAnchor(target);
					flag2 = true;
				}
				if (node2D3 != null)
				{
					_queue(actor, node2D3, cue, token);
				}
				else
				{
					WarnMissingTarget(cueLabel, cue);
				}
			}
			else if (cue.Anchor == CombatPresentationEffectAnchor.ScreenBack)
			{
				if (!flag4)
				{
					node2D5 = _findScreenBackAnchor();
					flag4 = true;
				}
				if (node2D5 != null)
				{
					_queue(actor, node2D5, cue, token);
				}
				else
				{
					WarnMissingScreen(cueLabel, cue);
				}
			}
			else if (cue.Anchor == CombatPresentationEffectAnchor.Screen)
			{
				if (!flag3)
				{
					node2D4 = _findScreenAnchor();
					flag3 = true;
				}
				if (node2D4 != null)
				{
					_queue(actor, node2D4, cue, token);
				}
				else
				{
					WarnMissingScreen(cueLabel, cue);
				}
			}
			else
			{
				if (node2D == null)
				{
					node2D = _findOwnerAnchor(actor);
				}
				if (node2D == null)
				{
						_warn("Transient effect owner anchor is unavailable; skipping cue.", 2);
						continue;
				}
				_queue(actor, node2D, cue, token);
			}
		}
	}

	public void PlayForTargets(TActor actor, IReadOnlyList<TTarget>? targets, string animationName, int token)
	{
		if (!_isEnabledFor(actor) || !TryGetCues(animationName, out IReadOnlyList<CombatPresentationEffectCue> cues))
		{
			return;
		}
		EnsureTargetUiDepth(targets);
		Node2D node2D = null;
		Node2D node2D2 = null;
		Node2D node2D3 = null;
		IReadOnlyList<Node2D> readOnlyList = null;
		IReadOnlyList<Node2D> readOnlyList2 = null;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		foreach (CombatPresentationEffectCue item in cues)
		{
			if (IsScreenTargetAnchor(item.Anchor))
			{
				IReadOnlyList<Node2D> readOnlyList3;
				if (UsesTargetSurfaceProjection(item.Anchor))
				{
					if (!flag2)
					{
						readOnlyList2 = ResolveTargetSurfaceAnchors(targets);
						flag2 = true;
					}
					readOnlyList3 = readOnlyList2;
				}
				else
				{
					if (!flag)
					{
						readOnlyList = ResolveTargetAnchors(targets);
						flag = true;
					}
					readOnlyList3 = readOnlyList;
				}
				if (IsScreenTargetBackAnchor(item.Anchor))
				{
					if (!flag4)
					{
						node2D3 = _findScreenBackAnchor();
						flag4 = true;
					}
					if (readOnlyList3 != null && readOnlyList3.Count > 0 && node2D3 != null)
					{
						foreach (Node2D item2 in readOnlyList3)
						{
							_queue(actor, node2D3, CombatPresentationEffectCueProjection.ProjectToScreenTarget(item, item2, node2D3, CombatPresentationEffectAnchor.ScreenBack), token);
						}
						continue;
					}
					if (readOnlyList3 == null || readOnlyList3.Count == 0)
					{
						WarnMissingTarget(animationName, item);
					}
					if (node2D3 == null)
					{
						WarnMissingScreen(animationName, item);
					}
					continue;
				}
				if (!flag3)
				{
					node2D2 = _findScreenAnchor();
					flag3 = true;
				}
				if (readOnlyList3 != null && readOnlyList3.Count > 0 && node2D2 != null)
				{
					foreach (Node2D item3 in readOnlyList3)
					{
						_queue(actor, node2D2, CombatPresentationEffectCueProjection.ProjectToScreenTarget(item, item3, node2D2), token);
					}
					continue;
				}
				if (readOnlyList3 == null || readOnlyList3.Count == 0)
				{
					WarnMissingTarget(animationName, item);
				}
				if (node2D2 == null)
				{
					WarnMissingScreen(animationName, item);
				}
			}
			else if (item.Anchor == CombatPresentationEffectAnchor.Target)
			{
				if (!flag2)
				{
					readOnlyList2 = ResolveTargetSurfaceAnchors(targets);
					flag2 = true;
				}
				if (readOnlyList2 != null && readOnlyList2.Count > 0)
				{
					foreach (Node2D item4 in readOnlyList2)
					{
						_queue(actor, item4, item, token);
					}
				}
				else
				{
					WarnMissingTarget(animationName, item);
				}
			}
			else if (item.Anchor == CombatPresentationEffectAnchor.ScreenBack)
			{
				if (!flag4)
				{
					node2D3 = _findScreenBackAnchor();
					flag4 = true;
				}
				if (node2D3 != null)
				{
					_queue(actor, node2D3, item, token);
				}
				else
				{
					WarnMissingScreen(animationName, item);
				}
			}
			else if (item.Anchor == CombatPresentationEffectAnchor.Screen)
			{
				if (!flag3)
				{
					node2D2 = _findScreenAnchor();
					flag3 = true;
				}
				if (node2D2 != null)
				{
					_queue(actor, node2D2, item, token);
				}
				else
				{
					WarnMissingScreen(animationName, item);
				}
			}
			else
			{
				if (node2D == null)
				{
					node2D = _findOwnerAnchor(actor);
				}
				if (node2D == null)
				{
						_warn("Transient effect owner anchor is unavailable; skipping cue.", 2);
						continue;
				}
				_queue(actor, node2D, item, token);
			}
		}
	}

	private bool TryGetCues(string animationName, out IReadOnlyList<CombatPresentationEffectCue> cues)
	{
		if (!_isEnabled() || !_profile.TryGetTransientEffects(animationName, out cues) || cues.Count == 0)
		{
			cues = Array.Empty<CombatPresentationEffectCue>();
			return false;
		}
		return true;
	}

	private static void EnsureTargetUiDepth(TTarget? target)
	{
		if (target is NCreature creature)
		{
			CombatCreatureUiDepthRuntime.EnsureAbovePresentation(creature);
		}
	}

	private static void EnsureTargetUiDepth(IReadOnlyList<TTarget>? targets)
	{
		if (targets == null)
		{
			return;
		}
		foreach (TTarget target in targets)
		{
			EnsureTargetUiDepth(target);
		}
	}

	private IReadOnlyList<Node2D>? ResolveTargetAnchors(IReadOnlyList<TTarget>? targets)
	{
		if (targets == null || targets.Count == 0)
		{
			return null;
		}
		if (targets.Count == 1)
		{
			Node2D node2D = _findTargetAnchor(targets[0]);
			if (node2D != null)
			{
				return new _003C_003Ez__ReadOnlySingleElementList<Node2D>(node2D);
			}
			return null;
		}
		List<Node2D> list = null;
		foreach (TTarget target in targets)
		{
			Node2D node2D2 = _findTargetAnchor(target);
			if (node2D2 != null)
			{
				if (list == null)
				{
					list = new List<Node2D>();
				}
				list.Add(node2D2);
			}
		}
		return list;
	}

	private static bool IsTargetCueAnchor(CombatPresentationEffectAnchor anchor)
	{
		if (anchor == CombatPresentationEffectAnchor.Target || (uint)(anchor - 4) <= 3u)
		{
			return true;
		}
		return false;
	}

	private static bool IsScreenTargetAnchor(CombatPresentationEffectAnchor anchor)
	{
		if ((uint)(anchor - 4) <= 3u)
		{
			return true;
		}
		return false;
	}

	private static bool IsScreenTargetBackAnchor(CombatPresentationEffectAnchor anchor)
	{
		if (anchor == CombatPresentationEffectAnchor.ScreenTargetBack || anchor == CombatPresentationEffectAnchor.ScreenTargetSurfaceBack)
		{
			return true;
		}
		return false;
	}

	private static bool UsesTargetSurfaceProjection(CombatPresentationEffectAnchor anchor)
	{
		if ((uint)(anchor - 6) <= 1u)
		{
			return true;
		}
		return false;
	}

	private IReadOnlyList<Node2D>? ResolveTargetSurfaceAnchors(IReadOnlyList<TTarget>? targets)
	{
		if (targets == null || targets.Count == 0)
		{
			return null;
		}
		if (targets.Count == 1)
		{
			Node2D node2D = _findTargetSurfaceAnchor(targets[0]);
			if (node2D != null)
			{
				return new _003C_003Ez__ReadOnlySingleElementList<Node2D>(node2D);
			}
			return null;
		}
		List<Node2D> list = null;
		foreach (TTarget target in targets)
		{
			Node2D node2D2 = _findTargetSurfaceAnchor(target);
			if (node2D2 != null)
			{
				if (list == null)
				{
					list = new List<Node2D>();
				}
				list.Add(node2D2);
			}
		}
		return list;
	}

	private void WarnMissingTarget(string animationName, CombatPresentationEffectCue cue)
	{
		_warn("Skipped target-anchored combat effect without valid target animation=" + animationName + " group=" + (cue.GroupName ?? "<null>"), 2);
	}

	private void WarnMissingScreen(string animationName, CombatPresentationEffectCue cue)
	{
		_warn("Skipped screen-anchored combat effect without valid screen anchor animation=" + animationName + " group=" + (cue.GroupName ?? "<null>"), 2);
	}
}
