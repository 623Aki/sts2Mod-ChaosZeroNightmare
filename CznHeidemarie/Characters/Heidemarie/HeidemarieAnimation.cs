using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// 30093.skel 各动画的真实时长（秒）。
///
/// <para>测量方式：用 <c>tools\SpineSkeletonDataConverter.exe</c> 把骨架转成 JSON，
/// 再取该动画所有 timeline 里的最大时间戳。**改动骨架后必须重新测量**，
/// 否则下面这些值会和实际演出长度脱节。</para>
///
/// <para>这些常量是卡牌「命中时机」的唯一依据 —— 之前散落在各卡里的
/// 0.2f / 0.3f / 0.5f 全是估的，其中 <c>ug_attack</c> 整体只有 0.3333 秒，
/// 却给了 0.5 秒的等待，等于动画早就播完了伤害还没结算。</para>
/// </summary>
internal static class HeidemarieAnimationSeconds
{
	/// <summary>普通攻击起手 attack_ready。</summary>
	public const float AttackReady = 0.2000f;

	/// <summary>u1 技能攻击起手 u1_attack_ready。</summary>
	public const float U1AttackReady = 0.4667f;

	/// <summary>u3 技能攻击起手 u3_attack_ready。</summary>
	public const float U3AttackReady = 0.4667f;

	/// <summary>ux 终结技 ux_attack（单段演出，无起手分段）。</summary>
	public const float UxAttack = 0.8333f;

	/// <summary>ug 终极技 ug_attack（单段演出，全骨架最短的攻击动作）。</summary>
	public const float UgAttack = 0.3333f;

	/// <summary>通用增益起手 buff_ready。</summary>
	public const float BuffReady = 0.4667f;

	/// <summary>u2 增益起手 u2_buff_ready。</summary>
	public const float U2BuffReady = 0.4667f;

	/// <summary>u4 增益起手 u4_buff_ready。</summary>
	public const float U4BuffReady = 0.4667f;

	/// <summary>治疗起手 cure_ready。</summary>
	public const float CureReady = 0.4667f;

	/// <summary>防御起手 defense_ready。</summary>
	public const float DefenseReady = 0.4667f;

	/// <summary>入场三段，合计 1.6667 秒。</summary>
	public const float EntryTotal = 0.4000f + 0.6000f + 0.6667f;

	/// <summary>胜利起手 victory_ready。</summary>
	public const float VictoryReady = 0.8000f;
}

/// <summary>
/// Heidemarie 的 Spine 动画控制层。
///
/// <para>提炼自参考 mod 的 <c>CznCore/Presentation</c>（Nine 那套 348 个文件的皮肤演出框架），
/// 但只保留与本项目相关的部分，并全部改用游戏原生 MegaSpine API 实现，
/// 不引入它那套反射兼容层、设置系统与泛型宿主。</para>
///
/// <para>对应关系：</para>
/// <list type="bullet">
///   <item><c>CombatSpineAnimationAvailabilityCache</c> → <see cref="HasAnimation"/> / <see cref="PickFirstAvailable"/>
///   —— 动画可用性查询缓存，避免反复跨语言调用；</item>
///   <item><c>CombatPresentationAttackPlan</c> 的 TimeScale 字段 → <see cref="PlayTriggerAsync"/> 的速度参数；</item>
///   <item><c>CombatPresentationAnimationFallback</c> → <see cref="PickFirstAvailable"/> 的候选降级链；</item>
///   <item>OffsetLoopingAnimation 的随机化思路 → <see cref="RandomizeLoopStart"/>。</item>
/// </list>
/// </summary>
internal static class HeidemarieAnimation
{
	/// <summary>
	/// 骨架动画可用性缓存。
	///
	/// <para><c>MegaSprite.HasAnimation</c> 每次都会跨 GDExtension 边界问一遍骨架数据，
	/// 放在每帧都会走的路径上是纯浪费；骨架在生命周期内不会变，缓存一次即可。
	/// 用 <see cref="ConditionalWeakTable{TKey,TValue}"/> 挂到节点上，节点释放缓存自动回收。</para>
	/// </summary>
	private static readonly ConditionalWeakTable<Node, Dictionary<string, bool>> AnimationCache = new();

	/// <summary>
	/// 骨架里有没有这个动画。同一个骨架只查一次，之后走缓存。
	/// </summary>
	public static bool HasAnimation(Node? spineNode, string animationName)
	{
		if (spineNode is null || string.IsNullOrEmpty(animationName) || !GodotObject.IsInstanceValid(spineNode))
		{
			return false;
		}

		Dictionary<string, bool> cache = AnimationCache.GetValue(
			spineNode,
			_ => new Dictionary<string, bool>(StringComparer.Ordinal));

		if (cache.TryGetValue(animationName, out bool cached))
		{
			return cached;
		}

		bool exists;
		try
		{
			exists = new MegaSprite(spineNode).HasAnimation(animationName);
		}
		catch (Exception ex)
		{
			// 骨架数据没加载好时 HasAnimation 会抛，这里按「没有」处理并留个痕
			GD.PushWarning($"[Heidemarie] Spine 动画查询失败：{animationName} —— {ex.Message}");
			exists = false;
		}

		cache[animationName] = exists;
		return exists;
	}

	/// <summary>
	/// 角色本体骨架上有没有这个动画。
	///
	/// <para>攻击卡在决定要不要用某个自定义触发器之前用它做一次校验：
	/// 动画缺失时 <c>CreatureAnimator</c> 只会写一条警告然后保持原状态，
	/// 表现为「卡片打出但角色不动」，不如提前发现并降级。</para>
	/// </summary>
	/// <param name="creature">目标角色。</param>
	/// <param name="animationName">要查询的动画名。</param>
	/// <returns>骨架存在该动画返回 <c>true</c>；角色节点或骨架不可用时返回 <c>false</c>。</returns>
	public static bool HasBodyAnimation(Creature creature, string animationName)
	{
		if (creature.GetCreatureNode() is not { } creatureNode)
		{
			return false;
		}

		return creatureNode.Visuals.SpineBody?.BoundObject is Node spineNode
			&& HasAnimation(spineNode, animationName);
	}

	/// <summary>
	/// 从候选动画里取第一个骨架真实拥有的。
	/// </summary>
	/// <param name="sprite">目标骨架，为 <c>null</c> 时直接返回第一个候选。</param>
	/// <param name="candidates">按优先级排列的候选动画名，至少一项。</param>
	/// <returns>第一个存在的候选；都不存在时返回第一个候选，让上层逻辑保持原样。</returns>
	public static string PickFirstAvailable(MegaSprite? sprite, params string[] candidates)
	{
		if (candidates is null || candidates.Length == 0)
		{
			throw new ArgumentException("候选动画列表不能为空", nameof(candidates));
		}

		if (sprite?.BoundObject is not Node node)
		{
			return candidates[0];
		}

		foreach (string candidate in candidates)
		{
			if (HasAnimation(node, candidate))
			{
				return candidate;
			}
		}

		return candidates[0];
	}

	/// <summary>
	/// 触发角色动画触发器，并按需覆盖播放速度。
	///
	/// <para>走游戏的 <see cref="CreatureCmd.TriggerAnim"/>，因此保有它对玩家角色的
	/// 音效处理（<c>Attack</c>/<c>Cast</c>/<c>PowerUp</c> 会播角色音效，自定义触发器不会）。
	/// 速度必须在动画已经开始之后才设得上，所以放在 await 之后。</para>
	/// </summary>
	/// <param name="creature">目标角色。</param>
	/// <param name="triggerName">触发器名，对应 <c>GenerateAnimator</c> 里注册的名字。</param>
	/// <param name="waitSeconds">触发后等待时长，一般填该动作的起手时长。</param>
	/// <param name="timeScale">播放速度，1 为原速。用于让不同动作有轻重缓急。</param>
	public static async Task PlayTriggerAsync(Creature creature, string triggerName, float waitSeconds, float timeScale = 1f)
	{
		await CreatureCmd.TriggerAnim(creature, triggerName, waitSeconds);
		ApplyTimeScale(creature, timeScale);
	}

	/// <summary>
	/// 直接排一段「动作序列」并连播，绕开 <c>CreatureAnimator</c> 状态机。
	///
	/// <para>提炼自参考 mod 的 <c>TipheraCombatBody.TryPlayCombatBodySequence</c>：
	/// 首段用 <c>SetAnimation</c> 立刻顶上，其余各段用 <c>AddAnimation</c> 依次排队，
	/// 末尾再排一次待机动画收尾。适合「起手 → 施放 → 收招」这类一次出牌要连播
	/// 多段动作、又不值得为它在 <c>GenerateAnimator</c> 里单独注册链条的场合。</para>
	///
	/// <para>与 <see cref="PlayTriggerAsync"/> 的区别：后者走游戏触发器与状态机，
	/// 保有游戏的音效处理；本方法直接操作骨架的动画队列，不过状态机，也不播音效。
	/// 任一动画缺失时整体放弃并返回 <c>false</c>，由调用方决定是否降级回触发器。</para>
	/// </summary>
	/// <param name="creature">目标角色。</param>
	/// <param name="animations">按播放顺序排列的动画名，至少一段；末尾会自动补待机。</param>
	/// <returns>成功接管播放返回 <c>true</c>；骨架不可用或有动画缺失返回 <c>false</c>。</returns>
	public static bool PlaySequence(Creature creature, params string[] animations)
	{
		if (animations is null || animations.Length == 0)
		{
			return false;
		}

		if (creature.GetCreatureNode() is not { } creatureNode)
		{
			return false;
		}

		MegaSprite? body = creatureNode.Visuals.SpineBody;
		if (body?.BoundObject is not Node spineNode)
		{
			return false;
		}

		// 先确认每一段都真实存在：缺一段就整体放弃，避免播到一半断在半路
		foreach (string animation in animations)
		{
			if (!HasAnimation(spineNode, animation))
			{
				return false;
			}
		}

		SpineAnimationAccess access = creatureNode.Visuals.SpineAnimation;
		if (!access.IsValid)
		{
			return false;
		}

		try
		{
			// 首段立刻顶上，其余各段顺序排队（delay 0 表示紧接上一段）
			access.SetAnimation(animations[0], loop: false);
			for (int i = 1; i < animations.Length; i++)
			{
				access.AddAnimation(animations[i], 0f, loop: false);
			}

			// 末尾回待机，保证整套动作播完不会停在最后一帧
			access.AddAnimation(PickFirstAvailable(body, "b_idle", "idle"), 0f, loop: true);
			return true;
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[Heidemarie] 动作序列播放失败：{string.Join(" -> ", animations)} —— {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 把当前播放条目的速度改成 <paramref name="timeScale"/>。
	///
	/// <para>不改变动画本身，只改这一条 track entry 的播放速率；
	/// 传入 1 表示恢复原速。</para>
	/// </summary>
	public static void ApplyTimeScale(Creature creature, float timeScale)
	{
		if (Mathf.IsEqualApprox(timeScale, 1f))
		{
			return;
		}

		if (creature.GetCreatureNode() is not { } creatureNode)
		{
			return;
		}

		using MegaTrackEntry? entry = creatureNode.Visuals.SpineAnimation.GetCurrentTrack();
		entry?.SetTimeScale(timeScale);
	}

	/// <summary>
	/// 把当前循环动画的起点随机到时间轴的任意位置，并给一点速度抖动。
	///
	/// <para>不这么做的话，同屏多个同类角色会完全同步地一起呼吸，
	/// 一眼就能看出是同一个动画实例（参考 mod 的 OffsetLoopingAnimation 也是这个用意）。</para>
	/// </summary>
	/// <param name="entry">目标播放条目，为 <c>null</c> 时不做任何事。</param>
	/// <param name="speedJitter">速度抖动量，0.9~1.1 这类幅度传 0.1。</param>
	public static void RandomizeLoopStart(MegaTrackEntry? entry, float speedJitter = 0.1f)
	{
		if (entry is null)
		{
			return;
		}

		float end = entry.GetAnimationEnd();
		if (end <= 0f)
		{
			return;
		}

		if (speedJitter > 0f)
		{
			entry.SetTimeScale(1f + (float)GD.RandRange(-speedJitter, speedJitter));
		}

		entry.SetTrackTime((float)GD.RandRange(0f, end));
	}
}
