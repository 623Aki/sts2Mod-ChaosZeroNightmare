using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Presentation;

internal static class SkinRunContext
{
	public static Player? TryGetLocalPlayer(IRunState runState)
	{
		try
		{
			return LocalContext.GetMe(runState);
		}
		catch
		{
			return null;
		}
	}

	public static bool IsMerchantRoomOrFakeMerchant(AbstractRoom room)
	{
		if (!(room is MerchantRoom))
		{
			if (room is EventRoom eventRoom)
			{
				return eventRoom.CanonicalEvent is FakeMerchant;
			}
			return false;
		}
		return true;
	}
}
