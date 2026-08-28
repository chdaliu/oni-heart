using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace ONIHeart
{
    public enum ManualBottlePhase
    {
        Gas,
        Liquid,
        Solid
    }

    /// <summary>手动装瓶器/卸瓶器/装载器与元素之心的连接。支持切换目标元素之心。</summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class ManualBottleLink : KMonoBehaviour, ISaveLoadable, ISim1000ms
    {
        public static readonly List<ManualBottleLink> All = new List<ManualBottleLink>();

        [Serialize]
        public int targetHeartCell = -1;

        private HeartOfElements heart;

        public HeartOfElements Heart => heart;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (!All.Contains(this))
            {
                All.Add(this);
            }

            ResolveHeart(force: true);
        }

        protected override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
            All.Remove(this);
            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            ResolveHeart(force: false);
        }

        public void ResolveHeart(bool force)
        {
            if (!force && heart != null && heart.Storage != null &&
                heart.GetBottomLeftCell() == targetHeartCell)
            {
                return;
            }

            int worldId = this.GetMyWorldId();
            HeartOfElements previous = heart;
            HeartOfElements found = null;

            if (targetHeartCell >= 0)
            {
                for (int i = 0; i < HeartOfElements.All.Count; i++)
                {
                    HeartOfElements candidate = HeartOfElements.All[i];
                    if (candidate != null && candidate.GetMyWorldId() == worldId &&
                        candidate.GetBottomLeftCell() == targetHeartCell)
                    {
                        found = candidate;
                        break;
                    }
                }
            }

            if (found == null)
            {
                float bestDistance = float.MaxValue;
                for (int i = 0; i < HeartOfElements.All.Count; i++)
                {
                    HeartOfElements candidate = HeartOfElements.All[i];
                    if (candidate == null || candidate.GetMyWorldId() != worldId)
                    {
                        continue;
                    }

                    float distance = (candidate.transform.GetPosition() - transform.GetPosition()).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        found = candidate;
                    }
                }

                if (found != null)
                {
                    targetHeartCell = found.GetBottomLeftCell();
                }
            }

            heart = found;
            if (previous != found)
            {
                RefreshUI();
            }
        }

        public void CycleHeart()
        {
            List<HeartOfElements> hearts = new List<HeartOfElements>();
            int worldId = this.GetMyWorldId();
            for (int i = 0; i < HeartOfElements.All.Count; i++)
            {
                HeartOfElements candidate = HeartOfElements.All[i];
                if (candidate != null && candidate.GetMyWorldId() == worldId)
                {
                    hearts.Add(candidate);
                }
            }

            if (hearts.Count == 0)
            {
                targetHeartCell = -1;
                heart = null;
                RefreshUI();
                return;
            }

            hearts.Sort((a, b) => a.GetBottomLeftCell().CompareTo(b.GetBottomLeftCell()));
            int index = hearts.FindIndex(h => h.GetBottomLeftCell() == targetHeartCell);
            HeartOfElements next = hearts[(index + 1) % hearts.Count];
            targetHeartCell = next.GetBottomLeftCell();
            heart = next;
            RefreshUI();
        }

        public string HeartName =>
            heart != null ? heart.gameObject.GetProperName() : STRINGS.UI.ONIHEART.NO_TARGET;

        private void RefreshUI()
        {
            if (Game.Instance != null)
            {
                Game.Instance.userMenu.Refresh(gameObject);
            }

            if (DetailsScreen.Instance != null)
            {
                DetailsScreen.Instance.Trigger((int)GameHashes.UIRefreshData);
            }
        }

        private void OnRefreshUserMenu(object data)
        {
            if (Game.Instance == null || Game.Instance.userMenu == null)
            {
                return;
            }

            Game.Instance.userMenu.AddButton(
                gameObject,
                new KIconButtonMenu.ButtonInfo(
                    "action_switch_toggle",
                    HeartName,
                    CycleHeart,
                    Action.NumActions,
                    null,
                    null,
                    null,
                    STRINGS.UI.ONIHEART.TARGET_TOOLTIP),
                0.5f);
        }
    }

    /// <summary>给物品打上来源/目标标签，用来阻断装瓶器 → 同一元素之心的卸瓶器循环。</summary>
    public static class ManualLoopGuard
    {
        public static Tag StoredTag(int heartCell)
        {
            return TagManager.Create("ONIHeartStored_" + heartCell);
        }

        public static Tag BottleSourceTag(int heartCell)
        {
            return TagManager.Create("ONIHeartBottleSource_" + heartCell);
        }
    }

    /// <summary>
    /// 手动装瓶器：像手压泵一样，只有复制人需要时才来操作一次。
    /// 建筑里维持一份“液体/气体源”标记物品，复制人取用该物品时触发工作，
    /// 完成后从元素之心取一份并生成一瓶。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class ManualBottleOutput : Workable, ISim1000ms, IGameObjectEffectDescriptor
    {
        private const float BOTTLE_MASS_KG = 200f;

        [Serialize]
        public SimHashes selectedElement = SimHashes.Water;

        public ManualBottlePhase phase = ManualBottlePhase.Liquid;

        private ManualBottleLink link;
        private Filterable filterable;
        private Storage sourceStorage;
        private GameObject sourceItem;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            SetWorkTime(5f);
            workAnims = new HashedString[] { "off" };
            showProgressBar = true;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            link = GetComponent<ManualBottleLink>();
            filterable = GetComponent<Filterable>();
            sourceStorage = GetComponent<Storage>();
            EnsureElement();

            if (filterable != null)
            {
                filterable.filterElementState = phase == ManualBottlePhase.Gas
                    ? Filterable.ElementState.Gas
                    : Filterable.ElementState.Liquid;

                if (selectedElement != SimHashes.Vacuum)
                {
                    filterable.SelectedTag = selectedElement.CreateTag();
                }

                filterable.onFilterChanged += OnFilterChanged;
            }
        }

        protected override void OnCleanUp()
        {
            if (filterable != null)
            {
                filterable.onFilterChanged -= OnFilterChanged;
            }

            base.OnCleanUp();
        }

        private void OnFilterChanged(Tag tag)
        {
            if (!tag.IsValid || tag == GameTags.Void)
            {
                selectedElement = SimHashes.Vacuum;
            }
            else
            {
                selectedElement = ElementLoader.GetElementID(tag);
            }

            ReturnSourceToHeart();
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (DetailsScreen.Instance != null)
            {
                DetailsScreen.Instance.Trigger((int)GameHashes.UIRefreshData);
            }
        }

        public void Sim1000ms(float dt)
        {
            MaintainSource();
        }

        private void MaintainSource()
        {
            if (link == null || link.Heart == null || link.Heart.Storage == null ||
                sourceStorage == null || selectedElement == SimHashes.Vacuum)
            {
                return;
            }

            if (sourceItem != null)
            {
                PrimaryElement existing = sourceItem.GetComponent<PrimaryElement>();
                if (existing != null && existing.Element.id == selectedElement && existing.Mass > 0.001f)
                {
                    Pickupable pickupable = sourceItem.GetComponent<Pickupable>();
                    if (pickupable != null)
                    {
                        pickupable.targetWorkable = this;
                        pickupable.SetOffsets(new CellOffset[] { new CellOffset(0, 1) });
                    }

                    return;
                }

                ReturnSourceToHeart();
            }

            PrimaryElement source = null;
            GameObject sourceObject = link.Heart.Storage.FindFirst(selectedElement.CreateTag());
            source = sourceObject != null ? sourceObject.GetComponent<PrimaryElement>() : null;
            if (source == null || source.Mass <= 0.001f)
            {
                return;
            }

            float amount = Mathf.Min(BOTTLE_MASS_KG, source.Mass);
            if (amount <= 0.001f)
            {
                return;
            }

            byte diseaseIdx = source.DiseaseIdx;
            int diseaseCount = Mathf.RoundToInt(source.DiseaseCount * (amount / source.Mass));
            Element element = source.Element;
            float temperature = source.Temperature;

            link.Heart.Storage.ConsumeIgnoringDisease(selectedElement.CreateTag(), amount);

            PrimaryElement added = sourceStorage.AddElement(
                element.id,
                amount,
                temperature,
                diseaseIdx,
                diseaseCount,
                false,
                false);

            if (added == null)
            {
                return;
            }

            sourceItem = added.gameObject;
            Pickupable itemPickupable = sourceItem.GetComponent<Pickupable>();
            if (itemPickupable != null)
            {
                itemPickupable.TotalAmount = amount;
                itemPickupable.targetWorkable = this;
                itemPickupable.SetOffsets(new CellOffset[] { new CellOffset(0, 1) });
            }

            KPrefabID prefabId = sourceItem.GetComponent<KPrefabID>();
            if (prefabId != null)
            {
                prefabId.AddTag(phase == ManualBottlePhase.Gas ? GameTags.GasSource : GameTags.LiquidSource);
                prefabId.AddTag(ManualLoopGuard.BottleSourceTag(link.Heart.GetBottomLeftCell()), true);
            }
        }

        private void ReturnSourceToHeart()
        {
            if (sourceItem == null || sourceStorage == null)
            {
                return;
            }

            PrimaryElement primary = sourceItem.GetComponent<PrimaryElement>();
            if (primary != null && link != null && link.Heart != null && link.Heart.Storage != null)
            {
                link.Heart.Storage.AddElement(
                    primary.Element.id,
                    primary.Mass,
                    primary.Temperature,
                    primary.DiseaseIdx,
                    primary.DiseaseCount,
                    false,
                    false);
            }

            sourceStorage.ConsumeIgnoringDisease(sourceItem);
            sourceItem = null;
        }

        protected override void OnCompleteWork(WorkerBase worker)
        {
            base.OnCompleteWork(worker);

            Pickupable.PickupableStartWorkInfo info = worker.GetStartWorkInfo() as Pickupable.PickupableStartWorkInfo;
            if (info == null || info.originalPickupable == null || sourceStorage == null)
            {
                return;
            }

            GameObject item = sourceItem != null ? sourceItem : info.originalPickupable.gameObject;
            if (item == null)
            {
                return;
            }

            Pickupable pickupable = item.GetComponent<Pickupable>();
            if (pickupable == null)
            {
                return;
            }

            if (pickupable.targetWorkable == this)
            {
                pickupable.targetWorkable = null;
            }

            KPrefabID prefabId = item.GetComponent<KPrefabID>();
            if (prefabId != null)
            {
                prefabId.RemoveTag(GameTags.GasSource);
                prefabId.RemoveTag(GameTags.LiquidSource);

                if (link != null && link.Heart != null)
                {
                    prefabId.AddTag(ManualLoopGuard.BottleSourceTag(link.Heart.GetBottomLeftCell()), true);
                }
            }

            Storage workerStorage = worker.GetComponent<Storage>();
            if (workerStorage != null)
            {
                if (sourceStorage.items != null && sourceStorage.items.Contains(item))
                {
                    sourceStorage.Transfer(item, workerStorage, block_events: false, hide_popups: true);
                }
                else if (workerStorage.items == null || !workerStorage.items.Contains(item))
                {
                    workerStorage.Store(item);
                }
            }

            worker.SetWorkCompleteData(pickupable);
            sourceItem = null;
        }

        public List<SimHashes> GetElementsForPhase()
        {
            List<SimHashes> result = new List<SimHashes>();
            bool gas = phase == ManualBottlePhase.Gas;

            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                if (element == null || element.disabled)
                {
                    continue;
                }

                if ((gas && element.IsGas) || (!gas && element.IsLiquid))
                {
                    result.Add(element.id);
                }
            }

            result.Sort((a, b) => ((int)a).CompareTo((int)b));
            return result;
        }

        public void EnsureElement()
        {
            List<SimHashes> valid = GetElementsForPhase();
            if (valid.Count == 0)
            {
                selectedElement = SimHashes.Vacuum;
                return;
            }

            if (!valid.Contains(selectedElement))
            {
                SimHashes preferred = phase == ManualBottlePhase.Gas
                    ? SimHashes.Oxygen
                    : SimHashes.Water;
                selectedElement = valid.Contains(preferred) ? preferred : valid[0];
            }
        }

        public override List<Descriptor> GetDescriptors(GameObject go)
        {
            return new List<Descriptor>
            {
                new Descriptor(
                    string.Format(STRINGS.UI.ONIHEART.PORT_TARGET, link != null ? link.HeartName : STRINGS.UI.ONIHEART.NO_TARGET),
                    STRINGS.UI.ONIHEART.TARGET_TOOLTIP),
                new Descriptor(
                    string.Format(STRINGS.UI.ONIHEART.MANUAL_OUTPUT_STATUS, SelectedElementName),
                    STRINGS.UI.ONIHEART.MANUAL_OUTPUT_TOOLTIP)
            };
        }

        public string SelectedElementName =>
            selectedElement == SimHashes.Vacuum
                ? STRINGS.UI.ONIHEART.NO_MATERIAL
                : selectedElement.CreateTag().ProperName();
    }

    /// <summary>
    /// 手动卸瓶器 / 手动装载器：让复制人把瓶装气体/液体或固体物品送进建筑，
    /// 再自动转入元素之心。支持勾选输入列表，但不从元素之心取物。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class ManualItemInput : KMonoBehaviour, ISim1000ms, IGameObjectEffectDescriptor
    {
        public ManualBottlePhase phase = ManualBottlePhase.Liquid;

        /// <summary>true：瓶装气体/液体，拆瓶后把元素质量写入元素之心；false：固体/物品，直接转移物品。</summary>
        public bool unbottle = true;

        private ManualBottleLink link;
        private Storage storage;
        private TreeFilterable filterable;
        private FilteredStorage filteredStorage;
        private int lastHeartCell = int.MinValue;
        private Tag storedTag;
        private Tag bottleSourceTag;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            link = GetComponent<ManualBottleLink>();
            storage = GetComponent<Storage>();
            filterable = GetComponent<TreeFilterable>();
            filteredStorage = new FilteredStorage(
                this,
                new Tag[0],
                null,
                use_logic_meter: false,
                Db.Get().ChoreTypes.StorageFetch);
            filteredStorage.SetHasMeter(false);
            filteredStorage.FilterChanged();
            UpdateLoopTags();
        }

        protected override void OnCleanUp()
        {
            if (filteredStorage != null)
            {
                filteredStorage.CleanUp();
                filteredStorage = null;
            }

            base.OnCleanUp();
        }

        public void Sim1000ms(float dt)
        {
            UpdateLoopTags();

            if (link == null || link.Heart == null || link.Heart.Storage == null ||
                storage == null)
            {
                return;
            }

            if (unbottle)
            {
                LoadBottles(link.Heart);
            }
            else
            {
                storage.Transfer(link.Heart.Storage, block_events: false, hide_popups: true);
            }
        }

        private void LoadBottles(HeartOfElements heart)
        {
            if (storage.items == null || storage.items.Count == 0)
            {
                return;
            }

            List<GameObject> items = new List<GameObject>(storage.items);
            for (int i = 0; i < items.Count; i++)
            {
                GameObject item = items[i];
                PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                if (primary == null || primary.Mass <= 0.001f)
                {
                    continue;
                }

                float mass = primary.Mass;
                float temperature = primary.Temperature;
                byte diseaseIdx = primary.DiseaseIdx;
                int diseaseCount = primary.DiseaseCount;
                PrimaryElement added = heart.Storage.AddElement(
                    primary.Element.id,
                    mass,
                    temperature,
                    diseaseIdx,
                    diseaseCount,
                    false,
                    false);

                if (added != null)
                {
                    primary.KeepZeroMassObject = false;
                    storage.ConsumeIgnoringDisease(item);
                }
            }
        }

        private void UpdateLoopTags()
        {
            int heartCell = link != null && link.Heart != null ? link.Heart.GetBottomLeftCell() : -1;
            if (heartCell == lastHeartCell)
            {
                return;
            }

            if (filteredStorage != null)
            {
                if (storedTag.IsValid)
                {
                    filteredStorage.RemoveForbiddenTag(storedTag);
                }

                if (bottleSourceTag.IsValid)
                {
                    filteredStorage.RemoveForbiddenTag(bottleSourceTag);
                }

                if (heartCell >= 0)
                {
                    if (link != null && link.Heart != null)
                    {
                        link.Heart.TagAllItems();
                    }

                    storedTag = ManualLoopGuard.StoredTag(heartCell);
                    filteredStorage.AddForbiddenTag(storedTag);

                    if (unbottle)
                    {
                        bottleSourceTag = ManualLoopGuard.BottleSourceTag(heartCell);
                        filteredStorage.AddForbiddenTag(bottleSourceTag);
                    }
                }
            }

            lastHeartCell = heartCell;
        }

        public List<Descriptor> GetDescriptors(GameObject go)
        {
            List<Descriptor> descriptors = new List<Descriptor>();
            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.PORT_TARGET, link != null ? link.HeartName : STRINGS.UI.ONIHEART.NO_TARGET),
                STRINGS.UI.ONIHEART.TARGET_TOOLTIP));

            descriptors.Add(new Descriptor(
                string.Format(
                    STRINGS.UI.ONIHEART.MANUAL_INPUT_STATUS,
                    PhaseName),
                unbottle
                    ? STRINGS.UI.ONIHEART.MANUAL_UNBOTTLE_TOOLTIP
                    : STRINGS.UI.ONIHEART.MANUAL_LOADER_TOOLTIP));

            return descriptors;
        }

        public string PhaseName
        {
            get
            {
                switch (phase)
                {
                    case ManualBottlePhase.Gas:
                        return STRINGS.UI.ONIHEART.MANUAL_PHASE_GAS;
                    case ManualBottlePhase.Solid:
                        return STRINGS.UI.ONIHEART.MANUAL_PHASE_SOLID;
                    default:
                        return STRINGS.UI.ONIHEART.MANUAL_PHASE_LIQUID;
                }
            }
        }
    }
}
