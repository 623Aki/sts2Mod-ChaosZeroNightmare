using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace CznCore.Presentation;

internal sealed class BattleReadyOverlayRuntime<TCard> where TCard : class
{
	private sealed class CardPlaySuppression
	{
	}

	private readonly BattleReadyOverlayProfile _profile;

	private readonly IBattleReadyOverlayRuntimeContext<TCard> _context;

	private readonly HashSet<string> _missingAnimationsLogged = new HashSet<string>(StringComparer.Ordinal);

	private readonly ConditionalWeakTable<TCard, CardPlaySuppression> _cardPlaySuppressions = new ConditionalWeakTable<TCard, CardPlaySuppression>();

	private PackedScene? _scene;

	private PackedScene? _idleEffectsScene;

	private Resource? _skeletonData;

	private Node? _node;

	private Node2D? _transformNode;

	private MegaSprite? _sprite;

	private Node2D? _idleEffectsBackRoot;

	private Node2D? _idleEffectsFrontRoot;

	private TCard? _activeHoldCard;

	private Vector2 _basePosition;

	private Vector2 _baseScale = Vector2.One;

	private Tween? _moveTween;

	private Tween? _hideDelayTween;

	private int _sceneLoadAttempted;

	private int _idleEffectsSceneLoadAttempted;

	private int _skeletonLoadAttempted;

	private int _visibilityToken;

	private bool _isShown;

	private bool _outPlaying;

	private bool _cardUsePlaying;

	private bool _hasInAnim;

	private bool _hasIdleAnim;

	private bool _hasOutAnim;

	private bool _hasAttackAnim;

	private bool _hasCastingAnim;

	private string? _lastFirst;

	private string? _lastNextLoop;

	private BattleReadyOverlayAnimations Animations => _profile.Animations;

	public BattleReadyOverlayRuntime(BattleReadyOverlayProfile profile, IBattleReadyOverlayRuntimeContext<TCard> context)
	{
		_profile = profile;
		_context = context;
	}

	public bool CanShow(TCard? card)
	{
		return _context.PresentationPolicy.CanShow(card);
	}

	public void Preload()
	{
		if (_context.PresentationPolicy.IsEnabled)
		{
			GetScene();
			GetSkeletonData();
			GetIdleEffectsScene();
			EnsureCreated();
		}
	}

	public void ShowIdleForCard(TCard? card)
	{
		try
		{
			if (!CanShow(card) || card == null || !EnsureCreated() || _sprite == null)
			{
				return;
			}
			if (!_hasIdleAnim)
			{
				LogMissingAnimation(Animations.Idle);
				return;
			}
			bool flag = IsCanvasVisible();
			bool restartAnimation = !flag || !IsBattleReadyIdleFireVisible();
			_activeHoldCard = card;
			_outPlaying = false;
			_cardUsePlaying = false;
			int token = ++_visibilityToken;
			KillTween(ref _hideDelayTween);
			SetCanvasVisible(visible: true);
			SetBattleReadyIdleFireVisible(visible: true, restartAnimation);
			if (_hasInAnim && !flag)
			{
				_isShown = true;
				ApplyTransformFromSettings(forceTargetPosition: true);
				PlaySequence(Animations.In, Animations.Idle);
			}
			else
			{
				ApplyScaleAndOpacityFromSettings();
				PlaySequence(Animations.Idle, Animations.Idle);
				StartSlideIn(token, !flag);
			}
		}
		catch (Exception ex)
		{
			_context.LogWarn("Battle ready overlay idle failed: " + SkinLog.FormatException(ex));
			Cleanup();
		}
	}

	public void HideForCard(TCard? card)
	{
		if (card != null && card == _activeHoldCard)
		{
			_activeHoldCard = null;
			ScheduleOut(_profile.OutDelaySeconds);
		}
	}

	public void CancelForCard(TCard? card)
	{
		if (card != null && card == _activeHoldCard)
		{
			_activeHoldCard = null;
			ScheduleOut(_profile.CancelOutDelaySeconds);
		}
	}

	public void SuppressNextPlayForCard(TCard? card)
	{
		if (card != null && _context.IsTargetCard(card))
		{
			_cardPlaySuppressions.Remove(card);
			_cardPlaySuppressions.Add(card, new CardPlaySuppression());
		}
	}

	public void PlayForCard(TCard card)
	{
		try
		{
			if (ConsumeCardPlaySuppression(card))
			{
				_context.LogInfo("Battle ready overlay card play suppressed.");
			}
			else if (CanShow(card) && EnsureCreated())
			{
				string cardAnimation = GetCardAnimation(card);
				if (cardAnimation != null && _sprite != null)
				{
					PlayAnimationForCard(card, cardAnimation);
				}
			}
		}
		catch (Exception ex)
		{
			_context.LogWarn("Battle ready overlay play failed: " + SkinLog.FormatException(ex));
			Cleanup();
		}
	}

	private bool ConsumeCardPlaySuppression(TCard card)
	{
		if (!_cardPlaySuppressions.TryGetValue(card, out CardPlaySuppression _))
		{
			return false;
		}
		_cardPlaySuppressions.Remove(card);
		return true;
	}

	public void PlayAnimationForCard(TCard card, string animationName)
	{
		try
		{
			if (!CanShow(card) || !EnsureCreated() || string.IsNullOrWhiteSpace(animationName) || _sprite == null)
			{
				return;
			}
			bool flag = IsCanvasVisible();
			bool isShown = _isShown;
			_activeHoldCard = null;
			_outPlaying = false;
			_cardUsePlaying = true;
			_isShown = true;
			int token = ++_visibilityToken;
			KillTween(ref _hideDelayTween);
			if (!flag)
			{
				KillTween(ref _moveTween);
			}
			SetCanvasVisible(visible: true);
			SetBattleReadyIdleFireVisible(visible: false, restartAnimation: false);
			ApplyTransformForCardUse(flag, isShown, token);
			if (!HasAnimation(animationName))
			{
				LogMissingAnimation(animationName);
				_cardUsePlaying = false;
				ScheduleOut(0f);
				return;
			}
			long startTimestamp = _context.TimingProbe.StartTimingProbe();
			PlaySingle(animationName);
			_context.TimingProbe.LogTimingProbe("battle_ready_play:" + animationName, startTimestamp);
			if (_profile.CardUseOutDelaySeconds >= 0f)
			{
				ScheduleCardUseOut(_profile.CardUseOutDelaySeconds);
			}
			_context.LogInfo("Battle ready overlay card anim=" + animationName);
		}
		catch (Exception ex)
		{
			_context.LogWarn("Battle ready overlay play failed: " + SkinLog.FormatException(ex));
			Cleanup();
		}
	}

	public void Cleanup(bool releaseResources = true)
	{
		try
		{
			KillTween(ref _hideDelayTween);
			KillTween(ref _moveTween);
			if (_node != null && GodotObject.IsInstanceValid(_node))
			{
				if (!releaseResources && _sprite != null)
				{
					HideNodeForReuse();
				}
				else
				{
					_node.QueueFree();
				}
			}
		}
		catch
		{
		}
		if (releaseResources || _sprite == null || _node == null || !GodotObject.IsInstanceValid(_node))
		{
			_node = null;
			_transformNode = null;
			_sprite = null;
			_idleEffectsBackRoot = null;
			_idleEffectsFrontRoot = null;
		}
		_activeHoldCard = null;
		_isShown = false;
		_outPlaying = false;
		_cardUsePlaying = false;
		_lastFirst = null;
		_lastNextLoop = null;
		if (releaseResources)
		{
			ReleaseLoadedResources();
		}
	}

	public void ApplyTransformFromSettings()
	{
		ApplyTransformFromSettings(_isShown || _cardUsePlaying || IsCanvasVisible());
	}

	private void ApplyTransformFromSettings(bool forceTargetPosition)
	{
		if (_transformNode != null && GodotObject.IsInstanceValid(_transformNode))
		{
			ApplyScaleAndOpacityFromSettings();
			_transformNode.Position = (forceTargetPosition ? TargetPosition() : HiddenPosition());
		}
	}

	private void ApplyTransformForCardUse(bool wasVisible, bool wasShown, int token)
	{
		if (_transformNode != null && GodotObject.IsInstanceValid(_transformNode))
		{
			ApplyScaleAndOpacityFromSettings();
			if (!wasVisible)
			{
				_transformNode.Position = TargetPosition();
			}
			else if (!wasShown)
			{
				TweenTo(TargetPosition(), hideWhenDone: false, token, _profile.CardUseSlideDurationSeconds);
			}
		}
	}

	private void ApplyScaleAndOpacityFromSettings()
	{
		if (_transformNode != null && GodotObject.IsInstanceValid(_transformNode))
		{
			_transformNode.Scale = _baseScale * _context.BattleReadyScale;
			ApplyOpacity();
		}
	}

	private void ApplyOpacity()
	{
		if (_node is CanvasItem canvasItem && GodotObject.IsInstanceValid(canvasItem))
		{
			Color modulate = canvasItem.Modulate;
			canvasItem.Modulate = new Color(modulate.R, modulate.G, modulate.B, Mathf.Clamp(_context.BattleReadyOpacity, 0.5f, 1f));
		}
	}

	private bool EnsureCreated()
	{
		if (_node != null && GodotObject.IsInstanceValid(_node) && _sprite != null)
		{
			return EnsureNodeAttached();
		}
		long startTimestamp = _context.TimingProbe.StartTimingProbe();
		try
		{
			Cleanup(releaseResources: false);
			PackedScene scene = GetScene();
			if (scene == null)
			{
				return false;
			}
			Node overlayParent = _context.GetOverlayParent();
			if (overlayParent == null)
			{
				return false;
			}
			Node node = scene.Instantiate(PackedScene.GenEditState.Disabled);
			Node node2 = FindSpineSpriteRecursive(node) ?? node;
			if (node2.GetClass() != "SpineSprite")
			{
				_context.LogWarn("Battle ready overlay scene has no SpineSprite.");
				node.QueueFree();
				return false;
			}
			MegaSprite megaSprite = new MegaSprite(node2);
			Resource skeletonData = GetSkeletonData();
			if (skeletonData != null)
			{
				GodotObject from = skeletonData;
				megaSprite.SetSkeletonDataRes(new MegaSkeletonDataResource(Variant.From(in from)));
			}
			megaSprite.ConnectAnimationCompleted(Callable.From((GodotObject _, GodotObject _, GodotObject _) =>
			{
				OnAnimationCompleted(node);
			}));
			if (node is CanvasItem canvasItem)
			{
				canvasItem.Visible = false;
				canvasItem.ZIndex = 0;
			}
			overlayParent.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
			_node = node;
			_transformNode = (node as Node2D) ?? (node2 as Node2D);
			if (_transformNode != null)
			{
				_basePosition = _transformNode.Position;
				_baseScale = _transformNode.Scale;
			}
			_sprite = megaSprite;
			InitAnimationCache(megaSprite);
			AttachBattleReadyIdleEffects(node, node2);
			ApplyTransformFromSettings(forceTargetPosition: false);
			return true;
		}
		finally
		{
			_context.TimingProbe.LogTimingProbe("battle_ready_create", startTimestamp);
		}
	}

	private bool EnsureNodeAttached()
	{
		if (_node == null || !GodotObject.IsInstanceValid(_node))
		{
			return false;
		}
		Node overlayParent = _context.GetOverlayParent();
		if (overlayParent == null || !GodotObject.IsInstanceValid(overlayParent))
		{
			return false;
		}
		Node parent = _node.GetParent();
		if (parent == overlayParent)
		{
			return true;
		}
		parent?.RemoveChild(_node);
		overlayParent.AddChild(_node, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	private void HideNodeForReuse()
	{
		SetBattleReadyIdleFireVisible(visible: false, restartAnimation: false);
		SetCanvasVisible(visible: false);
		if (_transformNode != null && GodotObject.IsInstanceValid(_transformNode))
		{
			ApplyTransformFromSettings(forceTargetPosition: false);
		}
		Node node = _node?.GetParent();
		if (node != null && _node != null)
		{
			node.RemoveChild(_node);
		}
	}

	private PackedScene? GetScene()
	{
		if (_scene != null)
		{
			if (GodotObject.IsInstanceValid(_scene))
			{
				return _scene;
			}
			_scene = null;
			Interlocked.Exchange(ref _sceneLoadAttempted, 0);
		}
		if (Interlocked.Exchange(ref _sceneLoadAttempted, 1) != 0)
		{
			return null;
		}
		long startTimestamp = _context.TimingProbe.StartTimingProbe();
		_scene = ResourceLoader.Load<PackedScene>(_profile.ScenePath, null, ResourceLoader.CacheMode.Reuse);
		_context.TimingProbe.LogTimingProbe("battle_ready_scene_load", startTimestamp);
		if (_scene == null)
		{
			_context.LogWarn("Missing battle ready overlay scene: " + _profile.ScenePath);
		}
		return _scene;
	}

	private Resource? GetSkeletonData()
	{
		if (_skeletonData != null)
		{
			if (GodotObject.IsInstanceValid(_skeletonData))
			{
				return _skeletonData;
			}
			_skeletonData = null;
			Interlocked.Exchange(ref _skeletonLoadAttempted, 0);
		}
		if (Interlocked.Exchange(ref _skeletonLoadAttempted, 1) != 0)
		{
			return null;
		}
		long startTimestamp = _context.TimingProbe.StartTimingProbe();
		_skeletonData = ResourceLoader.Load<Resource>(_profile.SkeletonDataPath, null, ResourceLoader.CacheMode.Reuse);
		_context.TimingProbe.LogTimingProbe("battle_ready_skeleton_load", startTimestamp);
		if (_skeletonData == null)
		{
			_context.LogWarn("Missing battle ready skeleton data: " + _profile.SkeletonDataPath);
		}
		return _skeletonData;
	}

	private PackedScene? GetIdleEffectsScene()
	{
		if (_idleEffectsScene != null)
		{
			if (GodotObject.IsInstanceValid(_idleEffectsScene))
			{
				return _idleEffectsScene;
			}
			_idleEffectsScene = null;
			Interlocked.Exchange(ref _idleEffectsSceneLoadAttempted, 0);
		}
		if (Interlocked.Exchange(ref _idleEffectsSceneLoadAttempted, 1) != 0)
		{
			return null;
		}
		BattleReadyOverlayIdleEffectPlan idleEffect = _profile.IdleEffect;
		if (idleEffect == null || idleEffect.Source.Kind != CombatPresentationEffectSourceKind.Scene || string.IsNullOrEmpty(idleEffect.Source.ScenePath))
		{
			return null;
		}
		long startTimestamp = _context.TimingProbe.StartTimingProbe();
		_idleEffectsScene = ResourceLoader.Load<PackedScene>(idleEffect.Source.ScenePath, null, ResourceLoader.CacheMode.Reuse);
		_context.TimingProbe.LogTimingProbe("battle_ready_idle_effect_scene_load", startTimestamp);
		if (_idleEffectsScene == null)
		{
			_context.LogWarn("Missing battle ready idle fire scene: " + idleEffect.Source.ScenePath);
		}
		return _idleEffectsScene;
	}

	private void ReleaseLoadedResources()
	{
		_scene = null;
		_idleEffectsScene = null;
		_skeletonData = null;
		Interlocked.Exchange(ref _sceneLoadAttempted, 0);
		Interlocked.Exchange(ref _idleEffectsSceneLoadAttempted, 0);
		Interlocked.Exchange(ref _skeletonLoadAttempted, 0);
		_missingAnimationsLogged.Clear();
	}

	private static Node? FindSpineSpriteRecursive(Node root)
	{
		if (root.GetClass() == "SpineSprite")
		{
			return root;
		}
		foreach (Node child in root.GetChildren())
		{
			Node node = FindSpineSpriteRecursive(child);
			if (node != null)
			{
				return node;
			}
		}
		return null;
	}

	private string? GetCardAnimation(TCard card)
	{
		CombatPresentationCardRoute route = _context.ResolveRoute(card);
		if (!_profile.TryGetCardAnimation(route, out string animationName))
		{
			return null;
		}
		return animationName;
	}

	private void InitAnimationCache(MegaSprite sprite)
	{
		_hasInAnim = sprite.HasAnimation(Animations.In);
		_hasIdleAnim = sprite.HasAnimation(Animations.Idle);
		_hasOutAnim = sprite.HasAnimation(Animations.Out);
		_hasAttackAnim = sprite.HasAnimation(Animations.Attack);
		_hasCastingAnim = sprite.HasAnimation(Animations.Casting);
		_lastFirst = null;
		_lastNextLoop = null;
	}

	private bool HasAnimation(string anim)
	{
		if (string.Equals(anim, Animations.In, StringComparison.Ordinal))
		{
			return _hasInAnim;
		}
		if (string.Equals(anim, Animations.Idle, StringComparison.Ordinal))
		{
			return _hasIdleAnim;
		}
		if (string.Equals(anim, Animations.Out, StringComparison.Ordinal))
		{
			return _hasOutAnim;
		}
		if (string.Equals(anim, Animations.Attack, StringComparison.Ordinal))
		{
			return _hasAttackAnim;
		}
		if (string.Equals(anim, Animations.Casting, StringComparison.Ordinal))
		{
			return _hasCastingAnim;
		}
		return _sprite?.HasAnimation(anim) ?? false;
	}

	private void PlaySequence(string first, string nextLoop)
	{
		if (_sprite == null)
		{
			return;
		}
		MegaAnimationState animationState = _sprite.GetAnimationState();
		if (!HasAnimation(first))
		{
			LogMissingAnimation(first);
			if (!HasAnimation(nextLoop))
			{
				LogMissingAnimation(nextLoop);
				return;
			}
			SpineAnimationCompat.SetAnimation(animationState, nextLoop);
			_lastFirst = nextLoop;
			_lastNextLoop = null;
		}
		else if (string.Equals(first, nextLoop, StringComparison.Ordinal))
		{
			if (!string.Equals(_lastFirst, first, StringComparison.Ordinal) || _lastNextLoop != null)
			{
				SpineAnimationCompat.SetAnimation(animationState, first);
				_lastFirst = first;
				_lastNextLoop = null;
			}
		}
		else if (!string.Equals(_lastFirst, first, StringComparison.Ordinal) || !string.Equals(_lastNextLoop, nextLoop, StringComparison.Ordinal))
		{
			SpineAnimationCompat.SetAnimation(animationState, first, loop: false);
			if (HasAnimation(nextLoop))
			{
				SpineAnimationCompat.AddAnimation(animationState, nextLoop);
				_lastNextLoop = nextLoop;
			}
			else
			{
				LogMissingAnimation(nextLoop);
				_lastNextLoop = null;
			}
			_lastFirst = first;
		}
	}

	private bool PlaySingle(string anim)
	{
		if (_sprite == null || !HasAnimation(anim))
		{
			LogMissingAnimation(anim);
			return false;
		}
		SpineAnimationCompat.SetAnimation(_sprite.GetAnimationState(), anim, loop: false);
		_lastFirst = anim;
		_lastNextLoop = null;
		return true;
	}

	private void OnAnimationCompleted(Node createdNode)
	{
		if (_node != createdNode)
		{
			return;
		}
		if (_cardUsePlaying)
		{
			_cardUsePlaying = false;
			if (_profile.CardUseOutDelaySeconds >= 0f)
			{
				BeginOut(_visibilityToken, _profile.CardUseSlideDurationSeconds, allowOutAnimation: false);
			}
			else
			{
				BeginOut(_visibilityToken, _profile.CardUseSlideDurationSeconds);
			}
		}
		else if (_outPlaying)
		{
			_outPlaying = false;
			if (!_isShown)
			{
				SetCanvasVisible(visible: false);
			}
		}
	}

	private void StartSlideIn(int token, bool resetFromHidden)
	{
		if (_transformNode != null && GodotObject.IsInstanceValid(_transformNode))
		{
			_isShown = true;
			if (resetFromHidden)
			{
				_transformNode.Position = HiddenPosition();
			}
			TweenTo(TargetPosition(), hideWhenDone: false, token);
		}
	}

	private void ScheduleOut(float delaySeconds)
	{
		int token = ++_visibilityToken;
		KillTween(ref _hideDelayTween);
		if (_transformNode == null || !GodotObject.IsInstanceValid(_transformNode))
		{
			SetCanvasVisible(visible: false);
			return;
		}
		if (delaySeconds <= 0f)
		{
			BeginOut(token);
			return;
		}
		_hideDelayTween = _transformNode.CreateTween();
		_hideDelayTween.TweenInterval(delaySeconds);
		_hideDelayTween.TweenCallback(Callable.From(() =>
		{
			if (token == _visibilityToken && !_cardUsePlaying)
			{
				BeginOut(token);
			}
		}));
	}

	private void ScheduleCardUseOut(float delaySeconds)
	{
		int token = _visibilityToken;
		KillTween(ref _hideDelayTween);
		if (delaySeconds <= 0f)
		{
			BeginCardUseOut(token);
			return;
		}
		if (_transformNode == null || !GodotObject.IsInstanceValid(_transformNode))
		{
			SetCanvasVisible(visible: false);
			return;
		}
		_hideDelayTween = _transformNode.CreateTween();
		_hideDelayTween.TweenInterval(delaySeconds);
		_hideDelayTween.TweenCallback(Callable.From(() =>
		{
			BeginCardUseOut(token);
		}));
	}

	private void BeginCardUseOut(int token)
	{
		if (token == _visibilityToken && _cardUsePlaying)
		{
			_cardUsePlaying = false;
			BeginOut(token, _profile.CardUseSlideDurationSeconds, allowOutAnimation: false);
		}
	}

	private void BeginOut(int token, float? slideDurationSeconds = null, bool allowOutAnimation = true)
	{
		if (token != _visibilityToken)
		{
			return;
		}
		_isShown = false;
		KillTween(ref _moveTween);
		if (allowOutAnimation && _hasOutAnim && _sprite != null)
		{
			_outPlaying = true;
			if (PlaySingle(Animations.Out))
			{
				return;
			}
			_outPlaying = false;
		}
		TweenTo(HiddenPosition(), hideWhenDone: true, token, slideDurationSeconds);
	}

	private void TweenTo(Vector2 targetPosition, bool hideWhenDone, int token, float? durationSeconds = null)
	{
		if (_transformNode == null || !GodotObject.IsInstanceValid(_transformNode))
		{
			return;
		}
		float num = Math.Max(0f, durationSeconds ?? _profile.SlideDurationSeconds);
		KillTween(ref _moveTween);
		if (num <= 0.001f)
		{
			_transformNode.Position = targetPosition;
			if (hideWhenDone && token == _visibilityToken && !_isShown && !_cardUsePlaying)
			{
				SetCanvasVisible(visible: false);
			}
			return;
		}
		_moveTween = _transformNode.CreateTween();
		_moveTween.SetTrans(Tween.TransitionType.Cubic);
		_moveTween.SetEase((Tween.EaseType)(hideWhenDone ? 2 : 1));
		_moveTween.TweenProperty(_transformNode, "position", targetPosition, num);
		if (!hideWhenDone)
		{
			return;
		}
		_moveTween.TweenCallback(Callable.From(() =>
		{
			if (token == _visibilityToken && !_isShown && !_cardUsePlaying)
			{
				SetCanvasVisible(visible: false);
			}
		}));
	}

	private Vector2 TargetPosition()
	{
		return _basePosition + new Vector2(_context.BattleReadyOffsetX, _context.BattleReadyOffsetY);
	}

	private Vector2 HiddenPosition()
	{
		return TargetPosition() + new Vector2(0f - _profile.SlideOffsetX, 0f);
	}

	private bool IsCanvasVisible()
	{
		if (_node is CanvasItem canvasItem)
		{
			return canvasItem.Visible;
		}
		return false;
	}

	private void SetCanvasVisible(bool visible)
	{
		if (_node is CanvasItem canvasItem && GodotObject.IsInstanceValid(canvasItem))
		{
			canvasItem.Visible = visible;
			if (!visible)
			{
				SetBattleReadyIdleFireVisible(visible: false, restartAnimation: false);
			}
		}
	}

	private static void KillTween(ref Tween? tween)
	{
		try
		{
			if (tween != null && GodotObject.IsInstanceValid(tween))
			{
				tween.Kill();
			}
		}
		catch
		{
		}
		tween = null;
	}

	private void AttachBattleReadyIdleEffects(Node overlayRoot, Node spineNode)
	{
		if ((_idleEffectsBackRoot != null && GodotObject.IsInstanceValid(_idleEffectsBackRoot)) || (_idleEffectsFrontRoot != null && GodotObject.IsInstanceValid(_idleEffectsFrontRoot)))
		{
			return;
		}
		PackedScene idleEffectsScene = GetIdleEffectsScene();
		BattleReadyOverlayIdleEffectPlan idleEffect = _profile.IdleEffect;
		if (idleEffectsScene == null || idleEffect == null)
		{
			return;
		}
		Node node = idleEffectsScene.Instantiate(PackedScene.GenEditState.Disabled);
		if (!(node is Node2D node2D))
		{
			_context.LogWarn("Battle ready idle fire scene root is not Node2D.");
			node.QueueFree();
			return;
		}
		Node2D node2D2 = _context.DetachIdleEffectLayer(node2D, "Back");
		Node2D node2D3 = _context.DetachIdleEffectLayer(node2D, "Front");
		node2D.QueueFree();
		if (node2D2 == null && node2D3 == null)
		{
			_context.LogWarn("Battle ready idle fire scene has no Spine layers.");
			return;
		}
		if (node2D2 != null)
		{
			_idleEffectsBackRoot = AttachBattleReadyIdleEffectLayer(overlayRoot, spineNode, node2D2, "Back", idleEffect, behindParent: true);
		}
		if (node2D3 != null)
		{
			_idleEffectsFrontRoot = AttachBattleReadyIdleEffectLayer(overlayRoot, spineNode, node2D3, "Front", idleEffect, behindParent: false);
		}
		_context.LogInfo(string.Concat(new string[10]
		{
			"Attached battle ready idle fire overlayRoot=",
			overlayRoot.Name,
			" spine=",
			spineNode.Name,
			" layout=",
			(node2D2 == null) ? "front_only" : ((node2D3 == null) ? "back_only" : "back_front"),
			" position=",
			GetBattleReadyIdleEffectRootPosition(idleEffect).ToString(),
			" scale=",
			idleEffect.RootScale.ToString()
		}));
	}

	private Node2D AttachBattleReadyIdleEffectLayer(Node overlayRoot, Node spineNode, Node2D layer, string layerName, BattleReadyOverlayIdleEffectPlan idleEffect, bool behindParent)
	{
		layer.Position = Vector2.Zero;
		layer.Scale = Vector2.One;
		layer.Rotation = 0f;
		layer.TopLevel = false;
		layer.ZAsRelative = true;
		layer.ZIndex = 0;
		layer.ShowBehindParent = false;
		Node battleReadyIdleEffectParent = GetBattleReadyIdleEffectParent(overlayRoot, spineNode, idleEffect, layerName);
		bool flag = battleReadyIdleEffectParent == spineNode;
		int num = ((spineNode is CanvasItem canvasItem) ? canvasItem.ZIndex : 0);
		Node2D node2D = new Node2D
		{
			Name = _profile.RuntimeNodeName("IdleFire") + layerName,
			Position = GetBattleReadyIdleEffectRootPosition(idleEffect),
			Scale = Vector2.One * idleEffect.RootScale,
			Rotation = 0f,
			TopLevel = false,
			ZAsRelative = true,
			ZIndex = ((!flag) ? (num + ((!behindParent) ? 1 : (-1))) : ((!behindParent) ? 1 : (-1))),
			ShowBehindParent = (flag & behindParent),
			Visible = false
		};
		if (spineNode is CanvasItem canvasItem2)
		{
			node2D.VisibilityLayer = canvasItem2.VisibilityLayer;
			node2D.LightMask = canvasItem2.LightMask;
		}
		battleReadyIdleEffectParent.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
		node2D.AddChild(layer, forceReadableName: false, Node.InternalMode.Disabled);
		_context.ApplyIdleEffectMaterial(node2D);
		_context.ApplyIdleEffectMaterialDeferred(node2D, 4);
		return node2D;
	}

	private Node GetBattleReadyIdleEffectParent(Node overlayRoot, Node spineNode, BattleReadyOverlayIdleEffectPlan idleEffect, string layerName)
	{
		if (!string.IsNullOrWhiteSpace(idleEffect.AttachBoneName) && spineNode is Node2D)
		{
			Node node = ClassDB.Instantiate("SpineBoneNode").AsGodotObject() as Node;
			if (node is Node2D node2D)
			{
				node2D.Name = _profile.RuntimeNodeName("IdleEffectBone") + layerName;
				node2D.Set("bone_name", idleEffect.AttachBoneName);
				spineNode.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
				return node2D;
			}
			node?.Free();
			_context.LogWarn("Battle ready idle effect bone attachment is unavailable: " + idleEffect.AttachBoneName);
		}
		if (spineNode is Node2D)
		{
			return spineNode;
		}
		if (overlayRoot is Node2D)
		{
			return overlayRoot;
		}
		return spineNode.GetParent() ?? spineNode;
	}

	private static Vector2 GetBattleReadyIdleEffectRootPosition(BattleReadyOverlayIdleEffectPlan idleEffect)
	{
		return new Vector2(idleEffect.RootPositionX, idleEffect.RootPositionY);
	}

	private void SetBattleReadyIdleFireVisible(bool visible, bool restartAnimation)
	{
		bool flag = false;
		foreach (Node2D item in BattleReadyIdleFireRoots())
		{
			item.Visible = visible;
			if (!visible)
			{
				CombatEffectRuntime.StopParticles(item);
			}
			flag = true;
		}
		if (!flag || !(visible & restartAnimation))
		{
			return;
		}
		foreach (Node2D item2 in BattleReadyIdleFireRoots())
		{
			_context.StartIdleEffects(item2);
		}
	}

	private bool IsBattleReadyIdleFireVisible()
	{
		foreach (Node2D item in BattleReadyIdleFireRoots())
		{
			if (item.Visible)
			{
				return true;
			}
		}
		return false;
	}

	private IEnumerable<Node2D> BattleReadyIdleFireRoots()
	{
		if (_idleEffectsBackRoot != null && GodotObject.IsInstanceValid(_idleEffectsBackRoot))
		{
			yield return _idleEffectsBackRoot;
		}
		if (_idleEffectsFrontRoot != null && GodotObject.IsInstanceValid(_idleEffectsFrontRoot))
		{
			yield return _idleEffectsFrontRoot;
		}
	}

	private void LogMissingAnimation(string anim)
	{
		if (_missingAnimationsLogged.Add(anim))
		{
			_context.LogWarn("Battle ready overlay missing animation: " + anim);
		}
	}
}
