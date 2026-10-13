using System;
using System.Globalization;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatEffectMaterialRuntime
{
	private readonly string _premultipliedAlphaMaterialPath;

	private readonly string? _premultipliedAlphaMultiplyMaterialPath;

	private readonly CombatEffectMaterialPolicyProfile _policy;

	private readonly float _referenceEffectBrightness;

	private readonly ICombatEffectMaterialHost _host;

	private Material? _premultipliedAlphaMaterial;

	private Material? _premultipliedAlphaAdditiveMaterial;

	private Material? _premultipliedAlphaMultiplyMaterial;

	private Material? _premultipliedAlphaScreenMaterial;

	private CombatSpineMaterialSet? _premultipliedAlphaSpineMaterials;

	private CombatSpineMaterialSet? _attackEffectSpineMaterials;

	private Material? _idleEffectAdditiveMaterial;

	private Material? _attackEffectAdditiveMaterial;

	private Material? _idleEffectSubviewportBackMaterial;

	private Material? _idleEffectSubviewportFrontMaterial;

	public CombatEffectMaterialRuntime(string premultipliedAlphaMaterialPath, CombatEffectMaterialPolicyProfile policy, float referenceEffectBrightness, ICombatEffectMaterialHost host, string? premultipliedAlphaMultiplyMaterialPath = null)
	{
		_premultipliedAlphaMaterialPath = premultipliedAlphaMaterialPath;
		_premultipliedAlphaMultiplyMaterialPath = premultipliedAlphaMultiplyMaterialPath;
		_policy = policy;
		_referenceEffectBrightness = Math.Max(0.001f, referenceEffectBrightness);
		_host = host ?? throw new ArgumentNullException("host");
	}

	public Material GetPremultipliedAlphaMaterial()
	{
		if (_premultipliedAlphaMaterial != null)
		{
			return _premultipliedAlphaMaterial;
		}
		if (!string.IsNullOrWhiteSpace(_premultipliedAlphaMaterialPath))
		{
			Material material = ResourceLoader.Load<Material>(_premultipliedAlphaMaterialPath, null, ResourceLoader.CacheMode.Reuse);
			if (material != null)
			{
				_premultipliedAlphaMaterial = material;
				return material;
			}
		}
		CanvasItemMaterial canvasItemMaterial = new CanvasItemMaterial();
		canvasItemMaterial.Set("blend_mode", Variant.From<int>(4));
		canvasItemMaterial.Set("light_mode", Variant.From<int>(0));
		_premultipliedAlphaMaterial = canvasItemMaterial;
		return canvasItemMaterial;
	}

	public int ApplyEffectMaterial(Node root, Func<Node, Color>? modulateForNode = null, string? skipRootMeta = null)
	{
		return CombatEffectRuntime.ApplySpineMaterialsAndResetCanvas(root, GetPremultipliedAlphaSpineMaterials(), CreateEffectBrightnessModulate(modulateForNode), skipRootMeta);
	}

	public int ApplySpineOnlyEffectMaterial(Node root, string? skipRootMeta = null)
	{
		return CombatEffectRuntime.ApplySpineMaterialsOnly(root, GetPremultipliedAlphaSpineMaterials(), skipRootMeta);
	}

	public int ApplyAttackEffectMaterial(Node root, float alpha = 1f, float brightnessMultiplier = 1f)
	{
		float num = Math.Max(0f, brightnessMultiplier);
		Color modulate = new Color(num, num, num, Mathf.Clamp(alpha, 0f, 1f));
		return CombatEffectRuntime.ApplySpineMaterialsAndResetCanvas(root, GetAttackSpineMaterials(), CreateEffectBrightnessModulate((Node _) => modulate));
	}

	public int ApplyEffectCueMaterial(Node root, CombatPresentationEffectCue cue, Func<Node, Color>? defaultModulateForNode = null, string? defaultSkipRootMeta = null)
	{
		if (cue.UseAttackBoost)
		{
			return ApplyAttackEffectMaterial(root, cue.Alpha, cue.BrightnessMultiplier);
		}
		if (cue.UseNeutralPmaMaterial)
		{
			return ApplyEffectMaterial(root, CreateCueBrightnessModulate(cue, null));
		}
		return ApplyEffectMaterial(root, CreateCueBrightnessModulate(cue, defaultModulateForNode), defaultSkipRootMeta);
	}

	public int ApplySpineOnlyEffectCueMaterial(Node root, CombatPresentationEffectCue cue, string? defaultSkipRootMeta = null)
	{
		if (cue.UseAttackBoost)
		{
			return CombatEffectRuntime.ApplySpineMaterialsOnly(root, GetAttackSpineMaterials());
		}
		if (cue.UseNeutralPmaMaterial)
		{
			return ApplySpineOnlyEffectMaterial(root);
		}
		return ApplySpineOnlyEffectMaterial(root, defaultSkipRootMeta);
	}

	private CombatSpineMaterialSet GetPremultipliedAlphaSpineMaterials()
	{
		if (_premultipliedAlphaSpineMaterials != null)
		{
			return _premultipliedAlphaSpineMaterials;
		}
		_premultipliedAlphaSpineMaterials = new CombatSpineMaterialSet(GetPremultipliedAlphaMaterial(), GetPremultipliedAlphaAdditiveMaterial(), GetPremultipliedAlphaMultiplyMaterial(), GetPremultipliedAlphaScreenMaterial());
		return _premultipliedAlphaSpineMaterials;
	}

	private CombatSpineMaterialSet GetAttackSpineMaterials()
	{
		if (_attackEffectSpineMaterials != null)
		{
			return _attackEffectSpineMaterials;
		}
		_attackEffectSpineMaterials = new CombatSpineMaterialSet(GetPremultipliedAlphaMaterial(), GetAttackAdditiveMaterial(), GetPremultipliedAlphaMultiplyMaterial(), GetPremultipliedAlphaScreenMaterial());
		return _attackEffectSpineMaterials;
	}

	private Material GetPremultipliedAlphaMultiplyMaterial()
	{
		if (_premultipliedAlphaMultiplyMaterial != null)
		{
			return _premultipliedAlphaMultiplyMaterial;
		}
		if (!string.IsNullOrWhiteSpace(_premultipliedAlphaMultiplyMaterialPath))
		{
			Material material = ResourceLoader.Load<Material>(_premultipliedAlphaMultiplyMaterialPath, null, ResourceLoader.CacheMode.Reuse);
			if (material != null)
			{
				_premultipliedAlphaMultiplyMaterial = material;
				return material;
			}
		}
		_premultipliedAlphaMultiplyMaterial = GetPremultipliedAlphaMaterial();
		return _premultipliedAlphaMultiplyMaterial;
	}

	public Material GetIdleAdditiveMaterial()
	{
		if (_idleEffectAdditiveMaterial != null)
		{
			return _idleEffectAdditiveMaterial;
		}
		ShaderMaterial shaderMaterial = CreateAdditiveMaterial(new Color(1.18f, 0.95f, 1.38f), 1.35f, 1.8f, 1.85f);
		shaderMaterial.SetShaderParameter("tint", _policy.IdleTint);
		shaderMaterial.SetShaderParameter("brightness", ScaleBrightness(_policy.IdleBrightness));
		shaderMaterial.SetShaderParameter("alpha_boost", ScaleOpacity(_policy.IdleAlphaBoost));
		shaderMaterial.SetShaderParameter("alpha_power", _policy.IdleAlphaPower);
		_idleEffectAdditiveMaterial = shaderMaterial;
		return shaderMaterial;
	}

	public Material GetAttackAdditiveMaterial()
	{
		if (_attackEffectAdditiveMaterial != null)
		{
			return _attackEffectAdditiveMaterial;
		}
		ShaderMaterial shaderMaterial = CreateAdditiveMaterial(Colors.White, 1.4f, 1.18f, 1.15f);
		shaderMaterial.SetShaderParameter("tint", _policy.AttackTint);
		shaderMaterial.SetShaderParameter("brightness", ScaleBrightness(_policy.AttackBrightness));
		shaderMaterial.SetShaderParameter("alpha_boost", ScaleOpacity(_policy.AttackAlphaBoost));
		shaderMaterial.SetShaderParameter("alpha_power", _policy.AttackAlphaPower);
		_attackEffectAdditiveMaterial = shaderMaterial;
		return shaderMaterial;
	}

	private Material GetPremultipliedAlphaAdditiveMaterial()
	{
		if (_premultipliedAlphaAdditiveMaterial != null)
		{
			return _premultipliedAlphaAdditiveMaterial;
		}
		ShaderMaterial shaderMaterial = CreateAdditiveMaterial(Colors.White, 1f, 1f, 1f);
		shaderMaterial.SetShaderParameter("brightness", ScaleBrightness(1f));
		_premultipliedAlphaAdditiveMaterial = shaderMaterial;
		return shaderMaterial;
	}

	private Material GetPremultipliedAlphaScreenMaterial()
	{
		if (_premultipliedAlphaScreenMaterial != null)
		{
			return _premultipliedAlphaScreenMaterial;
		}
		ShaderMaterial shaderMaterial = CreateAdditiveMaterial(Colors.White, 1f, 1f, 1f);
		shaderMaterial.SetShaderParameter("brightness", ScaleBrightness(1f));
		_premultipliedAlphaScreenMaterial = shaderMaterial;
		return shaderMaterial;
	}

	public Material GetIdleSubviewportMaterial(bool backLayer)
	{
		if (backLayer && _idleEffectSubviewportBackMaterial != null)
		{
			return _idleEffectSubviewportBackMaterial;
		}
		if (!backLayer && _idleEffectSubviewportFrontMaterial != null)
		{
			return _idleEffectSubviewportFrontMaterial;
		}
		ShaderMaterial shaderMaterial = CreateSubviewportMaterial();
		if (backLayer)
		{
			ApplySubviewportMaterialParameters(shaderMaterial, _policy.IdleBackViewport);
			_idleEffectSubviewportBackMaterial = shaderMaterial;
		}
		else
		{
			ApplySubviewportMaterialParameters(shaderMaterial, _policy.IdleFrontViewport);
			_idleEffectSubviewportFrontMaterial = shaderMaterial;
		}
		return shaderMaterial;
	}

	public void ApplyBrightnessSettings()
	{
		SetShaderBrightness(_premultipliedAlphaAdditiveMaterial, 1f);
		SetShaderBrightness(_premultipliedAlphaScreenMaterial, 1f);
		SetShaderBrightness(_idleEffectAdditiveMaterial, _policy.IdleBrightness);
		SetShaderBrightness(_attackEffectAdditiveMaterial, _policy.AttackBrightness);
		SetShaderOpacity(_idleEffectAdditiveMaterial, _policy.IdleAlphaBoost);
		SetShaderOpacity(_attackEffectAdditiveMaterial, _policy.AttackAlphaBoost);
		SetShaderBrightness(_idleEffectSubviewportBackMaterial, _policy.IdleBackViewport.Brightness);
		SetShaderBrightness(_idleEffectSubviewportFrontMaterial, _policy.IdleFrontViewport.Brightness);
		SetSubviewportOpacity(_idleEffectSubviewportBackMaterial, _policy.IdleBackViewport.AlphaMultiplier);
		SetSubviewportOpacity(_idleEffectSubviewportFrontMaterial, _policy.IdleFrontViewport.AlphaMultiplier);
	}

	private static ShaderMaterial CreateAdditiveMaterial(Color defaultTint, float defaultBrightness, float defaultAlphaBoost, float defaultAlphaPower)
	{
		Shader shader = new Shader();
		shader.Code = "shader_type canvas_item;\nrender_mode blend_add, unshaded;\nuniform vec4 tint : source_color = " + ColorDefault(defaultTint) + ";\nuniform float brightness = " + FloatDefault(defaultBrightness) + ";\nuniform float alpha_boost = " + FloatDefault(defaultAlphaBoost) + ";\nuniform float alpha_power = " + FloatDefault(defaultAlphaPower) + ";\nvoid fragment() {\n\tvec4 tex = texture(TEXTURE, UV) * COLOR;\n\tfloat alpha = max(tex.a, 0.0001);\n\tvec3 straight_rgb = clamp(tex.rgb / alpha, vec3(0.0), vec3(1.0));\n\tfloat shaped_alpha = pow(clamp(tex.a, 0.0, 1.0), alpha_power) * tint.a * alpha_boost;\n\tfloat output_alpha = clamp(shaped_alpha, 0.0, 1.0);\n\tCOLOR = vec4(straight_rgb * tint.rgb * brightness * output_alpha, output_alpha);\n}\n";
		Shader shader2 = shader;
		return new ShaderMaterial
		{
			Shader = shader2
		};
	}

	private static ShaderMaterial CreateSubviewportMaterial()
	{
		Shader shader = new Shader
		{
			Code = "shader_type canvas_item;\nrender_mode blend_add, unshaded;\nuniform float brightness = 1.0;\nuniform float contrast = 1.0;\nuniform float saturation = 1.0;\nuniform float alpha_mul = 1.0;\nvec3 apply_saturation(vec3 color, float amount) {\n\tfloat luma = dot(color, vec3(0.2126, 0.7152, 0.0722));\n\treturn mix(vec3(luma), color, amount);\n}\nvoid fragment() {\n\tvec4 tex = texture(TEXTURE, UV);\n\tfloat alpha = clamp(tex.a * alpha_mul, 0.0, 1.0);\n\tvec3 rgb = tex.rgb;\n\trgb = (rgb - vec3(0.5)) * contrast + vec3(0.5);\n\trgb = apply_saturation(rgb, saturation) * brightness;\n\trgb = max(rgb, vec3(0.0));\n\tCOLOR = vec4(rgb, alpha);\n}\n"
		};
		return new ShaderMaterial
		{
			Shader = shader
		};
	}

	private void ApplySubviewportMaterialParameters(ShaderMaterial material, CombatEffectViewportMaterialProfile profile)
	{
		material.SetShaderParameter("brightness", ScaleBrightness(profile.Brightness));
		material.SetShaderParameter("contrast", profile.Contrast);
		material.SetShaderParameter("saturation", profile.Saturation);
		material.SetShaderParameter("alpha_mul", ScaleOpacity(profile.AlphaMultiplier));
	}

	private float ScaleBrightness(float tunedValue)
	{
		return tunedValue * GetEffectBrightnessScale();
	}

	private float ScaleOpacity(float tunedValue)
	{
		return tunedValue * GetEffectOpacityScale();
	}

	private Func<Node, Color> CreateEffectBrightnessModulate(Func<Node, Color>? modulateForNode)
	{
		float brightnessScale = GetEffectBrightnessScale();
		float opacityScale = GetEffectOpacityScale();
		return (Node node) => ApplyEffectBrightnessScale((modulateForNode == null) ? Colors.White : modulateForNode(node), brightnessScale, opacityScale);
	}

	private static Func<Node, Color>? CreateCueBrightnessModulate(CombatPresentationEffectCue cue, Func<Node, Color>? modulateForNode)
	{
		if (Math.Abs(cue.BrightnessMultiplier - 1f) <= 0.001f)
		{
			return modulateForNode;
		}
		float brightness = Math.Max(0f, cue.BrightnessMultiplier);
		return (Node node) =>
		{
			Color color = ((modulateForNode == null) ? Colors.White : modulateForNode(node));
			return new Color(color.R * brightness, color.G * brightness, color.B * brightness, color.A);
		};
	}

	private static Color ApplyEffectBrightnessScale(Color source, float brightnessScale, float opacityScale)
	{
		return new Color(source.R * brightnessScale, source.G * brightnessScale, source.B * brightnessScale, source.A * opacityScale);
	}

	private float GetEffectBrightnessScale()
	{
		return Mathf.Clamp(_host.GetEffectBrightness() / _referenceEffectBrightness, 0f, 2f);
	}

	private float GetEffectOpacityScale()
	{
		return Mathf.Clamp(_host.GetEffectOpacity(), 0f, 1f);
	}

	private void SetShaderBrightness(Material? material, float tunedValue)
	{
		if (material is ShaderMaterial shaderMaterial && GodotObject.IsInstanceValid(shaderMaterial))
		{
			shaderMaterial.SetShaderParameter("brightness", ScaleBrightness(tunedValue));
		}
	}

	private void SetShaderOpacity(Material? material, float tunedValue)
	{
		if (material is ShaderMaterial shaderMaterial && GodotObject.IsInstanceValid(shaderMaterial))
		{
			shaderMaterial.SetShaderParameter("alpha_boost", ScaleOpacity(tunedValue));
		}
	}

	private void SetSubviewportOpacity(Material? material, float tunedValue)
	{
		if (material is ShaderMaterial shaderMaterial && GodotObject.IsInstanceValid(shaderMaterial))
		{
			shaderMaterial.SetShaderParameter("alpha_mul", ScaleOpacity(tunedValue));
		}
	}

	private static string ColorDefault(Color color)
	{
		return "vec4(" + FloatDefault(color.R) + ", " + FloatDefault(color.G) + ", " + FloatDefault(color.B) + ", " + FloatDefault(color.A) + ")";
	}

	private static string FloatDefault(float value)
	{
		string text = value.ToString("0.###", CultureInfo.InvariantCulture);
		if (!text.Contains('.'))
		{
			return text + ".0";
		}
		return text;
	}
}
