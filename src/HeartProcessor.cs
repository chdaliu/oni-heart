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

        private const float PHASE_MARGIN_KELVIN = 2f;

        private const float CONVERSION_RATE_KG_PER_SECOND = 0.5f;
        private const float DECOMPOSE_POWER_PER_KG = 120f;
        private const float DECOMPOSE_HEAT_PER_KG = 1250f;
        private const float DECOMPOSE_TEMPERATURE = 343.15f;
        private const float WATER_TO_OXYGEN = 0.888f;
        private const float WATER_TO_HYDROGEN = 0.112f;

        // Only pure, single-substance phase changes are exposed as conversion recipes.
        // Keys are Element.id names (SimHashes names), values are substance-family keys.
        // Metals, solutions/mixtures and all chemical/industrial transitions are intentionally absent:
        // combustion/oxidation, refining, cracking, smelting, ore processing, etc.
        private static readonly Dictionary<string, string> AllowedPhaseFamilies =
            new Dictionary<string, string>
            {
                // Water (pure water and its solid/gas phases)
                { "Water", "water" },
                { "Steam", "water" },
                { "Ice", "water" },
                { "Snow", "water" },
                { "CrushedIce", "water" },
                { "StableSnow", "water" },

                // Carbon dioxide
                { "CarbonDioxide", "carbon_dioxide" },
                { "LiquidCarbonDioxide", "carbon_dioxide" },
                { "SolidCarbonDioxide", "carbon_dioxide" },

                // Oxygen
                { "Oxygen", "oxygen" },
                { "LiquidOxygen", "oxygen" },
                { "SolidOxygen", "oxygen" },

                // Hydrogen
                { "Hydrogen", "hydrogen" },
                { "LiquidHydrogen", "hydrogen" },
                { "SolidHydrogen", "hydrogen" },

                // Methane
                { "Methane", "methane" },
                { "LiquidMethane", "methane" },
                { "SolidMethane", "methane" },

                // Chlorine (Chlorine is the liquid phase, ChlorineGas the gas phase)
                { "ChlorineGas", "chlorine" },
                { "Chlorine", "chlorine" },
                { "SolidChlorine", "chlorine" },

                // Phosphorus
                { "PhosphorusGas", "phosphorus" },
                { "LiquidPhosphorus", "phosphorus" },
                { "Phosphorus", "phosphorus" },

                // Sulfur
                { "SulfurGas", "sulfur" },
                { "LiquidSulfur", "sulfur" },
                { "Sulfur", "sulfur" },

                // Ethanol
                { "EthanolGas", "ethanol" },
                { "Ethanol", "ethanol" },
                { "SolidEthanol", "ethanol" },

                // Pure oils, inks, resins and sugar
                { "PhytoOil", "phytooil" },
                { "FrozenPhytoOil", "phytooil" },
                { "Ink", "ink" },
                { "FrozenInk", "ink" },
                { "NaturalResin", "natural_resin" },
                { "NaturalSolidResin", "natural_resin" },
                { "Sucrose", "sucrose" },
                { "MoltenSucrose", "sucrose" }
            };

        private struct PhasePath
        {
            public Element Target;
            public float OutputTemperature;
            public Element PhaseA;
            public Element PhaseB;
            public Element PhaseC;
            public float TransitionA;
            public float TransitionB;
            public int PhaseCount;
            public int TransitionCount;

            public void SetSingle(Element source, Element target, float transition, float output)
            {
                Target = target;
                OutputTemperature = output;
                PhaseA = source;
                PhaseB = target;
                PhaseC = null;
                TransitionA = transition;
                TransitionB = 0f;
                PhaseCount = 2;
                TransitionCount = 1;
            }

            public void SetDouble(Element source, Element middle, Element target, float firstTransition, float secondTransition, float output)
            {
                Target = target;
                OutputTemperature = output;
                PhaseA = source;
                PhaseB = middle;
                PhaseC = target;
                TransitionA = firstTransition;
                TransitionB = secondTransition;
                PhaseCount = 3;
                TransitionCount = 2;
            }
        }

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
            bool markDiscovered = false;
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
                    markDiscovered = true;
                }
            }

            // Discover outside the pending lock: DiscoveredResources raises events that may
            // touch other game systems.
            if (markDiscovered)
            {
                DiscoverElement(ElementLoader.GetElement(element));
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
                    if (pair.Value <= 0.001f)
                    {
                        continue;
                    }

                    // Hide entries that are no longer valid under the current recipe filter
                    // (for example pending recipes saved before a filter change).
                    Element element = ElementLoader.GetElement(pair.Key);
                    if (element == null || !IsValidInput(element, operationKind))
                    {
                        continue;
                    }

                    result.Add(pair.Key);
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
        /// For the panel: list valid recipes whose input material has appeared in this save
        /// (discovered by the game) or has entered this heart / has a pending amount.
        /// Materials that have never appeared are hidden until they are encountered.
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

            HashSet<Tag> appeared = CollectAppearedInputs();
            bool canCheckDiscovery = DiscoveredResources.Instance != null;

            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                if (!IsValidInput(element, operationKind))
                {
                    continue;
                }

                if (canCheckDiscovery &&
                    !DiscoveredResources.Instance.IsDiscovered(element.tag) &&
                    !appeared.Contains(element.tag))
                {
                    continue;
                }

                result.Add(element.tag);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.ProperName(), b.ProperName()));
            return result;
        }

        /// <summary>
        /// Current heart inventory and pending conversion targets always count as "appeared".
        /// This also seeds discovery for old saves that configured recipes before discovery tracking existed.
        /// </summary>
        private HashSet<Tag> CollectAppearedInputs()
        {
            HashSet<Tag> appeared = new HashSet<Tag>();

            if (heart != null && heart.Storage != null)
            {
                for (int i = 0; i < heart.Storage.items.Count; i++)
                {
                    GameObject item = heart.Storage.items[i];
                    PrimaryElement primary = item != null ? item.GetComponent<PrimaryElement>() : null;
                    if (primary == null || primary.Element == null)
                    {
                        continue;
                    }

                    appeared.Add(primary.Element.tag);
                    DiscoverElement(primary.Element);
                }
            }

            AddPendingToAppeared(HeartProcessOperation.Solidify, appeared);
            AddPendingToAppeared(HeartProcessOperation.Liquefy, appeared);
            AddPendingToAppeared(HeartProcessOperation.Vaporize, appeared);
            return appeared;
        }

        private void AddPendingToAppeared(HeartProcessOperation operationKind, HashSet<Tag> appeared)
        {
            List<Tag> pending = GetPendingElements(operationKind);
            for (int i = 0; i < pending.Count; i++)
            {
                Element element = ElementLoader.GetElement(pending[i]);
                if (element == null)
                {
                    continue;
                }

                appeared.Add(element.tag);
                DiscoverElement(element);
            }
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
                        terminal.HasEnabledThermostats)
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
            PhasePath path;
            if (!TryGetPhasePath(operationKind, sourceElement, out path))
            {
                return 0f;
            }

            // Sensible heat needed per kg for the whole path (negative = released).
            // Each phase uses its own specific heat capacity; latent heat is not modelled by ONI.
            float heatPerKg = GetPhaseHeatPerKg(path, source.Temperature);

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
            heart.AddElementMerged(path.Target.id, mass, path.OutputTemperature, byte.MaxValue, 0, false, false);

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
            PhasePath path;
            if (!TryGetPhasePath(operationKind, source, out path))
            {
                target = null;
                temperature = 0f;
                return false;
            }

            target = path.Target;
            temperature = path.OutputTemperature;
            return true;
        }

        /// <summary>
        /// Builds the phase path for one conversion. Every edge must stay inside the same pure-substance
        /// family; chemical transformations (combustion, refining, smelting, cracking, mixtures, etc.) are rejected.
        /// </summary>
        private bool TryGetPhasePath(HeartProcessOperation operationKind, Element source, out PhasePath path)
        {
            path = new PhasePath();
            if (source == null || source.disabled)
            {
                return false;
            }

            switch (operationKind)
            {
                case HeartProcessOperation.Solidify:
                    if (source.IsLiquid)
                    {
                        Element solid = source.lowTempTransition;
                        if (!HasSpawnableTarget(solid, source, Element.State.Solid) ||
                            !IsSameSubstanceFamily(source, solid) ||
                            HasOreByproduct(source, false) ||
                            source.lowTemp <= 0f)
                        {
                            return false;
                        }

                        path.SetSingle(source, solid, source.lowTemp, source.lowTemp - PHASE_MARGIN_KELVIN);
                    }
                    else if (source.IsGas)
                    {
                        // Gas first condenses to liquid and then solidifies: gas -> liquid -> solid.
                        Element liquid = source.lowTempTransition;
                        if (liquid == null || liquid.disabled || !liquid.IsLiquid)
                        {
                            return false;
                        }

                        Element solid = liquid.lowTempTransition;
                        if (!HasSpawnableTarget(solid, source, Element.State.Solid) ||
                            !IsSameSubstanceFamily(source, liquid) ||
                            !IsSameSubstanceFamily(liquid, solid) ||
                            HasOreByproduct(source, false) ||
                            HasOreByproduct(liquid, false) ||
                            source.lowTemp <= 0f ||
                            liquid.lowTemp <= 0f)
                        {
                            return false;
                        }

                        path.SetDouble(source, liquid, solid, source.lowTemp, liquid.lowTemp,
                            liquid.lowTemp - PHASE_MARGIN_KELVIN);
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case HeartProcessOperation.Liquefy:
                    if (source.IsGas)
                    {
                        Element liquid = source.lowTempTransition;
                        if (!HasSpawnableTarget(liquid, source, Element.State.Liquid) ||
                            !IsSameSubstanceFamily(source, liquid) ||
                            HasOreByproduct(source, false) ||
                            source.lowTemp <= 0f)
                        {
                            return false;
                        }

                        path.SetSingle(source, liquid, source.lowTemp, source.lowTemp - PHASE_MARGIN_KELVIN);
                    }
                    else if (source.IsSolid)
                    {
                        Element liquid = source.highTempTransition;
                        if (!HasSpawnableTarget(liquid, source, Element.State.Liquid) ||
                            !IsSameSubstanceFamily(source, liquid) ||
                            HasOreByproduct(source, true) ||
                            source.highTemp <= 0f)
                        {
                            return false;
                        }

                        path.SetSingle(source, liquid, source.highTemp, source.highTemp + PHASE_MARGIN_KELVIN);
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case HeartProcessOperation.Vaporize:
                    if (source.IsLiquid)
                    {
                        Element gas = source.highTempTransition;
                        if (!HasSpawnableTarget(gas, source, Element.State.Gas) ||
                            !IsSameSubstanceFamily(source, gas) ||
                            HasOreByproduct(source, true) ||
                            source.highTemp <= 0f)
                        {
                            return false;
                        }

                        path.SetSingle(source, gas, source.highTemp, source.highTemp + PHASE_MARGIN_KELVIN);
                    }
                    else if (source.IsSolid)
                    {
                        // Solids either sublimate directly or go through a liquid before vaporizing.
                        Element first = source.highTempTransition;
                        if (first == null)
                        {
                            return false;
                        }

                        if (first.IsGas)
                        {
                            if (!HasSpawnableTarget(first, source, Element.State.Gas) ||
                                !IsSameSubstanceFamily(source, first) ||
                                HasOreByproduct(source, true) ||
                                source.highTemp <= 0f)
                            {
                                return false;
                            }

                            path.SetSingle(source, first, source.highTemp,
                                source.highTemp + PHASE_MARGIN_KELVIN);
                        }
                        else if (!first.disabled && first.IsLiquid)
                        {
                            Element gas = first.highTempTransition;
                            if (!HasSpawnableTarget(gas, source, Element.State.Gas) ||
                                !IsSameSubstanceFamily(source, first) ||
                                !IsSameSubstanceFamily(first, gas) ||
                                HasOreByproduct(source, true) ||
                                HasOreByproduct(first, true) ||
                                source.highTemp <= 0f ||
                                first.highTemp <= 0f)
                            {
                                return false;
                            }

                            path.SetDouble(source, first, gas, source.highTemp, first.highTemp,
                                first.highTemp + PHASE_MARGIN_KELVIN);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }

            path.OutputTemperature = Mathf.Clamp(path.OutputTemperature, 1f, 9999f);
            if (path.OutputTemperature < MIN_CONVENTIONAL_TEMPERATURE ||
                path.OutputTemperature > MAX_CONVENTIONAL_TEMPERATURE)
            {
                return false;
            }

            return IsTemperatureStable(path);
        }

        private static void DiscoverElement(Element element)
        {
            if (element == null || element.disabled || DiscoveredResources.Instance == null)
            {
                return;
            }

            DiscoveredResources.Instance.Discover(element.tag, element.GetMaterialCategoryTag());
        }

        /// <summary>
        /// Some high/low temperature transitions also produce a separate ore byproduct
        /// (for example smelting ore or boiling a solution). Those are not pure phase changes.
        /// </summary>
        private static bool HasOreByproduct(Element element, bool heating)
        {
            if (element == null)
            {
                return false;
            }

            return heating
                ? element.highTempTransitionOreID != SimHashes.Vacuum &&
                  element.highTempTransitionOreMassConversion > 0f
                : element.lowTempTransitionOreID != SimHashes.Vacuum &&
                  element.lowTempTransitionOreMassConversion > 0f;
        }

        private static bool IsSameSubstanceFamily(Element first, Element second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            string firstFamily;
            string secondFamily;
            if (!AllowedPhaseFamilies.TryGetValue(first.id.ToString(), out firstFamily) ||
                !AllowedPhaseFamilies.TryGetValue(second.id.ToString(), out secondFamily))
            {
                return false;
            }

            return firstFamily == secondFamily;
        }

        private static bool IsTemperatureStable(PhasePath path)
        {
            Element target = path.Target;
            if (target == null)
            {
                return false;
            }

            float temperature = path.OutputTemperature;
            if (target.IsGas)
            {
                return target.lowTemp <= 0f || temperature > target.lowTemp;
            }

            if (target.IsLiquid)
            {
                bool aboveFreezing = target.lowTemp <= 0f || temperature > target.lowTemp;
                bool belowBoiling = target.highTemp <= 0f || temperature < target.highTemp;
                return aboveFreezing && belowBoiling;
            }

            if (target.IsSolid)
            {
                return target.highTemp <= 0f || temperature < target.highTemp;
            }

            return false;
        }

        private static float GetPhaseHeatPerKg(PhasePath path, float inputTemperature)
        {
            float heat = 0f;
            float current = inputTemperature;

            if (path.TransitionCount >= 1)
            {
                heat += PhaseHeat(path.PhaseA, current, path.TransitionA);
                current = path.TransitionA;
            }

            if (path.TransitionCount >= 2)
            {
                heat += PhaseHeat(path.PhaseB, current, path.TransitionB);
                current = path.TransitionB;
            }

            Element finalPhase = path.PhaseCount > 2 ? path.PhaseC : path.PhaseB;
            heat += PhaseHeat(finalPhase, current, path.OutputTemperature);
            return heat;
        }

        private static float PhaseHeat(Element phase, float fromTemperature, float toTemperature)
        {
            return phase == null
                ? 0f
                : phase.specificHeatCapacity * (toTemperature - fromTemperature) * 1000f;
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
        /// Elements that can be assigned to a thermostat terminal. A thermostat terminal can
        /// control several elements at the same time, and each element has its own target and switch.
        /// </summary>
        public List<Tag> GetThermostatCandidates(HeartTerminal terminal)
        {
            List<Tag> result = new List<Tag>();
            for (int i = 0; i < ElementLoader.elements.Count; i++)
            {
                Element element = ElementLoader.elements[i];
                float min;
                float max;
                if (!TryGetThermostatRange(element, out min, out max))
                {
                    continue;
                }

                if (!result.Contains(element.tag))
                {
                    result.Add(element.tag);
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
                    terminal.operation != HeartProcessOperation.Thermostat)
                {
                    continue;
                }

                List<Tag> elements = terminal.GetEnabledThermostatElements();
                for (int j = 0; j < elements.Count; j++)
                {
                    Element element = ElementLoader.GetElement(elements[j]);
                    float min;
                    float max;
                    if (!TryGetThermostatRange(element, out min, out max) || !handled.Add(element.id))
                    {
                        continue;
                    }

                    float target = terminal.GetThermostatTargetKelvin(elements[j]);
                    ApplyThermostat(element, Mathf.Clamp(target, min, max));
                }
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
                    terminal.operation == HeartProcessOperation.Thermostat)
                {
                    thermostatCount += terminal.GetEnabledThermostatElements().Count;
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
