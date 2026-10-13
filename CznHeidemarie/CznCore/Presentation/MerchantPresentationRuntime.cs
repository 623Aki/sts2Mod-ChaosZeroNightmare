using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace CznCore.Presentation;

internal sealed class MerchantPresentationRuntime
{
	private readonly Func<Player?, bool> _appliesTo;

	private readonly float _bodySpineScaleMultiplier;

	private readonly MerchantSurfaceProfile _surface;

	private readonly Func<Node, IReadOnlyList<Node>> _findSpineSprites;

	private readonly string _surfaceMeta;

	private readonly string _fakeMerchantSurfaceMeta;

	public MerchantPresentationRuntime(CharacterSkinProfile profile, MerchantSurfaceProfile surface, Func<Node, IReadOnlyList<Node>> findSpineSprites)
		: this(profile.RuntimeMetaKeyPrefix, (Player? player) => profile.AppliesTo(player), profile.BodySpineScaleMultiplier, surface, findSpineSprites)
	{
	}

	public MerchantPresentationRuntime(string runtimeMetaKeyPrefix, Func<Player?, bool> appliesTo, float bodySpineScaleMultiplier, MerchantSurfaceProfile surface, Func<Node, IReadOnlyList<Node>> findSpineSprites)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(runtimeMetaKeyPrefix, "runtimeMetaKeyPrefix");
		_appliesTo = appliesTo ?? throw new ArgumentNullException("appliesTo");
		_bodySpineScaleMultiplier = bodySpineScaleMultiplier;
		_surface = surface ?? throw new ArgumentNullException("surface");
		_findSpineSprites = findSpineSprites ?? throw new ArgumentNullException("findSpineSprites");
		_surfaceMeta = runtimeMetaKeyPrefix + "_merchant_surface_applied";
		_fakeMerchantSurfaceMeta = runtimeMetaKeyPrefix + "_fake_merchant_surface_applied";
	}

	public MerchantPresentationResult Apply(NMerchantCharacter merchantCharacter)
	{
		Player player = ResolveMerchantCharacterPlayer(merchantCharacter);
		if (player != null && !_appliesTo(player))
		{
			return new MerchantPresentationResult(Applied: false, StartedLoop: false, string.Empty, HasPlayerMapping: true);
		}
		foreach (Node item in _findSpineSprites(merchantCharacter))
		{
			if (item is Node2D spineNode && !(item.GetClass() != "SpineSprite"))
			{
				ApplySurface(spineNode);
				PlayLoopWhenReady(merchantCharacter, spineNode);
				return new MerchantPresentationResult(Applied: true, StartedLoop: true, _surface.LoopAnimationName, player != null);
			}
		}
		return new MerchantPresentationResult(Applied: false, StartedLoop: false, string.Empty, player != null);
	}

	public FakeMerchantPresentationResult Apply(NFakeMerchant fakeMerchant)
	{
		List<Player> value = Traverse.Create(fakeMerchant).Field("_players").GetValue<List<Player>>();
		if (value == null || value.Count == 0)
		{
			return new FakeMerchantPresentationResult(0, _surface.LoopAnimationName, Vector2.Zero);
		}
		Control value2 = Traverse.Create(fakeMerchant).Field("_characterContainer").GetValue<Control>();
		if (value2 == null)
		{
			return new FakeMerchantPresentationResult(0, _surface.LoopAnimationName, Vector2.Zero);
		}
		List<NCreatureVisuals> list = new List<NCreatureVisuals>();
		foreach (Node child in value2.GetChildren())
		{
			if (child is NCreatureVisuals item)
			{
				list.Add(item);
			}
		}
		int num = 0;
		int num2 = Math.Min(value.Count, list.Count);
		for (int i = 0; i < num2; i++)
		{
			Player arg = value[num2 - i - 1];
			if (_appliesTo(arg) && ApplyFakeMerchantVisualSurface(list[i]))
			{
				num++;
			}
		}
		return new FakeMerchantPresentationResult(num, _surface.LoopAnimationName, CharacterSkinSurfaceDefaults.ResolveCznCombatBodySpineScale(Vector2.One, _bodySpineScaleMultiplier));
	}

	private void ApplySurface(Node2D spineNode)
	{
		if (!spineNode.HasMeta(_surfaceMeta))
		{
			spineNode.Scale = _surface.SpineScale;
			spineNode.SetMeta(_surfaceMeta, Variant.From<bool>(true));
		}
	}

	private bool ApplyFakeMerchantVisualSurface(NCreatureVisuals visuals)
	{
		Node2D currentBody = visuals.GetCurrentBody();
		if (currentBody.GetClass() != "SpineSprite")
		{
			return false;
		}
		GodotObject godotObject = currentBody;
		if (godotObject.HasMeta(_fakeMerchantSurfaceMeta))
		{
			return false;
		}
		Vector2 scale = currentBody.Scale;
		currentBody.Scale = CharacterSkinSurfaceDefaults.ResolveCznCombatBodySpineScale(scale, _bodySpineScaleMultiplier);
		PlayLoopWhenReady(visuals, currentBody);
		godotObject.SetMeta(_fakeMerchantSurfaceMeta, Variant.From<bool>(true));
		return true;
	}

	private void PlayLoopWhenReady(Node host, Node spineNode)
	{
		MegaSprite sprite = new MegaSprite(spineNode);
		host.RunWhenSpineReady(sprite, (MegaAnimationState _) =>
		{
			TryPlayLoop(sprite, "Merchant loop animation failed: ");
			GodotScheduler.Deferred(() =>
			{
				if (GodotObject.IsInstanceValid(host) && GodotObject.IsInstanceValid(sprite.BoundObject))
				{
					TryPlayLoop(sprite, "Merchant deferred loop animation failed: ");
				}
			});
		});
	}

	private void TryPlayLoop(MegaSprite sprite, string warningPrefix)
	{
		try
		{
			PlayLoop(sprite);
		}
		catch (Exception ex)
		{
			SkinLog.Warn(warningPrefix + _surface.LoopAnimationName + " " + SkinLog.FormatException(ex));
		}
	}

	private void PlayLoop(MegaSprite sprite)
	{
		if (!sprite.HasAnimation(_surface.LoopAnimationName))
		{
			throw new InvalidOperationException("Merchant SpineSprite missing animation: " + _surface.LoopAnimationName);
		}
		SpineAnimationCompat.SetAnimation(sprite.GetAnimationState(), _surface.LoopAnimationName);
	}

	private Player? ResolveMerchantCharacterPlayer(NMerchantCharacter merchantCharacter)
	{
		NMerchantRoom instance = NMerchantRoom.Instance;
		if (instance == null)
		{
			return null;
		}
		List<Player> value = Traverse.Create(instance).Field("_players").GetValue<List<Player>>();
		if (value == null)
		{
			return null;
		}
		int index = merchantCharacter.GetIndex();
		if (index < 0 || index >= value.Count)
		{
			return null;
		}
		return value[index];
	}
}
