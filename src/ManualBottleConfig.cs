using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// 手动装瓶器（输出）、手动卸瓶器（气体/液体输入）、手动装载器（固体/物品输入）。
    /// 三种建筑都用同一个 1x2 外观，但职责固定，不提供输入/输出模式切换。
    /// </summary>
    public abstract class ManualBottleConfigBase : IBuildingConfig
    {
        protected abstract string Id { get; }
        protected abstract ManualBottlePhase Phase { get; }
        protected abstract bool IsOutput { get; }

        protected bool IsSolidLoader => Phase == ManualBottlePhase.Solid;

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                Id,
                1,
                2,
                "oniheart_bottler_kanim",
                30,
                30f,
                new float[] { 200f },
                new string[] { "BuildableRaw" },
                1600f,
                BuildLocationRule.OnFloor,
                TUNING.BUILDINGS.DECOR.NONE,
                TUNING.NOISE_POLLUTION.NONE);

            def.ObjectLayer = ObjectLayer.Building;
            def.ContinuouslyCheckFoundation = true;
            def.Overheatable = false;
            def.Floodable = false;
            def.Entombable = false;
            def.ThermalConductivity = 0f;
            def.UseStructureTemperature = false;
            def.ExhaustKilowattsWhenActive = 0f;
            def.SelfHeatKilowattsWhenActive = 0f;
            def.EnergyConsumptionWhenActive = 0f;
            def.AudioCategory = "Metal";
            def.AudioSize = "small";
            def.PermittedRotations = PermittedRotations.Unrotatable;
            def.DefaultAnimState = "off";
            def.BaseTimeUntilRepair = -1f;

            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            if (IsOutput)
            {
                Prioritizable.AddRef(go);
                go.AddOrGet<BuildingComplete>().isManuallyOperated = true;
                Storage sourceStorage = go.AddOrGet<Storage>();
                sourceStorage.showInUI = false;
                sourceStorage.showDescriptor = true;
                sourceStorage.allowItemRemoval = true;
                sourceStorage.SetDefaultStoredItemModifiers(Storage.StandardInsulatedStorage);
                go.AddTag(Phase == ManualBottlePhase.Gas ? GameTags.GasSource : GameTags.LiquidSource);
                return;
            }

            Prioritizable.AddRef(go);

            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = 200f;
            storage.showInUI = true;
            storage.showDescriptor = true;
            storage.allowItemRemoval = true;
            storage.storageFilters = GetStorageFilters();
            storage.SetDefaultStoredItemModifiers(new List<Storage.StoredItemModifier>
            {
                Storage.StoredItemModifier.Hide,
                Storage.StoredItemModifier.Seal,
                Storage.StoredItemModifier.Insulate
            });

            go.AddOrGet<TreeFilterable>();
        }

        private List<Tag> GetStorageFilters()
        {
            switch (Phase)
            {
                case ManualBottlePhase.Gas:
                    return new List<Tag>(STORAGEFILTERS.GASES);
                case ManualBottlePhase.Liquid:
                    return new List<Tag>(STORAGEFILTERS.LIQUIDS);
                default:
                    return HeartConfig.GetSolidItemCategories();
            }
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<ManualBottleLink>();

            if (IsOutput)
            {
                ManualBottleOutput output = go.AddOrGet<ManualBottleOutput>();
                output.phase = Phase;

                Filterable filterable = go.AddOrGet<Filterable>();
                filterable.filterElementState = Phase == ManualBottlePhase.Gas
                    ? Filterable.ElementState.Gas
                    : Filterable.ElementState.Liquid;
            }
            else
            {
                ManualItemInput input = go.AddOrGet<ManualItemInput>();
                input.phase = Phase;
                input.unbottle = !IsSolidLoader;
            }
        }
    }

    public sealed class ManualBottleGasConfig : ManualBottleConfigBase
    {
        public const string ID = "ONIHeartManualBottleGas";

        protected override string Id => ID;
        protected override ManualBottlePhase Phase => ManualBottlePhase.Gas;
        protected override bool IsOutput => true;
    }

    public sealed class ManualBottleLiquidConfig : ManualBottleConfigBase
    {
        public const string ID = "ONIHeartManualBottleLiquid";

        protected override string Id => ID;
        protected override ManualBottlePhase Phase => ManualBottlePhase.Liquid;
        protected override bool IsOutput => true;
    }

    public sealed class ManualUnbottlerGasConfig : ManualBottleConfigBase
    {
        public const string ID = "ONIHeartManualUnbottlerGas";

        protected override string Id => ID;
        protected override ManualBottlePhase Phase => ManualBottlePhase.Gas;
        protected override bool IsOutput => false;
    }

    public sealed class ManualUnbottlerLiquidConfig : ManualBottleConfigBase
    {
        public const string ID = "ONIHeartManualUnbottlerLiquid";

        protected override string Id => ID;
        protected override ManualBottlePhase Phase => ManualBottlePhase.Liquid;
        protected override bool IsOutput => false;
    }

    public sealed class ManualLoaderConfig : ManualBottleConfigBase
    {
        public const string ID = "ONIHeartManualLoader";

        protected override string Id => ID;
        protected override ManualBottlePhase Phase => ManualBottlePhase.Solid;
        protected override bool IsOutput => false;
    }
}
