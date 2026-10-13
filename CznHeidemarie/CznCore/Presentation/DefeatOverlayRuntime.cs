using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznCore.Presentation;

internal sealed class DefeatOverlayRuntime
{
	private readonly Func<string, string> _runtimeNodeName;

	private readonly DefeatPresentationProfile _profile;

	private readonly IDefeatOverlayRuntimeContext _context;

	private readonly Dictionary<string, Texture2D> _cgTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	public DefeatOverlayRuntime(CharacterSkinProfile skinProfile, DefeatPresentationProfile profile, IDefeatOverlayRuntimeContext context)
		: this(RequireRuntimeNodeName(skinProfile), profile, context)
	{
	}

	private static Func<string, string> RequireRuntimeNodeName(CharacterSkinProfile? skinProfile)
	{
		ArgumentNullException.ThrowIfNull(skinProfile, "skinProfile");
		return skinProfile.RuntimeNodeName;
	}

	public DefeatOverlayRuntime(Func<string, string> runtimeNodeName, DefeatPresentationProfile profile, IDefeatOverlayRuntimeContext context)
	{
		_runtimeNodeName = runtimeNodeName ?? throw new ArgumentNullException("runtimeNodeName");
		_profile = profile ?? throw new ArgumentNullException("profile");
		_context = context ?? throw new ArgumentNullException("context");
	}

	public void Prewarm()
	{
		DefeatCgOverlayMode overlayMode = _context.OverlayMode;
		if (!_context.IsEnabled || overlayMode == DefeatCgOverlayMode.Off)
		{
			return;
		}
		foreach (DefeatSfxCue effectiveSfxCue in _profile.EffectiveSfxCues)
		{
			if (!string.IsNullOrWhiteSpace(effectiveSfxCue.Path))
			{
				_context.SfxPlayer.Prewarm(effectiveSfxCue.Path);
			}
		}
		foreach (string item in ResolvePrewarmCgTexturePaths(overlayMode))
		{
			GetDefeatCgTexture(item);
		}
		foreach (DefeatFrameSequenceLayerProfile effectiveFrameSequenceLayer in _profile.EffectiveFrameSequenceLayers)
		{
			foreach (string framePath in effectiveFrameSequenceLayer.FramePaths)
			{
				GetDefeatCgTexture(framePath);
			}
		}
	}

	public void Play(float startDelaySeconds)
	{
		DefeatCgOverlayMode overlayMode = _context.OverlayMode;
		if (!_context.IsEnabled || overlayMode == DefeatCgOverlayMode.Off)
		{
			return;
		}
		try
		{
			Viewport overlayViewport = GetOverlayViewport();
			if (overlayViewport == null)
			{
				_context.LogWarn("Defeat overlay skipped: no viewport.");
				return;
			}
			var (texturePath, textureScaleAdapter, selectedMode) = ResolveCgTextureSelection(overlayMode);
			PlayTextureOverlay(overlayViewport, GetDefeatCgTexture(texturePath), textureScaleAdapter, selectedMode, startDelaySeconds);
		}
		catch (Exception ex)
		{
			_context.LogWarn("Defeat overlay failed: " + SkinLog.FormatException(ex));
		}
	}

	private static Viewport? GetOverlayViewport()
	{
		Viewport viewport = NCombatRoom.Instance?.GetViewport();
		if (viewport != null && GodotObject.IsInstanceValid(viewport))
		{
			return viewport;
		}
		if (Engine.GetMainLoop() is SceneTree sceneTree && GodotObject.IsInstanceValid(sceneTree.Root))
		{
			return sceneTree.Root;
		}
		return null;
	}

	private void PlayTextureOverlay(Viewport viewport, Texture2D? texture, float textureScaleAdapter, DefeatCgOverlayMode selectedMode, float startDelaySeconds)
	{
		Node parent = viewport;
		if (startDelaySeconds <= 0.01f)
		{
			ShowOverlay();
		}
		else
		{
			RunNowOrOneShot(parent, startDelaySeconds, ShowOverlay);
		}
		void ShowOverlay()
		{
			if (GodotObject.IsInstanceValid(parent) && GodotObject.IsInstanceValid(viewport))
			{
				CanvasLayer layer = new CanvasLayer
				{
					Name = _runtimeNodeName("BattleDeadOverlayLayer"),
					Layer = 1000
				};
				parent.AddChild(layer, forceReadableName: false, Node.InternalMode.Disabled);
				Vector2 size = viewport.GetVisibleRect().Size;
				Vector2 position = size / 2f;
				float overlayLifetimeSeconds = _profile.OverlayLifetimeSeconds;
				ColorRect colorRect = new ColorRect
				{
					Name = _runtimeNodeName("BattleDeadDim"),
					Color = new Color(0f, 0f, 0f, 0f),
					Size = size,
					MouseFilter = Control.MouseFilterEnum.Ignore,
					ZIndex = 0,
					ZAsRelative = false
				};
				layer.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
				if (_profile.DimAlpha > 0.001f && _profile.DimFadeSeconds > 0.001f)
				{
					Tween tween = colorRect.CreateTween();
					tween.SetEase(Tween.EaseType.Out);
					tween.SetTrans(Tween.TransitionType.Sine);
					tween.TweenProperty(colorRect, "color", new Color(0f, 0f, 0f, _profile.DimAlpha), _profile.DimFadeSeconds);
				}
				else
				{
					colorRect.Color = new Color(0f, 0f, 0f, _profile.DimAlpha);
				}
				Node2D node2D = CreateEffectRoot();
				if (node2D != null)
				{
					node2D.Position = position;
					if (_profile.EffectReferenceHeight > 0.001f)
					{
						node2D.Scale = Vector2.One * (size.Y / _profile.EffectReferenceHeight);
					}
					layer.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
					StartEffectLayers(node2D, layer);
				}
				List<Sprite2D> cgSprites = new List<Sprite2D>();
				cgSprites.AddRange(CreateFrameSequenceSprites(size, layer));
				if (texture != null)
				{
					cgSprites.AddRange(CreateCollapseCgSprites(texture, size, textureScaleAdapter, selectedMode, layer));
				}
				StartCollapseSfx(layer);
				GodotScheduler.OneShot(layer, overlayLifetimeSeconds, () =>
				{
					foreach (Sprite2D item in cgSprites)
					{
						if (GodotObject.IsInstanceValid(item))
						{
							item.Visible = false;
						}
					}
				});
				GodotScheduler.OneShot(layer, overlayLifetimeSeconds + 0.05f, () =>
				{
					if (GodotObject.IsInstanceValid(layer))
					{
						layer.QueueFree();
					}
				});
			}
		}
	}

	private IReadOnlyList<Sprite2D> CreateCollapseCgSprites(Texture2D texture, Vector2 viewportSize, float textureScaleAdapter, DefeatCgOverlayMode selectedMode, Node timerParent)
	{
		List<Sprite2D> list = new List<Sprite2D>();
		Vector2 size = texture.GetSize();
		if (size.X <= 0f || size.Y <= 0f)
		{
			return list;
		}
		float num = MathF.Min(viewportSize.X / size.X, viewportSize.Y / size.Y) * MathF.Max(0.001f, textureScaleAdapter);
		foreach (DefeatCgMotionLayerProfile item in DefeatCgFraming.Reframe((selectedMode == DefeatCgOverlayMode.Type2) ? _profile.EffectiveCgType2MotionLayers : _profile.EffectiveCgMotionLayers, viewportSize, size, DefeatCgFraming.ContentRect(texture), num * _profile.RuntimeCgScaleAdapter))
		{
			Sprite2D sprite2D = new Sprite2D
			{
				Name = _runtimeNodeName("BattleDeadCg" + item.Name),
				Texture = texture,
				Centered = true,
				Visible = false,
				ZIndex = item.ZIndex,
				ZAsRelative = false
			};
			if (item.Additive)
			{
				sprite2D.Material = CreateAdditiveCanvasMaterial();
			}
			timerParent.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
			if (item.Keyframes.Count > 0)
			{
				ApplyCgMotionKey(sprite2D, viewportSize, num, item, item.Keyframes[0]);
			}
			StartCgMotion(sprite2D, viewportSize, num, item, timerParent);
			list.Add(sprite2D);
		}
		return list;
	}

	private IReadOnlyList<Sprite2D> CreateFrameSequenceSprites(Vector2 viewportSize, Node timerParent)
	{
		List<Sprite2D> list = new List<Sprite2D>();
		foreach (DefeatFrameSequenceLayerProfile effectiveFrameSequenceLayer in _profile.EffectiveFrameSequenceLayers)
		{
			if (effectiveFrameSequenceLayer.FramePaths.Count == 0)
			{
				continue;
			}
			Texture2D firstFrameSequenceTexture = GetFirstFrameSequenceTexture(effectiveFrameSequenceLayer);
			if (firstFrameSequenceTexture == null)
			{
				continue;
			}
			Vector2 size = firstFrameSequenceTexture.GetSize();
			if (!(size.X <= 0f) && !(size.Y <= 0f))
			{
				float num = MathF.Max(viewportSize.X / size.X, viewportSize.Y / size.Y) * MathF.Max(0.001f, effectiveFrameSequenceLayer.RuntimeScaleAdapter);
				Sprite2D sprite2D = new Sprite2D
				{
					Name = _runtimeNodeName("BattleDeadFrameSequence" + effectiveFrameSequenceLayer.Name),
					Texture = firstFrameSequenceTexture,
					Centered = true,
					Position = viewportSize / 2f,
					Scale = Vector2.One * num,
					Visible = false,
					ZIndex = effectiveFrameSequenceLayer.ZIndex,
					ZAsRelative = false
				};
				if (effectiveFrameSequenceLayer.Additive)
				{
					sprite2D.Material = CreateAdditiveCanvasMaterial();
				}
				timerParent.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
				StartFrameSequence(sprite2D, effectiveFrameSequenceLayer, timerParent);
				list.Add(sprite2D);
			}
		}
		return list;
	}

	private Texture2D? GetFirstFrameSequenceTexture(DefeatFrameSequenceLayerProfile spec)
	{
		foreach (string framePath in spec.FramePaths)
		{
			Texture2D defeatCgTexture = GetDefeatCgTexture(framePath);
			if (defeatCgTexture != null && GodotObject.IsInstanceValid(defeatCgTexture))
			{
				return defeatCgTexture;
			}
		}
		return null;
	}

	private void StartFrameSequence(Sprite2D sprite, DefeatFrameSequenceLayerProfile spec, Node timerParent)
	{
		float num = MathF.Max(0.001f, spec.FrameSeconds);
		float num2 = ((spec.VisibleSeconds > 0.001f) ? spec.VisibleSeconds : ((float)spec.FramePaths.Count * num));
		float num3 = spec.StartDelaySeconds + num2;
		for (int i = 0; i < spec.FramePaths.Count; i++)
		{
			float num4 = spec.StartDelaySeconds + (float)i * num;
			if (num4 > num3)
			{
				break;
			}
			string framePath = spec.FramePaths[i];
			RunNowOrOneShot(timerParent, num4, () =>
			{
				if (GodotObject.IsInstanceValid(sprite))
				{
					Texture2D defeatCgTexture = GetDefeatCgTexture(framePath);
					if (defeatCgTexture != null && GodotObject.IsInstanceValid(defeatCgTexture))
					{
						sprite.Texture = defeatCgTexture;
					}
					sprite.Modulate = new Color(1f, 1f, 1f, Math.Clamp(spec.Alpha, 0f, 1f));
					sprite.Visible = true;
				}
			});
		}
		GodotScheduler.OneShot(timerParent, MathF.Max(0.05f, num3), () =>
		{
			if (GodotObject.IsInstanceValid(sprite))
			{
				sprite.Visible = false;
			}
		});
	}

	private void StartCgMotion(Sprite2D sprite, Vector2 viewportSize, float baseScale, DefeatCgMotionLayerProfile spec, Node timerParent)
	{
		float num = MathF.Max(0.001f, _profile.CgMotionSeconds);
		for (int i = 0; i < spec.Keyframes.Count; i++)
		{
			DefeatCgMotionKeyProfile key = spec.Keyframes[i];
			float delaySeconds = _profile.CgIntroDelaySeconds + key.Offset * num;
			RunNowOrOneShot(timerParent, delaySeconds, () =>
			{
				if (GodotObject.IsInstanceValid(sprite))
				{
					ApplyCgMotionKey(sprite, viewportSize, baseScale, spec, key);
				}
			});
			if (i >= spec.Keyframes.Count - 1)
			{
				continue;
			}
			DefeatCgMotionKeyProfile nextKey = spec.Keyframes[i + 1];
			float durationSeconds = (nextKey.Offset - key.Offset) * num;
			if (durationSeconds <= 0.001f)
			{
				continue;
			}
			RunNowOrOneShot(timerParent, delaySeconds, () =>
			{
				if (GodotObject.IsInstanceValid(sprite))
				{
					TweenCgMotionKey(sprite, viewportSize, baseScale, spec, nextKey, durationSeconds);
				}
			});
		}
	}

	private void ApplyCgMotionKey(Sprite2D sprite, Vector2 viewportSize, float baseScale, DefeatCgMotionLayerProfile spec, DefeatCgMotionKeyProfile key)
	{
		sprite.Position = new Vector2(viewportSize.X * (0.5f + key.OffsetXRatio), viewportSize.Y * (0.5f + key.OffsetYRatio));
		sprite.Scale = Vector2.One * baseScale * key.ScaleMultiplier * _profile.RuntimeCgScaleAdapter;
		sprite.Modulate = new Color(spec.Tint.R, spec.Tint.G, spec.Tint.B, key.Alpha * spec.Tint.A);
		sprite.Visible = key.Alpha > 0.001f;
	}

	private void TweenCgMotionKey(Sprite2D sprite, Vector2 viewportSize, float baseScale, DefeatCgMotionLayerProfile spec, DefeatCgMotionKeyProfile key, float durationSeconds)
	{
		Vector2 vector = new Vector2(viewportSize.X * (0.5f + key.OffsetXRatio), viewportSize.Y * (0.5f + key.OffsetYRatio));
		Vector2 vector2 = Vector2.One * baseScale * key.ScaleMultiplier * _profile.RuntimeCgScaleAdapter;
		Color color = new Color(spec.Tint.R, spec.Tint.G, spec.Tint.B, key.Alpha * spec.Tint.A);
		sprite.Visible = sprite.Visible || key.Alpha > 0.001f;
		Tween tween = sprite.CreateTween();
		tween.SetParallel();
		tween.TweenProperty(sprite, "position", vector, durationSeconds);
		tween.TweenProperty(sprite, "scale", vector2, durationSeconds);
		tween.TweenProperty(sprite, "modulate", color, durationSeconds);
	}

	private static CanvasItemMaterial CreateAdditiveCanvasMaterial()
	{
		CanvasItemMaterial canvasItemMaterial = new CanvasItemMaterial();
		canvasItemMaterial.Set("blend_mode", Variant.From<int>(1));
		canvasItemMaterial.Set("light_mode", Variant.From<int>(0));
		return canvasItemMaterial;
	}

	private void StartCollapseSfx(Node parent)
	{
		foreach (DefeatSfxCue cue in _profile.EffectiveSfxCues)
		{
			if (string.IsNullOrWhiteSpace(cue.Path))
			{
				continue;
			}
			RunNowOrOneShot(parent, MathF.Max(0f, cue.DelaySeconds), () =>
			{
				if (GodotObject.IsInstanceValid(parent))
				{
					PlayDefeatSfx(cue);
				}
			});
		}
	}

	private void PlayDefeatSfx(DefeatSfxCue cue)
	{
		try
		{
			if (!_context.SfxPlayer.PlayVolumeDb(cue.Path, cue.VolumeDb))
			{
				_context.LogWarn("Missing defeat sfx " + cue.Name + ": " + cue.Path);
			}
		}
		catch (Exception ex)
		{
			_context.LogWarn("Defeat sfx failed " + cue.Name + ": " + SkinLog.FormatException(ex));
		}
	}

	private void StartEffectLayers(Node2D effectRoot, Node timerParent)
	{
		RunNowOrOneShot(timerParent, MathF.Max(0f, _profile.EffectIntroDelaySeconds), () =>
		{
			if (GodotObject.IsInstanceValid(effectRoot))
			{
				PlayEffectLayers(effectRoot, timerParent);
			}
		});
	}

	private void PlayEffectLayers(Node root, Node timerParent)
	{
		foreach (Node child in root.GetChildren())
		{
			CanvasItem canvas = child as CanvasItem;
			if (canvas == null)
			{
				continue;
			}
			DefeatEffectLayerProfile defeatEffectLayerProfile = null;
			foreach (DefeatEffectLayerProfile effectLayer in _profile.EffectLayers)
			{
				if ((StringName)effectLayer.Name == child.Name)
				{
					defeatEffectLayerProfile = effectLayer;
					break;
				}
			}
			if (defeatEffectLayerProfile == null)
			{
				continue;
			}
			canvas.Visible = true;
			if (child.GetClass() == "SpineSprite")
			{
				try
				{
					SpineAnimationCompat.SetAnimation(new MegaSprite((Node2D)child).GetAnimationState(), "animation", loop: false);
				}
				catch (Exception ex)
				{
					_context.LogWarn("Defeat effect layer animation failed: " + defeatEffectLayerProfile.Name + " " + SkinLog.FormatException(ex));
				}
			}
			GodotScheduler.OneShot(timerParent, MathF.Max(0.05f, defeatEffectLayerProfile.VisibleSeconds), () =>
			{
				if (GodotObject.IsInstanceValid(canvas))
				{
					canvas.Visible = false;
				}
			});
		}
	}

	private static void RunNowOrOneShot(Node parent, float delaySeconds, Action action)
	{
		if (delaySeconds <= 0.001f)
		{
			action();
		}
		else
		{
			GodotScheduler.OneShot(parent, delaySeconds, action);
		}
	}

	private Node2D? CreateEffectRoot()
	{
		try
		{
			Node2D node2D = new Node2D
			{
				Name = _runtimeNodeName("BattleDeadEffectRoot")
			};
			int num = 0;
			foreach (DefeatEffectLayerProfile effectLayer in _profile.EffectLayers)
			{
				Node node = CreateEffectLayerNode(effectLayer);
				if (node != null)
				{
					if (node is CanvasItem canvasItem)
					{
						canvasItem.Visible = false;
					}
					node2D.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
					num++;
				}
			}
			if (num == 0)
			{
				node2D.QueueFree();
				return null;
			}
			_context.ApplyEffectMaterial(node2D);
			_context.ApplyEffectMaterialDeferred(node2D, 4);
			return node2D;
		}
		catch (Exception ex)
		{
			_context.LogWarn("Defeat effect root instantiate failed: " + SkinLog.FormatException(ex));
			return null;
		}
	}

	private Node? CreateEffectLayerNode(DefeatEffectLayerProfile spec)
	{
		Node node = CreateEffectLayerFromSkeletonData(spec);
		if (node != null)
		{
			return node;
		}
		Node node2 = CreateEffectLayerFromRawFiles(spec);
		if (node2 != null)
		{
			return node2;
		}
		return CreateEffectFallbackSprite(spec);
	}

	private Node? CreateEffectLayerFromSkeletonData(DefeatEffectLayerProfile spec)
	{
		if (string.IsNullOrWhiteSpace(spec.SkeletonDataPath))
		{
			return null;
		}
		try
		{
			Resource resource = ResourceLoader.Load<Resource>(spec.SkeletonDataPath, null, ResourceLoader.CacheMode.Ignore);
			Node node = ClassDB.Instantiate("SpineSprite").AsGodotObject() as Node;
			if (resource == null || node == null)
			{
				return null;
			}
			node.Name = spec.Name;
			StringName property = "skeleton_data_res";
			GodotObject from = resource;
			node.Set(property, Variant.From(in from));
			ConfigureEffectLayerNode(node, spec);
			return node;
		}
		catch (Exception ex)
		{
			_context.LogWarn("Defeat effect layer skeleton data load failed: " + spec.Name + " " + SkinLog.FormatException(ex));
			return null;
		}
	}

	private Node? CreateEffectLayerFromRawFiles(DefeatEffectLayerProfile spec)
	{
		if (string.IsNullOrWhiteSpace(spec.AtlasPath) || string.IsNullOrWhiteSpace(spec.SkeletonPath))
		{
			return null;
		}
		try
		{
			GodotObject from = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
			GodotObject from2 = ClassDB.Instantiate("SpineSkeletonFileResource").AsGodotObject();
			GodotObject from3 = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
			Node node = ClassDB.Instantiate("SpineSprite").AsGodotObject() as Node;
			if (from == null || from2 == null || from3 == null || node == null)
			{
				return null;
			}
			Variant variant = from.Call("load_from_atlas_file", spec.AtlasPath);
			Variant variant2 = from2.Call("load_from_file", spec.SkeletonPath);
			if (variant.AsInt32() != 0 || variant2.AsInt32() != 0)
			{
				_context.LogWarn("Defeat effect layer load failed: " + spec.Name + " atlasErr=" + variant.AsInt32() + " skelErr=" + variant2.AsInt32());
				return null;
			}
			from3.Set("atlas_res", Variant.From(in from));
			from3.Set("skeleton_file_res", Variant.From(in from2));
			node.Name = spec.Name;
			node.Set("skeleton_data_res", Variant.From(in from3));
			ConfigureEffectLayerNode(node, spec);
			return node;
		}
		catch (Exception ex)
		{
			_context.LogWarn("Defeat effect layer instantiate failed: " + spec.Name + " " + SkinLog.FormatException(ex));
			return null;
		}
	}

	private Node? CreateEffectFallbackSprite(DefeatEffectLayerProfile spec)
	{
		if (string.IsNullOrWhiteSpace(spec.PngPath))
		{
			return null;
		}
		try
		{
			Image image = Image.LoadFromFile(spec.PngPath);
			if (image == null || image.IsEmpty())
			{
				return null;
			}
			Sprite2D sprite2D = new Sprite2D
			{
				Name = spec.Name,
				Texture = ImageTexture.CreateFromImage(image),
				Centered = true,
				Scale = Vector2.One * spec.FallbackScale
			};
			ConfigureEffectLayerNode(sprite2D, spec);
			_context.LogWarn("Defeat effect layer using PNG fallback: " + spec.Name);
			return sprite2D;
		}
		catch (Exception ex)
		{
			_context.LogWarn("Defeat effect layer PNG fallback failed: " + spec.Name + " " + SkinLog.FormatException(ex));
			return null;
		}
	}

	private void ConfigureEffectLayerNode(Node node, DefeatEffectLayerProfile spec)
	{
		node.Set("z_index", Variant.From<int>(spec.ZIndex));
		node.Set("z_as_relative", Variant.From<bool>(false));
		if (node is Node2D node2D && MathF.Abs(spec.RuntimeScale - 1f) > 0.001f)
		{
			node2D.Scale *= spec.RuntimeScale;
		}
		if (node is CanvasItem canvasItem)
		{
			canvasItem.SelfModulate = new Color(1f, 1f, 1f, spec.Alpha);
		}
		_context.ApplyNodeMaterial(node);
	}

	private IEnumerable<string> ResolvePrewarmCgTexturePaths(DefeatCgOverlayMode mode)
	{
		switch (mode)
		{
		case DefeatCgOverlayMode.Type1:
			yield return _profile.CgTexturePath;
			break;
		case DefeatCgOverlayMode.Type2:
			yield return ResolveType2TexturePath();
			break;
		case DefeatCgOverlayMode.Random:
		{
			yield return _profile.CgTexturePath;
			string text = ResolveType2TexturePath();
			if (!string.Equals(text, _profile.CgTexturePath, StringComparison.Ordinal))
			{
				yield return text;
			}
			break;
		}
		}
	}

	private (string? TexturePath, float ScaleAdapter, DefeatCgOverlayMode SelectedMode) ResolveCgTextureSelection(DefeatCgOverlayMode mode)
	{
		return mode switch
		{
			DefeatCgOverlayMode.Off => (TexturePath: null, ScaleAdapter: 1f, SelectedMode: DefeatCgOverlayMode.Off), 
			DefeatCgOverlayMode.Type2 => (TexturePath: ResolveType2TexturePath(), ScaleAdapter: ResolveType2ScaleAdapter(), SelectedMode: DefeatCgOverlayMode.Type2), 
			DefeatCgOverlayMode.Random => (Random.Shared.Next(2) == 0) ? (TexturePath: _profile.CgTexturePath, ScaleAdapter: _profile.CgType1ScaleAdapter, SelectedMode: DefeatCgOverlayMode.Type1) : (TexturePath: ResolveType2TexturePath(), ScaleAdapter: ResolveType2ScaleAdapter(), SelectedMode: DefeatCgOverlayMode.Type2), 
			_ => (TexturePath: _profile.CgTexturePath, ScaleAdapter: _profile.CgType1ScaleAdapter, SelectedMode: DefeatCgOverlayMode.Type1), 
		};
	}

	private string ResolveType2TexturePath()
	{
		if (!string.IsNullOrWhiteSpace(_profile.CgType2TexturePath))
		{
			return _profile.CgType2TexturePath;
		}
		return _profile.CgTexturePath;
	}

	private float ResolveType2ScaleAdapter()
	{
		if (!string.IsNullOrWhiteSpace(_profile.CgType2TexturePath))
		{
			return _profile.CgType2ScaleAdapter;
		}
		return _profile.CgType1ScaleAdapter;
	}

	private Texture2D? GetDefeatCgTexture(string? texturePath)
	{
		if (string.IsNullOrWhiteSpace(texturePath))
		{
			return null;
		}
		if (_cgTextures.TryGetValue(texturePath, out Texture2D value) && GodotObject.IsInstanceValid(value))
		{
			return value;
		}
		Texture2D texture2D = LoadTexture2D(texturePath);
		if (texture2D != null && GodotObject.IsInstanceValid(texture2D))
		{
			_cgTextures[texturePath] = texture2D;
		}
		return texture2D;
	}

	private static Texture2D? LoadTexture2D(string texturePath)
	{
		if (string.IsNullOrWhiteSpace(texturePath))
		{
			return null;
		}
		if (IsRawImagePath(texturePath))
		{
			Texture2D texture2D = LoadImageTexture2D(texturePath);
			if (texture2D != null && GodotObject.IsInstanceValid(texture2D))
			{
				return texture2D;
			}
		}
		try
		{
			return ResourceLoader.Load<Texture2D>(texturePath, null, ResourceLoader.CacheMode.Reuse);
		}
		catch
		{
		}
		if (!IsRawImagePath(texturePath))
		{
			return LoadImageTexture2D(texturePath);
		}
		return null;
	}

	private static bool IsRawImagePath(string texturePath)
	{
		if (!texturePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !texturePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !texturePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
		{
			return texturePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static Texture2D? LoadImageTexture2D(string texturePath)
	{
		try
		{
			if (!global::Godot.FileAccess.FileExists(texturePath))
			{
				return null;
			}
			Image image = Image.LoadFromFile(texturePath);
			if (image == null || image.IsEmpty())
			{
				return null;
			}
			return ImageTexture.CreateFromImage(image);
		}
		catch
		{
			return null;
		}
	}
}
