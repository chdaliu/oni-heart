using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace ONIHeart
{
    public enum HeartProcessOperation
    {
        Off,
        Decompose,
        Solidify,
        Liquefy,
        Vaporize,
        Thermostat
    }

    /// <summary>
    /// Element heart recipe processing:
    /// - Decompose: one water recipe using an adjustable rate (kg/s); rate 0 disables it.
    /// - Solidify / Liquefy / Vaporize: each recipe has one input element and a target output amount (kg);
    ///   amount 0 disables it. Multiple recipes can run simultaneously.
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class HeartProcessor : KMonoBehaviour, ISim200ms, IGameObjectEffectDescriptor
    {
        public const float MAX_DECOMPOSE_RATE = 20f;
        public const float MAX_CONVERSION_AMOUNT = 10000f;

        // Only allow elements within the normal temperature range; exclude extreme elements such as gaseous rock, magma, and molten metal.
        private const float MIN_CONVENTIONAL_TEMPERATURE = 10f;
        private const float MAX_CONVENTIONAL_TEMPERATURE = 1000f;

        public const float THERMOSTAT_MARGIN_KELVIN = 2f;

        private const float CONVERSION_RATE_KG_PER_SECOND = 0.5f;
        private const float DECOMPOSE_POWER_PER_KG = 120f;
        private const float DECOMPOSE_HEAT_PER_KG = 1250f;
        private const float DECOMPOSE_TEMPERATURE = 343.15f;
        private const float WATER_TO_OXYGEN = 0.888f;
        private const float WATER_TO_HYDROGEN = 0.112f;

        [Serialize]
        private float decomposeRate;

        [Serialize]
        private Dictionary<Tag, float> solidifyPending = new Dictionary<Tag, float>();

        [Serialize]
        private Dictionary<Tag, float> liquefyPending = new Dictionary<Tag, float>();

        [Serialize]
        private Dictionary<Tag, float> vaporizePending = new Dictionary<Tag, float>();

        private HeartOfElements heart;
        private readonly object pendingLock = new object();

        public float DecomposeRate => decomposeRate;

        public bool IsDecomposeActive => decomposeRate > 0.0001f;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            heart = GetComponent<HeartOfElements>();
            NormalizeDictionaries();
        }

        private void NormalizeDictionaries()
        {
            if (solidifyPending == null)
            {
                solidifyPending = new Dictionary<Tag, float>();
            }

            if (liquefyPending == null)
            {
                liquefyPending = new Dictionary<Tag, float>();
            }

            if (vaporizePending == null)
            {
                vaporizePending = new Dictionary<Tag, float>();
            }
        }

        public void SetDecomposeRate(float rate)
        {
            decomposeRate = Mathf.Clamp(rate, 0f, MAX_DECOMPOSE_RATE);
        }

        public float GetPending(HeartProcessOperation operationKind, Tag element)
        {
            lock (pendingLock)
            {
                NormalizeDictionaries();
                Dictionary<Tag, float> map = GetPendingMap(operationKind);
                if (map == null || !element.IsValid)
                {
                    return 0f;
                }

                float value;
                return map.TryGetValue(element, out value) ? value : 0f;
            }
        }

        public void SetPending(HeartProcessOperation operationKind, Tag element, float amount)
        {
            lock (pendingLock)
            {
                NormalizeDictionaries();
                Dictionary<Tag, float> map = GetPendingMap(operationKind);
                if (map == null || !element.IsValid)
                {
                    return;
                }

                if (amount <= 0.001f)
                {
                    map.Remove(element);
                }
                else
                {
                    map[element] = Mathf.Clamp(amount, 0f, MAX_CONVERSION_AMOUNT);
                }
            }
        }

        private Dictionary<Tag, float> GetPendingMap(HeartProcessOperation operationKind)
        {
            switch (operationKind)
            {
                case HeartProcessOperation.Solidify:
                    return solidifyPending;
                case HeartProcessOperation.Liquefy:
                    return liquefyPending;
                case HeartProcessOperation.Vaporize:
                    return vaporizePending;
                default:
                    return null;
            }
        }

        public List<Tag> GetPendingElements(HeartProcessOperation operationKind)
        {
            List<Tag> result = new List<Tag>();
            lock (pendingLock)
            {
                NormalizeDictionaries();
                Dictionary<Tag, float> map = GetPendingMap(operationKind);
                if (map == null)
                {
                    return result;
                }

                foreach (KeyValuePair<Tag, float> pair in map)
                {
                    if (pair.Value > 0.001f)
                    {
                        result.Add(pair.Key);
                    }
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.ProperName(), b.ProperName()));
            return result;
        }

        /// <summary>Recipe inputs for the UI: phase-valid inputs currently in storage plus inputs with pending amounts.</summary>
        public List<Tag> GetRecipeInputs(HeartProcessOperation operationKind)
        {
            if (operationKind == HeartProcessOperation.Thermostat)
            {
                return new List<Tag>();
            }

            NormalizeDictionaries();
            HashSet<Tag> tags = new HashSet<Tag>(GetPendingElements(operationKind));

            if (heart != null && heart.Storage != null)
            {
                for (int i = 0; i < heart.Storage.items.Count; i++)
                {
                    GameObject item = heart.Storage.items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    PrimaryElement primary = item.GetComponent<PrimaryElement>();
                    if (primary == null || primary.Mass <= 0f)
                    {
                        continue;
                    }

                    if (IsValidInput(primary.Element, operationKind))
                    {
                        tags.Add(primary.Element.tag);
                    }
                }
            }

            List<Tag> result = new List<Tag>(tags);
            result.Sort((a, b) => string.CompareOrdinal(a.ProperName(), b.ProperName()));
            return result;
        }

        /// <summary>
        /// For the panel: list all valid recipes for this operation regardless of current heart inventory.
        /// This lets players configure target amounts before delivering materials.
        /// </summary>
        public List<Tag> GetAllRecipeInputs(HeartProcessOperation operationKind)
        {
            List<Tag> result = new List<Tag>();
            if (operationKind == HeartProcessOperation.Thermostat)
            {
                return result;
            }

            if (operationKind == HeartProcessOperation.Decompose)
            {
                result.Add(SimHashes.Water.CreateTag());
                return result;
            }

            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                if (IsValidInput(element, operationKind))
                {
                    result.Add(element.tag);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.ProperName(), b.ProperName()));
            return result;
        }

        /// <summary>Amount of an element currently stored inside the heart.</summary>
        public float GetStoredMass(Tag tag)
        {
            if (heart == null || heart.Storage == null || !tag.IsValid)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < heart.Storage.items.Count; i++)
            {
                GameObject item = heart.Storage.items[i];
                PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                if (primary != null && primary.Element.tag == tag && primary.Mass > 0f)
                {
                    total += primary.Mass;
                }
            }

            return total;
        }

        public bool IsOperationActive(HeartProcessOperation operationKind)
        {
            if (operationKind == HeartProcessOperation.Decompose)
            {
                return IsDecomposeActive;
            }

            if (operationKind == HeartProcessOperation.Thermostat)
            {
                for (int i = 0; i < HeartTerminal.All.Count; i++)
                {
                    HeartTerminal terminal = HeartTerminal.All[i];
                    if (terminal != null && terminal.Heart == heart &&
                        terminal.operation == HeartProcessOperation.Thermostat &&
                        terminal.ThermostatEnabled)
                    {
                        return true;
                    }
                }

                return false;
            }

            return GetPendingElements(operationKind).Count > 0;
        }

        /// <summary>Whether the heart currently has any usable input for this operation.</summary>
        public bool HasStoredInput(HeartProcessOperation operationKind)
        {
            if (heart == null || heart.Storage == null)
            {
                return false;
            }

            if (operationKind == HeartProcessOperation.Decompose)
            {
                return GetStoredMass(SimHashes.Water.CreateTag()) > 0.001f;
            }

            for (int i = 0; i < heart.Storage.items.Count; i++)
            {
                GameObject item = heart.Storage.items[i];
                PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                if (primary == null || primary.Mass <= 0.001f || primary.Element == null)
                {
                    continue;
                }

                if (operationKind == HeartProcessOperation.Thermostat)
                {
                    if (primary.Element.IsGas || primary.Element.IsLiquid)
                    {
                        return true;
                    }
                }
                else if (IsValidInput(primary.Element, operationKind))
                {
                    return true;
                }
            }

            return false;
        }

        public string GetOperationName(HeartProcessOperation operationKind)
        {
            switch (operationKind)
            {
                case HeartProcessOperation.Decompose:
                    return STRINGS.UI.ONIHEART.MODULE_DECOMPOSE;
                case HeartProcessOperation.Solidify:
                    return STRINGS.UI.ONIHEART.MODULE_SOLIDIFY;
                case HeartProcessOperation.Liquefy:
                    return STRINGS.UI.ONIHEART.MODULE_LIQUEFY;
                case HeartProcessOperation.Vaporize:
                    return STRINGS.UI.ONIHEART.MODULE_VAPORIZE;
                case HeartProcessOperation.Thermostat:
                    return STRINGS.UI.ONIHEART.MODULE_THERMOSTAT;
                default:
                    return STRINGS.UI.ONIHEART.PROCESS_OFF;
            }
        }

        public string GetRecipeName(HeartProcessOperation operationKind, Tag element)
        {
            Element source = ElementLoader.GetElement(element);
            if (source == null)
            {
                return element.ProperName();
            }

            if (operationKind == HeartProcessOperation.Decompose)
            {
                return string.Format(STRINGS.UI.ONIHEART.RECIPE_DECOMPOSE, source.name);
            }

            if (operationKind == HeartProcessOperation.Thermostat)
            {
                return source.name;
            }

            Element target;
            float temperature;
            if (TryGetPhaseTarget(operationKind, source, out target, out temperature))
            {
                return string.Format(STRINGS.UI.ONIHEART.RECIPE_CONVERSION, source.name, target.name);
            }

            return source.name;
        }

        public void Sim200ms(float dt)
        {
            if (heart == null || heart.Storage == null)
            {
                return;
            }

            NormalizeDictionaries();
            heart.MergeGasLiquidStacksIfNeeded();

            if (decomposeRate > 0.0001f)
            {
                Decompose(dt);
            }

            ProcessPending(HeartProcessOperation.Solidify, dt);
            ProcessPending(HeartProcessOperation.Liquefy, dt);
            ProcessPending(HeartProcessOperation.Vaporize, dt);
            ProcessThermostats();
        }

        private void Decompose(float dt)
        {
            PrimaryElement source = FindSource(SimHashes.Water);
            if (source == null || source.Mass <= 0f)
            {
                return;
            }

            float mass = Mathf.Min(decomposeRate * dt, source.Mass);
            if (mass <= 0.0001f)
            {
                return;
            }

            float power = DECOMPOSE_POWER_PER_KG * mass;
            if (heart.StoredJoules < power)
            {
                mass = heart.StoredJoules / DECOMPOSE_POWER_PER_KG;
                power = DECOMPOSE_POWER_PER_KG * mass;
                if (mass <= 0.0001f)
                {
                    return;
                }
            }

            heart.ConsumePower(power);
            heart.Storage.ConsumeIgnoringDisease(SimHashes.Water.CreateTag(), mass);
            heart.AddElementMerged(SimHashes.Oxygen, mass * WATER_TO_OXYGEN, DECOMPOSE_TEMPERATURE, byte.MaxValue, 0, false, false);
            heart.AddElementMerged(SimHashes.Hydrogen, mass * WATER_TO_HYDROGEN, DECOMPOSE_TEMPERATURE, byte.MaxValue, 0, false, false);

            // Reaction heat is converted straight into electricity instead of being exchanged with the environment.
            heart.StorePower(DECOMPOSE_HEAT_PER_KG * mass);
        }

        private void ProcessPending(HeartProcessOperation operationKind, float dt)
        {
            lock (pendingLock)
            {
                Dictionary<Tag, float> map = GetPendingMap(operationKind);
                if (map == null || map.Count == 0)
                {
                    return;
                }

                List<Tag> elements = new List<Tag>(map.Keys);
                for (int i = 0; i < elements.Count; i++)
                {
                    Tag tag = elements[i];
                    float remaining;
                    if (!map.TryGetValue(tag, out remaining) || remaining <= 0.001f)
                    {
                        map.Remove(tag);
                        continue;
                    }

                    Element sourceElement = ElementLoader.GetElement(tag);
                    if (sourceElement == null || !IsValidInput(sourceElement, operationKind))
                    {
                        map.Remove(tag);
                        continue;
                    }

                    PrimaryElement source = FindSource(sourceElement.id);
                    if (source == null || source.Mass <= 0f)
                    {
                        continue;
                    }

                    float mass = Mathf.Min(CONVERSION_RATE_KG_PER_SECOND * dt, Mathf.Min(remaining, source.Mass));
                    if (mass <= 0.0001f)
                    {
                        continue;
                    }

                    float processed = PhaseChange(operationKind, sourceElement, source, mass);
                    if (processed <= 0.0001f)
                    {
                        // Not enough stored power to absorb the required heat; retry when power is available.
                        continue;
                    }

                    remaining -= processed;
                    if (remaining <= 0.001f)
                    {
                        map.Remove(tag);
                    }
                    else
                    {
                        map[tag] = remaining;
                    }
                }
            }
        }

        /// <summary>
        /// Converts <paramref name="mass"/> kg of the input element into its phase target.
        /// No heat is exchanged with the environment: heat that would be released is turned into
        /// electricity, and heat that would be absorbed is paid for with stored electricity.
        /// Returns the mass actually processed (0 when there is not enough power).
        /// </summary>
        private float PhaseChange(HeartProcessOperation operationKind, Element sourceElement, PrimaryElement source, float mass)
        {
            Element target;
            float outputTemperature;
            if (!TryGetPhaseTarget(operationKind, sourceElement, out target, out outputTemperature))
            {
                return 0f;
            }

            // Joules needed per kg to move the element to its output temperature (negative = released).
            float heatPerKg = sourceElement.specificHeatCapacity *
                              (outputTemperature - source.Temperature) * 1000f;

            if (heatPerKg > 0f)
            {
                float affordableMass = heart.StoredJoules / heatPerKg;
                if (affordableMass < mass)
                {
                    mass = affordableMass;
                }

                if (mass <= 0.0001f)
                {
                    return 0f;
                }
            }

            heart.Storage.ConsumeIgnoringDisease(sourceElement.id.CreateTag(), mass);
            heart.AddElementMerged(target.id, mass, outputTemperature, byte.MaxValue, 0, false, false);

            float heat = heatPerKg * mass;
            if (heat < 0f)
            {
                heart.StorePower(-heat);
            }
            else if (heat > 0f)
            {
                heart.ConsumePower(heat);
            }

            return mass;
        }

        public bool TryGetPhaseTargetForUI(HeartProcessOperation operationKind, Element source, out Element target, out float temperature)
        {
            return TryGetPhaseTarget(operationKind, source, out target, out temperature);
        }

        private bool TryGetPhaseTarget(HeartProcessOperation operationKind, Element source, out Element target, out float temperature)
        {
            target = null;
            temperature = 0f;

            switch (operationKind)
            {
                case HeartProcessOperation.Solidify:
                    if (source.IsLiquid)
                    {
                        target = source.lowTempTransition;
                        if (!HasSpawnableTarget(target, source, Element.State.Solid))
                        {
                            return false;
                        }

                        temperature = target.highTemp > 1f ? target.highTemp - 2f : source.lowTemp - 2f;
                    }
                    else if (source.IsGas)
                    {
                        // Gas first condenses to liquid and then solidifies: gas -> liquid -> solid.
                        Element liquid = source.lowTempTransition;
                        target = liquid != null ? liquid.lowTempTransition : null;
                        if (!HasSpawnableTarget(target, source, Element.State.Solid))
                        {
                            return false;
                        }

                        temperature = target.highTemp > 1f ? target.highTemp - 2f : target.defaultValues.temperature;
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case HeartProcessOperation.Liquefy:
                    if (source.IsGas)
                    {
                        target = source.lowTempTransition;
                        if (!HasSpawnableTarget(target, source, Element.State.Liquid))
                        {
                            return false;
                        }

                        temperature = target.highTemp > 1f ? target.highTemp - 2f : source.lowTemp - 2f;
                    }
                    else if (source.IsSolid)
                    {
                        target = source.highTempTransition;
                        if (!HasSpawnableTarget(target, source, Element.State.Liquid))
                        {
                            return false;
                        }

                        temperature = target.lowTemp + 2f;
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case HeartProcessOperation.Vaporize:
                    if (source.IsLiquid)
                    {
                        target = source.highTempTransition;
                        if (!HasSpawnableTarget(target, source, Element.State.Gas))
                        {
                            return false;
                        }

                        temperature = target.lowTemp + 2f;
                    }
                    else if (source.IsSolid)
                    {
                        // Solids sublimate directly; if there is no direct transition, go through liquid before vaporizing.
                        Element liquid = source.highTempTransition;
                        target = liquid != null && liquid.IsGas
                            ? liquid
                            : (liquid != null ? liquid.highTempTransition : null);
                        if (!HasSpawnableTarget(target, source, Element.State.Gas))
                        {
                            return false;
                        }

                        temperature = target.lowTemp + 2f;
                    }
                    else
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }

            temperature = Mathf.Clamp(temperature, 1f, 9999f);
            return true;
        }

        private bool IsValidInput(Element element, HeartProcessOperation operationKind)
        {
            if (element == null || element.disabled || !IsConventionalElement(element))
            {
                return false;
            }

            Element target;
            float temperature;
            return TryGetPhaseTarget(operationKind, element, out target, out temperature);
        }

        private static bool IsConventionalElement(Element element)
        {
            if (element == null)
            {
                return false;
            }

            float temperature = element.defaultValues.temperature;
            return temperature >= MIN_CONVENTIONAL_TEMPERATURE && temperature <= MAX_CONVENTIONAL_TEMPERATURE;
        }

        /// <summary>
        /// The target element must actually exist and be spawnable. Some elements (e.g. unused solid/liquid types)
        /// have definitions but no spawnable item; such recipes should not be shown or executed.
        /// </summary>
        private static bool HasSpawnableTarget(Element target, Element source, Element.State requiredState)
        {
            if (target == null || target == source || target.disabled || !IsConventionalElement(target))
            {
                return false;
            }

            if ((target.state & Element.State.Solid) != requiredState)
            {
                return false;
            }

            if (target.substance == null || target.substance.anim == null)
            {
                return false;
            }

            return Assets.TryGetPrefab(target.tag) != null;
        }

        private PrimaryElement FindSource(SimHashes id)
        {
            if (heart == null || heart.Storage == null)
            {
                return null;
            }

            GameObject item = heart.Storage.FindFirst(id.CreateTag());
            if (item == null)
            {
                return null;
            }

            PrimaryElement primary = item.GetComponent<PrimaryElement>();
            return primary != null && primary.Mass > 0f ? primary : null;
        }

        /// <summary>
        /// The element's stable temperature range. Keeping a 2 K margin from either phase-change
        /// point prevents the thermostat from freezing/boiling the stored element.
        /// </summary>
        public static bool TryGetThermostatRange(Element element, out float minKelvin, out float maxKelvin)
        {
            minKelvin = 0f;
            maxKelvin = 0f;
            if (element == null || element.disabled || (!element.IsGas && !element.IsLiquid) ||
                !IsConventionalElement(element))
            {
                return false;
            }

            float min = Mathf.Max(1f, element.lowTemp + THERMOSTAT_MARGIN_KELVIN);
            float max = element.highTemp > 1f && element.highTemp <= MAX_CONVENTIONAL_TEMPERATURE
                ? element.highTemp - THERMOSTAT_MARGIN_KELVIN
                : MAX_CONVENTIONAL_TEMPERATURE;
            max = Mathf.Min(max, MAX_CONVENTIONAL_TEMPERATURE);

            if (max <= min)
            {
                return false;
            }

            minKelvin = min;
            maxKelvin = max;
            return true;
        }

        /// <summary>
        /// Elements that can be assigned to a thermostat terminal. Elements already assigned to
        /// another thermostat on the same heart are excluded, so two terminals cannot fight over
        /// the same element's target temperature.
        /// </summary>
        public List<Tag> GetThermostatCandidates(HeartTerminal terminal)
        {
            List<Tag> result = new List<Tag>();
            HashSet<SimHashes> blocked = new HashSet<SimHashes>();

            if (heart != null)
            {
                for (int i = 0; i < HeartTerminal.All.Count; i++)
                {
                    HeartTerminal other = HeartTerminal.All[i];
                    if (other != null && other != terminal && other.Heart == heart &&
                        other.operation == HeartProcessOperation.Thermostat &&
                        other.ThermostatElement != SimHashes.Vacuum)
                    {
                        blocked.Add(other.ThermostatElement);
                    }
                }
            }

            SimHashes selected = terminal != null ? terminal.ThermostatElement : SimHashes.Vacuum;
            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                float min;
                float max;
                if (!TryGetThermostatRange(element, out min, out max))
                {
                    continue;
                }

                if (blocked.Contains(element.id) && element.id != selected)
                {
                    continue;
                }

                if (!result.Contains(element.tag))
                {
                    result.Add(element.tag);
                }
            }

            if (selected != SimHashes.Vacuum)
            {
                Tag tag = selected.CreateTag();
                if (!result.Contains(tag))
                {
                    Element element = ElementLoader.GetElement(tag);
                    float min;
                    float max;
                    if (TryGetThermostatRange(element, out min, out max))
                    {
                        result.Add(tag);
                    }
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.ProperName(), b.ProperName()));
            return result;
        }

        public float GetStoredTemperature(Tag tag)
        {
            return heart != null ? heart.GetStoredTemperature(tag) : 0f;
        }

        private void ProcessThermostats()
        {
            if (heart == null || heart.Storage == null)
            {
                return;
            }

            HashSet<SimHashes> handled = new HashSet<SimHashes>();
            for (int i = 0; i < HeartTerminal.All.Count; i++)
            {
                HeartTerminal terminal = HeartTerminal.All[i];
                if (terminal == null || terminal.Heart != heart ||
                    terminal.operation != HeartProcessOperation.Thermostat ||
                    !terminal.ThermostatEnabled)
                {
                    continue;
                }

                SimHashes id = terminal.ThermostatElement;
                if (id == SimHashes.Vacuum)
                {
                    continue;
                }

                Element element = ElementLoader.GetElement(id.CreateTag());
                float min;
                float max;
                if (!TryGetThermostatRange(element, out min, out max) || !handled.Add(id))
                {
                    continue;
                }

                ApplyThermostat(element, Mathf.Clamp(terminal.ThermostatTargetKelvin, min, max));
            }
        }

        private void ApplyThermostat(Element element, float targetKelvin)
        {
            for (int i = 0; i < heart.Storage.items.Count; i++)
            {
                GameObject item = heart.Storage.items[i];
                PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                if (primary == null || primary.Element == null || primary.Element.id != element.id ||
                    primary.Mass <= 0f)
                {
                    continue;
                }

                float heatCapacity = primary.Mass * element.specificHeatCapacity * 1000f;
                if (heatCapacity <= 0f)
                {
                    continue;
                }

                float current = primary.Temperature;
                float heat = heatCapacity * (targetKelvin - current);
                if (Mathf.Abs(heat) < 0.001f)
                {
                    continue;
                }

                if (heat > 0f)
                {
                    // Heating needs electricity; with limited power only part of the gap is closed.
                    float used = Mathf.Min(heat, heart.StoredJoules);
                    if (used <= 0f)
                    {
                        continue;
                    }

                    heart.ConsumePower(used);
                    primary.SetMassTemperature(primary.Mass, current + used / heatCapacity);
                }
                else
                {
                    // Excess heat is converted into stored electricity without loss.
                    primary.SetMassTemperature(primary.Mass, targetKelvin);
                    heart.StorePower(-heat);
                }
            }
        }

        public List<Descriptor> GetDescriptors(GameObject go)
        {
            List<Descriptor> descriptors = new List<Descriptor>();

            if (decomposeRate > 0.0001f)
            {
                descriptors.Add(new Descriptor(
                    string.Format(STRINGS.UI.ONIHEART.DECOMPOSE_STATUS, decomposeRate.ToString("0.##")),
                    STRINGS.UI.ONIHEART.PROCESS_STATUS_TOOLTIP));
            }

            AddPendingDescriptor(descriptors, HeartProcessOperation.Solidify);
            AddPendingDescriptor(descriptors, HeartProcessOperation.Liquefy);
            AddPendingDescriptor(descriptors, HeartProcessOperation.Vaporize);

            int thermostatCount = 0;
            for (int i = 0; i < HeartTerminal.All.Count; i++)
            {
                HeartTerminal terminal = HeartTerminal.All[i];
                if (terminal != null && terminal.Heart == heart &&
                    terminal.operation == HeartProcessOperation.Thermostat &&
                    terminal.ThermostatEnabled &&
                    terminal.ThermostatElement != SimHashes.Vacuum)
                {
                    thermostatCount++;
                }
            }

            if (thermostatCount > 0)
            {
                descriptors.Add(new Descriptor(
                    string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_STATUS, thermostatCount),
                    STRINGS.UI.ONIHEART.THERMOSTAT_STATUS_TOOLTIP));
            }

            if (descriptors.Count == 0)
            {
                descriptors.Add(new Descriptor(
                    STRINGS.UI.ONIHEART.PROCESS_STATUS_OFF,
                    STRINGS.UI.ONIHEART.PROCESS_STATUS_TOOLTIP));
            }

            return descriptors;
        }

        private void AddPendingDescriptor(List<Descriptor> descriptors, HeartProcessOperation operationKind)
        {
            List<Tag> pending = GetPendingElements(operationKind);
            if (pending.Count == 0)
            {
                return;
            }

            descriptors.Add(new Descriptor(
                string.Format(STRINGS.UI.ONIHEART.CONVERSION_STATUS, GetOperationName(operationKind), pending.Count),
                STRINGS.UI.ONIHEART.PROCESS_STATUS_TOOLTIP));
        }
    }
}
