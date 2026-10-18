namespace CznHeidemarie.Characters.Heidemarie.Presentation;

/// <summary>
/// 演出档位 = 一组身体动作链 + 一套特效组。
///
/// <para>**按动作分，不按特效素材分** —— 特效必须贴在动作上，而主模型骨架
/// （Spine 4.2.11 成品）禁止改动，所以档位只能是骨架里真实存在的动作组合。</para>
///
/// <para>档位表见 <see cref="HeidemariePresentationCatalog"/>；
/// 卡牌侧只需声明档位（见 <c>HeidemarieCard.PresentationTier</c>），
/// 动作链、命中延迟、特效组三者的对应关系全部收口在档位表一处。</para>
///
/// <para>必须是 <c>public</c>：它出现在 <c>HeidemarieCard</c>（public 类）的
/// protected 成员签名里，internal 会报可访问性不一致。</para>
/// </summary>
public enum HeidemarieEffectTier
{
	/// <summary>轻击：attack_ready → attack_play1（触发器 Attack，命中 0.2s）。</summary>
	AttackLight,

	/// <summary>中击：attack_ready → attack_play2（触发器 Attack2，命中 0.2s）。</summary>
	AttackMid,

	/// <summary>重击：u1_attack_ready → u1_attack_play（触发器 U1Attack，命中 0.467s）。</summary>
	AttackHeavy,

	/// <summary>顶击：u3_attack_ready → u3_attack_play（触发器 U3Attack，命中 0.467s）。</summary>
	AttackTop,

	/// <summary>特写：ux_attack 单段（触发器 XAttack，命中 0.333s）。当前无卡引用，预留。</summary>
	AttackUx,

	/// <summary>终极：ug_attack 单段（触发器 GAttack，命中 0.333s）。</summary>
	AttackUg,

	/// <summary>施法：buff_ready → buff_play。</summary>
	Cast,

	/// <summary>结阵：u2_buff_ready → u2_buff_play。</summary>
	Guard,

	/// <summary>蓄势：u4_buff_ready → u4_buff_play（全骨架最长的增益动作）。</summary>
	Charge,

	/// <summary>防御：defense_ready → defense_play。</summary>
	Defend,

	/// <summary>治疗：cure_ready → cure_play。骨架有动画、尚未开档，预留。</summary>
	Cure,

	/// <summary>减益：debuff_ready → debuff_play。骨架有动画、尚未开档，预留。</summary>
	Debuff,
}
