using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// Element terminal: 2x2 science building. Selects and controls one processing operation of an element heart.
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class HeartTerminal : KMonoBehaviour, ISaveLoadable, ISim1000ms, IGameObjectEffectDescriptor
    {
        public static readonly List<HeartTerminal> All = new List<HeartTerminal>();

        [Serialize]
        public int targetHeartCell = -1;

        // Legacy single-element thermostat fields. They are kept so old saves can be migrated
        // to the per-element thermostat dictionaries below.
#pragma warning disable 0649
        [Serialize]
        private SimHashes thermostatElement = SimHashes.Vacuum;

        [Serialize]
        private float thermostatTargetKelvin = 293.15f;

        [Serialize]
        private bool thermostatEnabled;
#pragma warning restore 0649

        [Serialize]
        private Dictionary<Tag, float> thermostatTargets = new Dictionary<Tag, float>();

        [Serialize]
        private Dictionary<Tag, bool> thermostatEnabledByElement = new Dictionary<Tag, bool>();

        // Set by the building config; not serialized.
        public HeartProcessOperation operation = HeartProcessOperation.Decompose;

        private HeartOfElements heart;

        public HeartOfElements Heart => heart;

        public HeartProcessor Processor => heart != null ? heart.GetComponent<HeartProcessor>() : null;

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
            NormalizeThermostat();
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
        }

        public bool HasEnabledThermostats
        {
            get
            {
                EnsureThermostatDictionaries();
                foreach (KeyValuePair<Tag, bool> pair in thermostatEnabledByElement)
                {
                    if (pair.Value && ElementLoader.GetElement(pair.Key) != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public List<Tag> GetEnabledThermostatElements()
        {
            EnsureThermostatDictionaries();
            List<Tag> result = new List<Tag>();
            foreach (KeyValuePair<Tag, bool> pair in thermostatEnabledByElement)
            {
                if (pair.Value && ElementLoader.GetElement(pair.Key) != null)
                {
                    result.Add(pair.Key);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.ProperName(), b.ProperName()));
            return result;
        }

        public bool IsThermostatEnabled(Tag element)
        {
            EnsureThermostatDictionaries();
            bool enabled;
            return element.IsValid && thermostatEnabledByElement.TryGetValue(element, out enabled) && enabled;
        }

        public float GetThermostatTargetKelvin(Tag element)
        {
            EnsureThermostatDictionaries();
            float target;
            if (!element.IsValid || !thermostatTargets.TryGetValue(element, out target) ||
                float.IsNaN(target) || float.IsInfinity(target) || target <= 0f)
            {
                target = 293.15f;
            }

            return ClampThermostatTarget(element, target);
        }

        public float GetThermostatTargetCelsius(Tag element)
        {
            return GetThermostatTargetKelvin(element) - 273.15f;
        }

        public bool TryGetThermostatRange(Tag element, out float minCelsius, out float maxCelsius)
        {
            minCelsius = 0f;
            maxCelsius = 0f;
            if (!element.IsValid)
            {
                return false;
            }

            Element selected = ElementLoader.GetElement(element);
            float minKelvin;
            float maxKelvin;
            if (!HeartProcessor.TryGetThermostatRange(selected, out minKelvin, out maxKelvin))
            {
                return false;
            }

            minCelsius = minKelvin - 273.15f;
            maxCelsius = maxKelvin - 273.15f;
            return true;
        }

        public bool TryGetThermostatRange(SimHashes id, out float minCelsius, out float maxCelsius)
        {
            return TryGetThermostatRange(id.CreateTag(), out minCelsius, out maxCelsius);
        }

        public void SetThermostatEnabled(Tag element, bool enabled)
        {
            if (!element.IsValid)
            {
                return;
            }

            EnsureThermostatDictionaries();

            if (enabled && heart != null)
            {
                // One element can only be controlled by one thermostat terminal per heart.
                for (int i = 0; i < All.Count; i++)
                {
                    HeartTerminal other = All[i];
                    if (other == null || other == this || other.heart != heart ||
                        other.operation != HeartProcessOperation.Thermostat)
                    {
                        continue;
                    }

                    other.EnsureThermostatDictionaries();
                    if (other.thermostatEnabledByElement.ContainsKey(element))
                    {
                        other.thermostatEnabledByElement[element] = false;
                        other.RefreshUI();
                    }
                }
            }

            if (enabled && !thermostatTargets.ContainsKey(element))
            {
                thermostatTargets[element] = GetThermostatTargetKelvin(element);
            }

            thermostatEnabledByElement[element] = enabled;
            RefreshUI();
        }

        public void SetThermostatTargetCelsius(Tag element, float celsius)
        {
            if (!element.IsValid)
            {
                return;
            }

            EnsureThermostatDictionaries();
            thermostatTargets[element] = ClampThermostatTarget(element, celsius + 273.15f);
            RefreshUI();
        }

        private void EnsureThermostatDictionaries()
        {
            if (thermostatTargets == null)
            {
                thermostatTargets = new Dictionary<Tag, float>();
            }

            if (thermostatEnabledByElement == null)
            {
                thermostatEnabledByElement = new Dictionary<Tag, bool>();
            }
        }

        private void NormalizeThermostat()
        {
            EnsureThermostatDictionaries();

            // Migrate the old save format: a thermostat terminal used to store exactly one element.
            if (thermostatTargets.Count == 0 && thermostatEnabledByElement.Count == 0 &&
                thermostatElement != SimHashes.Vacuum)
            {
                Tag legacyTag = thermostatElement.CreateTag();
                if (ElementLoader.GetElement(legacyTag) != null)
                {
                    thermostatTargets[legacyTag] = thermostatTargetKelvin;
                    thermostatEnabledByElement[legacyTag] = thermostatEnabled;
                }

                thermostatElement = SimHashes.Vacuum;
            }

            if (operation != HeartProcessOperation.Thermostat)
            {
                return;
            }

            List<Tag> invalidTargets = new List<Tag>();
            List<Tag> targetKeys = new List<Tag>(thermostatTargets.Keys);
            for (int i = 0; i < targetKeys.Count; i++)
            {
                Tag key = targetKeys[i];
                if (ElementLoader.GetElement(key) == null)
                {
                    invalidTargets.Add(key);
                }
                else
                {
                    thermostatTargets[key] = ClampThermostatTarget(key, thermostatTargets[key]);
                }
            }

            for (int i = 0; i < invalidTargets.Count; i++)
            {
                thermostatTargets.Remove(invalidTargets[i]);
                thermostatEnabledByElement.Remove(invalidTargets[i]);
            }

            List<Tag> invalidEnabled = new List<Tag>();
            foreach (KeyValuePair<Tag, bool> pair in thermostatEnabledByElement)
            {
                if (ElementLoader.GetElement(pair.Key) == null)
                {
                    invalidEnabled.Add(pair.Key);
                }
            }

            for (int i = 0; i < invalidEnabled.Count; i++)
            {
                thermostatEnabledByElement.Remove(invalidEnabled[i]);
            }
        }

        private float ClampThermostatTarget(Tag element, float kelvin)
        {
            if (float.IsNaN(kelvin) || float.IsInfinity(kelvin) || kelvin <= 0f)
            {
                kelvin = 293.15f;
            }

            float minCelsius;
            float maxCelsius;
            if (TryGetThermostatRange(element, out minCelsius, out maxCelsius))
            {
                kelvin = Mathf.Clamp(kelvin, minCelsius + 273.15f, maxCelsius + 273.15f);
            }

            return kelvin;
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

        private void RefreshUI()
        {
            if (Game.Instance != null)
            {
                Game.Instance.userMenu.Refresh(gameObject);
            }

            // The "recipe / heart" status in the property panel must refresh too.
            if (DetailsScreen.Instance != null)
            {
                DetailsScreen.Instance.Trigger((int)GameHashes.UIRefreshData);
            }
        }

        public string HeartName =>
            heart != null ? heart.gameObject.GetProperName() : STRINGS.UI.ONIHEART.NO_TARGET;

        public string OperationTitle
        {
            get
            {
                switch (operation)
                {
                    case HeartProcessOperation.Decompose:
                        return STRINGS.UI.ONIHEART.PANEL_DECOMPOSE;
                    case HeartProcessOperation.Solidify:
                        return STRINGS.UI.ONIHEART.PANEL_SOLIDIFY;
                    case HeartProcessOperation.Liquefy:
                        return STRINGS.UI.ONIHEART.PANEL_LIQUEFY;
                    case HeartProcessOperation.Vaporize:
                        return STRINGS.UI.ONIHEART.PANEL_VAPORIZE;
                    case HeartProcessOperation.Thermostat:
                        return STRINGS.UI.ONIHEART.PANEL_THERMOSTAT;
                    default:
                        return STRINGS.UI.ONIHEART.PROCESS_OFF;
                }
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
            string state = STRINGS.UI.ONIHEART.TERMINAL_STATE_DISCONNECTED;
            HeartProcessor processor = Processor;
            if (heart != null && processor != null)
            {
                if (operation == HeartProcessOperation.Thermostat)
                {
                    List<Tag> enabled = GetEnabledThermostatElements();
                    if (enabled.Count == 0)
                    {
                        state = STRINGS.UI.ONIHEART.TERMINAL_STATE_IDLE;
                    }
                    else if (HasStoredThermostatInput(processor, enabled))
                    {
                        state = STRINGS.UI.ONIHEART.TERMINAL_STATE_RUNNING;
                    }
                    else
                    {
                        state = STRINGS.UI.ONIHEART.TERMINAL_STATE_WAITING;
                    }
                }
                else if (processor.IsOperationActive(operation))
                {
                    state = processor.HasStoredInput(operation)
                        ? STRINGS.UI.ONIHEART.TERMINAL_STATE_RUNNING
                        : STRINGS.UI.ONIHEART.TERMINAL_STATE_WAITING;
                }
                else
                {
                    state = STRINGS.UI.ONIHEART.TERMINAL_STATE_IDLE;
                }
            }

            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.TERMINAL_STATE, state),
                STRINGS.UI.ONIHEART.TERMINAL_STATE_TOOLTIP));

            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.TERMINAL_STATUS, OperationTitle, HeartName),
                STRINGS.UI.ONIHEART.TERMINAL_STATUS_TOOLTIP));

            if (operation == HeartProcessOperation.Solidify ||
                operation == HeartProcessOperation.Liquefy ||
                operation == HeartProcessOperation.Vaporize)
            {
                descriptors.Add(new Descriptor(
                    STRINGS.UI.ONIHEART.PHASE_ONLY_STATUS,
                    STRINGS.UI.ONIHEART.PHASE_ONLY_TOOLTIP));
            }

            if (operation == HeartProcessOperation.Thermostat)
            {
                List<Tag> enabled = GetEnabledThermostatElements();
                if (enabled.Count == 0)
                {
                    descriptors.Add(new Descriptor(
                        string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_STATUS,
                            STRINGS.UI.ONIHEART.THERMOSTAT_NOT_ENABLED),
                        STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_TOOLTIP));
                }
                else
                {
                    for (int i = 0; i < enabled.Count; i++)
                    {
                        Element element = ElementLoader.GetElement(enabled[i]);
                        if (element == null)
                        {
                            continue;
                        }

                        float minCelsius;
                        float maxCelsius;
                        string range = TryGetThermostatRange(enabled[i], out minCelsius, out maxCelsius)
                            ? string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_RANGE_STATUS,
                                FormatCelsius(minCelsius), FormatCelsius(maxCelsius))
                            : STRINGS.UI.ONIHEART.THERMOSTAT_NO_RANGE;

                        descriptors.Add(new Descriptor(
                            string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_STATUS,
                                string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_TARGET,
                                    element.name,
                                    FormatCelsius(GetThermostatTargetCelsius(enabled[i])),
                                    range)),
                            STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_TOOLTIP));
                    }
                }
            }

            return descriptors;
        }

        private static bool HasStoredThermostatInput(HeartProcessor processor, List<Tag> elements)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                if (processor.GetStoredMass(elements[i]) > 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatCelsius(float celsius)
        {
            return celsius.ToString("0.#") + " °C";
        }
    }
}
