using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Presentation;

internal readonly record struct RoomEnteringEvent(IRunState RunState, AbstractRoom Room);
