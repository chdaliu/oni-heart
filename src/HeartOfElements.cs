using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace ONIHeart
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class HeartOfElements : KMonoBehaviour, IGameObjectEffectDescriptor, ISaveLoadable
    {
        public static readonly List<HeartOfElements> All = new List<HeartOfElements>();

        [Serialize]
        private float storedJoules;

        private Storage storage;
        private bool mergeDirty;
        private bool merging;

        public Storage Storage => storage;
        public float StoredJoules => storedJoules;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            storage = GetComponent<Storage>();
            if (storage != null)
            {
                storage.OnStorageChange += OnStorageChanged;
                TagAllItems();
                mergeDirty = true;
                MergeGasLiquidStacksIfNeeded();
            }

            if (!All.Contains(this))
            {
                All.Add(this);
            }
        }

        protected override void OnCleanUp()
        {
            if (storage != null)
            {
                storage.OnStorageChange -= OnStorageChanged;
            }

            All.Remove(this);
            base.OnCleanUp();
        }

        private void OnStorageChanged(GameObject item)
        {
            TagItem(item);
            DiscoverItem(item);

            // Gases/liquids of the same element must be kept as one stack with a mass-weighted
            // temperature. New items can arrive through pipes, bottles, recipes or vanilla transfers.
            if (!merging)
            {
                PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                if (primary != null && primary.Element != null &&
                    (primary.Element.IsGas || primary.Element.IsLiquid))
                {
                    mergeDirty = true;
                }
            }
        }

        public void TagAllItems()
        {
            if (storage == null || storage.items == null)
            {
                return;
            }

            for (int i = 0; i < storage.items.Count; i++)
            {
                TagItem(storage.items[i]);
                DiscoverItem(storage.items[i]);
            }
        }

        private static void DiscoverItem(GameObject item)
        {
            if (item == null || DiscoveredResources.Instance == null)
            {
                return;
            }

            PrimaryElement primary = item.GetComponent<PrimaryElement>();
            if (primary != null && primary.Element != null)
            {
                DiscoveredResources.Instance.Discover(primary.Element.tag, primary.Element.GetMaterialCategoryTag());
            }
        }

        private void TagItem(GameObject item)
        {
            if (item == null)
            {
                return;
            }

            KPrefabID prefabId = item.GetComponent<KPrefabID>();
            if (prefabId == null)
            {
                return;
            }

            prefabId.AddTag(TagManager.Create("ONIHeartStored_" + GetBottomLeftCell()), true);
        }

        /// <summary>
        /// Adds a gas/liquid element into the heart. If the same element is already stored, the two
        /// portions are merged into one stack and the temperature becomes the mass-weighted average
        /// instead of keeping two stacks with different temperatures.
        /// </summary>
        public PrimaryElement AddElementMerged(SimHashes id, float mass, float temperature,
                                               byte diseaseIdx, int diseaseCount,
                                               bool keepZeroMassObject, bool doDiseaseTransfer)
        {
            if (storage == null || mass <= 0f)
            {
                return null;
            }

            Element element = ElementLoader.GetElement(id.CreateTag());
            if (element == null || (!element.IsGas && !element.IsLiquid))
            {
                return storage.AddElement(id, mass, temperature, diseaseIdx, diseaseCount,
                                          keepZeroMassObject, doDiseaseTransfer);
            }

            GameObject existingObject = FindStack(id);
            if (existingObject == null)
            {
                PrimaryElement added = storage.AddElement(id, mass, temperature, diseaseIdx, diseaseCount,
                                                          keepZeroMassObject, doDiseaseTransfer);
                mergeDirty = false;
                return added;
            }

            PrimaryElement existing = existingObject.GetComponent<PrimaryElement>();
            if (existing == null)
            {
                PrimaryElement added = storage.AddElement(id, mass, temperature, diseaseIdx, diseaseCount,
                                                          keepZeroMassObject, doDiseaseTransfer);
                mergeDirty = false;
                return added;
            }

            double totalMass = (double)existing.Mass + mass;
            if (totalMass <= 0d)
            {
                return existing;
            }

            float mergedTemperature = (float)(
                ((double)existing.Mass * existing.Temperature + (double)mass * temperature) / totalMass);
            merging = true;
            existing.SetMassTemperature((float)Math.Min(float.MaxValue, totalMass), mergedTemperature);
            if (diseaseCount > 0 &&
                (existing.DiseaseCount <= 0 || existing.DiseaseIdx == diseaseIdx))
            {
                existing.AddDisease(diseaseIdx, diseaseCount, "ONIHeart temperature merge");
            }
            merging = false;
            mergeDirty = false;
            return existing;
        }

        /// <summary>Moves every item from a source Storage into the heart, merging gas/liquid elements by temperature.</summary>
        public void AbsorbStorage(Storage source)
        {
            if (storage == null || source == null || source.items == null)
            {
                return;
            }

            List<GameObject> items = new List<GameObject>(source.items);
            for (int i = 0; i < items.Count; i++)
            {
                GameObject item = items[i];
                if (item == null)
                {
                    continue;
                }

                PrimaryElement primary = item.GetComponent<PrimaryElement>();
                if (primary == null)
                {
                    continue;
                }

                if (primary.Mass <= 0f)
                {
                    source.ConsumeIgnoringDisease(item);
                    continue;
                }

                Element element = primary.Element;
                if (element != null && (element.IsGas || element.IsLiquid))
                {
                    PrimaryElement added = AddElementMerged(
                        element.id, primary.Mass, primary.Temperature,
                        primary.DiseaseIdx, primary.DiseaseCount, false, true);
                    if (added != null)
                    {
                        source.ConsumeIgnoringDisease(item);
                    }
                }
                else
                {
                    // Solid items keep their own identity and are simply stored.
                    storage.Store(item);
                }
            }
        }

        /// <summary>
        /// Normalizes loaded saves and any storage changes: one gas/liquid stack per element,
        /// with a mass-weighted average temperature.
        /// </summary>
        public void MergeGasLiquidStacksIfNeeded()
        {
            if (!mergeDirty || storage == null || storage.items == null)
            {
                return;
            }

            mergeDirty = false;
            merging = true;
            try
            {
                Dictionary<SimHashes, PrimaryElement> byElement = new Dictionary<SimHashes, PrimaryElement>();
                List<GameObject> items = new List<GameObject>(storage.items);
                for (int i = 0; i < items.Count; i++)
                {
                    GameObject item = items[i];
                    PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                    if (primary == null || primary.Mass <= 0f)
                    {
                        continue;
                    }

                    Element element = primary.Element;
                    if (element == null || (!element.IsGas && !element.IsLiquid))
                    {
                        continue;
                    }

                    PrimaryElement existing;
                    if (!byElement.TryGetValue(element.id, out existing) || existing == null)
                    {
                        byElement[element.id] = primary;
                        continue;
                    }

                    double totalMass = (double)existing.Mass + primary.Mass;
                    if (totalMass <= 0d)
                    {
                        continue;
                    }

                    float mergedTemperature = (float)(
                        ((double)existing.Mass * existing.Temperature +
                         (double)primary.Mass * primary.Temperature) / totalMass);
                    existing.SetMassTemperature((float)Math.Min(float.MaxValue, totalMass), mergedTemperature);
                    if (primary.DiseaseCount > 0 &&
                        (existing.DiseaseCount <= 0 || existing.DiseaseIdx == primary.DiseaseIdx))
                    {
                        existing.AddDisease(primary.DiseaseIdx, primary.DiseaseCount,
                                            "ONIHeart temperature merge");
                    }

                    storage.ConsumeIgnoringDisease(item);
                }
            }
            finally
            {
                merging = false;
                mergeDirty = false;
            }
        }

        private GameObject FindStack(SimHashes id)
        {
            if (storage == null || storage.items == null)
            {
                return null;
            }

            for (int i = 0; i < storage.items.Count; i++)
            {
                GameObject item = storage.items[i];
                PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                if (primary != null && primary.Mass > 0f && primary.Element != null &&
                    primary.Element.id == id)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Mass-weighted temperature of a stored element, or 0 when it is not present.</summary>
        public float GetStoredTemperature(Tag tag)
        {
            if (storage == null || storage.items == null || !tag.IsValid)
            {
                return 0f;
            }

            double totalMass = 0d;
            double weighted = 0d;
            for (int i = 0; i < storage.items.Count; i++)
            {
                GameObject item = storage.items[i];
                PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                if (primary != null && primary.Element != null && primary.Element.tag == tag &&
                    primary.Mass > 0f)
                {
                    totalMass += primary.Mass;
                    weighted += (double)primary.Mass * primary.Temperature;
                }
            }

            return totalMass > 0d ? (float)(weighted / totalMass) : 0f;
        }

        public int GetBottomLeftCell()
        {
            Building building = GetComponent<Building>();
            return building != null ? building.GetBottomLeftCell() : Grid.PosToCell(this);
        }

        public void StorePower(float joules)
        {
            if (joules <= 0f)
            {
                return;
            }

            double total = (double)storedJoules + joules;
            storedJoules = (float)Math.Min(float.MaxValue, total);
        }

        public void ClearPower()
        {
            storedJoules = 0f;
        }

        public float ConsumePower(float maxJoules)
        {
            if (maxJoules <= 0f)
            {
                return 0f;
            }

            float taken = Math.Min(storedJoules, maxJoules);
            storedJoules -= taken;
            return taken;
        }

        public List<Descriptor> GetDescriptors(GameObject go)
        {
            List<Descriptor> descriptors = new List<Descriptor>();

            float mass = storage != null ? storage.MassStored() : 0f;
            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.MASS_STORED, GameUtil.GetFormattedMass(mass)),
                STRINGS.UI.ONIHEART.MASS_STORED_TOOLTIP));

            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.POWER_STORED, GameUtil.GetFormattedJoules(storedJoules)),
                STRINGS.UI.ONIHEART.POWER_STORED_TOOLTIP));

            int terminals = 0;
            for (int i = 0; i < HeartTerminal.All.Count; i++)
            {
                HeartTerminal terminal = HeartTerminal.All[i];
                if (terminal != null && terminal.Heart == this)
                {
                    terminals++;
                }
            }

            int ports = 0;
            for (int i = 0; i < HeartPort.All.Count; i++)
            {
                HeartPort port = HeartPort.All[i];
                if (port != null && port.Heart == this)
                {
                    ports++;
                }
            }

            int bottles = 0;
            for (int i = 0; i < ManualBottleLink.All.Count; i++)
            {
                ManualBottleLink link = ManualBottleLink.All[i];
                if (link != null && link.Heart == this)
                {
                    bottles++;
                }
            }

            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.CONNECTED_DEVICES, terminals, ports, bottles),
                STRINGS.UI.ONIHEART.CONNECTED_DEVICES_TOOLTIP));

            descriptors.Add(new Descriptor(
                STRINGS.UI.ONIHEART.HEART_HELP,
                STRINGS.UI.ONIHEART.HEART_HELP_TOOLTIP));

            return descriptors;
        }
    }
}
