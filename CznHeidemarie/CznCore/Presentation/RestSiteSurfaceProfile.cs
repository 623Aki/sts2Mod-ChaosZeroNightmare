using Godot;

namespace CznCore.Presentation;

internal sealed record RestSiteSurfaceProfile(bool ApplySceneLayout, Vector2 SpinePosition, Vector2 SpineScale, RestSiteControlRectProfile? ControlRoot, RestSiteControlRectProfile? SelectionReticle, RestSiteControlRectProfile? Hitbox, RestSiteControlRectProfile? ThoughtBubbleRight, RestSiteControlRectProfile? ThoughtBubbleLeft);
