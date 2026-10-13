using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class SkinCardTransportPatches
{
	[HarmonyPatch(typeof(NHandCardHolder), "OnFocus")]
	private static class HandCardFocusedPatch
	{
		private static void Postfix(NHandCardHolder __instance)
		{
			SkinEventHub.Publish(new HandCardFocusedEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NHandCardHolder), "OnUnfocus")]
	private static class HandCardUnfocusedPatch
	{
		private static void Postfix(NHandCardHolder __instance)
		{
			SkinEventHub.Publish(new HandCardUnfocusedEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NHandCardHolder), "DoCardHoverEffects")]
	private static class HandCardHoverEffectsPatch
	{
		private static void Postfix(NHandCardHolder __instance, bool isHovered)
		{
			SkinEventHub.Publish(new HandCardHoverEffectsEvent(__instance, isHovered));
		}
	}

	[HarmonyPatch(typeof(NHandCardHolder), "OnMousePressed")]
	private static class HandCardMousePressedPatch
	{
		private static void Postfix(NHandCardHolder __instance, InputEvent inputEvent)
		{
			SkinEventHub.Publish(new HandCardMousePressedEvent(__instance, inputEvent));
		}
	}

	[HarmonyPatch(typeof(NMouseCardPlay), "Start")]
	private static class MouseCardPlayStartedPatch
	{
		private static void Postfix(NMouseCardPlay __instance)
		{
			SkinEventHub.Publish(new MouseCardPlayStartedEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NTargetManager), "StartTargeting", new Type[]
	{
		typeof(TargetType),
		typeof(Control),
		typeof(TargetMode),
		typeof(Func<bool>),
		typeof(Func<Node, bool>)
	})]
	private static class SingleCardTargetingStartedPatch
	{
		private static void Postfix(Control control)
		{
			if (control is NCard nCard)
			{
				CardModel model = nCard.Model;
				if (model != null)
				{
					SkinEventHub.Publish(new CardSelectionPresentationStartedEvent(model));
				}
			}
		}
	}

	[HarmonyPatch(typeof(NCardPlay), "ShowMultiCreatureTargetingVisuals")]
	private static class MultiCardTargetingStartedPatch
	{
		private static void Postfix(NCardPlay __instance)
		{
			CardModel cardModel = __instance.Holder?.CardModel;
			if (cardModel != null)
			{
				SkinEventHub.Publish(new CardSelectionPresentationStartedEvent(cardModel));
			}
		}
	}

	[HarmonyPatch(typeof(NCardPlay), "CancelPlayCard")]
	private static class CardPlayCancelledPatch
	{
		private static void Postfix(NCardPlay __instance)
		{
			SkinEventHub.Publish(new CardPlayCancelledEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(CardModel), "EnqueueManualPlay")]
	private static class ManualCardPlayEnqueuePatch
	{
		private static void Prefix(CardModel __instance, Creature? target)
		{
			SkinEventHub.Publish(new CardPlayEnqueuingEvent(__instance, target));
		}

		private static void Postfix(CardModel __instance)
		{
			SkinEventHub.Publish(new CardPlayCommittedEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NCardPlayQueue), "OnLocalCardPlayed")]
	private static class LocalCardPlayedPatch
	{
		private static void Postfix(PlayCardAction action, CardModel card)
		{
			SkinEventHub.Publish(new LocalCardPlayedEvent(card, action?.Target));
		}
	}

	[HarmonyPatch(typeof(NPlayerHand), "Add", new Type[]
	{
		typeof(NCard),
		typeof(int)
	})]
	private static class PlayerHandCardAddedPatch
	{
		private static void Postfix(NPlayerHand __instance, NCard? card, NHandCardHolder? __result)
		{
			SkinEventHub.Publish(new PlayerHandCardAddedEvent(__instance, card, __result));
		}
	}

	public static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlyArray<Type>(new Type[11]
	{
		typeof(HandCardFocusedPatch),
		typeof(HandCardUnfocusedPatch),
		typeof(HandCardHoverEffectsPatch),
		typeof(HandCardMousePressedPatch),
		typeof(MouseCardPlayStartedPatch),
		typeof(SingleCardTargetingStartedPatch),
		typeof(MultiCardTargetingStartedPatch),
		typeof(CardPlayCancelledPatch),
		typeof(ManualCardPlayEnqueuePatch),
		typeof(LocalCardPlayedPatch),
		typeof(PlayerHandCardAddedPatch)
	});
}
