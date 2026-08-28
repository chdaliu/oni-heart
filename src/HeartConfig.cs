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

            // 元素之心是正常建筑：占用 Building 层、需要地基、会被障碍阻挡。
            // 只有接口建筑才使用独立的占位层来无视障碍。
            def.ObjectLayer = ObjectLayer.Building;
            def.ContinuouslyCheckFoundation = true;
            def.Invincible = true;

            // 允许被液体淹没：不添加 Floodable，不会被“淹没”状态停机。
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

            // 轻微照明 + 大量装饰值
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

            // 允许复制人优先搬运，和普通箱子一样有工作优先级。
            Prioritizable.AddRef(go);

            // 允许玩家重命名，以便区分多个元素之心。
            go.AddOrGet<UserNameable>();

            // 像容器箱子一样提供材料勾选列表：只包含固体分类。
            // 气体/液体不在列表里，仍通过接口输入/输出；因此不允许过滤变更把已有气液掉出来。
            TreeFilterable filterable = go.AddOrGet<TreeFilterable>();
            filterable.dropIncorrectOnFilterChange = false;
            filterable.autoSelectStoredOnLoad = true;
            filterable.filterByStorageCategoriesOnSpawn = true;
        }

        /// <summary>
        /// 元素之心可以收集所有固体物品（含种子、衣服、蛋、食物等），
        /// 但不会把气体/液体分类加入材料列表。
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
            // 调试：首次模拟时预装全部元素，便于直接测试所有配方。
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
