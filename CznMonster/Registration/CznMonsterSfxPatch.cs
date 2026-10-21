using CznMonster.Content.Audio;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Registration;

/// <summary>
/// 把本 mod 的占位 FMOD 事件路径接管成本地 ogg 播放（设计文档 §5.4）。
///
/// <para>两条入口都要拦：</para>
/// <list type="bullet">
///   <item><see cref="SfxCmd.Play(string, float)"/> —— 攻击前摇（<c>WithAttackerFx</c>）、
///         受击 <c>HurtSfx</c>、战果页/图鉴的 <c>DeathSfx</c> 都走这里；</item>
///   <item><see cref="SfxCmd.PlayDeath(MonsterModel)"/> —— 死亡音效<b>不经过</b>
///         <c>Play</c>，而是直接调 <c>NAudioManager.PlayOneShot</c>，必须单独拦。</item>
/// </list>
///
/// <para>只识别 <see cref="CznMonsterSfx.PlaceholderPrefix"/> 前缀；其它路径原样放行，
/// 对原生怪与其它 mod 零侵入（铁律 6）。</para>
/// </summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
internal static class CznMonsterPlaySfxPatch
{
	private static bool Prefix(string sfx, float volume)
	{
		// true = 继续原实现；false = 已被本地播放器接管
		return !CznMonsterSfx.TryPlayPlaceholder(sfx, volume);
	}
}

/// <summary>死亡音效入口：<c>SfxCmd.PlayDeath</c> 绕过了 <c>Play</c>，单独接管。
/// 这里走 <see cref="MonsterModel.DeathSfx"/> 而不是固定 clip，让模型可以按形态
/// 换死亡采样（塞拉菲翁濒死 → <c>death_fading</c>，阿克鲁斯救援形态 → <c>death_rescue</c>）。</summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDeath), new[] { typeof(MonsterModel) })]
internal static class CznMonsterDeathSfxPatch
{
	private static bool Prefix(MonsterModel? monster)
	{
		if (!CznMonsterSfx.IsOurs(monster))
		{
			return true;
		}

		CznMonsterSfx.TryPlayPlaceholder(monster!.DeathSfx, 1f);
		return false;
	}
}
