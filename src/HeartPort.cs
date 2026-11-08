using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace ONIHeart
{
    public enum HeartPortKind
    {
        GasInput,
        GasOutput,
        LiquidInput,
        LiquidOutput,
        Power
    }

    /// <summary>
    /// Shared connection state for all interface buildings. Responsibilities:
    /// 1. Find a heart in the same world.
    /// 2. Point the input/output components at that heart's Storage.
    /// 3. Provide a target-heart switch in the property panel button area.
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class HeartPort : KMonoBehaviour, ISaveLoadable, ISim1000ms, IGameObjectEffectDescriptor
    {
        public static readonly List<HeartPort> All = new List<HeartPort>();

        [Serialize]
        public int targetHeartCell = -1;

        [Serialize]
        public SimHashes selectedElement = SimHashes.Vacuum;

        public HeartPortKind kind;

        private HeartOfElements heart;
        private ConduitConsumer consumer;
        private ConduitDispenser dispenser;
        private Filterable filterable;

        public HeartOfElements Heart => heart;

        public bool IsGasPort =>
            kind == HeartPortKind.GasInput || kind == HeartPortKind.GasOutput;

        public bool IsLiquidPort =>
            kind == HeartPortKind.LiquidInput || kind == HeartPortKind.LiquidOutput;

        public bool IsInputPort =>
            kind == HeartPortKind.GasInput || kind == HeartPortKind.LiquidInput;

        public bool IsOutputPort =>
            kind == HeartPortKind.GasOutput || kind == HeartPortKind.LiquidOutput;

        public bool IsPowerPort => kind == HeartPortKind.Power;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            consumer = GetComponent<ConduitConsumer>();
            dispenser = GetComponent<ConduitDispenser>();
            filterable = GetComponent<Filterable>();

            if (filterable != null)
            {
                filterable.filterElementState = IsGasPort
                    ? Filterable.ElementState.Gas
                    : Filterable.ElementState.Liquid;
                if (selectedElement == SimHashes.Vacuum)
                {
                    selectedElement = DefaultElementForKind();
                }

                if (selectedElement != SimHashes.Vacuum)
                {
                    filterable.SelectedTag = selectedElement.CreateTag();
                }

                filterable.onFilterChanged += OnFilterChanged;
            }

            if (!All.Contains(this))
            {
                All.Add(this);
            }

            ResolveTarget(force: true);
        }

        protected override void OnCleanUp()
        {
            if (filterable != null)
            {
                filterable.onFilterChanged -= OnFilterChanged;
            }

            Unsubscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenu);
            All.Remove(this);
            base.OnCleanUp();
        }

        private void OnFilterChanged(Tag tag)
        {
            SetSelectedElement(tag);
        }

        public void Sim1000ms(float dt)
        {
            ResolveTarget(force: false);
        }

        public void ResolveTarget(bool force)
        {
            if (!force && heart != null && heart.gameObject != null &&
                heart.Storage != null && heart.GetBottomLeftCell() == targetHeartCell)
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
            ApplyTarget();
            if (previous != found)
            {
                RefreshUI();
            }
        }

        private void ApplyTarget()
        {
            if (heart == null || heart.Storage == null)
            {
                if (consumer != null)
                {
                    consumer.isConsuming = false;
                }

                if (dispenser != null)
                {
                    dispenser.isOn = false;
                }

                return;
            }

            if (consumer != null)
            {
                // Inputs use the interface's own Storage as a buffer; PortTransfer moves items into the heart.
                // Input interfaces do not filter elements; they accept whatever the pipe delivers.
                consumer.storage = GetComponent<Storage>();
                consumer.capacityKG = float.PositiveInfinity;
                consumer.consumptionRate = float.PositiveInfinity;
                consumer.ignoreMinMassCheck = true;
                consumer.forceAlwaysSatisfied = true;
                consumer.alwaysConsume = true;
                consumer.keepZeroMassObject = true;
                consumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
                consumer.capacityTag = GameTags.Any;
                consumer.isConsuming = true;
            }

            if (dispenser != null)
            {
                // Output interfaces must select one element and can output only that element.
                bool hasElement = selectedElement != SimHashes.Vacuum;
                dispenser.storage = heart.Storage;
                dispenser.elementFilter = hasElement ? new SimHashes[] { selectedElement } : null;
                dispenser.alwaysDispense = true;
                dispenser.isOn = hasElement;
            }
        }

        public void CycleHeart()
        {
            List<HeartOfElements> hearts = GetHeartsInWorld();
            if (hearts.Count == 0)
            {
                targetHeartCell = -1;
                heart = null;
                ApplyTarget();
                RefreshUI();
                return;
            }

            hearts.Sort((a, b) => a.GetBottomLeftCell().CompareTo(b.GetBottomLeftCell()));

            int index = hearts.FindIndex(h => h.GetBottomLeftCell() == targetHeartCell);
            HeartOfElements next = hearts[(index + 1) % hearts.Count];
            targetHeartCell = next.GetBottomLeftCell();
            heart = next;
            ApplyTarget();
            RefreshUI();
        }

        public void SetSelectedElement(Tag tag)
        {
            if (!tag.IsValid || tag == GameTags.Void)
            {
                selectedElement = SimHashes.Vacuum;
            }
            else
            {
                SimHashes element = ElementLoader.GetElementID(tag);
                selectedElement = element;
            }

            ApplyTarget();
            RefreshUI();
        }

        public void CycleSubstance()
        {
            List<SimHashes> elements = GetSubstanceElements();
            if (elements.Count == 0)
            {
                return;
            }

            int index = elements.IndexOf(selectedElement);
            selectedElement = elements[(index + 1) % elements.Count];

            Filterable filterable = GetComponent<Filterable>();
            if (filterable != null)
            {
                filterable.SelectedTag = selectedElement.CreateTag();
            }
            else
            {
                ApplyTarget();
            }

            RefreshUI();
        }

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

        public List<Descriptor> GetDescriptors(GameObject go)
        {
            List<Descriptor> descriptors = new List<Descriptor>();
            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.PORT_TARGET, HeartName),
                STRINGS.UI.ONIHEART.TARGET_TOOLTIP));

            string direction = STRINGS.UI.ONIHEART.PORT_DIRECTION_BIDIRECTIONAL;
            if (IsInputPort)
            {
                direction = STRINGS.UI.ONIHEART.PORT_DIRECTION_INPUT;
            }
            else if (IsOutputPort)
            {
                direction = STRINGS.UI.ONIHEART.PORT_DIRECTION_OUTPUT;
            }

            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.PORT_DIRECTION, direction),
                STRINGS.UI.ONIHEART.PORT_DIRECTION_TOOLTIP));

            if (IsOutputPort)
            {
                descriptors.Add(new Descriptor(
                    string.Format(STRINGS.UI.ONIHEART.PORT_ELEMENT, SelectedElementName),
                    STRINGS.UI.ONIHEART.SUBSTANCE_TOOLTIP));
            }
            else if (IsInputPort)
            {
                descriptors.Add(new Descriptor(
                    STRINGS.UI.ONIHEART.PORT_INPUT_AUTO,
                    STRINGS.UI.ONIHEART.PORT_INPUT_AUTO_TOOLTIP));
            }

            return descriptors;
        }

        public string SelectedElementName
        {
            get
            {
                if (selectedElement == SimHashes.Vacuum)
                {
                    return STRINGS.UI.ONIHEART.NO_SUBSTANCE;
                }

                return selectedElement.CreateTag().ProperName();
            }
        }

        public string HeartName
        {
            get
            {
                return heart != null
                    ? heart.gameObject.GetProperName()
                    : STRINGS.UI.ONIHEART.NO_TARGET;
            }
        }

        private List<HeartOfElements> GetHeartsInWorld()
        {
            int worldId = this.GetMyWorldId();
            List<HeartOfElements> hearts = new List<HeartOfElements>();
            for (int i = 0; i < HeartOfElements.All.Count; i++)
            {
                HeartOfElements candidate = HeartOfElements.All[i];
                if (candidate != null && candidate.GetMyWorldId() == worldId)
                {
                    hearts.Add(candidate);
                }
            }

            return hearts;
        }

        private List<SimHashes> GetSubstanceElements()
        {
            List<SimHashes> result = new List<SimHashes>();
            bool gas = IsGasPort;
            bool liquid = IsLiquidPort;

            if (!gas && !liquid)
            {
                return result;
            }

            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                if (element == null || element.disabled)
                {
                    continue;
                }

                if ((gas && element.IsGas) || (liquid && element.IsLiquid))
                {
                    result.Add(element.id);
                }
            }

            result.Sort((a, b) => ((int)a).CompareTo((int)b));
            return result;
        }

        public SimHashes DefaultElementForKind()
        {
            switch (kind)
            {
                case HeartPortKind.GasOutput:
                    return SimHashes.Oxygen;

                case HeartPortKind.LiquidOutput:
                    return SimHashes.Water;

                default:
                    return SimHashes.Vacuum;
            }
        }
    }
}
