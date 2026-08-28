using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// 调试辅助：元素之心第一次进入模拟时，预装全部气体、液体和固体元素各一份，
    /// 方便直接测试分解 / 固化 / 液化 / 气化配方，不必先接管道或手动搬运。
    ///
    /// 把 Enabled 改成 false 即可关闭。SeedVersion 用于在更新调试内容后，
    /// 让已有存档的元素之心再补装一次缺少的元素；正常消耗不会自动补满。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class HeartDebugSeeder : KMonoBehaviour, ISim200ms
    {
        public const bool Enabled = true;
        public const float MassPerElement = 1000f;

        // 每次需要重新灌注全部元素时加 1。
        public const int SeedVersion = 2;

        [Serialize]
        private int seedVersion;

        private HeartOfElements heart;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            heart = GetComponent<HeartOfElements>();
        }

        public void Sim200ms(float dt)
        {
            if (!Enabled || seedVersion >= SeedVersion || heart == null || heart.Storage == null)
            {
                return;
            }

            seedVersion = SeedVersion;
            SeedElements(heart.Storage);
            SelectAllStoredTags();
        }

        private static void SeedElements(Storage storage)
        {
            Dictionary<Tag, float> stored = new Dictionary<Tag, float>();
            if (storage.items != null)
            {
                for (int i = 0; i < storage.items.Count; i++)
                {
                    GameObject item = storage.items[i];
                    PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                    if (primary == null || primary.Mass <= 0f)
                    {
                        continue;
                    }

                    Tag tag = primary.Element.tag;
                    float existing;
                    stored.TryGetValue(tag, out existing);
                    stored[tag] = existing + primary.Mass;
                }
            }

            int added = 0;
            int gases = 0;
            int liquids = 0;
            int solids = 0;
            int skipped = 0;
            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                if (element == null || element.disabled)
                {
                    continue;
                }

                if (element.id == SimHashes.Vacuum ||
                    element.id == SimHashes.Void ||
                    element.id == SimHashes.Unobtanium ||
                    element.id == SimHashes.Creature ||
                    element.id == SimHashes.COMPOSITION)
                {
                    continue;
                }

                if (element.substance == null || element.substance.anim == null)
                {
                    continue;
                }

                // 固体元素需要能生成对应物品，否则 AddElement 只会报错。
                if (element.IsSolid && Assets.TryGetPrefab(element.tag) == null)
                {
                    continue;
                }

                float existing;
                stored.TryGetValue(element.tag, out existing);
                if (existing >= MassPerElement - 0.01f)
                {
                    continue;
                }

                float temperature = StableTemperature(element);
                try
                {
                    PrimaryElement spawned = storage.AddElement(
                        element.id,
                        MassPerElement - existing,
                        temperature,
                        byte.MaxValue,
                        0,
                        false,
                        false);
                    if (spawned == null && !element.IsSolid)
                    {
                        skipped++;
                        Debug.LogWarning("[ONI-Heart] Debug seed could not add " + element.tag);
                    }
                    else
                    {
                        added++;
                        if (element.IsGas)
                        {
                            gases++;
                        }
                        else if (element.IsLiquid)
                        {
                            liquids++;
                        }
                        else if (element.IsSolid)
                        {
                            solids++;
                        }
                    }
                }
                catch (Exception exception)
                {
                    skipped++;
                    Debug.LogWarning("[ONI-Heart] Debug seed failed for " + element.tag + ": " + exception.Message);
                }
            }

            Debug.Log(string.Format(
                "[ONI-Heart] Debug seed added {0} element stacks ({1} gas, {2} liquid, {3} solid), skipped {4}.",
                added, gases, liquids, solids, skipped));
        }

        private static float StableTemperature(Element element)
        {
            float low = element.lowTemp;
            float high = element.highTemp;
            if (high - low > 2f)
            {
                return Mathf.Clamp(293.15f, low + 1f, high - 1f);
            }

            return Mathf.Clamp(element.defaultValues.temperature, 1f, 9999f);
        }

        private void SelectAllStoredTags()
        {
            TreeFilterable filterable = GetComponent<TreeFilterable>();
            Storage storage = heart != null ? heart.Storage : null;
            if (filterable == null || storage == null)
            {
                return;
            }

            HashSet<Tag> tags = new HashSet<Tag>(filterable.AcceptedTags);
            if (storage.items != null)
            {
                for (int i = 0; i < storage.items.Count; i++)
                {
                    KPrefabID prefabId = storage.items[i] != null
                        ? storage.items[i].GetComponent<KPrefabID>()
                        : null;
                    // 只勾选固体物品；气体/液体仍通过接口进出，不应产生搬运任务。
                    if (prefabId != null && prefabId.HasTag(GameTags.Solid))
                    {
                        tags.Add(prefabId.PrefabTag);
                    }
                }
            }

            // 同时勾选当前已发现的全部同类别资源，让复制人能把所有品类搬进来。
            if (storage.storageFilters != null)
            {
                for (int i = 0; i < storage.storageFilters.Count; i++)
                {
                    HashSet<Tag> discovered = DiscoveredResources.Instance
                        .GetDiscoveredResourcesFromTag(storage.storageFilters[i]);
                    if (discovered != null)
                    {
                        tags.UnionWith(discovered);
                    }
                }
            }

            filterable.UpdateFilters(tags);
        }
    }
}
