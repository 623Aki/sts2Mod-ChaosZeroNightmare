using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie 的角色卡池。
///
/// <para>卡池内容直接在本类列举：<see cref="CardPoolModel.AllCards"/> 会先取
/// <see cref="GenerateAllCards"/>，再拼接各 mod 通过
/// <c>ModHelper.AddModelToPool</c> 追加的内容，因此这里必须给出完整列表。</para>
///
/// <para><b>哪些卡不进池</b>：奖励 / 商店只从本池里按稀有度挑选
/// （<c>CardFactory</c> 走 <c>CardPoolModel.GetUnlockedCards</c>，
/// 且奖励生成会过滤掉 <see cref="CardRarity.Ancient"/>）。
/// 因此<strong>不应被常规获取</strong>的卡一律不列在这里：</para>
/// <list type="bullet">
///   <item>「极光剑」—— 衍生牌（Token），只能由剑雨 / 凝锋 / 极光之光 生成；</item>
///   <item>「凝聚极光」—— 只能由解放极光的「焚烧」生成；</item>
///   <item>「解放极光」—— 只能由能力「极光之光」生成。</item>
/// </list>
/// 它们仍会被 <c>ModelDb</c> 扫描注册（<c>ModelDb.Card&lt;T&gt;()</c> 可用），
/// 只是不参与任何池内抽取。
///
/// <para><b>「终焉」是例外</b>：它列在池里只是为了图鉴可见与
/// <c>ModelDb</c> 注册（对齐参考 mod CznNineCharacter 把「反射：零」
/// 列进 NineCardPool 的做法）；稀有度是 <see cref="CardRarity.Ancient"/>，
/// 奖励生成路径会过滤掉它，实际获取渠道只有「太古之牙」淬炼
/// （见 <c>Registration/HeidemarieAncientTranscendencePatch.cs</c>）。</para>
/// </summary>
public sealed class HeidemarieCardPool : CardPoolModel
{
	/// <inheritdoc/>
	public override string Title => "heidemarie";

	/// <summary>
	/// 能量图标配色键。
	///
	/// <para>本值会被
	/// <see cref="MegaCrit.Sts2.Core.Helpers.EnergyIconHelper.GetPath(string)"/> 拼成
	/// <c>res://images/atlases/ui_atlas.sprites/card/energy_&lt;值&gt;.tres</c>，
	/// 用于卡牌费用与遗物 / 药水提示中的能量图标。</para>
	///
	/// <para><b>注意：这条图集路径在本工程里会被游戏 <c>AtlasResourceLoader</c> 拦截</b>
	/// （<c>ui_atlas</c> 内无本角色 sprite，且无 fallback，会回落到
	/// <c>missing_power.png</c>）。因此真正生效的是
	/// <see cref="Registration.HeidemarieEnergyIconPathPatch"/> 这条 Postfix 补丁，
	/// 它把本配色键重定向到 <c>images/ui/energy/heidemarie_energy_card_74.png</c>。
	/// 改本值时必须同步改补丁里的配色键。</para>
	///
	/// <para>卡池 / 遗物池 / 药水池三处必须严格一致，否则同一次提示里会出现两种图标。</para>
	/// </summary>
	public override string EnergyColorName => "heidemarie";

	/// <inheritdoc/>
	/// <remarks>
	/// 卡框颜色由材质决定：游戏把本值拼成
	/// <c>res://materials/cards/frames/&lt;值&gt;_mat.tres</c> 挂到卡牌节点的 <c>%Frame</c> 上。
	/// <para>本角色用的是自建材质 <c>materials/cards/frames/card_frame_heidemarie_mat.tres</c> ——
	/// 内容只是游戏自带的 <c>res://shaders/hsv.gdshader</c> + 一组 h/s/v 参数
	/// （h 色相旋转量、s 饱和度倍率、v 明度倍率），把共享卡框贴图染成玫红。
	/// 要换颜色只改那个 .tres 里的三个数，不必动代码。</para>
	///
	/// <para>参数是<b>从贴图反解出来的</b>，不是估的：卡框贴图 <c>card_frame_attack_s</c> 的边框本色
	/// 平均为 <c>#983036</c>（暗红），把 <c>#F63260</c> 当目标色代进着色器公式反解得
	/// <c>h=0.9808 / s=1.2592 / v=1.4302</c>，复算边框色正好是 <c>#F63260</c>。
	/// 换色时同样可以这样反解 —— 游戏自带那几套作对照：
	/// red <c>0.025 / 0.85 / 1.0</c>（边框出 <c>#8A3929</c>）、
	/// green <c>0.3 / 0.9 / 1.0</c>（<c>#307400</c>）、blue <c>0.55 / 0.9 / 1.0</c>（<c>#166289</c>）。</para>
	/// </remarks>
	public override string CardFrameMaterialPath => "card_frame_heidemarie";

	/// <inheritdoc/>
	public override Color DeckEntryCardColor => new Color("C8B6E2");

	/// <inheritdoc/>
	public override Color EnergyOutlineColor => new Color("5B4A7A");

	/// <inheritdoc/>
	public override bool IsColorless => false;

	/// <inheritdoc/>
	/// <remarks>
	/// 只列举「可常规获取」的卡：基础攻防 + 全部角色卡。
	/// 衍生的极光剑、凝聚极光、解放极光不进池，见类注释。
	/// 「绝响」虽是基础牌（初始卡组自带），仍必须列在这里：百科图鉴的解锁集合
	/// 按「所有卡池 GetUnlockedCards 的并集」计算（NCardLibraryGrid.GetCardVisibility），
	/// 不进池就会在图鉴里显示为「锁定」。奖励 / 商店 / 战斗生成都会过滤 Basic 稀有度
	/// （CardFactory 四处显式排除），因此列池没有副作用。
	/// 「终焉」虽为先古牌（太古之牙淬炼专属），仍列在此供图鉴与 ModelDb 注册。
	/// </remarks>
	protected override CardModel[] GenerateAllCards() => new CardModel[54]
	{
		// 基础攻防
		ModelDb.Card<HeidemarieStrike>(),
		ModelDb.Card<HeidemarieDefend>(),
		// 基础牌「绝响」：列池仅为图鉴解锁，见类注释 remarks
		ModelDb.Card<HeidemarieFinalEcho>(),
		// 原有角色卡
		ModelDb.Card<HeidemarieSwordlight>(),
		ModelDb.Card<HeidemarieSwordrain>(),
		ModelDb.Card<HeidemarieSwordTomb>(),
		ModelDb.Card<HeidemarieCondenseEdge>(),
		ModelDb.Card<HeidemarieRequiemBlade>(),
		// 新角色卡（连接 / 极光引擎扩展）
		ModelDb.Card<HeidemarieAllHeroes>(),
		ModelDb.Card<HeidemarieBindStar>(),
		ModelDb.Card<HeidemarieFaintLight>(),
		ModelDb.Card<HeidemarieRequiem>(),
		ModelDb.Card<HeidemarieRecover>(),
		ModelDb.Card<HeidemarieCallBlade>(),
		ModelDb.Card<HeidemarieRayOfLight>(),
		ModelDb.Card<HeidemarieTripleGlow>(),
		ModelDb.Card<HeidemarieResonance>(),
		ModelDb.Card<HeidemarieSkyfall>(),
		ModelDb.Card<HeidemarieUnfoldAurora>(),
		ModelDb.Card<HeidemarieNorthPolar>(),
		ModelDb.Card<HeidemarieSwordCrypt>(),
		ModelDb.Card<HeidemarieMirrorBlades>(),
		ModelDb.Card<HeidemarieStoreLight>(),
		ModelDb.Card<HeidemarieFallenStar>(),
		ModelDb.Card<HeidemarieForesee>(),
		// 第 1 轮：格挡 / 过牌 / 丢弃三体系补强（10 张）
		ModelDb.Card<HeidemarieAuroraWard>(),
		ModelDb.Card<HeidemarieVigil>(),
		ModelDb.Card<HeidemarieFallingEdge>(),
		ModelDb.Card<HeidemarieGatherStar>(),
		ModelDb.Card<HeidemarieBuryLight>(),
		ModelDb.Card<HeidemarieStarTrail>(),
		ModelDb.Card<HeidemarieLightPrism>(),
		ModelDb.Card<HeidemarieWallOfSwords>(),
		ModelDb.Card<HeidemarieDaybreak>(),
		ModelDb.Card<HeidemarieOldOath>(),
		// 第 2 轮：减益 / 多人 / 混合补强（18 张；状态牌「骇人的记忆」进衍生池）
		ModelDb.Card<HeidemarieLuredHand>(),
		ModelDb.Card<HeidemarieHangedChosen>(),
		ModelDb.Card<HeidemarieJudgement>(),
		ModelDb.Card<HeidemarieDreamBorder>(),
		ModelDb.Card<HeidemarieDiscardEverything>(),
		ModelDb.Card<HeidemarieNightmareOtherSide>(),
		ModelDb.Card<HeidemarieInnerAwakening>(),
		ModelDb.Card<HeidemarieFacelessWoman>(),
		ModelDb.Card<HeidemarieEmotionalOutburst>(),
		ModelDb.Card<HeidemarieMutualAid>(),
		ModelDb.Card<HeidemariePatchwork>(),
		ModelDb.Card<HeidemarieOfferEdge>(),
		ModelDb.Card<HeidemarieExpiredStar>(),
		ModelDb.Card<HeidemarieStandReady>(),
		ModelDb.Card<HeidemarieWhetEdge>(),
		ModelDb.Card<HeidemarieStarfall>(),
		ModelDb.Card<HeidemarieUnsheathe>(),
		ModelDb.Card<HeidemarieBackToBack>(),
		// 先古牌：由「绝响」经太古之牙淬炼而来，列池供图鉴 / ModelDb 注册
		ModelDb.Card<HeidemarieTerminus>(),
	};
}
