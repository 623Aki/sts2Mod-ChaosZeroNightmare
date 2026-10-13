using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznCore.Presentation;

internal static class SkinTransportPatches
{
	[HarmonyPatch(typeof(NRestSiteCharacter), "_Ready")]
	private static class RestSiteCharacterReadyPatch
	{
		private static void Postfix(NRestSiteCharacter __instance)
		{
			SkinEventHub.Publish(new RestSiteCharacterReadyEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NRestSiteCharacter), "FlipX")]
	private static class RestSiteCharacterFlipPatch
	{
		private static void Postfix(NRestSiteCharacter __instance)
		{
			SkinEventHub.Publish(new RestSiteCharacterFlippedEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NMerchantCharacter), "_Ready")]
	private static class MerchantCharacterReadyPatch
	{
		private static void Postfix(NMerchantCharacter __instance)
		{
			SkinEventHub.Publish(new MerchantCharacterReadyEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NFakeMerchant), "AfterRoomIsLoaded")]
	private static class FakeMerchantRoomLoadedPatch
	{
		private static void Postfix(NFakeMerchant __instance)
		{
			SkinEventHub.Publish(new FakeMerchantRoomLoadedEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterRestSiteHeal")]
	private static class RestSiteHealPatch
	{
		private static void Prefix(Player player, bool isMimicked)
		{
			SkinEventHub.Publish(new RestSiteHealedEvent(player, isMimicked));
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterRestSiteSmith")]
	private static class RestSiteSmithPatch
	{
		private static void Prefix(Player player)
		{
			SkinEventHub.Publish(new RestSiteSmithedEvent(player));
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterRoomEntered")]
	private static class RoomEnteringPatch
	{
		private static void Prefix(IRunState runState, AbstractRoom room)
		{
			SkinEventHub.Publish(new RoomEnteringEvent(runState, room));
		}
	}

	[HarmonyPatch(typeof(Hook), "BeforeCombatStart")]
	private static class CombatStartingPatch
	{
		private static void Prefix(IRunState runState, ICombatState? combatState)
		{
			SkinEventHub.Publish(new CombatStartingEvent(runState, combatState));
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterPlayerTurnStart")]
	private static class PlayerTurnStartedPatch
	{
		private static void Prefix(ICombatState combatState, PlayerChoiceContext choiceContext, Player player)
		{
			SkinEventHub.Publish(new PlayerTurnStartedEvent(combatState, choiceContext, player));
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterDamageReceived")]
	private static class DamageReceivedPatch
	{
		private static void Postfix(PlayerChoiceContext choiceContext, IRunState runState, ICombatState? combatState, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
		{
			SkinEventHub.Publish(new DamageReceivedEvent(choiceContext, runState, combatState, target, result, props, dealer, cardSource));
		}
	}

	[HarmonyPatch(typeof(Creature), "GainBlockInternal")]
	private static class BlockActuallyIncreasedPatch
	{
		private static void Prefix(Creature __instance, out int __state)
		{
			__state = __instance.Block;
		}

		private static void Postfix(Creature __instance, int __state)
		{
			int block = __instance.Block;
			if (block > __state)
			{
				SkinEventHub.Publish(new BlockActuallyIncreasedEvent(__instance, __state, block));
			}
		}
	}

	[HarmonyPatch(typeof(NTreasureRoom), "OpenChest")]
	private static class TreasureChestOpeningPatch
	{
		private static void Prefix(NTreasureRoom __instance)
		{
			SkinEventHub.Publish(new TreasureChestOpeningEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterPotionUsed")]
	private static class PotionUsedPatch
	{
		private static void Prefix(IRunState runState, ICombatState? combatState, PotionModel potion, Creature? target)
		{
			SkinEventHub.Publish(new PotionUsedEvent(runState, combatState, potion, target));
		}
	}

	[HarmonyPatch(typeof(Hook), "BeforeCardPlayed")]
	private static class CardPlayingPatch
	{
		private static void Prefix(ICombatState combatState, CardPlay cardPlay)
		{
			SkinEventHub.Publish(new CardPlayingEvent(combatState, cardPlay));
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterCardPlayed")]
	private static class CardPlayedPatch
	{
		private static void Prefix(ICombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
		{
			SkinEventHub.Publish(new CardPlayedEvent(combatState, choiceContext, cardPlay));
		}
	}

	public static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlyArray<Type>(new Type[15]
	{
		typeof(RestSiteCharacterReadyPatch),
		typeof(RestSiteCharacterFlipPatch),
		typeof(MerchantCharacterReadyPatch),
		typeof(FakeMerchantRoomLoadedPatch),
		typeof(RestSiteHealPatch),
		typeof(RestSiteSmithPatch),
		typeof(RoomEnteringPatch),
		typeof(CombatStartingPatch),
		typeof(PlayerTurnStartedPatch),
		typeof(DamageReceivedPatch),
		typeof(BlockActuallyIncreasedPatch),
		typeof(TreasureChestOpeningPatch),
		typeof(PotionUsedPatch),
		typeof(CardPlayingPatch),
		typeof(CardPlayedPatch)
	});
}
