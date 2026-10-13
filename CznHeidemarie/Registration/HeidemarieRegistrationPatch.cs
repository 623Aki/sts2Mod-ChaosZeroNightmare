using System.Collections.Generic;
using System.Linq;
using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Registration;

/// <summary>
/// 把 Heidemarie 追加进游戏的角色表。
///
/// <para><c>CharacterModel.VisualsPath</c> 是 private 成员，无法 override，
/// 所以本体场景靠 pck 同名路径覆盖（见 <see cref="Heidemarie"/> 的说明）。
/// 这里只需要让角色出现在 <c>ModelDb.AllCharacters</c> 里即可。</para>
/// </summary>
[HarmonyPatch(typeof(ModelDb), "get_AllCharacters")]
internal static class HeidemarieRegistrationPatch
{
	private static void Postfix(ref IEnumerable<CharacterModel> __result)
	{
		CharacterModel? model = ModelDb.GetByIdOrNull<CharacterModel>(ModelDb.GetId(typeof(Heidemarie)));
		if (model != null)
		{
			__result = __result.Append(model).Distinct().ToArray();
		}
	}
}