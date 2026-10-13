using System;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace CznCore.Presentation;

internal sealed class CombatEntryBodyVisibilityRuntime
{
	private readonly CharacterSkinProfile _profile;

	private readonly CombatCreatureSpineResolver _spines;

	private readonly Func<Player?, bool> _shouldHidePlayer;

	private readonly Func<bool>? _isEnabled;

	private readonly Action<string>? _warn;

	private readonly Action<string>? _debug;

	private readonly float _fallbackSeconds;

	private readonly string _hiddenMeta;

	public CombatEntryBodyVisibilityRuntime(CharacterSkinProfile profile, CombatCreatureSpineResolver spines, Func<Player?, bool> shouldHidePlayer, Func<bool>? isEnabled = null, Action<string>? warn = null, Action<string>? debug = null, float fallbackSeconds = 2f)
	{
		_profile = profile;
		_spines = spines;
		_shouldHidePlayer = shouldHidePlayer;
		_isEnabled = isEnabled;
		_warn = warn;
		_debug = debug;
		_fallbackSeconds = fallbackSeconds;
		_hiddenMeta = profile.RuntimeMetaKey("combat_entry_body_hidden_until_start");
	}

	public void HideUntilEntryStart(NCreature? creatureNode)
	{
		try
		{
			if (!ShouldHideUntilEntryStart(creatureNode))
			{
				return;
			}
			GodotObject godotObject = creatureNode;
			if (godotObject.HasMeta(_hiddenMeta))
			{
				return;
			}
			CanvasItem canvasItem = _spines.Find(creatureNode);
			if (canvasItem == null)
			{
				return;
			}
			canvasItem.Visible = false;
			godotObject.SetMeta(_hiddenMeta, Variant.From<bool>(true));
			_debug?.Invoke("combat entry body hidden until entry_start");
			if (_fallbackSeconds > 0f)
			{
				GodotScheduler.Delay(_fallbackSeconds, () =>
				{
					Reveal(creatureNode, "fallback");
				});
			}
		}
		catch (Exception ex)
		{
			_warn?.Invoke("combat entry pre-hide failed: " + SkinLog.FormatException(ex));
		}
	}

	public void Reveal(NCreature? creatureNode, string reason)
	{
		try
		{
			if (creatureNode == null || !GodotObject.IsInstanceValid(creatureNode))
			{
				return;
			}
			if (creatureNode.HasMeta(_hiddenMeta))
			{
				CanvasItem canvasItem = _spines.Find(creatureNode);
				if (canvasItem != null)
				{
					canvasItem.Visible = true;
				}
				creatureNode.RemoveMeta(_hiddenMeta);
				_debug?.Invoke("combat entry body revealed reason=" + reason);
			}
		}
		catch (Exception ex)
		{
			_warn?.Invoke("combat entry reveal failed: " + SkinLog.FormatException(ex));
		}
	}

	private bool ShouldHideUntilEntryStart(NCreature? creatureNode)
	{
		NCombatRoom instance = NCombatRoom.Instance;
		if ((_isEnabled != null && !_isEnabled()) || creatureNode == null || !GodotObject.IsInstanceValid(creatureNode) || instance == null || instance.Mode != CombatRoomMode.ActiveCombat || _profile.CombatBody.EntrySequence.Length == 0 || !CombatBodyEntryPlaybackRuntime.HasRunIn(_profile.CombatBody))
		{
			return false;
		}
		Player player = creatureNode.Entity?.Player;
		if (_profile.AppliesTo(player))
		{
			return _shouldHidePlayer(player);
		}
		return false;
	}
}
