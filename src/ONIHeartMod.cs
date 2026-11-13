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
    /// The game scans all loaded assemblies for IBuildingConfig, so building registration itself needs no manual call.
    /// This class adds strings, build-menu entries, and the shared research requirement.
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

            // The heart is in the Base category; other buildings are in their gas/liquid/power categories.
            // Tech requirements are assigned in ONIHeartTechRequirementPatch.
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
            AddToPlanScreen("Equipment", HeartTerminalThermostatConfig.ID, "research");

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
            TUNING.BUILDINGS.PLANSUBCATEGORYSORTING[HeartTerminalThermostatConfig.ID] = "research";
        }

        /// <summary>
        /// The vanilla PlanScreen and Codex use the building ID as a unique key; adding the same ID to two categories
        /// throws "An item with the same key has already been added" on load and breaks the build menu.
        /// This helper ensures each building is registered only once.
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

    /// <summary>
    /// Assigns every ONI-Heart building to a research tech. The game has no tech literally named
    /// "Advanced Computer"; the tier associated with the Super Computer is "Advanced Research".
    /// Change REQUIRED_TECH here if a different tech (for example DupeTrafficControl / Computing) is wanted.
    /// </summary>
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    public static class ONIHeartTechRequirementPatch
    {
        public const string REQUIRED_TECH = "AdvancedResearch";

        private static bool initialized;

        public static void Postfix()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Tech tech = Db.Get().Techs.Get(REQUIRED_TECH);
            if (tech == null)
            {
                return;
            }

            tech.AddUnlockedItemIDs(
                HeartConfig.ID,
                ManualLoaderConfig.ID,
                GasInConfig.ID,
                GasOutConfig.ID,
                ManualBottleGasConfig.ID,
                ManualUnbottlerGasConfig.ID,
                LiquidInConfig.ID,
                LiquidOutConfig.ID,
                ManualBottleLiquidConfig.ID,
                ManualUnbottlerLiquidConfig.ID,
                PowerConfig.ID,
                HeartTerminalDecomposeConfig.ID,
                HeartTerminalSolidifyConfig.ID,
                HeartTerminalLiquefyConfig.ID,
                HeartTerminalVaporizeConfig.ID,
                HeartTerminalThermostatConfig.ID);
        }
    }
}
