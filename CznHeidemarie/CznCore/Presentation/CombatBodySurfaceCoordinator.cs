using System;
using System.Collections.Generic;
using System.Threading;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class CombatBodySurfaceCoordinator
{
	private readonly CharacterSkinProfile _profile;

	private readonly CombatCreatureSpineResolver _spines;

	private readonly CombatBodySurfaceRuntime _surface;

	private readonly CombatSpineAnimationAvailabilityCache _animations = new CombatSpineAnimationAvailabilityCache();

	private readonly Action<NCreature>? _afterSurfaceApplied;

	private readonly Action<NCreature>? _afterSurfaceReady;

	private readonly Func<IEnumerable<string>>? _enumerateAdditionalRequiredAnimations;

	private int _appliedLogged;

	private int _skippedLogged;

	private int _alreadyAppliedLogged;

	private int _positionPassLogged;

	private int _bindingValidationLogged;

	public CombatBodySurfaceCoordinator(CharacterSkinProfile profile, CombatCreatureSpineResolver spines, Action<NCreature>? afterSurfaceApplied = null, Action<NCreature>? afterSurfaceReady = null, Func<IEnumerable<string>>? enumerateAdditionalRequiredAnimations = null, Action<string>? warn = null, Action<string>? debug = null)
	{
		_profile = profile;
		_spines = spines;
		_afterSurfaceApplied = afterSurfaceApplied;
		_afterSurfaceReady = afterSurfaceReady;
		_enumerateAdditionalRequiredAnimations = enumerateAdditionalRequiredAnimations;
		_surface = new CombatBodySurfaceRuntime(profile, spines, warn, debug);
	}

	public CombatBodySurfaceState GetState(NCreature? creature)
	{
		return _surface.GetState(creature);
	}

	public bool Ensure(NCreature? creatureNode, string reason)
	{
		if (creatureNode == null || !GodotObject.IsInstanceValid(creatureNode) || !_profile.AppliesTo(creatureNode.Entity?.Player))
		{
			return false;
		}
		CombatBodySurfaceState state = _surface.GetState(creatureNode);
		if (!_surface.Apply(creatureNode))
		{
			if (state.Applied)
			{
				ValidateRequiredBindings(creatureNode, reason);
				_afterSurfaceReady?.Invoke(creatureNode);
				LogAlreadyApplied(reason, state);
				return true;
			}
			LogSkipped(reason, state);
			return false;
		}
		_animations.Clear();
		_afterSurfaceApplied?.Invoke(creatureNode);
		ValidateRequiredBindings(creatureNode, reason);
		_afterSurfaceReady?.Invoke(creatureNode);
		LogApplied(reason, creatureNode);
		return true;
	}

	public int EnsurePositionPass(IReadOnlyList<NCreature>? creatureNodes, string reason = "combat_position")
	{
		if (creatureNodes == null || creatureNodes.Count == 0)
		{
			return 0;
		}
		int targetPlayers = 0;
		foreach (NCreature creatureNode in creatureNodes)
		{
			if (creatureNode != null && creatureNode.Entity?.IsPlayer == true && _profile.AppliesTo(creatureNode.Entity.Player))
			{
				Ensure(creatureNode, reason);
				targetPlayers++;
			}
		}
		if (targetPlayers > 0 && Interlocked.Exchange(ref _positionPassLogged, 1) == 0)
		{
			SkinLog.Debug(() => "Applied " + _profile.ProfileId + " combat body surface position pass: players=" + targetPlayers);
		}
		return targetPlayers;
	}

	public bool HasAnimation(NCreature creatureNode, string animationName, string warningPrefix)
	{
		Ensure(creatureNode, "body_animation_check");
		return _animations.HasAnimation(_spines.Find(creatureNode), animationName, (string message) =>
		{
			SkinLog.Warn(warningPrefix + " " + message);
		});
	}

	private void ValidateRequiredBindings(NCreature creatureNode, string reason)
	{
		if (Interlocked.Exchange(ref _bindingValidationLogged, 1) != 0)
		{
			return;
		}
		IReadOnlyList<string> requiredBindingAnimations = GetRequiredBindingAnimations();
		if (requiredBindingAnimations.Count == 0)
		{
			return;
		}
		Node2D node2D = _spines.Find(creatureNode);
		if (node2D == null || !GodotObject.IsInstanceValid(node2D))
		{
			SkinLog.Warn(_profile.ProfileId + " combat body binding validation skipped: spine unavailable reason=" + reason);
			return;
		}
		List<string> list = new List<string>();
		foreach (string item in requiredBindingAnimations)
		{
			if (!_animations.HasAnimation(node2D, item))
			{
				list.Add(item);
			}
		}
		if (list.Count > 0)
		{
			SkinLog.Warn(_profile.ProfileId + " combat body binding validation failed: missing=" + string.Join(", ", list) + " checked=" + requiredBindingAnimations.Count + " reason=" + reason + " skeleton=" + _profile.BodySkeletonDataPath);
		}
		else
		{
			SkinLog.Info(_profile.ProfileId + " combat body binding validation passed: checked=" + requiredBindingAnimations.Count + " reason=" + reason);
		}
	}

	private IReadOnlyList<string> GetRequiredBindingAnimations()
	{
		List<string> list = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		AddAnimations(list, seen, _profile.CombatBody.GetRequiredBindingAnimations());
		if (_profile.CombatPresentation is ICombatBodyBindingAnimationSource combatBodyBindingAnimationSource)
		{
			AddAnimations(list, seen, combatBodyBindingAnimationSource.EnumerateRequiredBodyBindingAnimations());
		}
		if (_enumerateAdditionalRequiredAnimations != null)
		{
			try
			{
				AddAnimations(list, seen, _enumerateAdditionalRequiredAnimations());
			}
			catch (Exception ex)
			{
				SkinLog.Warn(_profile.ProfileId + " combat body binding validation source failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
		return list;
	}

	private static void AddAnimations(List<string> animations, HashSet<string> seen, IEnumerable<string>? values)
	{
		if (values == null)
		{
			return;
		}
		foreach (string value in values)
		{
			AddAnimation(animations, seen, value);
		}
	}

	private static void AddAnimation(List<string> animations, HashSet<string> seen, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
		{
			animations.Add(value);
		}
	}

	private void LogApplied(string reason, NCreature creatureNode)
	{
		if (Interlocked.Exchange(ref _appliedLogged, 1) == 0)
		{
			CombatBodySurfaceState state = _surface.GetState(creatureNode);
			SkinLog.Info("Applied " + _profile.ProfileId + " combat body surface: " + _profile.BodySkeletonDataPath + " reason=" + reason + " spineScale=" + FormatScale(state));
		}
	}

	private void LogSkipped(string reason, CombatBodySurfaceState state)
	{
		if (Interlocked.Exchange(ref _skippedLogged, 1) == 0)
		{
			SkinLog.Warn(_profile.ProfileId + " combat body surface apply skipped: reason=" + reason + " state=" + state.Reason + " featureEnabled=" + state.FeatureEnabled + " creatureValid=" + state.CreatureValid + " spineFound=" + state.SpineFound + " applied=" + state.Applied + " spineClass=" + (state.SpineClass ?? "<none>") + " spineScale=" + FormatScale(state) + " skeleton=" + _profile.BodySkeletonDataPath);
		}
	}

	private void LogAlreadyApplied(string reason, CombatBodySurfaceState state)
	{
		if (Interlocked.Exchange(ref _alreadyAppliedLogged, 1) == 0)
		{
			SkinLog.Info(_profile.ProfileId + " combat body surface already applied: reason=" + reason + " spineClass=" + (state.SpineClass ?? "<none>") + " spineScale=" + FormatScale(state));
		}
	}

	private static string FormatScale(CombatBodySurfaceState state)
	{
		if (!state.SpineScale.HasValue)
		{
			return "<none>";
		}
		return state.SpineScale.Value.ToString();
	}
}
