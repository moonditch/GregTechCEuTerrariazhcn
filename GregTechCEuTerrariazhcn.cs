using GregTechCEuTerrariazhcnPublicizerPart;
using Terraria.Localization;
using Terraria.ModLoader;
using TigerForceLocalizationLib;
using TigerForceLocalizationLib.Filters;

#if false
相关信息:
	泰拉格雷科技 git: https://github.com/caxapexac/TerrariaGregTech
	正在等待的任务书: https://github.com/bereft-souls/QuestBooks
	    据 Pringull 所说, Quest book 除了美术部分之外其余的都基本完成了 (Bereft Souls Discord at 2026/06/05 05:51)
	作者 caxapexac 坦言任务书重做要花大量的时间 (Discord #development at 2026/06/09 07:13)
    格雷科技汉化包下载: https://www.ningnana.top/index.php/15612-gregtech-community-packmodern-1-20-1
可能需要特殊处理内容:
    任务书: GregTechCEuTerraria.TerrariaCompat.Questbook.QuestbookSystem.LoadQuestbook() 中的 "Data/Questbook/questlog.json"
    qb 任务书 (应该无需汉化): GregTechCEuTerraria.TerrariaCompat.QuestBooksInterop.GTQuestBookLoader.PostSetupContent() 中的 "Data/Questbook/qb-questlog.json"
	GregTechCEuTerraria.Api.Data.Chemical.Material.Formula 目前会直接被 GregTechCEuTerraria.TerrariaCompat.Items.MaterialItem.ModifyTooltips 使用
    GregTechCEuTerraria.TerrariaCompat.UI.GlobalRecipeBrowserState._equipCats
    某些用到 GregTechCEuTerraria.TerrariaCompat.Pipelike.PipeSizes.Words() 的地方
    GregTechCEuTerraria.TerrariaCompat.Machine.MachineDefinitions.RegisterAll() 中的 Label
#endif

namespace GregTechCEuTerrariazhcn
{
    public class GregTechCEuTerrariazhcn : Mod
    {
        private static bool Registering { get; } = false;

        public override void Load()
        {
            LoadQuestbookHook();
        }
        public override void Unload()
        {
            UnloadQuestbookHook();
        }

        public override void PostSetupContent()
        {
            if (Registering)
            {
                TigerForceLocalizationHelper.ForceAllWeakReferences = false;
                TigerForceLocalizationHelper.ShowLocalizationRegisterProgress();
            }
            TigerForceLocalizationHelper.LocalizeFilters? filters = !Registering ? null : new()
            {
                MethodFilter = MethodFilter.CommonMethodFilter,
                CursorFilter = ILCursorFilter.CommonCursorFilter,
            };
            TigerForceLocalizationHelper.LocalizeAll(nameof(GregTechCEuTerrariazhcn), "GregTechCEuTerraria", Registering, filters: filters);
        }

        #region 任务书汉化
        private QuestbookLocalizer? QuestbookLocalizer { get; set; }

        private string TranslateQuestbook(string key, string content)
        {
            var localizationKey = $"Mods.{nameof(GregTechCEuTerrariazhcn)}.Questbook.{key}";
            return Language.GetOrRegister(localizationKey, () => content).Value;
        }

        private void LoadQuestbookHook()
        {
            // 由于目标模组注册任务书的时机是在 PostSetupContent, 所以 Hook 需要在此之前
            QuestbookLocalizer?.Unload();
            QuestbookLocalizer = new(TranslateQuestbook);
            QuestbookLocalizer.Load();
        }
        private void UnloadQuestbookHook()
        {
            if (QuestbookLocalizer != null)
            {
                QuestbookLocalizer.Unload();
                QuestbookLocalizer = null;
            }
        }
        #endregion
    }
}
