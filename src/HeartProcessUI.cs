using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ONIHeart
{
    /// <summary>终端配方面板：食物压制器风格的配方行（图标 + 名称 + 当前数量/速率），增减在配方详情面板中调整。</summary>
    public sealed class HeartRecipePanel : KMonoBehaviour
    {
        private sealed class RowBinding
        {
            public GameObject row;
            public Tag element;
            public bool isRate;
            public LocText countLabel;
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
            // 终端的“目标元素之心”按钮可能随时切换，配方面板必须跟着换绑；
            // 元素之心改名时标题也要跟着更新。
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

            // 原版配方列表预制体里还带着分类标题/占位配方，全部隐藏，只保留我们自己的行。
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

            // 配方与元素之心的信息显示在建筑的属性面板状态里，配方列表不再单独加标题行。
            if (processor == null)
            {
                rows.Add(new RowBinding { row = CreateRow(STRINGS.UI.ONIHEART.TERMINAL_NO_HEART, null, true) });
                signature = "";
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

            Image icon = row.transform.Find("Button/ToIcons/ToIcon") != null
                ? row.transform.Find("Button/ToIcons/ToIcon").GetComponent<Image>()
                : null;
            if (icon != null)
            {
                var sprite = Def.GetUISprite(element);
                if (sprite != null)
                {
                    icon.sprite = sprite.first;
                    icon.color = sprite.second;
                }
            }

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

            RowBinding binding = new RowBinding
            {
                row = row,
                element = element,
                isRate = isRate,
                countLabel = countLabel
            };
            rows.Add(binding);

            // 配方行只显示当前数量/速率，增减交给点击后打开的配方详情面板，
            // 与食物压制器的配方详情页保持一致。
            HideChild(row, "Queue/IncButton");
            HideChild(row, "Queue/DecButton");

            KToggle toggle = row.GetComponent<KToggle>();
            if (toggle != null)
            {
                toggle.onClick += delegate { OpenDetail(binding); };
            }

            if (references != null && references.HasReference("QueueBoxButton"))
            {
                KButton queueBox = references.GetReference<KButton>("QueueBoxButton");
                if (queueBox != null)
                {
                    queueBox.onClick += delegate { OpenDetail(binding); };
                }
            }

            HideChild(row, "Queue/TechRequired");
            HideChild(row, "Queue/Infinite");
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

            KScreen screen = DetailsScreen.Instance.SetSecondarySideScreen(detailPrefab, STRINGS.UI.ONIHEART.RECIPE_DETAIL_TITLE);
            HeartRecipeDetailScreen detail = screen != null ? screen.GetComponent<HeartRecipeDetailScreen>() : null;
            Debug.Log("[ONIHeart] OpenDetail element=" + binding.element + ", screen=" +
                (screen != null ? screen.name : "null") + ", detail=" + (detail != null));
            if (detail != null)
            {
                detail.SetRecipe(processor, operation, binding.element, binding.isRate);
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
            if (binding == null || binding.countLabel == null || processor == null)
            {
                return;
            }

            float value = binding.isRate
                ? processor.DecomposeRate
                : processor.GetPending(operation, binding.element);
            binding.countLabel.text = value.ToString("0.##");
        }
    }

    /// <summary>左侧“配方详情”面板：显示选中配方并控制速率/数量。</summary>
    public sealed class HeartRecipeDetailScreen : KScreen
    {
        private Image icon;
        private LocText titleLabel;
        private LocText descriptionLabel;
        private KNumberInputField amountInput;
        private MultiToggle decrement;
        private MultiToggle increment;
        private bool initialized;
        private bool loggedLabels;
        private readonly List<GameObject> forcedHidden = new List<GameObject>();

        private HeartProcessor processor;
        private HeartProcessOperation operation;
        private Tag element;
        private bool isRate;
        private float nextRefreshTime;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            InitializeUI();
        }

        public override void ScreenUpdate(bool topLevel)
        {
            base.ScreenUpdate(topLevel);

            // 原版 SelectedRecipeQueueScreen 的某些逻辑可能重新激活占位区域；
            // 这里持续保持隐藏，确保不会再次出现“尚未发现的原料”。
            for (int i = 0; i < forcedHidden.Count; i++)
            {
                GameObject go = forcedHidden[i];
                if (go != null && go.activeSelf)
                {
                    go.SetActive(false);
                }
            }

            // 配方详情打开时，目标数量可能因为处理完成而减少；未在输入时同步显示。
            if (processor == null || amountInput == null || amountInput.field == null ||
                amountInput.field.isFocused || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 1f;
            float value = GetValue();
            if (!Mathf.Approximately(value, amountInput.currentValue))
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
            InitializeUI();
            HideVanillaRecipeSections();
            Refresh();
            LogVisibleLabels();
        }

        private void InitializeUI()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Debug.Log("[ONIHeart] Recipe detail InitializeUI on '" + name + "', vanilla=" +
                (GetComponent<SelectedRecipeQueueScreen>() != null));

            // 如果实例上还残留原版组件，立即移除，避免它重新激活原料占位区域。
            SelectedRecipeQueueScreen vanillaScreen = GetComponent<SelectedRecipeQueueScreen>();
            if (vanillaScreen != null)
            {
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

            Transform dec = transform.Find("Controls/Queue/DecButton");
            decrement = dec != null ? dec.GetComponent<MultiToggle>() : null;

            Transform inc = transform.Find("Controls/Queue/IncButton");
            increment = inc != null ? inc.GetComponent<MultiToggle>() : null;

            Transform controls = transform.Find("Controls");
            if (controls != null)
            {
                controls.gameObject.SetActive(true);
            }

            if (amountInput == null)
            {
                amountInput = GetComponentInChildren<KNumberInputField>(true);
            }

            if (decrement == null)
            {
                MultiToggle[] toggles = GetComponentsInChildren<MultiToggle>(true);
                for (int i = 0; i < toggles.Length; i++)
                {
                    if (toggles[i].name.Contains("Dec"))
                    {
                        decrement = toggles[i];
                        break;
                    }
                }
            }

            if (increment == null)
            {
                MultiToggle[] toggles = GetComponentsInChildren<MultiToggle>(true);
                for (int i = 0; i < toggles.Length; i++)
                {
                    if (toggles[i].name.Contains("Inc"))
                    {
                        increment = toggles[i];
                        break;
                    }
                }
            }

            if (amountInput == null)
            {
                Debug.LogWarning("[ONIHeart] Recipe detail amount input not found.");
            }

            if (decrement != null)
            {
                decrement.gameObject.SetActive(true);
            }

            if (increment != null)
            {
                increment.gameObject.SetActive(true);
            }

            Hide("MinionPortrait");
            HideVanillaRecipeSections();
            Hide("SelectionSummaryIcons WIP");
            Hide("CycleRecipesRow");
            Hide("InfiniteButton");
            Hide("ResearchRequired");
            Hide("UndiscoveredMaterials");

            Transform subtitle = transform.Find("ControlsTitlebar/SubtitleText");
            if (subtitle != null)
            {
                LocText label = subtitle.GetComponent<LocText>();
                if (label != null)
                {
                    label.text = STRINGS.UI.ONIHEART.RECIPE_DETAIL_TITLE;
                }
            }

            if (amountInput != null)
            {
                amountInput.onEndEdit += delegate
                {
                    ApplyValue(amountInput.currentValue);
                };
            }

            if (decrement != null)
            {
                decrement.onClick = delegate { ApplyValue(GetValue() - GetStep()); };
            }

            if (increment != null)
            {
                increment.onClick = delegate { ApplyValue(GetValue() + GetStep()); };
            }
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

        /// <summary>
        /// 隐藏原版配方详情里的原料/效果区域，只留下我们自己的配方说明和底部数量控制栏。
        /// 不依赖固定层级，避免不同版本/DLC 的 prefab 结构差异留下“尚未发现原料”等占位文本。
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
        /// 彻底销毁原版占位区域，避免任何原版逻辑再次把它激活。
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
        /// 直接按本地化 key 找原版占位文本，连它所在的整个容器一起隐藏。
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

        private void LogVisibleLabels()
        {
            if (loggedLabels)
            {
                return;
            }

            loggedLabels = true;
            StringBuilder builder = new StringBuilder();
            LocText[] labels = GetComponentsInChildren<LocText>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                LocText label = labels[i];
                if (label != null && label.gameObject.activeInHierarchy)
                {
                    builder.Append(GetPath(label.transform)).Append('=').Append(label.text).Append(" | ");
                }
            }

            Debug.Log("[ONIHeart] Recipe detail labels: " + builder);

            StringBuilder sections = new StringBuilder();
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                {
                    continue;
                }

                string n = t.name;
                if (n.IndexOf("Undiscovered", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Ingredient", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Research", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sections.Append(GetPath(t)).Append("(active=")
                        .Append(t.gameObject.activeSelf).Append(") | ");
                }
            }

            Debug.Log("[ONIHeart] Recipe detail leftover sections: " + sections);
        }

        private string GetPath(Transform target)
        {
            if (target == null)
            {
                return "";
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null && current != transform)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
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
            if (processor == null)
            {
                return;
            }

            if (icon != null)
            {
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

            if (amountInput != null)
            {
                amountInput.minValue = 0f;
                amountInput.maxValue = isRate ? HeartProcessor.MAX_DECOMPOSE_RATE : HeartProcessor.MAX_CONVERSION_AMOUNT;
                amountInput.decimalPlaces = isRate ? 1 : 0;
                amountInput.Activate();
                amountInput.SetAmount(GetValue());
                amountInput.field.characterLimit = 10;
            }
        }

        private float GetValue()
        {
            if (processor == null)
            {
                return 0f;
            }

            return isRate ? processor.DecomposeRate : processor.GetPending(operation, element);
        }

        private float GetStep() => isRate ? 0.5f : 10f;

        private void ApplyValue(float value)
        {
            if (processor == null)
            {
                return;
            }

            float max = isRate ? HeartProcessor.MAX_DECOMPOSE_RATE : HeartProcessor.MAX_CONVERSION_AMOUNT;
            value = Mathf.Clamp(value, 0f, max);

            if (isRate)
            {
                processor.SetDecomposeRate(value);
            }
            else
            {
                processor.SetPending(operation, element, value);
            }

            if (amountInput != null)
            {
                amountInput.SetAmount(value);
            }
        }
    }

    /// <summary>标记我们克隆的食物压制器屏幕，并保存它要显示的终端面板。</summary>
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

            // 只使用原版的食物压制器屏幕外观，不运行它的配方/队列逻辑。
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

            Debug.Log("[ONIHeart] Terminal recipe side screen registered.");
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
    /// 兜底：如果某个 SelectedRecipeQueueScreen 没有绑定 ComplexFabricator（例如被误用),
    /// 隐藏原版的“所需研究/尚未发现的原料”占位区域。
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
