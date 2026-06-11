using MonoMod.Cil;
using System.Reflection;
using Terraria.ID;
using Terraria.ModLoader;
using TigerForceLocalizationLib;
using TigerForceLocalizationLib.Filters;

#if false
相关信息:
	泰拉格雷科技 git: https://github.com/caxapexac/TerrariaGregTech
	正在等待的任务书: https://github.com/bereft-souls/QuestBooks
	    据 Pringull 所说, Quest book 除了美术部分之外其余的都基本完成了 (Bereft Souls Discord at 2026/06/05 05:51)
	作者 caxapexac 坦言任务书重做要花大量的时间 (Discord #development at 2026/06/09 07:13)
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
        public override void PostSetupContent()
        {
            if (Registering)
            {
                TigerForceLocalizationHelper.ForceAllWeakReferences = false;
                TigerForceLocalizationHelper.ShowLocalizationRegisterProgress();
            }
            TigerForceLocalizationHelper.LocalizeFilters filters = !Registering ? null : new()
            {
                MethodFilter = MethodFilter.CommonMethodFilter,
                CursorFilter = ILCursorFilter.CommonCursorFilter,
            };
        }
        public void SomeHook()
        {
            if (!ModLoader.TryGetMod("GregTechCEuTerraria", out var targetMod))
            {
                Logger.Error("Can't find Greg Mod!");
                return;
            }
            var  targetAssembly = targetMod.GetType().Assembly;
            var questbookSystemType = targetAssembly.GetType("GregTechCEuTerraria.TerrariaCompat.Questbook.QuestbookSystem");
            if (questbookSystemType != null)
            {
                // TODO: 使用 hjson 扩展处理 hjson 文件
                var setDataMethod = questbookSystemType.GetMethod("set_Data", BFALL);
                if (setDataMethod == null)
                {
                    Logger.Warn("No set_Data method in type QuestbookSystem");
                    return;
                }
                var loadQuestbookMethod = questbookSystemType.GetMethod("LoadQuestbook", BFALL);
                if (loadQuestbookMethod == null)
                {
                    Logger.Warn("No LoadQuestbook method in type QuestbookSystem");
                    return;
                }
                MonoModHooks.Modify(loadQuestbookMethod, il =>
                {
                    ILCursor cursor = new(il);
                    if (!cursor.TryGotoNext(MoveType.AfterLabel, i => i.MatchCallOrCallvirt(setDataMethod)))
                    {
                        Logger.Warn("No set_Data call in method LoadQuestbook of type QuestbookSystem");
                        return;
                    }
                    cursor.EmitDelegate((QuestLogData data) =>
                    {
                        // TODO: Localize the data
                    });
                    // 格雷科技汉化包下载: https://www.ningnana.top/index.php/15612-gregtech-community-packmodern-1-20-1
                });
            }
            else
            {
                Logger.Warn("Can't find type QuestbookSystem");
            }
        }

        public const BindingFlags BFS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        public const BindingFlags BFI = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public const BindingFlags BFALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    }

    // TODO: 需要使用 Publicizer 得到 QuestLogData 类型
    class QuestLogData;
}
