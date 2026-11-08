using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace ONIHeart
{
    public abstract class HeartPortConfig : IBuildingConfig
    {
        protected abstract string Id { get; }
        protected abstract HeartPortKind Kind { get; }
        protected abstract string Anim { get; }
        protected abstract int Width { get; }
        protected abstract int Height { get; }
        protected abstract BuildLocationRule LocationRule { get; }
        protected abstract ConduitType? InputConduit { get; }
        protected abstract ConduitType? OutputConduit { get; }
        protected abstract HashedString ViewMode { get; }
        protected abstract HashSet<Tag> OverlayScreenId { get; }
        protected abstract bool IsPower { get; }
        protected abstract ObjectLayer Layer { get; }

        protected virtual CellOffset InputOffset => new CellOffset(0, 0);
        protected virtual CellOffset OutputOffset => new CellOffset(0, 0);

        protected virtual Filterable.ElementState FilterState => Filterable.ElementState.None;

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                Id,
                Width,
                Height,
                Anim,
                IsPower ? 100 : 30,
                30f,
                new float[] { IsPower ? 200f : 100f },
                new string[] { IsPower ? "Metal&RefinedMetal" : "BuildableRaw" },
                1600f,
                LocationRule,
                TUNING.BUILDINGS.DECOR.NONE,
                TUNING.NOISE_POLLUTION.NONE);

            def.ObjectLayer = Layer;
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

            if (InputConduit.HasValue)
            {
                def.InputConduitType = InputConduit.Value;
                def.UtilityInputOffset = InputOffset;
            }

            if (OutputConduit.HasValue)
            {
                def.OutputConduitType = OutputConduit.Value;
                def.UtilityOutputOffset = OutputOffset;
            }

            if (IsPower)
            {
                def.RequiresPowerOutput = true;
                def.PowerOutputOffset = new CellOffset(0, 0);
                def.UseWhitePowerOutputConnectorColour = true;
            }

            def.ViewMode = ViewMode;
            GeneratedBuildings.RegisterWithOverlay(OverlayScreenId, Id);
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            Storage storage = go.AddOrGet<Storage>();
            // Input interfaces first write into their own infinite buffer, then transfer into the heart;
            // this prevents multiple interfaces with the same settings from sharing capacity or input quota.
            storage.capacityKg = float.MaxValue;
            storage.showInUI = false;
            storage.showDescriptor = false;
            storage.showCapacityStatusItem = false;
            storage.allowItemRemoval = false;
            storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);

            if (InputConduit.HasValue && !IsPower)
            {
                ConduitConsumer consumer = go.AddOrGet<ConduitConsumer>();
                consumer.conduitType = InputConduit.Value;
                consumer.ignoreMinMassCheck = true;
                consumer.forceAlwaysSatisfied = true;
                consumer.alwaysConsume = true;
                consumer.keepZeroMassObject = true;
                consumer.capacityKG = float.PositiveInfinity;
                consumer.consumptionRate = float.PositiveInfinity;
                consumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
                consumer.isConsuming = false;
            }

            if (OutputConduit.HasValue && !IsPower)
            {
                ConduitDispenser dispenser = go.AddOrGet<ConduitDispenser>();
                dispenser.conduitType = OutputConduit.Value;
                dispenser.useSecondaryOutput = false;
                dispenser.elementFilter = null;
                dispenser.alwaysDispense = true;
                dispenser.isOn = false;
            }
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            HeartPort port = go.AddOrGet<HeartPort>();
            port.kind = Kind;
            port.selectedElement = port.DefaultElementForKind();

            if (FilterState != Filterable.ElementState.None)
            {
                Filterable filterable = go.AddOrGet<Filterable>();
                filterable.filterElementState = FilterState;
            }

            if (InputConduit.HasValue && !IsPower)
            {
                go.AddOrGet<PortTransfer>();
            }

            if (IsPower)
            {
                Battery battery = go.AddOrGet<Battery>();
                battery.capacity = 1_000_000f;
                battery.chargeWattage = float.PositiveInfinity;
                battery.joulesLostPerSecond = 0f;
                battery.powerSortOrder = 1000;
                go.AddOrGet<PowerTransfer>();
            }
        }
    }

    public sealed class GasInConfig : HeartPortConfig
    {
        public const string ID = "ONIHeartGasIn";
        protected override string Id => ID;
        protected override HeartPortKind Kind => HeartPortKind.GasInput;
        protected override string Anim => "oniheart_gas_in_kanim";
        protected override int Width => 1;
        protected override int Height => 1;
        protected override BuildLocationRule LocationRule => BuildLocationRule.Anywhere;
        protected override ConduitType? InputConduit => ConduitType.Gas;
        protected override ConduitType? OutputConduit => null;
        protected override HashedString ViewMode => OverlayModes.GasConduits.ID;
        protected override HashSet<Tag> OverlayScreenId => OverlayScreen.GasVentIDs;
        protected override bool IsPower => false;
        protected override ObjectLayer Layer => ObjectLayer.Canvases;
    }

    public sealed class GasOutConfig : HeartPortConfig
    {
        public const string ID = "ONIHeartGasOut";
        protected override string Id => ID;
        protected override HeartPortKind Kind => HeartPortKind.GasOutput;
        protected override string Anim => "oniheart_gas_out_kanim";
        protected override int Width => 1;
        protected override int Height => 1;
        protected override BuildLocationRule LocationRule => BuildLocationRule.Anywhere;
        protected override ConduitType? InputConduit => null;
        protected override ConduitType? OutputConduit => ConduitType.Gas;
        protected override HashedString ViewMode => OverlayModes.GasConduits.ID;
        protected override HashSet<Tag> OverlayScreenId => OverlayScreen.GasVentIDs;
        protected override CellOffset OutputOffset => new CellOffset(0, 0);
        protected override bool IsPower => false;
        protected override ObjectLayer Layer => ObjectLayer.FillPlacer;
        protected override Filterable.ElementState FilterState => Filterable.ElementState.Gas;
    }

    public sealed class LiquidInConfig : HeartPortConfig
    {
        public const string ID = "ONIHeartLiquidIn";
        protected override string Id => ID;
        protected override HeartPortKind Kind => HeartPortKind.LiquidInput;
        protected override string Anim => "oniheart_liquid_in_kanim";
        protected override int Width => 1;
        protected override int Height => 1;
        protected override BuildLocationRule LocationRule => BuildLocationRule.Anywhere;
        protected override ConduitType? InputConduit => ConduitType.Liquid;
        protected override ConduitType? OutputConduit => null;
        protected override HashedString ViewMode => OverlayModes.LiquidConduits.ID;
        protected override HashSet<Tag> OverlayScreenId => OverlayScreen.LiquidVentIDs;
        protected override bool IsPower => false;
        protected override ObjectLayer Layer => ObjectLayer.PlasticTile;
    }

    public sealed class LiquidOutConfig : HeartPortConfig
    {
        public const string ID = "ONIHeartLiquidOut";
        protected override string Id => ID;
        protected override HeartPortKind Kind => HeartPortKind.LiquidOutput;
        protected override string Anim => "oniheart_liquid_out_kanim";
        protected override int Width => 1;
        protected override int Height => 1;
        protected override BuildLocationRule LocationRule => BuildLocationRule.Anywhere;
        protected override ConduitType? InputConduit => null;
        protected override ConduitType? OutputConduit => ConduitType.Liquid;
        protected override HashedString ViewMode => OverlayModes.LiquidConduits.ID;
        protected override HashSet<Tag> OverlayScreenId => OverlayScreen.LiquidVentIDs;
        protected override CellOffset OutputOffset => new CellOffset(0, 0);
        protected override bool IsPower => false;
        protected override ObjectLayer Layer => ObjectLayer.TravelTube;
        protected override Filterable.ElementState FilterState => Filterable.ElementState.Liquid;
    }

    public sealed class PowerConfig : HeartPortConfig
    {
        public const string ID = "ONIHeartPower";
        protected override string Id => ID;
        protected override HeartPortKind Kind => HeartPortKind.Power;
        protected override string Anim => "oniheart_power_kanim";
        protected override int Width => 1;
        protected override int Height => 1;
        protected override BuildLocationRule LocationRule => BuildLocationRule.Anywhere;
        protected override ConduitType? InputConduit => null;
        protected override ConduitType? OutputConduit => null;
        protected override HashedString ViewMode => OverlayModes.Power.ID;
        protected override HashSet<Tag> OverlayScreenId => OverlayScreen.WireIDs;
        protected override bool IsPower => true;
        protected override ObjectLayer Layer => ObjectLayer.TravelTubeConnection;
    }
}
