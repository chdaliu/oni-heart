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

        [Serialize]
        public SimHashes thermostatElement = SimHashes.Vacuum;

        [Serialize]
        public float thermostatTargetKelvin = 293.15f;

        [Serialize]
        public bool thermostatEnabled;

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

        public bool ThermostatEnabled => thermostatEnabled;

        public SimHashes ThermostatElement => thermostatElement;

        public float ThermostatTargetKelvin => thermostatTargetKelvin;

        public float ThermostatTargetCelsius => thermostatTargetKelvin - 273.15f;

        public string ThermostatElementName
        {
            get
            {
                if (thermostatElement == SimHashes.Vacuum)
                {
                    return STRINGS.UI.ONIHEART.NO_SUBSTANCE;
                }

                return thermostatElement.CreateTag().ProperName();
            }
        }

        public bool TryGetThermostatRange(out float minCelsius, out float maxCelsius)
        {
            minCelsius = 0f;
            maxCelsius = 0f;
            if (thermostatElement == SimHashes.Vacuum)
            {
                return false;
            }

            Element element = ElementLoader.GetElement(thermostatElement.CreateTag());
            float minKelvin;
            float maxKelvin;
            if (!HeartProcessor.TryGetThermostatRange(element, out minKelvin, out maxKelvin))
            {
                return false;
            }

            minCelsius = minKelvin - 273.15f;
            maxCelsius = maxKelvin - 273.15f;
            return true;
        }

        public void SetThermostatEnabled(bool enabled)
        {
            thermostatEnabled = enabled;
            RefreshUI();
        }

        public void SetThermostatTargetCelsius(float celsius)
        {
            float kelvin = celsius + 273.15f;
            float minCelsius;
            float maxCelsius;
            if (TryGetThermostatRange(out minCelsius, out maxCelsius))
            {
                kelvin = Mathf.Clamp(kelvin, minCelsius + 273.15f, maxCelsius + 273.15f);
            }

            thermostatTargetKelvin = kelvin;
            RefreshUI();
        }

        public void SetThermostatElement(Tag tag)
        {
            thermostatElement = tag.IsValid && tag != GameTags.Void
                ? ElementLoader.GetElementID(tag)
                : SimHashes.Vacuum;
            ClampThermostatTarget();
            RefreshUI();
        }

        private void NormalizeThermostat()
        {
            if (operation != HeartProcessOperation.Thermostat)
            {
                return;
            }

            if (float.IsNaN(thermostatTargetKelvin) || float.IsInfinity(thermostatTargetKelvin) ||
                thermostatTargetKelvin <= 0f)
            {
                thermostatTargetKelvin = 293.15f;
            }

            ClampThermostatTarget();
        }

        private void ClampThermostatTarget()
        {
            float minCelsius;
            float maxCelsius;
            if (TryGetThermostatRange(out minCelsius, out maxCelsius))
            {
                thermostatTargetKelvin = Mathf.Clamp(
                    thermostatTargetKelvin,
                    minCelsius + 273.15f,
                    maxCelsius + 273.15f);
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
                    if (!thermostatEnabled || thermostatElement == SimHashes.Vacuum)
                    {
                        state = STRINGS.UI.ONIHEART.TERMINAL_STATE_IDLE;
                    }
                    else if (processor.GetStoredMass(thermostatElement.CreateTag()) > 0.001f)
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

            if (operation == HeartProcessOperation.Thermostat)
            {
                descriptors.Add(new Descriptor(
                    string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_STATUS, ThermostatElementName),
                    STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_TOOLTIP));

                descriptors.Add(new Descriptor(
                    string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_TARGET_STATUS,
                        FormatCelsius(ThermostatTargetCelsius)),
                    STRINGS.UI.ONIHEART.THERMOSTAT_TARGET_STATUS_TOOLTIP));

                float minCelsius;
                float maxCelsius;
                if (TryGetThermostatRange(out minCelsius, out maxCelsius))
                {
                    descriptors.Add(new Descriptor(
                        string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_RANGE_STATUS,
                            FormatCelsius(minCelsius), FormatCelsius(maxCelsius)),
                        STRINGS.UI.ONIHEART.THERMOSTAT_RANGE_TOOLTIP));
                }
            }

            return descriptors;
        }

        private static string FormatCelsius(float celsius)
        {
            return celsius.ToString("0.#") + " °C";
        }
    }
}
