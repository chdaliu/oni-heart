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
