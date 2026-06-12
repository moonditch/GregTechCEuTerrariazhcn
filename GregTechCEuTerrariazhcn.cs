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
    材料化学式 (无需汉化): GregTechCEuTerraria.Api.Data.Chemical.Material.Formula 目前会直接被 GregTechCEuTerraria.TerrariaCompat.Items.MaterialItem.ModifyTooltips 使用
    GregTechCEuTerraria.TerrariaCompat.UI.GlobalRecipeBrowserState._equipCats
    某些用到 GregTechCEuTerraria.TerrariaCompat.Pipelike.PipeSizes.Words() 的地方
        GregTechCEuTerraria.TerrariaCompat.Items.Pipes.PipeHeldItemBehavior.Hover()
        GregTechCEuTerraria.TerrariaCompat.Items.Pipes.SimpleFluidPipeItem.ModifyTooltips()
    GregTechCEuTerraria.TerrariaCompat.Machine.MachineDefinitions.RegisterAll() 中的 Label (硬编码汉化中已完成)
#endif

namespace GregTechCEuTerrariazhcn
{
    public class GregTechCEuTerrariazhcn : Mod
    {
        public override void Load()
        {
            Load_Localizers();
        }
        public override void Unload()
        {
            Unload_Localizers();
        }

        public override void PostSetupContent()
        {
            PostSetupContent_Localizers();
            PostSetupContent_ForceLocalizations();
        }

        #region 硬编码汉化
        private static bool Registering { get; } = false;

        private void PostSetupContent_ForceLocalizations()
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
        #endregion
        #region 专门汉化
        private QuestbookLocalizer? QuestbookLocalizer { get; set; }
        private MiscLocalizer? MiscLocalizer { get; set; }

        private string TranslateQuestbook(string key, string content)
        {
            var localizationKey = $"Mods.{nameof(GregTechCEuTerrariazhcn)}.Questbook.{key}";
            return Language.GetOrRegister(localizationKey, () => content).Value;
        }

        private string TranslateMisc(string key, string content)
        {
            var localizationKey = $"Mods.{nameof(GregTechCEuTerrariazhcn)}.Misc.{key}";
            return Language.GetOrRegister(localizationKey, () => content).Value;
        }

        private void Load_Localizers()
        {
            // 由于目标模组注册任务书的时机是在 PostSetupContent, 所以 Hook 需要在此之前
            QuestbookLocalizer?.Unload();
            QuestbookLocalizer = new(TranslateQuestbook);
            QuestbookLocalizer.Load();
            MiscLocalizer?.Unload();
            MiscLocalizer = new(TranslateMisc);
            MiscLocalizer.Load();
        }
        private void Unload_Localizers()
        {
            if (QuestbookLocalizer != null)
            {
                QuestbookLocalizer.Unload();
                QuestbookLocalizer = null;
            }
            if (MiscLocalizer != null)
            {
                MiscLocalizer.Unload();
                MiscLocalizer = null;
            }
        }
        private void PostSetupContent_Localizers()
        {
            MiscLocalizer?.PostSetupContent();
        }
        #endregion
    }
}
