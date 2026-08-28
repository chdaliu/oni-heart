using TUNING;
using UnityEngine;

namespace ONIHeart
{
    /// <summary>2x2 科学仪器：元素终端机。每种终端只控制一种处理操作。</summary>
    public abstract class HeartTerminalConfigBase : IBuildingConfig
    {
        protected abstract string Id { get; }
        protected abstract HeartProcessOperation Operation { get; }
        protected abstract string Anim { get; }

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                Id,
                2,
                2,
                Anim,
                30,
                30f,
                new float[] { 200f },
                new string[] { "Metal&RefinedMetal" },
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
            def.DefaultAnimState = "idle";
            def.BaseTimeUntilRepair = -1f;

            def.BaseDecor = 10f;
            def.BaseDecorRadius = 4f;

            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            HeartTerminal terminal = go.AddOrGet<HeartTerminal>();
            terminal.operation = Operation;
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            HeartTerminal terminal = go.AddOrGet<HeartTerminal>();
            terminal.operation = Operation;
        }
    }

    public sealed class HeartTerminalDecomposeConfig : HeartTerminalConfigBase
    {
        public const string ID = "ONIHeartTerminalDecompose";
        protected override string Id => ID;
        protected override HeartProcessOperation Operation => HeartProcessOperation.Decompose;
        protected override string Anim => "oniheart_terminal_decompose_kanim";
    }

    public sealed class HeartTerminalSolidifyConfig : HeartTerminalConfigBase
    {
        public const string ID = "ONIHeartTerminalSolidify";
        protected override string Id => ID;
        protected override HeartProcessOperation Operation => HeartProcessOperation.Solidify;
        protected override string Anim => "oniheart_terminal_solidify_kanim";
    }

    public sealed class HeartTerminalLiquefyConfig : HeartTerminalConfigBase
    {
        public const string ID = "ONIHeartTerminalLiquefy";
        protected override string Id => ID;
        protected override HeartProcessOperation Operation => HeartProcessOperation.Liquefy;
        protected override string Anim => "oniheart_terminal_liquefy_kanim";
    }

    public sealed class HeartTerminalVaporizeConfig : HeartTerminalConfigBase
    {
        public const string ID = "ONIHeartTerminalVaporize";
        protected override string Id => ID;
        protected override HeartProcessOperation Operation => HeartProcessOperation.Vaporize;
        protected override string Anim => "oniheart_terminal_vaporize_kanim";
    }
}
