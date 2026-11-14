using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ONIHeart
{
    /// <summary>Terminal recipe panel: food-press-style recipe rows (icon + name + current quantity/rate); amounts are adjusted in the recipe detail panel.</summary>
    public sealed class HeartRecipePanel : KMonoBehaviour
    {
        private enum RowKind
        {
            Recipe,
            ThermostatElement
        }

        private sealed class RowBinding
        {
            public GameObject row;
            public Tag element;
            public bool isRate;
            public LocText countLabel;
            public LocText statusLabel;
            public RowKind kind;
        }

        public RectTransform content;
        public GameObject rowPrefab;
        public KScreen detailPrefab;

        private HeartTerminal terminal;
        private HeartProcessor processor;
        private HeartProcessOperation operation;
        private string title = "";
        private string lastHeartName = "";
        private string signature = "";
        private float nextRefreshTime;

        private readonly List<RowBinding> rows = new List<RowBinding>();

        public void Bind(HeartTerminal terminal, HeartProcessor processor,
                         HeartProcessOperation operation, string title)
        {
            string heartName = terminal != null ? terminal.HeartName : "";
            bool changed = this.terminal != terminal || this.processor != processor ||
                           this.operation != operation || this.title != title ||
                           this.lastHeartName != heartName;
            this.terminal = terminal;
            this.processor = processor;
            this.operation = operation;
            this.title = title;
            this.lastHeartName = heartName;

            if (changed || ComputeSignature() != signature)
            {
                Rebuild();
            }
        }

        private void Update()
        {
            // The terminal's target-heart button can change at any time, so the recipe panel must rebind.
            // The title must also update when the heart is renamed.
            if (terminal != null &&
                (terminal.Processor != processor || terminal.HeartName != lastHeartName))
            {
                Bind(terminal, terminal.Processor, operation, title);
                return;
            }

            if (processor == null || !gameObject.activeInHierarchy || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 1f;

            if (ComputeSignature() != signature)
            {
                Rebuild();
                return;
            }

            RefreshCounts();
        }

        private List<Tag> GetInputs()
        {
            if (processor == null)
            {
                return new List<Tag>();
            }

            if (operation == HeartProcessOperation.Thermostat)
            {
                return processor.GetThermostatCandidates(terminal);
            }

            if (operation == HeartProcessOperation.Decompose)
            {
                return new List<Tag> { SimHashes.Water.CreateTag() };
            }

            return processor.GetAllRecipeInputs(operation);
        }

        private string ComputeSignature()
        {
            if (processor == null || content == null)
            {
                return "";
            }

            List<Tag> inputs = GetInputs();
            StringBuilder builder = new StringBuilder();
            builder.Append((int)operation);
            for (int i = 0; i < inputs.Count; i++)
            {
                builder.Append('|');
                builder.Append((int)inputs[i].GetHash());
            }

            return builder.ToString();
        }

        private void Rebuild()
        {
            if (content == null || rowPrefab == null)
            {
                Debug.LogWarning("[ONIHeart] Recipe panel is missing content or row prefab.");
                return;
            }

            Clear();

            // The vanilla recipe-list prefab still contains category headers and placeholder recipes; hide them all and keep only our rows.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (rowPrefab != null && child.gameObject == rowPrefab)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }

            // The original recipe button is only a template; hide it so it does
            // not appear as an extra blank row.
            if (rowPrefab != null)
            {
                rowPrefab.SetActive(false);
            }

            // Recipe and heart info are shown as a status item in the building properties panel, so the recipe list no longer needs a header row.
            if (processor == null)
            {
                rows.Add(new RowBinding { row = CreateRow(STRINGS.UI.ONIHEART.TERMINAL_NO_HEART, null, true) });
                signature = "";
                return;
            }

            if (operation == HeartProcessOperation.Thermostat)
            {
                RebuildThermostat();
                signature = ComputeSignature();
                return;
            }

            List<Tag> inputs = GetInputs();
            if (operation == HeartProcessOperation.Decompose)
            {
                Tag water = SimHashes.Water.CreateTag();
                CreateRowForElement(water, true);
            }
            else if (inputs.Count == 0)
            {
                CreateRow(STRINGS.UI.ONIHEART.NO_RECIPES, null, true);
            }
            else
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    CreateRowForElement(inputs[i], false);
                }
            }

            signature = ComputeSignature();
        }

        private void Clear()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].row != null)
                {
                    Object.Destroy(rows[i].row);
                }
            }

            rows.Clear();
        }

        private void RebuildThermostat()
        {
            List<Tag> inputs = GetInputs();
            if (inputs.Count == 0)
            {
                rows.Add(new RowBinding
                {
                    row = CreateRow(STRINGS.UI.ONIHEART.THERMOSTAT_NO_ELEMENTS, null, true),
                    kind = RowKind.Recipe
                });
                return;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                CreateThermostatElementRow(inputs[i]);
            }
        }

        private void CreateThermostatElementRow(Tag elementTag)
        {
            Element element = ElementLoader.GetElement(elementTag);
            string elementName = element != null ? element.name : elementTag.ProperName();

            GameObject row = CreateRow(
                elementName,
                string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_ELEMENT_ROW_TOOLTIP, elementName),
                false);
            if (row == null)
            {
                return;
            }

            SetRowIcon(row, elementTag);
            LocText countLabel = GetCountLabel(row);
            RowBinding binding = new RowBinding
            {
                row = row,
                element = elementTag,
                countLabel = countLabel,
                statusLabel = CreateStatusLabel(countLabel),
                kind = RowKind.ThermostatElement
            };
            rows.Add(binding);

            HideChild(row, "Queue/IncButton");
            HideChild(row, "Queue/DecButton");
            HideChild(row, "Queue/TechRequired");
            HideChild(row, "Queue/Infinite");
            RegisterRowClick(row, binding);
            UpdateCount(binding);
        }

        private void RegisterRowClick(GameObject row, RowBinding binding)
        {
            KToggle toggle = row.GetComponent<KToggle>();
            if (toggle != null)
            {
                toggle.onClick += delegate { OnRowClicked(binding); };
            }

            HierarchyReferences references = row.GetComponent<HierarchyReferences>();
            if (references != null && references.HasReference("QueueBoxButton"))
            {
                KButton queueBox = references.GetReference<KButton>("QueueBoxButton");
                if (queueBox != null)
                {
                    queueBox.onClick += delegate { OnRowClicked(binding); };
                }
            }
        }

        private void OnRowClicked(RowBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            OpenDetail(binding);
        }

        private LocText GetCountLabel(GameObject row)
        {
            LocText countLabel = null;
            HierarchyReferences references = row.GetComponent<HierarchyReferences>();
            if (references != null)
            {
                countLabel = references.TryGetReference<LocText>("CountLabel");
                HideReference(references, "InfiniteIcon");
                HideReference(references, "TechRequired");
                HideReference(references, "DotContainer");
            }

            if (countLabel == null && row.transform.Find("Queue/Box/label/x") != null)
            {
                countLabel = row.transform.Find("Queue/Box/label/x").GetComponent<LocText>();
            }

            return countLabel;
        }

        private static void SetRowIcon(GameObject row, Tag element)
        {
            Image icon = row.transform.Find("Button/ToIcons/ToIcon") != null
                ? row.transform.Find("Button/ToIcons/ToIcon").GetComponent<Image>()
                : null;
            if (icon == null)
            {
                return;
            }

            var sprite = Def.GetUISprite(element);
            if (sprite != null)
            {
                icon.sprite = sprite.first;
                icon.color = sprite.second;
            }
        }

        private void CreateRowForElement(Tag element, bool isRate)
        {
            Element inputElement = ElementLoader.GetElement(element);
            string inputName = inputElement != null ? inputElement.name : element.ProperName();
            string label = processor != null ? processor.GetRecipeName(operation, element) : inputName;
            string stock = processor != null
                ? GameUtil.GetFormattedMass(processor.GetStoredMass(element))
                : GameUtil.GetFormattedMass(0f);
            string tooltip = label + "\n" +
                string.Format(STRINGS.UI.ONIHEART.STOCK_AMOUNT, stock) + "\n" +
                STRINGS.UI.ONIHEART.RECIPE_ROW_TOOLTIP;

            GameObject row = CreateRow(label, tooltip, false);
            SetRowIcon(row, element);

            LocText countLabel = GetCountLabel(row);
            RowBinding binding = new RowBinding
            {
                row = row,
                element = element,
                isRate = isRate,
                countLabel = countLabel,
                kind = RowKind.Recipe
            };
            rows.Add(binding);

            // Recipe rows only display the current quantity/rate; increment/decrement is handled in the recipe detail panel.
            // Keep this consistent with the food press recipe detail page.
            HideChild(row, "Queue/IncButton");
            HideChild(row, "Queue/DecButton");
            HideChild(row, "Queue/TechRequired");
            HideChild(row, "Queue/Infinite");
            RegisterRowClick(row, binding);
            UpdateCount(binding);
        }

        private GameObject CreateRow(string text, string tooltip, bool isHeader)
        {
            GameObject row = Util.KInstantiateUI(rowPrefab, content.gameObject, true);
            if (row == null)
            {
                return null;
            }

            row.SetActive(true);

            if (isHeader)
            {
                HideChild(row, "Queue");
                HideChild(row, "Button/ToIcons/ToIcon");
            }

            Transform labelTransform = row.transform.Find("Button/ToIcons/Label");
            if (labelTransform != null)
            {
                LocText label = labelTransform.GetComponent<LocText>();
                if (label != null)
                {
                    label.text = text;
                }
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                Transform button = row.transform.Find("Button");
                ToolTip tip = button != null ? button.GetComponent<ToolTip>() : null;
                if (tip != null)
                {
                    tip.SetSimpleTooltip(tooltip);
                }
            }

            return row;
        }

        private static void HideChild(GameObject row, string path)
        {
            Transform child = row.transform.Find(path);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void HideReference(HierarchyReferences references, string name)
        {
            if (references.HasReference(name))
            {
                Component reference = references.GetReference(name);
                if (reference != null)
                {
                    reference.gameObject.SetActive(false);
                }
            }
        }

        private void OpenDetail(RowBinding binding)
        {
            if (detailPrefab == null || processor == null || binding == null || DetailsScreen.Instance == null)
            {
                return;
            }

            string detailTitle = binding.kind == RowKind.ThermostatElement
                ? STRINGS.UI.ONIHEART.PANEL_THERMOSTAT
                : STRINGS.UI.ONIHEART.RECIPE_DETAIL_TITLE;
            KScreen screen = DetailsScreen.Instance.SetSecondarySideScreen(detailPrefab, detailTitle);
            HeartRecipeDetailScreen detail = screen != null ? screen.GetComponent<HeartRecipeDetailScreen>() : null;
            if (detail != null)
            {
                if (binding.kind == RowKind.ThermostatElement)
                {
                    detail.SetThermostat(terminal, binding.element);
                }
                else
                {
                    detail.SetRecipe(processor, operation, binding.element, binding.isRate);
                }
            }
            else
            {
                Debug.LogWarning("[ONIHeart] Secondary recipe detail screen missing HeartRecipeDetailScreen.");
            }
        }

        private void RefreshCounts()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                UpdateCount(rows[i]);
            }
        }

        private void UpdateCount(RowBinding binding)
        {
            if (binding == null || binding.countLabel == null)
            {
                return;
            }

            if (binding.kind == RowKind.ThermostatElement)
            {
                bool enabled = terminal != null && terminal.IsThermostatEnabled(binding.element);
                if (binding.statusLabel != null)
                {
                    binding.statusLabel.text = STRINGS.UI.ONIHEART.THERMOSTAT_NOT_ENABLED;
                    binding.statusLabel.gameObject.SetActive(!enabled);
                }

                binding.countLabel.text = enabled
                    ? FormatCelsius(terminal.GetThermostatTargetCelsius(binding.element))
                    : "";
                return;
            }

            if (processor == null)
            {
                return;
            }

            float value = binding.isRate
                ? processor.DecomposeRate
                : processor.GetPending(operation, binding.element);
            binding.countLabel.text = value.ToString("0.##");
        }

        private static LocText CreateStatusLabel(LocText source)
        {
            if (source == null)
            {
                return null;
            }

            GameObject labelObject = Object.Instantiate(source.gameObject, source.transform.parent, false);
            labelObject.name = source.name + "Status";
            SetTextStyleSetting style = labelObject.GetComponent<SetTextStyleSetting>();
            if (style != null)
            {
                Object.Destroy(style);
            }

            ContentSizeFitter fitter = labelObject.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                Object.Destroy(fitter);
            }

            LocText label = labelObject.GetComponent<LocText>();
            if (label == null)
            {
                Object.Destroy(labelObject);
                return null;
            }

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            label.key = "";
            label.textStyleSetting = null;
            label.text = "";
            label.font = source.font;
            label.color = source.color;
            label.fontStyle = source.fontStyle;
            label.fontSize = 12f;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            labelObject.SetActive(true);
            return label;
        }

        private static string FormatCelsius(float celsius)
        {
            return celsius.ToString("0.#") + " °C";
        }
    }

    /// <summary>Left "recipe detail" panel: shows the selected recipe and controls rate/amount.</summary>
    public sealed class HeartRecipeDetailScreen : KScreen
    {
        private Image icon;
        private LocText titleLabel;
        private LocText descriptionLabel;
        private LocText subtitleLabel;
        private bool initialized;
        private readonly List<GameObject> forcedHidden = new List<GameObject>();

        private KNumberInputField amountInput;
        private MultiToggle decrement;
        private MultiToggle increment;
        private Transform controlsQueue;
        private Transform controlsBox;
        private GameObject sliderRow;
        private Slider valueSlider;
        private Toggle thermostatToggle;
        private GameObject thermostatCheckMark;
        private LocText sliderLabel;
        private LocText thermostatToggleLabel;
        private bool customControlsBuilt;
        private bool useSlider;
        private bool suppressSliderEvent;
        private bool suppressToggleEvent;

        private HeartProcessor processor;
        private HeartProcessOperation operation;
        private Tag element;
        private bool isRate;
        private bool isTemperature;
        private HeartTerminal thermostatTerminal;
        private float nextRefreshTime;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            InitializeUI();
        }

        public override void ScreenUpdate(bool topLevel)
        {
            base.ScreenUpdate(topLevel);

            // Some vanilla SelectedRecipeQueueScreen logic may reactivate the placeholder areas;
            // keep them hidden continuously so "undiscovered ingredients" never reappears.
            for (int i = 0; i < forcedHidden.Count; i++)
            {
                GameObject go = forcedHidden[i];
                if (go != null && go.activeSelf)
                {
                    go.SetActive(false);
                }
            }

            // The target may decrease after processing; sync the active control while the player is not editing.
            if ((processor == null && thermostatTerminal == null) || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 1f;

            if (isTemperature)
            {
                UpdateThermostatDescription();
            }

            float value = GetValue();
            if (useSlider)
            {
                if (valueSlider != null && !Input.GetMouseButton(0))
                {
                    if (!Mathf.Approximately(value, valueSlider.value))
                    {
                        SetSliderValue(value);
                    }

                    UpdateSliderLabel();
                }
            }
            else if (amountInput != null && amountInput.field != null &&
                     !amountInput.field.isFocused && !Mathf.Approximately(value, amountInput.currentValue))
            {
                amountInput.SetAmount(value);
            }
        }

        public void SetRecipe(HeartProcessor processor, HeartProcessOperation operation, Tag element, bool isRate)
        {
            this.processor = processor;
            this.operation = operation;
            this.element = element;
            this.isRate = isRate;
            this.isTemperature = false;
            this.thermostatTerminal = null;
            InitializeUI();
            HideVanillaRecipeSections();
            Refresh();
        }

        public void SetThermostat(HeartTerminal terminal, Tag element)
        {
            this.thermostatTerminal = terminal;
            this.processor = terminal != null ? terminal.Processor : null;
            this.operation = HeartProcessOperation.Thermostat;
            this.element = element;
            this.isRate = false;
            this.isTemperature = true;
            InitializeUI();
            HideVanillaRecipeSections();
            Refresh();
        }

        private void InitializeUI()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            // Remove any remaining vanilla component immediately so it cannot reactivate the ingredient placeholder area.
            SelectedRecipeQueueScreen vanillaScreen = GetComponent<SelectedRecipeQueueScreen>();
            if (vanillaScreen != null)
            {
                HideVanillaButton(vanillaScreen, "previousRecipeButton");
                HideVanillaButton(vanillaScreen, "nextRecipeButton");
                HideVanillaButton(vanillaScreen, "InfiniteButton");
                Object.Destroy(vanillaScreen);
            }

            Transform itemIcon = transform.Find("Item/Icon");
            icon = itemIcon != null ? itemIcon.GetComponent<Image>() : null;

            Transform itemLabel = transform.Find("Item/Label");
            titleLabel = itemLabel != null ? itemLabel.GetComponent<LocText>() : null;

            Transform description = transform.Find("OtherDescriptors/MainDescription");
            descriptionLabel = description != null ? description.GetComponent<LocText>() : null;
            if (descriptionLabel == null)
            {
                descriptionLabel = FindLabel("MainDescription");
            }

            Transform input = transform.Find("Controls/Queue/Box/NumberField");
            amountInput = input != null ? input.GetComponent<KNumberInputField>() : null;
            if (amountInput == null)
            {
                amountInput = GetComponentInChildren<KNumberInputField>(true);
            }

            Transform dec = transform.Find("Controls/Queue/DecButton");
            decrement = dec != null ? dec.GetComponent<MultiToggle>() : null;
            Transform inc = transform.Find("Controls/Queue/IncButton");
            increment = inc != null ? inc.GetComponent<MultiToggle>() : null;

            controlsQueue = transform.Find("Controls/Queue");
            controlsBox = transform.Find("Controls/Queue/Box");

            Hide("MinionPortrait");
            HideVanillaRecipeSections();
            Hide("SelectionSummaryIcons WIP");
            // Recipe cycling and the "forever" queue controls are not used by the terminal details.
            RemoveDeep("CycleRecipesRow");
            Remove("InfiniteButton");
            Remove("Controls/InfiniteButton");
            Remove("Controls/Queue/Box/Infinite");
            Hide("ResearchRequired");
            Hide("UndiscoveredMaterials");

            Transform subtitle = transform.Find("ControlsTitlebar/SubtitleText");
            if (subtitle != null)
            {
                subtitleLabel = subtitle.GetComponent<LocText>();
                if (subtitleLabel != null)
                {
                    subtitleLabel.text = STRINGS.UI.ONIHEART.RECIPE_DETAIL_TITLE;
                }
            }

            if (amountInput != null)
            {
                amountInput.onEndEdit += delegate
                {
                    if (!useSlider)
                    {
                        ApplyValue(amountInput.currentValue);
                    }
                };
            }

            if (decrement != null)
            {
                decrement.onClick = delegate
                {
                    if (!useSlider)
                    {
                        ApplyValue(GetValue() - GetStep());
                    }
                };
            }

            if (increment != null)
            {
                increment.onClick = delegate
                {
                    if (!useSlider)
                    {
                        ApplyValue(GetValue() + GetStep());
                    }
                };
            }

            BuildCustomControls();
        }

        private void Hide(string path)
        {
            Transform child = transform.Find(path);
            if (child != null)
            {
                KeepHidden(child.gameObject);
            }
        }

        private void KeepHidden(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            go.SetActive(false);
            if (!forcedHidden.Contains(go))
            {
                forcedHidden.Add(go);
            }
        }

        private static void HideVanillaButton(SelectedRecipeQueueScreen screen, string fieldName)
        {
            Component button = Traverse.Create(screen).Field(fieldName).GetValue<Component>();
            if (button != null)
            {
                button.gameObject.SetActive(false);
            }
        }

        private void HideDeep(string name)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    KeepHidden(all[i].gameObject);
                }
            }
        }

        private void BuildCustomControls()
        {
            if (customControlsBuilt)
            {
                return;
            }

            customControlsBuilt = true;

            Transform controls = transform.Find("Controls");
            if (controls != null)
            {
                controls.gameObject.SetActive(true);
            }

            Transform rowParent = controls != null ? controls : transform;

            sliderRow = new GameObject("ONIHeartSliderRow", typeof(RectTransform));
            RectTransform rowRect = sliderRow.GetComponent<RectTransform>();
            rowRect.SetParent(rowParent, false);
            rowRect.localScale = Vector3.one;
            rowRect.SetAsLastSibling();

            HorizontalLayoutGroup rowLayout = sliderRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.spacing = 6f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            LayoutElement rowElement = sliderRow.AddComponent<LayoutElement>();
            rowElement.minHeight = 30f;
            rowElement.preferredHeight = 30f;
            rowElement.preferredWidth = 220f;
            rowElement.flexibleWidth = 1f;

            thermostatToggle = CreateToggle(rowRect);
            UpdateThermostatToggleVisual();
            LayoutElement toggleElement = thermostatToggle.gameObject.GetComponent<LayoutElement>();
            if (toggleElement == null)
            {
                toggleElement = thermostatToggle.gameObject.AddComponent<LayoutElement>();
            }

            toggleElement.minWidth = 24f;
            toggleElement.preferredWidth = 24f;
            toggleElement.flexibleWidth = 0f;
            toggleElement.minHeight = 24f;
            toggleElement.preferredHeight = 24f;
            toggleElement.flexibleHeight = 0f;

            thermostatToggleLabel = CreateLabel(
                "ONIHeartThermostatToggleLabel",
                rowRect,
                TextAlignmentOptions.Left,
                15f);
            LayoutElement toggleLabelElement = thermostatToggleLabel.gameObject.AddComponent<LayoutElement>();
            toggleLabelElement.minWidth = 70f;
            toggleLabelElement.preferredWidth = 70f;
            toggleLabelElement.flexibleWidth = 0f;
            toggleLabelElement.minHeight = 24f;
            toggleLabelElement.preferredHeight = 24f;
            toggleLabelElement.flexibleHeight = 0f;

            sliderLabel = CreateLabel(
                "ONIHeartSliderLabel",
                rowRect,
                TextAlignmentOptions.Left,
                15f);
            LayoutElement sliderLabelElement = sliderLabel.gameObject.AddComponent<LayoutElement>();
            sliderLabelElement.minWidth = 112f;
            sliderLabelElement.preferredWidth = 112f;
            sliderLabelElement.flexibleWidth = 0f;
            sliderLabelElement.minHeight = 24f;
            sliderLabelElement.preferredHeight = 24f;
            sliderLabelElement.flexibleHeight = 0f;

            valueSlider = CreateSlider(rowRect);
            LayoutElement sliderElement = valueSlider.gameObject.GetComponent<LayoutElement>();
            if (sliderElement == null)
            {
                sliderElement = valueSlider.gameObject.AddComponent<LayoutElement>();
            }

            sliderElement.ignoreLayout = false;
            sliderElement.minWidth = 50f;
            sliderElement.preferredWidth = 90f;
            sliderElement.flexibleWidth = 1f;
            sliderElement.minHeight = 24f;
            sliderElement.preferredHeight = 30f;
            sliderElement.flexibleHeight = 0f;

            thermostatToggle.gameObject.SetActive(false);
            thermostatToggleLabel.gameObject.SetActive(false);
            sliderRow.SetActive(false);
        }

        /// <summary>
        /// Clone a vanilla checkbox (CheckBox + CheckMark) so the thermostat switch uses the game's checkmark style.
        /// </summary>
        private Toggle CreateNativeCheckbox(RectTransform parent)
        {
            KToggle[] toggles = Resources.FindObjectsOfTypeAll<KToggle>();
            for (int i = 0; i < toggles.Length; i++)
            {
                KToggle source = toggles[i];
                if (source == null || source.transform.Find("CheckMark") == null)
                {
                    continue;
                }

                GameObject clone = Object.Instantiate(source.gameObject, parent, false);
                clone.name = "ONIHeartThermostatToggle";
                clone.SetActive(true);

                KToggle toggle = clone.GetComponent<KToggle>();
                if (toggle == null)
                {
                    Object.Destroy(clone);
                    continue;
                }

                toggle.group = null;
                toggle.navigation = new Navigation { mode = Navigation.Mode.None };
                toggle.ClearOnClick();
                toggle.ClearOnValueChanged();
                toggle.interactable = true;
                toggle.isOn = false;
                toggle.onValueChanged += OnThermostatToggleChanged;
                thermostatCheckMark = clone.transform.Find("CheckMark") != null
                    ? clone.transform.Find("CheckMark").gameObject
                    : null;

                RectTransform rect = clone.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                return toggle;
            }

            return null;
        }

        private Toggle CreateToggle(RectTransform parent)
        {
            Toggle nativeToggle = CreateNativeCheckbox(parent);
            if (nativeToggle != null)
            {
                return nativeToggle;
            }

            Debug.LogWarning("[ONIHeart] Native checkbox not found; using fallback toggle.");
            GameObject root = new GameObject("ONIHeartThermostatToggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
            root.SetActive(false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(24f, 24f);
            rect.anchoredPosition = Vector2.zero;

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.13f, 0.13f, 0.13f, 1f);

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            toggle.targetGraphic = background;

            GameObject checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.SetParent(root.transform, false);
            checkRect.anchorMin = new Vector2(0.18f, 0.18f);
            checkRect.anchorMax = new Vector2(0.82f, 0.82f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image checkImage = checkmark.GetComponent<Image>();
            checkImage.color = new Color(0.35f, 0.85f, 0.35f, 1f);
            toggle.graphic = checkImage;
            thermostatCheckMark = checkmark;

            root.SetActive(true);
            toggle.isOn = false;
            toggle.onValueChanged.AddListener(OnThermostatToggleChanged);
            return toggle;
        }

        /// <summary>
        /// Clone the game's own temperature-overlay slider so the handle and gradient track match the vanilla temperature bar.
        /// </summary>
        private static Slider CreateNativeTemperatureSlider(RectTransform parent)
        {
            TemperatureOverlayThresholdAdjustmentWidget[] widgets =
                Resources.FindObjectsOfTypeAll<TemperatureOverlayThresholdAdjustmentWidget>();
            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] == null)
                {
                    continue;
                }

                KSlider[] sliders = widgets[i].GetComponentsInChildren<KSlider>(true);
                for (int j = 0; j < sliders.Length; j++)
                {
                    Slider clone = CloneNativeSlider(sliders[j], parent);
                    if (clone != null)
                    {
                        return clone;
                    }
                }
            }

            KSlider[] allSliders = Resources.FindObjectsOfTypeAll<KSlider>();
            for (int i = 0; i < allSliders.Length; i++)
            {
                if (allSliders[i] != null && IsTemperatureStyleSlider(allSliders[i]))
                {
                    Slider clone = CloneNativeSlider(allSliders[i], parent);
                    if (clone != null)
                    {
                        return clone;
                    }
                }
            }

            return null;
        }

        private static bool IsTemperatureStyleSlider(KSlider slider)
        {
            Transform current = slider != null ? slider.transform : null;
            while (current != null)
            {
                if (current.name.IndexOf("Temperature", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Slider CloneNativeSlider(KSlider source, RectTransform parent)
        {
            if (source == null || source.fillRect == null || source.handleRect == null)
            {
                return null;
            }

            GameObject clone = Object.Instantiate(source.gameObject, parent, false);
            clone.name = "ONIHeartValueSlider";
            clone.SetActive(true);

            Slider slider = clone.GetComponent<Slider>();
            if (slider == null)
            {
                Object.Destroy(clone);
                return null;
            }

            slider.onValueChanged.RemoveAllListeners();
            RectTransform rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return slider;
        }

        private Slider CreateSlider(RectTransform parent)
        {
            Slider nativeSlider = CreateNativeTemperatureSlider(parent);
            if (nativeSlider != null)
            {
                nativeSlider.interactable = true;
                nativeSlider.onValueChanged.AddListener(OnSliderValueChanged);
                return nativeSlider;
            }

            Debug.LogWarning("[ONIHeart] Native temperature slider not found; using fallback slider.");
            GameObject root = new GameObject("ONIHeartValueSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
            root.SetActive(false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, 1f);
            rect.offsetMax = new Vector2(0f, 19f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.13f, 0.13f, 0.13f, 1f);

            Slider slider = root.GetComponent<Slider>();
            slider.interactable = true;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(root.transform, false);
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRect.sizeDelta = new Vector2(-20f, 8f);
            fillAreaRect.anchoredPosition = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(fillArea.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.28f, 0.65f, 0.38f, 1f);
            slider.fillRect = fillRect;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.SetParent(root.transform, false);
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.SetParent(handleArea.transform, false);
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(14f, 0f);
            handleRect.anchoredPosition = Vector2.zero;
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            root.SetActive(true);
            slider.onValueChanged.AddListener(OnSliderValueChanged);
            return slider;
        }

        private LocText CreateLabel(string name, RectTransform parent, TextAlignmentOptions alignment, float fontSize)
        {
            LocText source = descriptionLabel != null ? descriptionLabel : titleLabel;
            GameObject labelObject = source != null
                ? Object.Instantiate(source.gameObject, parent, false)
                : new GameObject(name, typeof(RectTransform));

            labelObject.name = name;
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            LocText label = labelObject.GetComponent<LocText>();
            if (label == null)
            {
                label = labelObject.AddComponent<LocText>();
            }

            if (source != null)
            {
                label.font = source.font;
                label.color = source.color;
                label.fontStyle = source.fontStyle;
            }

            label.key = "";
            label.textStyleSetting = null;
            label.text = "";
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = alignment;
            label.raycastTarget = false;
            labelObject.SetActive(true);

            return label;
        }

        private void OnSliderValueChanged(float value)
        {
            if (suppressSliderEvent)
            {
                return;
            }

            ApplyValue(value);
            UpdateSliderLabel();
            if (isTemperature)
            {
                UpdateThermostatToggleLabel();
                UpdateThermostatDescription();
            }
        }

        private void OnThermostatToggleChanged(bool enabled)
        {
            if (suppressToggleEvent || thermostatTerminal == null || !element.IsValid)
            {
                return;
            }

            thermostatTerminal.SetThermostatEnabled(element, enabled);
            UpdateThermostatToggleVisual();
            UpdateThermostatToggleLabel();
        }

        private void UpdateThermostatToggleVisual()
        {
            if (thermostatCheckMark == null)
            {
                return;
            }

            bool enabled = thermostatToggle != null && thermostatToggle.isOn;
            thermostatCheckMark.SetActive(enabled);
            CanvasRenderer renderer = thermostatCheckMark.GetComponent<CanvasRenderer>();
            if (renderer != null)
            {
                renderer.SetAlpha(enabled ? 1f : 0f);
            }
        }

        private void SetSliderValue(float value)
        {
            if (valueSlider == null)
            {
                return;
            }

            suppressSliderEvent = true;
            valueSlider.value = value;
            suppressSliderEvent = false;
        }

        private void UpdateSliderLabel()
        {
            if (sliderLabel == null)
            {
                return;
            }

            float value = GetValue();
            if (isTemperature)
            {
                sliderLabel.text = string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_SLIDER_VALUE, FormatCelsius(value));
            }
            else if (isRate)
            {
                sliderLabel.text = string.Format(STRINGS.UI.ONIHEART.DECOMPOSE_RATE_VALUE, value.ToString("0.##"));
            }
            else
            {
                sliderLabel.text = string.Format(STRINGS.UI.ONIHEART.CONVERSION_AMOUNT_VALUE, value.ToString("0"));
            }

            KSlider nativeSlider = valueSlider as KSlider;
            if (nativeSlider != null)
            {
                nativeSlider.SetTooltipText(sliderLabel.text);
            }
        }

        private void UpdateThermostatToggleState()
        {
            if (thermostatToggle == null)
            {
                return;
            }

            suppressToggleEvent = true;
            thermostatToggle.isOn = isTemperature && thermostatTerminal != null &&
                thermostatTerminal.IsThermostatEnabled(element);
            suppressToggleEvent = false;
            UpdateThermostatToggleVisual();
            UpdateThermostatToggleLabel();
        }

        private void UpdateThermostatToggleLabel()
        {
            if (thermostatToggleLabel == null || !isTemperature)
            {
                return;
            }

            thermostatToggleLabel.text = FormatCelsius(GetValue());
        }

        /// <summary>
        /// Hide the vanilla ingredient/effect areas in the recipe detail, leaving only our description and bottom amount controls.
        /// Do not rely on fixed hierarchy paths so different game/DLC prefab layouts cannot leave placeholder text behind.
        /// </summary>
        private void HideVanillaRecipeSections()
        {
            Transform descriptors = transform.Find("OtherDescriptors");
            if (descriptors != null)
            {
                for (int i = descriptors.childCount - 1; i >= 0; i--)
                {
                    Transform child = descriptors.GetChild(i);
                    if (child.name != "MainDescription")
                    {
                        Remove(child.gameObject);
                    }
                }
            }

            RemoveDeep("IngredientDescriptors");
            RemoveDeep("EffectDescriptors");
            RemoveDeep("MaterialSelectionContainerPrefab");
            RemoveDeep("MaterialFilterRow");
            RemoveDeep("NoDiscoveredMaterialsRow");
            RemoveDeep("NoRecipeSelectedLabel");
            Remove("UndiscoveredMaterials");
            Remove("ResearchRequired");
            HideUnwantedLabels();
        }

        /// <summary>
        /// Destroy vanilla placeholder sections completely so no vanilla logic can reactivate them.
        /// </summary>
        private void Remove(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            go.SetActive(false);
            Object.Destroy(go);
        }

        private void Remove(string path)
        {
            Transform child = transform.Find(path);
            if (child != null)
            {
                Remove(child.gameObject);
            }
        }

        private void RemoveDeep(string name)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    Remove(all[i].gameObject);
                }
            }
        }

        /// <summary>
        /// Find vanilla placeholder text by localization key and hide its whole container.
        /// </summary>
        private void HideUnwantedLabels()
        {
            LocText[] labels = GetComponentsInChildren<LocText>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                LocText label = labels[i];
                if (label == null || string.IsNullOrEmpty(label.key))
                {
                    continue;
                }

                if (label.key.IndexOf("UNDISCOVERED_INGREDIENT", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.key.IndexOf("RECIPE_RESEARCH_REQUIRED", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Transform parent = label.transform.parent;
                    Remove(parent != null ? parent.gameObject : label.gameObject);
                }
            }
        }

        private LocText FindLabel(string name)
        {
            LocText[] labels = GetComponentsInChildren<LocText>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].name == name)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private void Refresh()
        {
            if (isTemperature)
            {
                RefreshThermostat();
                return;
            }

            if (processor == null)
            {
                return;
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = STRINGS.UI.ONIHEART.RECIPE_DETAIL_TITLE;
            }

            if (icon != null)
            {
                icon.gameObject.SetActive(true);
                var sprite = Def.GetUISprite(element);
                if (sprite != null)
                {
                    icon.sprite = sprite.first;
                    icon.color = sprite.second;
                }
            }

            string recipeName = processor.GetRecipeName(operation, element);
            if (titleLabel != null)
            {
                titleLabel.text = recipeName;
            }

            if (descriptionLabel != null)
            {
                string stock = GameUtil.GetFormattedMass(processor.GetStoredMass(element));
                descriptionLabel.text = recipeName + "\n" +
                    string.Format(STRINGS.UI.ONIHEART.STOCK_AMOUNT, stock) + "\n" +
                    (isRate ? STRINGS.UI.ONIHEART.DECOMPOSE_RATE_TOOLTIP : STRINGS.UI.ONIHEART.CONVERSION_AMOUNT_TOOLTIP);
            }

            RefreshCustomControls();

            if (!isRate && amountInput != null)
            {
                amountInput.minValue = 0f;
                amountInput.maxValue = HeartProcessor.MAX_CONVERSION_AMOUNT;
                amountInput.decimalPlaces = 0;
                amountInput.Activate();
                amountInput.SetAmount(GetValue());
                amountInput.field.characterLimit = 10;
            }
        }

        private void RefreshThermostat()
        {
            if (thermostatTerminal == null || !element.IsValid)
            {
                return;
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = STRINGS.UI.ONIHEART.PANEL_THERMOSTAT;
            }

            Element selectedElement = ElementLoader.GetElement(element);
            string elementName = selectedElement != null
                ? selectedElement.name
                : element.ProperName();

            if (icon != null)
            {
                icon.gameObject.SetActive(selectedElement != null);
                if (selectedElement != null)
                {
                    var sprite = Def.GetUISprite(selectedElement.tag);
                    if (sprite != null)
                    {
                        icon.sprite = sprite.first;
                        icon.color = sprite.second;
                    }
                }
            }

            if (titleLabel != null)
            {
                titleLabel.text = string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_DETAIL_TITLE, elementName);
            }

            UpdateThermostatDescription();
            RefreshCustomControls();
        }

        private void UpdateThermostatDescription()
        {
            if (thermostatTerminal == null || descriptionLabel == null || !element.IsValid)
            {
                return;
            }

            Element selectedElement = ElementLoader.GetElement(element);
            float current = processor != null && selectedElement != null
                ? processor.GetStoredTemperature(selectedElement.tag)
                : 0f;
            string currentText = current > 0f
                ? FormatCelsius(current - 273.15f)
                : STRINGS.UI.ONIHEART.THERMOSTAT_NO_STOCK;
            string rangeText;
            float minCelsius;
            float maxCelsius;
            if (thermostatTerminal.TryGetThermostatRange(element, out minCelsius, out maxCelsius))
            {
                rangeText = string.Format(STRINGS.UI.ONIHEART.THERMOSTAT_RANGE_STATUS,
                    FormatCelsius(minCelsius), FormatCelsius(maxCelsius));
            }
            else
            {
                rangeText = STRINGS.UI.ONIHEART.THERMOSTAT_NO_RANGE;
            }

            string target = FormatCelsius(thermostatTerminal.GetThermostatTargetCelsius(element));
            string state = thermostatTerminal.IsThermostatEnabled(element)
                ? STRINGS.UI.ONIHEART.THERMOSTAT_TOGGLE_ON
                : STRINGS.UI.ONIHEART.THERMOSTAT_TOGGLE_OFF;
            descriptionLabel.text = string.Format(
                STRINGS.UI.ONIHEART.THERMOSTAT_DETAIL,
                target + "（" + state + "）",
                currentText,
                rangeText,
                STRINGS.UI.ONIHEART.THERMOSTAT_DETAIL_RULES);
        }

        private void RefreshCustomControls()
        {
            if (valueSlider == null)
            {
                return;
            }

            useSlider = isRate || isTemperature;
            if (sliderRow != null)
            {
                sliderRow.SetActive(useSlider);
            }

            if (controlsQueue != null)
            {
                controlsQueue.gameObject.SetActive(!useSlider);
            }

            if (controlsBox != null)
            {
                controlsBox.gameObject.SetActive(!useSlider);
            }

            if (decrement != null)
            {
                decrement.gameObject.SetActive(!useSlider);
            }

            if (increment != null)
            {
                increment.gameObject.SetActive(!useSlider);
            }

            if (amountInput != null && controlsBox == null)
            {
                amountInput.gameObject.SetActive(!useSlider);
            }

            if (!useSlider)
            {
                return;
            }

            bool showToggle = isTemperature && thermostatTerminal != null && element.IsValid;
            if (thermostatToggle != null)
            {
                thermostatToggle.gameObject.SetActive(showToggle);
            }

            if (thermostatToggleLabel != null)
            {
                thermostatToggleLabel.gameObject.SetActive(showToggle);
            }

            if (sliderLabel != null)
            {
                sliderLabel.gameObject.SetActive(!isTemperature);
            }

            float minValue;
            float maxValue;
            if (isTemperature)
            {
                float minCelsius;
                float maxCelsius;
                if (thermostatTerminal.TryGetThermostatRange(element, out minCelsius, out maxCelsius))
                {
                    minValue = minCelsius;
                    maxValue = maxCelsius;
                }
                else
                {
                    minValue = -273f;
                    maxValue = 1000f;
                }
            }
            else
            {
                minValue = 0f;
                maxValue = isRate ? HeartProcessor.MAX_DECOMPOSE_RATE : HeartProcessor.MAX_CONVERSION_AMOUNT;
            }

            valueSlider.minValue = minValue;
            valueSlider.maxValue = maxValue;
            valueSlider.wholeNumbers = isTemperature || !isRate;
            SetSliderValue(Mathf.Clamp(GetValue(), minValue, maxValue));
            UpdateSliderLabel();
            UpdateThermostatToggleState();
        }

        private static string FormatCelsius(float celsius)
        {
            return celsius.ToString("0.#") + " °C";
        }

        private float GetValue()
        {
            if (isTemperature)
            {
                return thermostatTerminal != null && element.IsValid
                    ? thermostatTerminal.GetThermostatTargetCelsius(element)
                    : 0f;
            }

            if (processor == null)
            {
                return 0f;
            }

            return isRate ? processor.DecomposeRate : processor.GetPending(operation, element);
        }

        private float GetStep() => isTemperature ? 1f : (isRate ? 0.5f : 10f);

        private void ApplyValue(float value)
        {
            if (isTemperature)
            {
                if (thermostatTerminal == null || !element.IsValid)
                {
                    return;
                }

                value = Mathf.Round(value * 10f) / 10f;
                float minCelsius;
                float maxCelsius;
                if (thermostatTerminal.TryGetThermostatRange(element, out minCelsius, out maxCelsius))
                {
                    value = Mathf.Clamp(value, minCelsius, maxCelsius);
                }

                thermostatTerminal.SetThermostatTargetCelsius(element, value);
                return;
            }

            if (processor == null)
            {
                return;
            }

            float max = isRate ? HeartProcessor.MAX_DECOMPOSE_RATE : HeartProcessor.MAX_CONVERSION_AMOUNT;
            value = Mathf.Clamp(value, 0f, max);
            value = isRate ? Mathf.Round(value * 10f) / 10f : Mathf.Round(value);

            if (isRate)
            {
                processor.SetDecomposeRate(value);
            }
            else
            {
                processor.SetPending(operation, element, value);
            }
        }
    }

    /// <summary>Marks the cloned food-press screen and stores the terminal panel it should display.</summary>
    public sealed class HeartTerminalRecipeMarker : KMonoBehaviour
    {
        public HeartRecipePanel panel;
        public string title = "";
    }

    [HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
    public static class HeartTerminalScreensPatch
    {
        public static void Postfix(System.Collections.Generic.List<DetailsScreen.SideScreenRef> ___sideScreens)
        {
            if (___sideScreens == null || DetailsScreen.Instance == null)
            {
                return;
            }

            for (int i = 0; i < ___sideScreens.Count; i++)
            {
                if (___sideScreens[i].screenPrefab != null &&
                    ___sideScreens[i].screenPrefab.GetComponent<HeartTerminalRecipeMarker>() != null)
                {
                    return;
                }
            }

            ComplexFabricatorSideScreen template = null;
            ComplexFabricatorSideScreen[] candidates = Resources.FindObjectsOfTypeAll<ComplexFabricatorSideScreen>();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                {
                    template = candidates[i];
                    if (!template.gameObject.scene.IsValid())
                    {
                        break;
                    }
                }
            }

            if (template == null)
            {
                Debug.LogWarning("[ONIHeart] ComplexFabricatorSideScreen template not found.");
                return;
            }

            DetailsScreen.SidescreenTab tab =
                DetailsScreen.Instance.GetTabOfType(DetailsScreen.SidescreenTabTypes.Config);
            if (tab == null || tab.bodyInstance == null)
            {
                return;
            }

            GameObject clone = Object.Instantiate(template.gameObject, tab.bodyInstance.transform, false);
            clone.name = "HeartTerminalRecipeScreen";
            clone.SetActive(false);

            ComplexFabricatorSideScreen screen = clone.GetComponent<ComplexFabricatorSideScreen>();
            if (screen == null)
            {
                Object.Destroy(clone);
                return;
            }

            // Use only the vanilla food-press screen appearance; do not run its recipe/queue logic.
            screen.enabled = false;

            RectTransform rect = clone.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, 340f);
                rect.localScale = Vector3.one;
            }

            LayoutElement layout = clone.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = clone.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = 340f;
            layout.minHeight = 220f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;

            string[] hide = { "Header", "SelectedRecipeTitleBar", "DetailsScrollView", "DescriptionLabel", "ScrollViewport" };
            Transform[] all = clone.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                for (int j = 0; j < hide.Length; j++)
                {
                    if (all[i].name == hide[j])
                    {
                        all[i].gameObject.SetActive(false);
                    }
                }
            }

            if (screen.buttonScrollContainer != null)
            {
                screen.buttonScrollContainer.minHeight = 220f;
                screen.buttonScrollContainer.preferredHeight = -1f;
                screen.buttonScrollContainer.flexibleWidth = 1f;
                screen.buttonScrollContainer.flexibleHeight = 1f;
            }

            RectTransform content = screen.buttonContentContainer;
            GameObject rowPrefab = null;
            Transform[] rows = clone.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].name == "RecipeButtonWithQueue")
                {
                    rowPrefab = rows[i].gameObject;
                    break;
                }
            }

            if (content == null || rowPrefab == null)
            {
                Debug.LogWarning("[ONIHeart] Recipe list content or row prefab not found.");
                Object.Destroy(clone);
                return;
            }

            Transform noRecipes = content.Find("NoRecipesDiscovered");
            if (noRecipes != null)
            {
                noRecipes.gameObject.SetActive(false);
            }

            HeartRecipePanel panel = clone.AddComponent<HeartRecipePanel>();
            panel.content = content;
            panel.rowPrefab = rowPrefab;

            SelectedRecipeQueueScreen detailTemplate = screen.recipeScreenPrefab;
            if (detailTemplate == null)
            {
                detailTemplate = clone.GetComponentInChildren<SelectedRecipeQueueScreen>(true);
            }

            if (detailTemplate != null)
            {
                GameObject detailObject = Object.Instantiate(detailTemplate.gameObject, DetailsScreen.Instance.transform, false);
                detailObject.name = "ONIHeartRecipeDetail";
                detailObject.SetActive(false);

                SelectedRecipeQueueScreen original = detailObject.GetComponent<SelectedRecipeQueueScreen>();
                if (original != null)
                {
                    Object.DestroyImmediate(original);
                }

                HeartRecipeDetailScreen detail = detailObject.AddComponent<HeartRecipeDetailScreen>();
                panel.detailPrefab = detail;
            }
            else
            {
                Debug.LogWarning("[ONIHeart] SelectedRecipeQueueScreen prefab not found.");
            }

            HeartTerminalRecipeMarker marker = clone.AddComponent<HeartTerminalRecipeMarker>();
            marker.panel = panel;
            marker.title = STRINGS.UI.ONIHEART.TERMINAL_TITLE;

            ___sideScreens.Add(new DetailsScreen.SideScreenRef
            {
                name = clone.name,
                screenPrefab = screen,
                screenInstance = screen,
                tab = DetailsScreen.SidescreenTabTypes.Config
            });
        }
    }

    [HarmonyPatch(typeof(ComplexFabricatorSideScreen), nameof(ComplexFabricatorSideScreen.IsValidForTarget))]
    public static class HeartTerminalScreenIsValidPatch
    {
        public static bool Prefix(ComplexFabricatorSideScreen __instance, GameObject target, ref bool __result)
        {
            HeartTerminalRecipeMarker marker = __instance.GetComponent<HeartTerminalRecipeMarker>();
            if (marker == null)
            {
                return true;
            }

            __result = target != null && target.GetComponent<HeartTerminal>() != null;
            return false;
        }
    }

    [HarmonyPatch(typeof(ComplexFabricatorSideScreen), nameof(ComplexFabricatorSideScreen.SetTarget))]
    public static class HeartTerminalScreenSetTargetPatch
    {
        public static bool Prefix(ComplexFabricatorSideScreen __instance, GameObject target)
        {
            HeartTerminalRecipeMarker marker = __instance.GetComponent<HeartTerminalRecipeMarker>();
            if (marker == null)
            {
                return true;
            }

            HeartTerminal terminal = target != null ? target.GetComponent<HeartTerminal>() : null;
            if (terminal == null)
            {
                marker.panel.Bind(null, null, HeartProcessOperation.Off,
                                  STRINGS.UI.ONIHEART.TERMINAL_TITLE);
                return false;
            }

            marker.title = GetTitle(terminal.operation);
            marker.panel.Bind(terminal, terminal.Processor, terminal.operation, marker.title);
            return false;
        }

        private static string GetTitle(HeartProcessOperation operation)
        {
            switch (operation)
            {
                case HeartProcessOperation.Decompose:
                    return STRINGS.UI.ONIHEART.PANEL_DECOMPOSE;
                case HeartProcessOperation.Solidify:
                    return STRINGS.UI.ONIHEART.PANEL_SOLIDIFY;
                case HeartProcessOperation.Liquefy:
                    return STRINGS.UI.ONIHEART.PANEL_LIQUEFY;
                case HeartProcessOperation.Thermostat:
                    return STRINGS.UI.ONIHEART.PANEL_THERMOSTAT;
                default:
                    return STRINGS.UI.ONIHEART.PANEL_VAPORIZE;
            }
        }
    }

    [HarmonyPatch(typeof(ComplexFabricatorSideScreen), nameof(ComplexFabricatorSideScreen.GetTitle))]
    public static class HeartTerminalScreenTitlePatch
    {
        public static void Postfix(ComplexFabricatorSideScreen __instance, ref string __result)
        {
            HeartTerminalRecipeMarker marker = __instance.GetComponent<HeartTerminalRecipeMarker>();
            if (marker != null && !string.IsNullOrEmpty(marker.title))
            {
                __result = marker.title;
            }
        }
    }

    [HarmonyPatch(typeof(SideScreenContent), nameof(SideScreenContent.GetSideScreenSortOrder))]
    public static class HeartTerminalScreenSortPatch
    {
        public static void Postfix(SideScreenContent __instance, ref int __result)
        {
            if (__instance.GetComponent<HeartTerminalRecipeMarker>() != null)
            {
                __result = 20;
            }
        }
    }

    /// <summary>
    /// Fallback: if a SelectedRecipeQueueScreen is not bound to a ComplexFabricator (e.g. misused),
    /// hide the vanilla "research required / undiscovered ingredients" placeholders.
    /// </summary>
    [HarmonyPatch(typeof(KScreen), nameof(KScreen.ScreenUpdate))]
    public static class HeartOrphanRecipeDetailPatch
    {
        public static void Postfix(KScreen __instance)
        {
            SelectedRecipeQueueScreen screen = __instance as SelectedRecipeQueueScreen;
            if (screen == null)
            {
                return;
            }

            ComplexFabricator target = Traverse.Create(screen).Field("target").GetValue<ComplexFabricator>();
            if (target != null)
            {
                return;
            }

            if (screen.ResearchRequiredContainer != null && screen.ResearchRequiredContainer.activeSelf)
            {
                screen.ResearchRequiredContainer.SetActive(false);
            }

            if (screen.UndiscoveredMaterialsContainer != null && screen.UndiscoveredMaterialsContainer.activeSelf)
            {
                screen.UndiscoveredMaterialsContainer.SetActive(false);
            }
        }
    }
}
