using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace CznCore.Presentation;

internal readonly record struct HandCardMousePressedEvent(NHandCardHolder Holder, InputEvent Input);
