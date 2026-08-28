using System.Collections.Generic;
using HarmonyLib;
using KMod;
using TUNING;

namespace ONIHeart
{
    public sealed class ONIHeartMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }

    /// <summary>
    /// 原版会扫描所有已加载程序集中的 IBuildingConfig，因此建筑注册本身不需要手动调用。
    /// 这里只负责把字符串与建造菜单入口加进去。
    /// </summary>
    [HarmonyPatch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.LoadGeneratedBuildings))]
    public static class GeneratedBuildingsPatch
    {
        private static bool initialized;

        public static void Prefix()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            LocString.CreateLocStringKeys(typeof(STRINGS), null);

            // 元素之心在基地栏；其余建筑在各自的气体/液体/电力栏，且都不进科技树。
            AddToPlanScreen("Base", HeartConfig.ID);
            AddToPlanScreen("Base", ManualLoaderConfig.ID);
            AddToPlanScreen("HVAC", GasInConfig.ID);
            AddToPlanScreen("HVAC", GasOutConfig.ID);
            AddToPlanScreen("HVAC", ManualBottleGasConfig.ID);
            AddToPlanScreen("HVAC", ManualUnbottlerGasConfig.ID);
            AddToPlanScreen("Plumbing", LiquidInConfig.ID);
            AddToPlanScreen("Plumbing", LiquidOutConfig.ID);
            AddToPlanScreen("Plumbing", ManualBottleLiquidConfig.ID);
            AddToPlanScreen("Plumbing", ManualUnbottlerLiquidConfig.ID);
            AddToPlanScreen("Power", PowerConfig.ID);

            AddToPlanScreen("Equipment", HeartTerminalDecomposeConfig.ID, "research");
            AddToPlanScreen("Equipment", HeartTerminalSolidifyConfig.ID, "research");
            AddToPlanScreen("Equipment", HeartTerminalLiquefyConfig.ID, "research");
            AddToPlanScreen("Equipment", HeartTerminalVaporizeConfig.ID, "research");

            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[HeartConfig.ID] = "storage";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[ManualLoaderConfig.ID] = "storage";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[ManualBottleGasConfig.ID] = "storage";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[ManualUnbottlerGasConfig.ID] = "storage";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[ManualBottleLiquidConfig.ID] = "storage";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[ManualUnbottlerLiquidConfig.ID] = "storage";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[GasInConfig.ID] = "buildmenuports";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[GasOutConfig.ID] = "buildmenuports";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[LiquidInConfig.ID] = "buildmenuports";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[LiquidOutConfig.ID] = "buildmenuports";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[PowerConfig.ID] = "batteries";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[HeartTerminalDecomposeConfig.ID] = "research";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[HeartTerminalSolidifyConfig.ID] = "research";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[HeartTerminalLiquefyConfig.ID] = "research";
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[HeartTerminalVaporizeConfig.ID] = "research";
        }

        /// <summary>
        /// 原版 PlanScreen 与 Codex 都以建筑 ID 作为唯一键，同一个 ID 被加入两个分类
        /// 会在进入游戏时抛出 "An item with the same key has already been added"，
        /// 导致建造菜单无法生成。这里保证每栋建筑只登记一次。
        /// </summary>
        private static void AddToPlanScreen(HashedString category, string buildingId, string subcategory = "uncategorized")
        {
            foreach (PlanScreen.PlanInfo planInfo in TUNING.BUILDINGS.PLANORDER)
            {
                foreach (KeyValuePair<string, string> entry in planInfo.buildingAndSubcategoryData)
                {
                    if (entry.Key == buildingId)
                    {
                        return;
                    }
                }
            }

            ModUtil.AddBuildingToPlanScreen(category, buildingId, subcategory);
        }
    }
}
