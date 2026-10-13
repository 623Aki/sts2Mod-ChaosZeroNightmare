using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace CznCore.Presentation;

internal sealed class RunStartLoadingOverlay
{
	private readonly CanvasLayer _layer;

	private readonly ProgressBar _bar;

	private readonly Label _percent;

	private readonly Label _status;

	private RunStartLoadingOverlay(CanvasLayer layer, ProgressBar bar, Label percent, Label status)
	{
		_layer = layer;
		_bar = bar;
		_percent = percent;
		_status = status;
	}

	public static RunStartLoadingOverlay? TryCreate(Node host, RunStartPreloadProfile profile)
	{
		SceneTree treeOrNull = host.GetTreeOrNull();
		if (treeOrNull?.Root == null)
		{
			return null;
		}
		CanvasLayer canvasLayer = new CanvasLayer
		{
			Name = profile.SkinProfile.RuntimeNodeName("RunStartLoadingOverlay"),
			Layer = 4096
		};
		ColorRect colorRect = new ColorRect
		{
			Color = new Color(0.02f, 0.018f, 0.024f, 0.92f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		colorRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		CenterContainer centerContainer = new CenterContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(620f, 160f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		vBoxContainer.AddThemeConstantOverride("separation", 14);
		Label label = new Label
		{
			Text = profile.LoadingTitle,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 30);
		ProgressBar progressBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			Value = 0.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(620f, 18f)
		};
		Label label2 = new Label
		{
			Text = "0%",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label2.AddThemeFontSizeOverride("font_size", 24);
		Label label3 = new Label
		{
			Text = string.Empty,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label3.AddThemeFontSizeOverride("font_size", 18);
		vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(progressBar, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(label3, forceReadableName: false, Node.InternalMode.Disabled);
		centerContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		canvasLayer.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
		canvasLayer.AddChild(centerContainer, forceReadableName: false, Node.InternalMode.Disabled);
		treeOrNull.Root.AddChild(canvasLayer, forceReadableName: false, Node.InternalMode.Disabled);
		return new RunStartLoadingOverlay(canvasLayer, progressBar, label2, label3);
	}

	public void SetProgress(float progress, string status)
	{
		if (GodotObject.IsInstanceValid(_layer))
		{
			int num = Math.Clamp((int)MathF.Round(Math.Clamp(progress, 0f, 1f) * 100f), 0, 100);
			_bar.Value = num;
			_percent.Text = num + "%";
			_status.Text = status;
		}
	}

	public void Close()
	{
		if (GodotObject.IsInstanceValid(_layer))
		{
			_layer.QueueFree();
		}
	}
}
