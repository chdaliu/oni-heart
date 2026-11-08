using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace ONIHeart
{
    public sealed class HeartConfig : IBuildingConfig
    {
        public const string ID = "ONIHeart";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID,
                4,
                4,
                "oniheart_kanim",
                1000,
                120f,
                new float[] { 1600f },
                new string[] { "BuildableRaw&Metal" },
                9999f,
                BuildLocationRule.OnFloor,
                TUNING.BUILDINGS.DECOR.NONE,
                TUNING.NOISE_POLLUTION.NONE);

            // The heart is a normal building: it occupies the Building layer, needs a foundation, and is blocked by obstacles.
            // Only interface buildings use separate placeholder layers to ignore obstacles.
            def.ObjectLayer = ObjectLayer.Building;
            def.ContinuouslyCheckFoundation = true;
            def.Invincible = true;

            // Allow flooding: do not add Floodable, so it never stops due to a "flooded" state.
            def.Floodable = false;

            def.Overheatable = false;
            def.Breakable = false;
            def.Repairable = true;
            def.ThermalConductivity = 0f;
            def.UseStructureTemperature = false;
            def.ExhaustKilowattsWhenActive = 0f;
            def.SelfHeatKilowattsWhenActive = 0f;
            def.EnergyConsumptionWhenActive = 0f;
            def.AudioCategory = "Metal";
            def.AudioSize = "large";
            def.PermittedRotations = PermittedRotations.Unrotatable;
            def.DefaultAnimState = "idle";
            def.BaseTimeUntilRepair = -1f;

            // Gentle lighting + high decor value
            def.BaseDecor = 100f;
            def.BaseDecorRadius = 10f;

            def.AddSearchTerms(global::STRINGS.SEARCH_TERMS.STORAGE);

            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.GetComponent<KPrefabID>().AddTag(GameTags.LightSource);

            BuildingComplete building = go.AddOrGet<BuildingComplete>();
            building.isManuallyOperated = false;

            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = float.MaxValue;
            storage.showInUI = true;
            storage.showDescriptor = false;
            storage.showCapacityStatusItem = false;
            storage.showCapacityAsMainStatus = false;
            storage.allowItemRemoval = true;
            storage.storageFilters = GetSolidItemCategories();
            storage.fetchCategory = Storage.FetchCategory.GeneralStorage;
            storage.SetDefaultStoredItemModifiers(new List<Storage.StoredItemModifier>
            {
                Storage.StoredItemModifier.Hide,
                Storage.StoredItemModifier.Seal,
                Storage.StoredItemModifier.Preserve,
                Storage.StoredItemModifier.Insulate
            });

            // Allow duplicants to prioritize hauling to it, like a normal storage bin.
            Prioritizable.AddRef(go);

            // Allow renaming so multiple hearts can be distinguished.
            go.AddOrGet<UserNameable>();

            // Provide a storage-bin-like material checklist containing only solid categories.
            // Gases/liquids are not listed and still enter/leave through interfaces, so filter changes must not drop existing gas/liquid.
            TreeFilterable filterable = go.AddOrGet<TreeFilterable>();
            filterable.dropIncorrectOnFilterChange = false;
            filterable.autoSelectStoredOnLoad = true;
            filterable.filterByStorageCategoriesOnSpawn = true;
        }

        /// <summary>
        /// The heart can collect all solid items (seeds, clothing, eggs, food, etc.),
        /// but gas/liquid categories are not added to the material list.
        /// </summary>
        public static List<Tag> GetSolidItemCategories()
        {
            List<Tag> categories = new List<Tag>(STORAGEFILTERS.STORAGE_LOCKERS_STANDARD);
            categories.AddRange(STORAGEFILTERS.FOOD);

            HashSet<Tag> seen = new HashSet<Tag>(categories);
            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                if (element == null || element.disabled || !element.IsSolid)
                {
                    continue;
                }

                Tag category = element.materialCategory;
                if (category.IsValid && seen.Add(category))
                {
                    categories.Add(category);
                }
            }

            return categories;
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<HeartOfElements>();
            go.AddOrGet<HeartDeconstructor>();
            go.AddOrGet<HeartProcessor>();
            go.AddOrGet<HeartSolidCollector>();
            go.AddOrGet<HeartAnimator>();
            // Debug: preload all elements on the first simulation tick for immediate recipe testing.
            go.AddOrGet<HeartDebugSeeder>();

            Light2D light = go.AddOrGet<Light2D>();
            light.Color = new Color(1f, 0.96f, 0.88f, 1f);
            light.Range = 6f;
            light.Lux = 600;
            light.IntensityAnimation = 0.62f;
            light.Offset = new Vector2(2f, 2f);
            light.shape = LightShape.Circle;
            light.FalloffRate = 0.5f;
            light.drawOverlay = false;
            go.AddOrGet<HeartLight>();

            go.GetComponent<KPrefabID>().AddTag(GameTags.OverlayBehindConduits);
        }
    }
}
