namespace CznCore.Presentation;

internal enum CombatPresentationCardRoute
{
	Unknown,
	Attack,
	Skill,
	GeneratedCard,
	EnemySkillDebuff,
	BuffU2,
	BuffU3,

	/// <summary>
	/// 终结技专用路线（本工程给「终焉剑舞」用）。
	///
	/// <para>必须追加在末尾：<c>CombatPresentationSupportRoutePolicy</c> 用
	/// <c>(uint)(route - 5) &lt;= 1u</c> 判定 BuffU2/BuffU3，前面各值的数值不能动。</para>
	/// </summary>
	Ultimate
}
