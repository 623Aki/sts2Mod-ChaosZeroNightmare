namespace CznHeidemarie.Integration;

/// <summary>
/// 特效锚点语义。全部返回**世界坐标**，由 <see cref="HeidemarieVfx"/> 统一换算。
///
/// <para>对应游戏源码里的两个官方锚点：
/// <see cref="Center"/> = <c>NCreature.VfxSpawnPosition</c>（<c>%CenterPos</c>），
/// <see cref="Floor"/> = <c>NCreature.GetBottomOfHitbox()</c>。
/// <see cref="FloorScreenOffset"/> 只是在 <see cref="Floor"/> 上补一个屏幕像素偏移，
/// 用于兼容本 mod 现有的血条锚点标定。</para>
/// </summary>
internal enum HeidemarieVfxAnchor
{
	/// <summary>身体中心：<c>VfxSpawnPosition</c>（<c>%CenterPos</c> 的世界坐标）。</summary>
	Center,

	/// <summary>脚底中心：<c>GetBottomOfHitbox()</c>。</summary>
	Floor,

	/// <summary>脚底中心 + 屏幕像素偏移（本 mod 旧 healthbar 锚点 = (0, -40)）。</summary>
	FloorScreenOffset,

	/// <summary>骨架节点原点（施法者侧沿用旧挂载的坐标基准）。</summary>
	BodyOrigin,
}
