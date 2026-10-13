using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CombatGeneratedCardSignalDetector
{
	private enum SignalDetectionState
	{
		Unavailable,
		NotDetected,
		Detected
	}

	private readonly record struct SignalDetectionCacheEntry(SignalDetectionState State, long RetryAfter, bool FailureLogged);

	private sealed class RecoverableTypeSignalCache
	{
		private readonly string _signal;

		private readonly Func<Type, bool> _detector;

		private readonly ConcurrentDictionary<Type, SignalDetectionCacheEntry> _entries = new ConcurrentDictionary<Type, SignalDetectionCacheEntry>();

		public RecoverableTypeSignalCache(string signal, Func<Type, bool> detector)
		{
			_signal = (string.IsNullOrWhiteSpace(signal) ? "unknown" : signal);
			_detector = detector ?? throw new ArgumentNullException("detector");
		}

		public bool TryResolve(Type modelType, out bool detected)
		{
			bool flag = _entries.TryGetValue(modelType, out var value);
			if (flag && value.State != SignalDetectionState.Unavailable)
			{
				detected = value.State == SignalDetectionState.Detected;
				return true;
			}
			long tickCount = Environment.TickCount64;
			if (flag && tickCount < value.RetryAfter)
			{
				detected = false;
				return false;
			}
			try
			{
				detected = _detector(modelType);
				_entries[modelType] = new SignalDetectionCacheEntry((!detected) ? SignalDetectionState.NotDetected : SignalDetectionState.Detected, 0L, FailureLogged: false);
				return true;
			}
			catch (SignalDetectionUnavailableException)
			{
				detected = false;
				_entries[modelType] = new SignalDetectionCacheEntry(SignalDetectionState.Unavailable, tickCount + 5000, flag && value.FailureLogged);
				return false;
			}
			catch (Exception ex2)
			{
				detected = false;
				bool num = !flag || !value.FailureLogged;
				_entries[modelType] = new SignalDetectionCacheEntry(SignalDetectionState.Unavailable, tickCount + 5000, FailureLogged: true);
				if (num)
				{
					SkinLog.Warn("Combat card signal detection unavailable signal=" + _signal + " model=" + (modelType.FullName ?? modelType.Name) + ": " + SkinLog.FormatException(ex2));
				}
				return false;
			}
		}
	}

	private sealed class SignalDetectionUnavailableException : Exception
	{
	}

	private const string CardCmdTypeName = "MegaCrit.Sts2.Core.Commands.CardCmd";

	private const string CardPileCmdTypeName = "MegaCrit.Sts2.Core.Commands.CardPileCmd";

	private const string ForgeCmdTypeName = "MegaCrit.Sts2.Core.Commands.ForgeCmd";

	private const string PowerCmdTypeName = "MegaCrit.Sts2.Core.Commands.PowerCmd";

	private const string AddGeneratedCardToCombatName = "AddGeneratedCardToCombat";

	private const string AddGeneratedCardsToCombatName = "AddGeneratedCardsToCombat";

	private const string CreateInHandName = "CreateInHand";

	private const string ApplyPowerName = "Apply";

	private const string ForgeName = "Forge";

	private const string TransformName = "Transform";

	private const string OnPlayName = "OnPlay";

	private const long FailedDetectionRetryDelayMilliseconds = 5000L;

	private static readonly RecoverableTypeSignalCache CardTypeSignals;

	private static readonly RecoverableTypeSignalCache PowerTypeSignals;

	private static readonly RecoverableTypeSignalCache EnemyDebuffCardTypeSignals;

	private static readonly RecoverableTypeSignalCache EnemyDebuffPowerTypeSignals;

	private static readonly OpCode[] SingleByteOpCodes;

	private static readonly OpCode[] MultiByteOpCodes;

	static CombatGeneratedCardSignalDetector()
	{
		CardTypeSignals = new RecoverableTypeSignalCache("generated_card", DetectCardType);
		PowerTypeSignals = new RecoverableTypeSignalCache("generated_power", DetectPowerType);
		EnemyDebuffCardTypeSignals = new RecoverableTypeSignalCache("enemy_debuff_card", DetectEnemyDebuffCardType);
		EnemyDebuffPowerTypeSignals = new RecoverableTypeSignalCache("enemy_debuff_power", IsEnemyDebuffPowerType);
		SingleByteOpCodes = new OpCode[256];
		MultiByteOpCodes = new OpCode[256];
		FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
		for (int i = 0; i < fields.Length; i++)
		{
			if (fields[i].GetValue(null) is OpCode opCode)
			{
				ushort num = (ushort)opCode.Value;
				if (num < 256)
				{
					SingleByteOpCodes[num] = opCode;
				}
				else if ((num & 0xFF00) == 65024)
				{
					MultiByteOpCodes[num & 0xFF] = opCode;
				}
			}
		}
	}

	public static bool HasGeneratedCardOnPlaySignal(CardModel card)
	{
		bool detected;
		return CardTypeSignals.TryResolve(card.GetType(), out detected) & detected;
	}

	public static bool HasEnemyDebuffOnPlaySignal(CardModel card)
	{
		bool detected;
		return EnemyDebuffCardTypeSignals.TryResolve(card.GetType(), out detected) & detected;
	}

	private static bool DetectCardType(Type cardType)
	{
		MethodInfo methodInfo = FindOnPlayMethod(cardType);
		if (methodInfo != null)
		{
			return MethodHasGeneratedCardSignal(methodInfo, cardType, new HashSet<MethodBase>());
		}
		return false;
	}

	private static bool DetectEnemyDebuffCardType(Type cardType)
	{
		MethodInfo methodInfo = FindOnPlayMethod(cardType);
		if (methodInfo != null)
		{
			return MethodHasEnemyDebuffSignal(methodInfo, cardType, new HashSet<MethodBase>());
		}
		return false;
	}

	private static MethodInfo? FindOnPlayMethod(Type cardType)
	{
		Type type = cardType;
		while (type != null && type != typeof(object))
		{
			MethodInfo method = type.GetMethod("OnPlay", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method != null)
			{
				return method;
			}
			type = type.BaseType;
		}
		return null;
	}

	private static bool DetectPowerType(Type powerType)
	{
		HashSet<MethodBase> visited = new HashSet<MethodBase>();
		MethodInfo[] methods = powerType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo in methods)
		{
			if (!methodInfo.IsSpecialName && MethodHasGeneratedCardSignal(methodInfo, powerType, visited))
			{
				return true;
			}
		}
		return false;
	}

	private static bool MethodHasGeneratedCardSignal(MethodBase method, Type ownerType, HashSet<MethodBase> visited)
	{
		if (!visited.Add(method))
		{
			return false;
		}
		return ScanMethodBody(TryGetAsyncMoveNext(method) ?? method, ownerType, visited);
	}

	private static bool MethodHasEnemyDebuffSignal(MethodBase method, Type ownerType, HashSet<MethodBase> visited)
	{
		if (!visited.Add(method))
		{
			return false;
		}
		return ScanMethodBodyForEnemyDebuff(TryGetAsyncMoveNext(method) ?? method, ownerType, visited);
	}

	private static MethodInfo? TryGetAsyncMoveNext(MethodBase method)
	{
		return method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	private static bool ScanMethodBody(MethodBase method, Type ownerType, HashSet<MethodBase> visited)
	{
		byte[] array = method.GetMethodBody()?.GetILAsByteArray();
		if (array == null || array.Length == 0)
		{
			return false;
		}
		Module module = method.Module;
		Type[] typeArguments = method.DeclaringType?.GetGenericArguments() ?? Type.EmptyTypes;
		Type[] methodArguments = ((method is MethodInfo methodInfo) ? methodInfo.GetGenericArguments() : Type.EmptyTypes);
		int offset = 0;
		while (offset < array.Length)
		{
			OpCode opCode = ReadOpCode(array, ref offset);
			OperandType operandType = opCode.OperandType;
			if ((operandType == OperandType.InlineMethod || operandType == OperandType.InlineTok) ? true : false)
			{
				int token = ReadInt32(array, ref offset);
				MethodBase methodBase = ResolveMethod(module, token, typeArguments, methodArguments);
				if (!(methodBase == null))
				{
					if (IsGeneratedCardRouteSignalCall(methodBase))
					{
						return true;
					}
					if (TryGetAppliedPowerType(methodBase, out Type powerType) && ResolveRequiredCachedSignal(PowerTypeSignals, powerType))
					{
						return true;
					}
					if (methodBase.DeclaringType == ownerType && !methodBase.IsSpecialName && MethodHasGeneratedCardSignal(methodBase, ownerType, visited))
					{
						return true;
					}
				}
			}
			else
			{
				SkipOperand(array, ref offset, opCode.OperandType);
			}
		}
		return false;
	}

	private static bool ScanMethodBodyForEnemyDebuff(MethodBase method, Type ownerType, HashSet<MethodBase> visited)
	{
		byte[] array = method.GetMethodBody()?.GetILAsByteArray();
		if (array == null || array.Length == 0)
		{
			return false;
		}
		Module module = method.Module;
		Type[] typeArguments = method.DeclaringType?.GetGenericArguments() ?? Type.EmptyTypes;
		Type[] methodArguments = ((method is MethodInfo methodInfo) ? methodInfo.GetGenericArguments() : Type.EmptyTypes);
		int offset = 0;
		while (offset < array.Length)
		{
			OpCode opCode = ReadOpCode(array, ref offset);
			OperandType operandType = opCode.OperandType;
			if ((operandType == OperandType.InlineMethod || operandType == OperandType.InlineTok) ? true : false)
			{
				int token = ReadInt32(array, ref offset);
				MethodBase methodBase = ResolveMethod(module, token, typeArguments, methodArguments);
				if (!(methodBase == null))
				{
					if (TryGetAppliedPowerType(methodBase, out Type powerType) && ResolveRequiredCachedSignal(EnemyDebuffPowerTypeSignals, powerType))
					{
						return true;
					}
					if (methodBase.DeclaringType == ownerType && !methodBase.IsSpecialName && MethodHasEnemyDebuffSignal(methodBase, ownerType, visited))
					{
						return true;
					}
				}
			}
			else
			{
				SkipOperand(array, ref offset, opCode.OperandType);
			}
		}
		return false;
	}

	private static bool IsEnemyDebuffPowerType(Type powerType)
	{
		return ModelDb.DebugPower(powerType).Type == PowerType.Debuff;
	}

	private static bool ResolveRequiredCachedSignal(RecoverableTypeSignalCache cache, Type modelType)
	{
		if (cache.TryResolve(modelType, out var detected))
		{
			return detected;
		}
		throw new SignalDetectionUnavailableException();
	}

	private static OpCode ReadOpCode(byte[] il, ref int offset)
	{
		byte b = il[offset++];
		if (b == 254 && offset < il.Length)
		{
			return MultiByteOpCodes[il[offset++]];
		}
		return SingleByteOpCodes[b];
	}

	private static MethodBase? ResolveMethod(Module module, int token, Type[] typeArguments, Type[] methodArguments)
	{
		try
		{
			return module.ResolveMethod(token, typeArguments, methodArguments);
		}
		catch
		{
			try
			{
				return module.ResolveMethod(token);
			}
			catch
			{
				return null;
			}
		}
	}

	private static bool IsGeneratedCardRouteSignalCall(MethodBase method)
	{
		if (string.Equals(method.DeclaringType?.FullName, "MegaCrit.Sts2.Core.Commands.CardPileCmd", StringComparison.Ordinal) && (string.Equals(method.Name, "AddGeneratedCardToCombat", StringComparison.Ordinal) || string.Equals(method.Name, "AddGeneratedCardsToCombat", StringComparison.Ordinal)))
		{
			return true;
		}
		if (IsForgeCall(method) || IsTransformCall(method))
		{
			return true;
		}
		if (string.Equals(method.Name, "CreateInHand", StringComparison.Ordinal) && method.DeclaringType != null)
		{
			return typeof(CardModel).IsAssignableFrom(method.DeclaringType);
		}
		return false;
	}

	private static bool IsForgeCall(MethodBase method)
	{
		if (string.Equals(method.DeclaringType?.FullName, "MegaCrit.Sts2.Core.Commands.ForgeCmd", StringComparison.Ordinal))
		{
			return string.Equals(method.Name, "Forge", StringComparison.Ordinal);
		}
		return false;
	}

	private static bool IsTransformCall(MethodBase method)
	{
		if (string.Equals(method.DeclaringType?.FullName, "MegaCrit.Sts2.Core.Commands.CardCmd", StringComparison.Ordinal))
		{
			if (!string.Equals(method.Name, "Transform", StringComparison.Ordinal))
			{
				return method.Name.StartsWith("TransformTo", StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	private static bool TryGetAppliedPowerType(MethodBase method, out Type powerType)
	{
		powerType = null;
		if (!string.Equals(method.DeclaringType?.FullName, "MegaCrit.Sts2.Core.Commands.PowerCmd", StringComparison.Ordinal) || !string.Equals(method.Name, "Apply", StringComparison.Ordinal) || !(method is MethodInfo { IsGenericMethod: not false } methodInfo))
		{
			return false;
		}
		Type[] genericArguments = methodInfo.GetGenericArguments();
		if (genericArguments.Length == 0)
		{
			return false;
		}
		powerType = genericArguments[0];
		return powerType != null;
	}

	private static int ReadInt32(byte[] il, ref int offset)
	{
		int result = BitConverter.ToInt32(il, offset);
		offset += 4;
		return result;
	}

	private static void SkipOperand(byte[] il, ref int offset, OperandType operandType)
	{
		switch (operandType)
		{
		case OperandType.ShortInlineBrTarget:
		case OperandType.ShortInlineI:
		case OperandType.ShortInlineVar:
			offset++;
			break;
		case OperandType.InlineVar:
			offset += 2;
			break;
		case OperandType.InlineBrTarget:
		case OperandType.InlineField:
		case OperandType.InlineI:
		case OperandType.InlineSig:
		case OperandType.InlineString:
		case OperandType.InlineType:
		case OperandType.ShortInlineR:
			offset += 4;
			break;
		case OperandType.InlineI8:
		case OperandType.InlineR:
			offset += 8;
			break;
		case OperandType.InlineSwitch:
		{
			int num = BitConverter.ToInt32(il, offset);
			offset += 4 + num * 4;
			break;
		}
		case OperandType.InlineMethod:
		case OperandType.InlineNone:
		case OperandType.InlinePhi:
		case (OperandType)8:
		case OperandType.InlineTok:
			break;
		}
	}
}
