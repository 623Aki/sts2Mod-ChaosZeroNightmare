using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznCore.Presentation;

internal static class CombatBlockGainSfxReplacementPatches
{
	[HarmonyPatch(typeof(CreatureCmd), "GainBlock", new Type[]
	{
		typeof(Creature),
		typeof(decimal),
		typeof(ValueProp),
		typeof(CardPlay),
		typeof(bool)
	})]
	private static class GainBlockScopePatch
	{
		private static void Prefix(Creature creature, out BlockGainSfxScope? __state)
		{
			NativeBlockGainSfxReplacementRequest nativeBlockGainSfxReplacementRequest = new NativeBlockGainSfxReplacementRequest(creature);
			SkinEventHub.Publish(nativeBlockGainSfxReplacementRequest);
			if (!nativeBlockGainSfxReplacementRequest.HasReplacement)
			{
				__state = null;
				return;
			}
			__state = new BlockGainSfxScope(nativeBlockGainSfxReplacementRequest, CurrentScope.Value);
			CurrentScope.Value = __state;
		}

		private static void Postfix(BlockGainSfxScope? __state)
		{
			if (__state != null && CurrentScope.Value == __state)
			{
				CurrentScope.Value = __state.Parent;
			}
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterModifyingBlockAmount")]
	private static class AfterModifyingBlockAmountArmPatch
	{
		private static void Postfix(decimal modifiedBlock, ref Task __result)
		{
			BlockGainSfxScope value = CurrentScope.Value;
			if (value != null && !(modifiedBlock <= 0m))
			{
				if (__result.IsCompletedSuccessfully)
				{
					value.Arm();
				}
				else
				{
					__result = ArmAfterCompletion(__result, value);
				}
			}
		}
	}

	[HarmonyPatch(typeof(SfxCmd), "Play", new Type[]
	{
		typeof(string),
		typeof(float)
	})]
	private static class NativeBlockGainSfxPatch
	{
		private static bool Prefix(string sfx)
		{
			if (!string.Equals(sfx, "event:/sfx/block_gain", StringComparison.Ordinal))
			{
				return true;
			}
			if (NonInteractiveMode.IsActive || CombatManager.Instance.IsEnding)
			{
				return true;
			}
			BlockGainSfxScope scope = CurrentScope.Value;
			if (scope == null || !scope.TryConsume())
			{
				return true;
			}
			bool replaced = false;
			SkinPatchGuard.Run("Native block gain SFX replacement", () =>
			{
				replaced = scope.Request.TryReplace();
			});
			return !replaced;
		}
	}

	private sealed class BlockGainSfxScope(NativeBlockGainSfxReplacementRequest request, BlockGainSfxScope? parent)
	{
		private bool _armed;

		private bool _consumed;

		public NativeBlockGainSfxReplacementRequest Request { get; } = request;

		public BlockGainSfxScope? Parent { get; } = parent;

		public void Arm()
		{
			_armed = true;
		}

		public bool TryConsume()
		{
			if (!_armed || _consumed)
			{
				return false;
			}
			_consumed = true;
			return true;
		}
	}

	private static readonly AsyncLocal<BlockGainSfxScope?> CurrentScope = new AsyncLocal<BlockGainSfxScope>();

	public static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlyArray<Type>(new Type[3]
	{
		typeof(GainBlockScopePatch),
		typeof(AfterModifyingBlockAmountArmPatch),
		typeof(NativeBlockGainSfxPatch)
	});

	private static async Task ArmAfterCompletion(Task original, BlockGainSfxScope scope)
	{
		await original;
		scope.Arm();
	}
}
