using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Gameplay.HandSize;

public static class CznMaxHandSizeRuntime
{
	public const int DefaultMaxHandSize = 10;

	private const string RegistryKey = "CznCore.Gameplay.HandSize.Modifiers.v1";

	private const string PatchGateKey = "CznCore.Gameplay.HandSize.Patched.v1";

	private static readonly MethodInfo HandLimitTranspilerMethod = RequireOwnMethod("HandLimitTranspiler");

	private static readonly MethodInfo ApplyModifiersMethod = RequireOwnMethod("ApplyModifiers");

	private static readonly MethodInfo StartCardPlayTranspilerMethod = RequireOwnMethod("StartCardPlayTranspiler");

	private static readonly MethodInfo GetPositionPrefixMethod = RequireOwnMethod("GetPositionPrefix");

	private static readonly MethodInfo GetAnglePrefixMethod = RequireOwnMethod("GetAnglePrefix");

	private static readonly MethodInfo GetScalePrefixMethod = RequireOwnMethod("GetScalePrefix");

	private static readonly MethodInfo GetShortcutOrDefaultMethod = RequireOwnMethod("GetShortcutOrDefault");

	public static void RegisterModifier(string id, Func<Player, int, int> modifier)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, "id");
		ArgumentNullException.ThrowIfNull(modifier, "modifier");
		GetRegistry()[id] = modifier;
	}

	public static int Calculate(Player player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		return ApplyModifiers(CardPile.MaxCardsInHand, player);
	}

	public static void EnsurePatched(Harmony harmony)
	{
		ArgumentNullException.ThrowIfNull(harmony, "harmony");
		object data = AppDomain.CurrentDomain.GetData("CznCore.Gameplay.HandSize.Patched.v1");
		if (!(data is bool) || !(bool)data)
		{
			PatchNativeHandLimitSites(harmony);
			AppDomain.CurrentDomain.SetData("CznCore.Gameplay.HandSize.Patched.v1", true);
		}
	}

	private static ConcurrentDictionary<string, Func<Player, int, int>> GetRegistry()
	{
		if (AppDomain.CurrentDomain.GetData("CznCore.Gameplay.HandSize.Modifiers.v1") is ConcurrentDictionary<string, Func<Player, int, int>> result)
		{
			return result;
		}
		ConcurrentDictionary<string, Func<Player, int, int>> concurrentDictionary = new ConcurrentDictionary<string, Func<Player, int, int>>(StringComparer.Ordinal);
		AppDomain.CurrentDomain.SetData("CznCore.Gameplay.HandSize.Modifiers.v1", concurrentDictionary);
		return concurrentDictionary;
	}

	private static int ApplyModifiers(int baseLimit, Player player)
	{
		int num = baseLimit;
		foreach (KeyValuePair<string, Func<Player, int, int>> item in GetRegistry().OrderBy((KeyValuePair<string, Func<Player, int, int>> pair) => pair.Key, StringComparer.Ordinal))
		{
			num = item.Value(player, num);
		}
		return Math.Max(0, num);
	}

	private static void PatchNativeHandLimitSites(Harmony harmony)
	{
		PatchHandLimit(harmony, AccessTools.Method(typeof(CardPileCmd), "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot", new Type[1] { typeof(Player) }));
		PatchHandLimit(harmony, AccessTools.AsyncMoveNext(FindSetupPlayerTurn()));
		PatchHandLimit(harmony, AccessTools.AsyncMoveNext(AccessTools.Method(typeof(CardPileCmd), "DrawInternal", new Type[4]
		{
			typeof(PlayerChoiceContext),
			typeof(decimal),
			typeof(Player),
			typeof(bool)
		})));
		PatchHandLimit(harmony, AccessTools.AsyncMoveNext(AccessTools.Method(typeof(CardPileCmd), "Add", new Type[6]
		{
			typeof(IEnumerable<CardModel>),
			typeof(CardPile),
			typeof(CardPilePosition),
			typeof(AbstractModel),
			typeof(bool),
			typeof(bool)
		})));
		PatchHandLimit(harmony, AccessTools.Method(typeof(CardConsoleCmd), "Process", new Type[2]
		{
			typeof(Player),
			typeof(string[])
		}));
		Type[] array = new Type[6]
		{
			typeof(Scrawl),
			typeof(Anointed),
			typeof(Dredge),
			typeof(NeowsFury),
			typeof(CrashLanding),
			typeof(Pillage)
		};
		foreach (Type type in array)
		{
			PatchHandLimit(harmony, AccessTools.AsyncMoveNext(AccessTools.Method(type, "OnPlay", new Type[2]
			{
				typeof(PlayerChoiceContext),
				typeof(CardPlay)
			})));
		}
		harmony.Patch(AccessTools.Method(typeof(NPlayerHand), "StartCardPlay", new Type[2]
		{
			typeof(NHandCardHolder),
			typeof(bool)
		}), null, null, new HarmonyMethod(StartCardPlayTranspilerMethod));
		harmony.Patch(AccessTools.Method(typeof(HandPosHelper), "GetPosition", new Type[2]
		{
			typeof(int),
			typeof(int)
		}), new HarmonyMethod(GetPositionPrefixMethod));
		harmony.Patch(AccessTools.Method(typeof(HandPosHelper), "GetAngle", new Type[2]
		{
			typeof(int),
			typeof(int)
		}), new HarmonyMethod(GetAnglePrefixMethod));
		harmony.Patch(AccessTools.Method(typeof(HandPosHelper), "GetScale", new Type[1] { typeof(int) }), new HarmonyMethod(GetScalePrefixMethod));
	}

	private static void PatchHandLimit(Harmony harmony, MethodInfo? target)
	{
		if (target == null)
		{
			throw new MissingMethodException("STS2 v0.111 maximum-hand-size patch target was not found.");
		}
		harmony.Patch(target, null, null, new HarmonyMethod(HandLimitTranspilerMethod)
		{
			priority = 800
		});
	}

	private static MethodInfo? FindSetupPlayerTurn()
	{
		return typeof(CombatManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).SingleOrDefault((MethodInfo method) =>
		{
			if (method.Name != "SetupPlayerTurn")
			{
				return false;
			}
			ParameterInfo[] parameters = method.GetParameters();
			return parameters.Length == 3 && parameters[1].ParameterType == typeof(Player) && parameters[2].ParameterType == typeof(HookPlayerChoiceContext);
		});
	}

	private static IEnumerable<CodeInstruction> HandLimitTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		MethodInfo getter = AccessTools.PropertyGetter(typeof(CardPile), "MaxCardsInHand");
		CodeInstruction[] loadPlayer = LoadPlayer(__originalMethod);
		bool patched = false;
		foreach (CodeInstruction instruction in instructions)
		{
			yield return instruction;
			if (instruction.Calls(getter))
			{
				CodeInstruction[] array = loadPlayer;
				foreach (CodeInstruction codeInstruction in array)
				{
					yield return codeInstruction.Clone();
				}
				yield return new CodeInstruction(OpCodes.Call, ApplyModifiersMethod);
				patched = true;
			}
		}
		if (!patched)
		{
			throw new InvalidOperationException("No STS2 v0.111 hand-limit read found in " + __originalMethod.FullDescription() + ".");
		}
	}

	private static CodeInstruction[] LoadPlayer(MethodBase method)
	{
		ParameterInfo parameterInfo = method.GetParameters().SingleOrDefault((ParameterInfo parameter) => parameter.ParameterType == typeof(Player));
		if (parameterInfo == null)
		{
			FieldInfo[] fields = method.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo fieldInfo = fields.SingleOrDefault((FieldInfo field) => field.FieldType == typeof(Player));
			if (!(fieldInfo != null))
			{
				FieldInfo operand = fields.Single((FieldInfo field) => typeof(CardModel).IsAssignableFrom(field.FieldType));
				return new CodeInstruction[3]
				{
					new CodeInstruction(OpCodes.Ldarg_0),
					new CodeInstruction(OpCodes.Ldfld, operand),
					new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(CardModel), "Owner"))
				};
			}
			return new CodeInstruction[2]
			{
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Ldfld, fieldInfo)
			};
		}
		return new CodeInstruction[1] { CodeInstruction.LoadArgument(parameterInfo.Position + ((!method.IsStatic) ? 1 : 0)) };
	}

	private static IEnumerable<CodeInstruction> StartCardPlayTranspiler(IEnumerable<CodeInstruction> instructions)
	{
		List<CodeInstruction> list = instructions.ToList();
		FieldInfo field = AccessTools.Field(typeof(NPlayerHand), "_selectCardShortcuts");
		FieldInfo field2 = AccessTools.Field(typeof(NPlayerHand), "_draggedHolderIndex");
		bool flag = false;
		for (int i = 4; i < list.Count; i++)
		{
			if (!(list[i].opcode != OpCodes.Ldelem_Ref) && list[i - 3].LoadsField(field) && list[i - 1].LoadsField(field2))
			{
				CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Call, GetShortcutOrDefaultMethod);
				codeInstruction.labels.AddRange(list[i].labels);
				codeInstruction.blocks.AddRange(list[i].blocks);
				list[i] = codeInstruction;
				flag = true;
			}
		}
		if (!flag)
		{
			GD.PushWarning("[CznCore.HandSize] NPlayerHand shortcut pattern was not found; cards above slot 10 may not support mouse drag.");
		}
		return list;
	}

	private static StringName GetShortcutOrDefault(StringName[] shortcuts, int index)
	{
		if (index < 0 || index >= shortcuts.Length)
		{
			return MegaInput.down;
		}
		return shortcuts[index];
	}

	private static bool GetPositionPrefix(int handSize, int cardIndex, ref Vector2 __result)
	{
		if (handSize <= 10)
		{
			return true;
		}
		ValidateCardIndex(handSize, cardIndex);
		float halfSpread = GetHalfSpread(handSize);
		float edgeLift = GetEdgeLift(handSize);
		float num = ((handSize <= 1) ? 0f : (2f * (float)cardIndex / ((float)handSize - 1f) - 1f));
		__result = new Vector2(halfSpread * num, Math.Min(18f, -64f + edgeLift * num * num));
		return false;
	}

	private static bool GetAnglePrefix(int handSize, int cardIndex, ref float __result)
	{
		if (handSize <= 10)
		{
			return true;
		}
		ValidateCardIndex(handSize, cardIndex);
		float num = ((handSize <= 1) ? 0f : (2f * (float)cardIndex / ((float)handSize - 1f) - 1f));
		float value = Mathf.RadToDeg(Mathf.Atan2(2f * GetEdgeLift(handSize) * num, Math.Max(1f, GetHalfSpread(handSize))));
		__result = Mathf.Clamp(value, -18f, 18f);
		return false;
	}

	private static bool GetScalePrefix(int handSize, ref Vector2 __result)
	{
		if (handSize <= 10)
		{
			return true;
		}
		float val = 0.64f * MathF.Pow(0.95f, handSize - 11);
		__result = Vector2.One * Math.Max(0.48f, val);
		return false;
	}

	private static float GetHalfSpread(int handSize)
	{
		float weight = Mathf.Clamp((float)(Math.Min(handSize, 14) - 10) / 4f, 0f, 1f);
		return Mathf.Lerp(610f, 690f, weight);
	}

	private static float GetEdgeLift(int handSize)
	{
		return Math.Max(72f, 88f - (float)(handSize - 10) * 1.5f);
	}

	private static void ValidateCardIndex(int handSize, int cardIndex)
	{
		if (cardIndex < 0 || cardIndex >= handSize)
		{
			throw new ArgumentOutOfRangeException("cardIndex", $"Card index {cardIndex} is outside hand size {handSize}.");
		}
	}

	private static MethodInfo RequireOwnMethod(string name)
	{
		return AccessTools.Method(typeof(CznMaxHandSizeRuntime), name) ?? throw new MissingMethodException(typeof(CznMaxHandSizeRuntime).FullName, name);
	}
}
