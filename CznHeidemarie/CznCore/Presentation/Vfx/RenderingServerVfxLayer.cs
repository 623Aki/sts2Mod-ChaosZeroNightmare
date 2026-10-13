using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace CznCore.Presentation.Vfx;

internal sealed partial class RenderingServerVfxLayer : Node2D
{
	private readonly List<VfxInstance> _pool = new List<VfxInstance>();

	private readonly List<VfxInstance> _drawScratch = new List<VfxInstance>();

	private static readonly Comparison<VfxInstance> SortByOrder = (VfxInstance a, VfxInstance b) => a.SortingOrder.CompareTo(b.SortingOrder);

	private Rid _normalCanvasItem;

	private Rid _additiveCanvasItem;

	private CanvasItemMaterial? _additiveMaterial;

	private bool _canvasReady;

	private bool _clearedWhileEmpty;

	private int _capacity;

	private bool _poolExhaustedWarned;

	private readonly Stopwatch _frameStopwatch = new Stopwatch();

	private int _peakActive;

	private double _lastUpdateMs;

	private double _peakUpdateMs;

	private double _statsAccum;

	public bool Enabled { get; set; } = true;

	public int PrewarmCount { get; set; } = 128;

	public int MaxCapacity { get; set; } = 512;

	public int BaseZIndex { get; set; }

	public float StatsLogIntervalSeconds { get; set; }

	public int ActiveCount { get; private set; }

	public int Capacity => _capacity;

	public double LastUpdateMs => _lastUpdateMs;

	public override void _Ready()
	{
		EnsureCanvasItems();
		Prewarm(PrewarmCount);
	}

	public override void _Process(double delta)
	{
		if (Enabled && _canvasReady)
		{
			_frameStopwatch.Restart();
			UpdateAndDraw((float)delta);
			_frameStopwatch.Stop();
			_lastUpdateMs = _frameStopwatch.Elapsed.TotalMilliseconds;
			if (_lastUpdateMs > _peakUpdateMs)
			{
				_peakUpdateMs = _lastUpdateMs;
			}
			if (ActiveCount > _peakActive)
			{
				_peakActive = ActiveCount;
			}
			MaybeLogStats(delta);
		}
	}

	public override void _ExitTree()
	{
		FreeCanvasItems();
	}

	public bool PlayVfx(Texture2D texture, Vector2 position, float rotation, Vector2 scale, Color color, float lifetime)
	{
		return PlayVfx(new VfxSpawnParams
		{
			Texture = texture,
			Position = position,
			Rotation = rotation,
			Scale = scale,
			Modulate = color,
			Lifetime = lifetime
		});
	}

	public bool PlayVfx(VfxSpawnParams p)
	{
		if (!Enabled || p.Texture == null)
		{
			return false;
		}
		EnsureCanvasItems();
		VfxInstance vfxInstance = RentInstance();
		if (vfxInstance == null)
		{
			if (!_poolExhaustedWarned)
			{
				_poolExhaustedWarned = true;
				SkinLog.Warn("RenderingServerVfxLayer pool exhausted (capacity=" + _capacity + "); spawn dropped. Raise MaxCapacity or PrewarmCount.");
			}
			return false;
		}
		Vector2 size = ((p.Size != Vector2.Zero) ? p.Size : p.Texture.GetSize());
		vfxInstance.Active = true;
		vfxInstance.Texture = p.Texture;
		vfxInstance.TextureRid = p.Texture.GetRid();
		vfxInstance.Position = p.Position;
		vfxInstance.Rotation = p.Rotation;
		vfxInstance.Scale = p.Scale;
		vfxInstance.Modulate = p.Modulate;
		vfxInstance.Lifetime = ((p.Lifetime > 0f) ? p.Lifetime : 0.0001f);
		vfxInstance.Age = 0f;
		vfxInstance.Size = size;
		vfxInstance.SourceRect = p.SourceRect;
		vfxInstance.SortingOrder = p.SortingOrder;
		vfxInstance.AdditiveBlend = p.AdditiveBlend;
		vfxInstance.FadeMode = p.FadeMode;
		vfxInstance.ScaleFrom = p.ScaleFrom;
		vfxInstance.ScaleTo = p.ScaleTo;
		vfxInstance.Velocity = p.Velocity;
		vfxInstance.AngularVelocity = p.AngularVelocity;
		vfxInstance.CurrentAlpha = p.Modulate.A;
		ActiveCount++;
		_clearedWhileEmpty = false;
		return true;
	}

	public void ClearAll()
	{
		foreach (VfxInstance item in _pool)
		{
			item.Reset();
		}
		ActiveCount = 0;
		if (_canvasReady)
		{
			RenderingServer.CanvasItemClear(_normalCanvasItem);
			RenderingServer.CanvasItemClear(_additiveCanvasItem);
		}
		_clearedWhileEmpty = true;
	}

	public void WarmUpPipelines(Texture2D texture)
	{
		if (texture != null)
		{
			EnsureCanvasItems();
			Color modulate = new Color(1f, 1f, 1f, 0f);
			Rect2 rect = new Rect2(new Vector2(-1f, -1f), new Vector2(2f, 2f));
			Rid rid = texture.GetRid();
			WarmUpPipelineCanvasItem(_normalCanvasItem, rect, rid, modulate);
			WarmUpPipelineCanvasItem(_additiveCanvasItem, rect, rid, modulate);
			_clearedWhileEmpty = false;
		}
	}

	public void ResetStats()
	{
		_peakActive = 0;
		_peakUpdateMs = 0.0;
		_lastUpdateMs = 0.0;
	}

	public string DescribeStats()
	{
		return "active=" + ActiveCount + " peakActive=" + _peakActive + " capacity=" + _capacity + " lastUpdateMs=" + _lastUpdateMs.ToString("0.###") + " peakUpdateMs=" + _peakUpdateMs.ToString("0.###");
	}

	private void Prewarm(int count)
	{
		int num = Math.Clamp(count, 0, MaxCapacity);
		while (_pool.Count < num)
		{
			_pool.Add(new VfxInstance());
		}
		_capacity = _pool.Count;
	}

	private VfxInstance? RentInstance()
	{
		for (int i = 0; i < _pool.Count; i++)
		{
			if (!_pool[i].Active)
			{
				return _pool[i];
			}
		}
		if (_pool.Count < MaxCapacity)
		{
			VfxInstance vfxInstance = new VfxInstance();
			_pool.Add(vfxInstance);
			_capacity = _pool.Count;
			return vfxInstance;
		}
		return null;
	}

	private void UpdateAndDraw(float delta)
	{
		_drawScratch.Clear();
		int num = 0;
		for (int i = 0; i < _pool.Count; i++)
		{
			VfxInstance vfxInstance = _pool[i];
			if (vfxInstance.Active)
			{
				vfxInstance.Age += delta;
				if (vfxInstance.Age >= vfxInstance.Lifetime)
				{
					vfxInstance.Reset();
					continue;
				}
				float t = vfxInstance.Age / vfxInstance.Lifetime;
				vfxInstance.Position += vfxInstance.Velocity * delta;
				vfxInstance.Rotation += vfxInstance.AngularVelocity * delta;
				vfxInstance.CurrentAlpha = vfxInstance.Modulate.A * FadeFactor(vfxInstance.FadeMode, t);
				num++;
				_drawScratch.Add(vfxInstance);
			}
		}
		ActiveCount = num;
		if (num == 0)
		{
			if (!_clearedWhileEmpty)
			{
				RenderingServer.CanvasItemClear(_normalCanvasItem);
				RenderingServer.CanvasItemClear(_additiveCanvasItem);
				_clearedWhileEmpty = true;
			}
			return;
		}
		_clearedWhileEmpty = false;
		if (_drawScratch.Count > 1)
		{
			_drawScratch.Sort(SortByOrder);
		}
		RenderingServer.CanvasItemClear(_normalCanvasItem);
		RenderingServer.CanvasItemClear(_additiveCanvasItem);
		for (int j = 0; j < _drawScratch.Count; j++)
		{
			DrawInstance(_drawScratch[j]);
		}
	}

	private void DrawInstance(VfxInstance instance)
	{
		Rid item = (instance.AdditiveBlend ? _additiveCanvasItem : _normalCanvasItem);
		float num = Mathf.Lerp(instance.ScaleFrom, instance.ScaleTo, instance.Age / instance.Lifetime);
		Vector2 vector = instance.Scale * num;
		float num2 = Mathf.Cos(instance.Rotation);
		float num3 = Mathf.Sin(instance.Rotation);
		Vector2 xAxis = new Vector2(num2, num3) * vector.X;
		Vector2 yAxis = new Vector2(0f - num3, num2) * vector.Y;
		Transform2D transform = new Transform2D(xAxis, yAxis, instance.Position);
		RenderingServer.CanvasItemAddSetTransform(item, transform);
		Color modulate = instance.Modulate;
		modulate.A = instance.CurrentAlpha;
		Rect2 rect = new Rect2(-instance.Size * 0.5f, instance.Size);
		if (instance.SourceRect.Size.X > 0f && instance.SourceRect.Size.Y > 0f)
		{
			RenderingServer.CanvasItemAddTextureRectRegion(item, rect, instance.TextureRid, instance.SourceRect, modulate);
		}
		else
		{
			RenderingServer.CanvasItemAddTextureRect(item, rect, instance.TextureRid, tile: false, modulate);
		}
	}

	private static void WarmUpPipelineCanvasItem(Rid canvasItem, Rect2 rect, Rid texRid, Color modulate)
	{
		RenderingServer.CanvasItemClear(canvasItem);
		RenderingServer.CanvasItemAddSetTransform(canvasItem, Transform2D.Identity);
		RenderingServer.CanvasItemAddTextureRect(canvasItem, rect, texRid, tile: false, modulate);
	}

	private static float FadeFactor(VfxFadeMode mode, float t)
	{
		switch (mode)
		{
		case VfxFadeMode.FadeOut:
			return 1f - t;
		case VfxFadeMode.FadeInOut:
			if (t < 0.2f)
			{
				return t / 0.2f;
			}
			if (t > 0.65f)
			{
				return (1f - t) / 0.35f;
			}
			return 1f;
		default:
			return 1f;
		}
	}

	private void EnsureCanvasItems()
	{
		if (!_canvasReady)
		{
			Rid canvasItem = GetCanvasItem();
			_normalCanvasItem = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetParent(_normalCanvasItem, canvasItem);
			RenderingServer.CanvasItemSetZIndex(_normalCanvasItem, BaseZIndex);
			_additiveMaterial = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
			_additiveCanvasItem = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetParent(_additiveCanvasItem, canvasItem);
			RenderingServer.CanvasItemSetZIndex(_additiveCanvasItem, BaseZIndex + 1);
			RenderingServer.CanvasItemSetMaterial(_additiveCanvasItem, _additiveMaterial.GetRid());
			_canvasReady = true;
			_clearedWhileEmpty = true;
		}
	}

	private void FreeCanvasItems()
	{
		if (_canvasReady)
		{
			if (_normalCanvasItem.IsValid)
			{
				RenderingServer.FreeRid(_normalCanvasItem);
				_normalCanvasItem = default;
			}
			if (_additiveCanvasItem.IsValid)
			{
				RenderingServer.FreeRid(_additiveCanvasItem);
				_additiveCanvasItem = default;
			}
			_additiveMaterial = null;
			_canvasReady = false;
		}
	}

	private void MaybeLogStats(double delta)
	{
		if (StatsLogIntervalSeconds <= 0f)
		{
			return;
		}
		_statsAccum += delta;
		if (!(_statsAccum < (double)StatsLogIntervalSeconds))
		{
			_statsAccum = 0.0;
			SkinLog.Debug(() => "RenderingServerVfxLayer " + DescribeStats());
		}
	}
}
