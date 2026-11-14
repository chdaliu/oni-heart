[中文](#chinese) | [English](#english)

<a id="chinese"></a>

# 元素之心 (ONI-Heart)

《缺氧》Mod：新增一个 4x4 建筑 **元素之心**，以及气体/液体输入输出接口和双向电力接口。

## 功能

### 元素之心
- 4x4，建造材料为原矿（砂岩、花岗岩、火成岩等）或金属矿石（铜矿、铁矿等）。
- 正常建筑：占用 `ObjectLayer.Building`、需要地基，会被普通建筑和地形阻挡。
- 提供类似容器箱子的材料勾选列表，包括种子、衣服、蛋、食物等所有固体物品；气体/液体不进入该列表，需要通过元素接口输入/输出。
- 有工作优先级；在列表中勾选材料后，复制人会像往箱子存放物品一样，把对应固体搬运进元素之心。
- 允许被液体淹没，不会出现“淹没”停机状态。
- 外观是低多边形切割的自然心形宝石：折线心形轮廓、下半部磨成连续圆弧底（底部两侧的收窄轮廓和尖端已去掉，下部不再有深色侧边描边/切缝）、冠部 / 星刻面 / 多面菱形中心切面分层，棱线有独立的暗缝和受光高光，内部带缓慢流动的彩虹折射与微小打磨颗粒；自定义 16 帧 KAnim、6 FPS 循环、512px 帧；图像铺满 4x4 占地范围，建造菜单图标保持普通大小。
- 无限容量存储固体、液体、气体；不同元素分别堆叠。
- 食物不会腐败（应用 `Preserve`）。
- 固体不会挥发/升华（应用 `Seal`）。
- 蛋不会孵化（存入普通 Storage 后孵化被抑制）。
- 内部物品不参与热交换（应用 `Insulate`），建筑自身 `UseStructureTemperature = false`。
- 不会被融化或破坏；只能通过“拆除”指令移除。
- 拆除时：固体直接掉出，气体/液体以瓶装/罐装形式保留，内部电力清空。
- 轻微发光：约 600 Lux 暖白呼吸光，颜色缓慢漂移，不耗电、不发热。
- 大量装饰值：基础装饰 +100，装饰半径 10 格。
- 不发热、不耗电、电力无损耗。

### 元素终端机

新增五种 2x2 科学仪器，建造在 **站台（Stations）** 栏：

- **元素终端机（分解）**
- **元素终端机（固化）**
- **元素终端机（液化）**
- **元素终端机（气化）**
- **元素终端机（温控）**

选中终端后，底部按钮区会出现当前连接的元素之心名称按钮，点击可切换连接的同世界元素之心。终端只控制所连接元素之心的对应操作；元素之心本身不再显示处理面板。

### 配方式处理

选中 **元素终端机** 并连接元素之心后，配方列表会像食物压制器一样显示在右侧详情栏的“配置”页里：

- **分解终端机**：只有水一个配方，点击配方打开配方详情后用拖动条调节速率（kg/s）；速率为 0 即不工作。
- **固化终端机**：可以把**液体或气体**固化成对应固体，每个配方单独设置数量（kg）；0 即不工作。
- **液化终端机**：可以把**气体或固体**液化成对应液体，每个配方单独设置数量（kg）；0 即不工作。
- **气化终端机**：可以把**液体或固体**气化成对应气体，每个配方单独设置数量（kg）；0 即不工作。

每个配方都有独立的目标值，多个配方可以同时工作。分解使用速率，转化使用数量，两种参数分开显示。

面板使用食物压制器风格的配方行：元素图标 + 配方名称 + 当前数量/速率。配方行本身不显示增减按钮；点击配方行会在左侧打开“配方详情”面板。分解速率和温控目标温度使用游戏自带的温度条样式拖动条调节；固化/液化/气化仍然使用原来的数量输入框和加减按钮。详情页中原版的原料/效果占位区域和配方循环按钮均已隐藏。配方列表不再显示配方/元素之心标题行，这些信息改为显示在建筑的属性面板状态里。

转化配方只接受**同一纯物质的相变**：目标产物必须真实存在、可以生成，并且源元素与目标元素属于同一物质家族。燃烧/氧化、精炼、裂化、冶炼、矿石加工、聚合/分解，以及盐水、脏水、牛奶、树脂等溶液/混合物都不会出现在配方列表里。

因此会保留的典型配方包括：水/冰/蒸汽、CO2/O2/H2/CH4 的三相、氯/磷/硫/乙醇的相变、纯油脂/墨水/树脂/糖的固液相变；不会出现动物油脂→生物柴油/二氧化碳、木材/橡胶/煤焦油→二氧化碳、原油→石油、矿石→熔融金属、塑料→石脑油等配方。只有实际相变温度在 10–1000K 内的配方才会显示，岩浆、熔融金属等极端温度转换不会进入列表。

配方列表还会进一步只显示**已经出现过的输入材料**：当前存档中已被游戏发现，或已经进入元素之心 / 已配置待处理数量。没有出现过的材料不会显示；转化产出的新物质会在第一次生成后自动解锁。分解终端不受这个过滤影响。

处理规则：

- **分解水**：按速率处理，1kg/s 水 → 0.888kg/s 氧气 + 0.112kg/s 氢气，耗电 120W，反应热 1.25kW，反应热直接转化为内部电力；耗电/产电随速率缩放。分解终端不受相变白名单影响。
- **固化 / 液化 / 气化**：按源元素实际跨越的相变点处理，输出温度落在目标相的稳定一侧，避免取出后立刻相变；处理时不与周围环境交换热量，放热直接转化为内部电力，吸热直接消耗内部电力。热量按路径分段计算，各相使用各自的比热。转化吞吐为 0.5kg/s。
- 转化配方按目标数量工作，达到设定数量后停止；输入不足时保留剩余数量，等材料补充后继续。
- 所有产物直接存入元素之心内部 Storage。
- 分解和相变都使用元素之心内部电力；需要吸热而电力不足时，会按可用电量降低处理量。

### 温控终端

**元素终端机（温控）** 同一个终端可以同时控制多种液体或气体元素，每种元素都有独立的目标温度和开关。

- 配置页会列出所有可温控的液体/气体元素；点击某个元素打开温控详情，在详情里用拖动条设置目标温度，并用单独的开关启用该元素的恒温控制。
- 目标温度会被限制在元素的稳定相区内部，并与相变点保持 2K 余量。例如水只能设在约 2°C ~ 98°C，不会结冰或汽化。
- 温度低于目标时消耗元素之心内部电力加热；温度高于目标时将多余热量按 100% 转换为内部电力。
- 电力不足时会按可用电量部分加热；冷却产生的电力直接进入元素之心的共享电力池。
- 同一元素之心上，同一种元素只会由一个温控终端控制；在另一个终端上启用同一元素时，会从原终端自动关闭，避免冲突。

同一种液体/气体进入元素之心时，会自动按质量加权平均温度合并成一个堆，而不是保留成两个不同温度的堆。例如已有 20°C 的水，再输入 50°C 的水，会合并为按质量加权计算后的单一水温。没有安装温控终端时不会主动调温，只进行输入温度合并。

### 手动装瓶 / 卸瓶 / 装载

新增五种 1x2 立式建筑，均用原矿制造：

- **手动装瓶器（气体）**：只输出，气体（HVAC）栏
- **手动装瓶器（液体）**：只输出，液体（Plumbing）栏
- **手动卸瓶器（气体）**：只输入，气体（HVAC）栏
- **手动卸瓶器（液体）**：只输入，液体（Plumbing）栏
- **手动装载器**：只输入固体/物品，基地（Base）栏

装瓶器像手压泵一样按需使用：建筑里会准备一份 200kg 的元素源，只有复制人需要时才来操作一次并取走，不会自动连续产瓶。卸瓶器接收复制人送来的瓶装气体/液体，把内容转入元素之心。手动装载器接收复制人送来的固体和物品，直接转移进元素之心。装瓶器是“按需手动取出”，卸瓶器/装载器是“持续接收输入”，两者职责和运行方式不同。

选中后：

- **底部按钮区**：显示当前元素之心名称，点击切换目标
- **配置页**：装瓶器用单选过滤列表选择要提取的元素；卸瓶器和装载器用勾选列表选择要接收的瓶子/固体物品

为防止循环：装瓶器产出的瓶子会带来源标记，卸瓶器不会从同一个元素之心拉取这些瓶子；卸瓶器和装载器也不会直接从所连接的元素之心取物。

### 元素接口（分别建造，全部 1x1；建造菜单中统一名为“元素接口”）
- 元素接口（气体输入）
- 元素接口（气体输出）
- 元素接口（液体输入）
- 元素接口（液体输出）
- 元素接口（电力，单个建筑同时负责输入和输出）

只有元素接口使用独立的占位层，可以无视普通建筑和地形障碍放置；**同一种接口不能重叠**，不同种接口可以叠在同一格。元素之心本身仍按正常建筑规则放置。

所有接口的管道/电力连接点都在建筑自身所在的 1 格内（偏移 `0,0`），不会占用相邻格；建筑外观也以格子中心对齐，不再偏半格。

接口建造材料：

- 气体 / 液体接口：原矿（砂岩、花岗岩、火成岩等）。
- 电力接口：金属矿石（铜矿、铁矿等）或精炼金属。

选中元素接口后，属性面板会显示连接的元素之心、方向和输出物质；底部按钮区显示当前元素之心的名称，点击可在同类元素之心之间循环切换目标。

- **气体/液体输入接口**：不限制元素类型，管道送来什么气体/液体就自动存入元素之心。输入接口没有过滤列表，也不会显示“物质”选项。
- **气体/液体输出接口**：只有一个输出管道口；必须在过滤列表中勾选一种要输出的元素（只能单选），未选择时不会输出。
- **电力接口**：只有元素之心切换按钮；它是一根双向连接，电网富余时存入、缺电时取出。

接口默认自动连接最近的、同世界的元素之心；也可以手动切换。切换目标或物质后立即生效。

每个输入接口都有自己独立的 Storage 缓冲，再由 `PortTransfer` 转入元素之心；因此多个同参数接口不会共享容量或输入额度，例如两个 10kg 水输入接口可以各自输入 10kg。输出接口各自从元素之心的无限存量取用，互不抢占容量，实际流量只受各自管道容量限制。

电力方面，所有电力接口通过元素之心的共享电力池互联，因此 A 接口接入的发电机可以给 B 接口的电网供电，反之亦然。每个接口的电池只是本地缓冲：本地没有用电需求时电量会全部送入共享池；本地有用电需求时只保留约两帧的需求量，不足部分从共享池补回，避免某个接口囤电导致其他接口缺电。

## 使用流程

1. **收集固体**：建造元素之心后，在右侧材料列表里勾选要收集的固体品类；复制人会像往箱子搬东西一样送入元素之心。
2. **配置处理**：建造元素终端机，它会自动连接最近的元素之心。选中终端后，右侧“配置”页会列出这一类操作中已经出现过的材料的合法配方（当前库存或待处理数量也算已出现）；没遇到过的材料会在第一次出现后解锁。点开配方详情，在底部控制栏里设置目标数量/速率。温控终端则在配置页选择一种液体/气体元素、开启恒温并设置目标温度。属性面板状态会显示当前配方类型和连接的元素之心。
3. **切换目标**：选中终端机、元素接口、装瓶器、卸瓶器或装载器后，点击底部按钮区的元素之心名称按钮，在该世界的元素之心之间循环切换；配方面板会立即跟随新目标刷新。
4. **接入气液电**：分别建造气体/液体输入输出接口和电力接口。输入接口无需选择物质，会自动接收管道内容；输出接口在过滤列表中单选一种输出元素。输入接口先写入自己的缓冲，再送入元素之心；输出接口直接从元素之心取用。
5. **手动装瓶/卸瓶/装载**：装瓶器从元素之心取物生成瓶装/元素块；卸瓶器把复制人送来的瓶子转入元素之心；手动装载器把固体/物品转入元素之心。装瓶器和卸瓶器使用来源标记避免同元素之心循环。
6. **查看状态**：元素之心的描述会显示储存质量、内部电力，以及已连接的终端/接口/装瓶器数量；终端的描述会显示“运行中/等待材料/待机/未连接”。

## 建造位置

- **元素之心**：基地（Base）栏
- **气体元素接口**：气体（HVAC）栏
- **液体元素接口**：液体（Plumbing）栏
- **电力元素接口**：电力（Power）栏
- **手动装瓶器（气体）**：气体（HVAC）栏
- **手动装瓶器（液体）**：液体（Plumbing）栏
- **手动卸瓶器（气体）**：气体（HVAC）栏
- **手动卸瓶器（液体）**：液体（Plumbing）栏
- **手动装载器**：基地（Base）栏
- **元素终端机（分解 / 固化 / 液化 / 气化 / 温控）**：站台（Stations）栏

所有建筑都归入 **高级研究（Advanced Research）** 科技，需要先完成该科技才能建造。

## DLC 兼容

- 不依赖任何 DLC：`requiredDlcIds` 和 `forbiddenDlcIds` 均为空。
- 原版、仅启用部分 DLC、以及多个 DLC 混合启用时都可以加载。
- 没有使用 DLC 专属的建筑、元素或动画资源；接口的过滤列表会自动使用当前游戏已加载的元素。

## 构建

环境：macOS + .NET SDK（本机安装在 `~/.dotnet`）。

```bash
./build.sh
```

默认引用本机 Steam 游戏目录：

```
~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/Resources/Data/Managed
```

如果游戏安装在别处，可覆盖：

```bash
dotnet build -c Release /p:ManagedDir="/path/to/Managed"
```

构建后会自动复制到本地 Mod 目录：

```
~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local/ONI-Heart
```

在游戏主菜单的“模组”中启用 **元素之心** 即可。

## 目录

```
src/
  ONIHeartMod.cs        Mod 入口 + 建造菜单注册
  STRINGS.cs            中文文本
  HeartConfig.cs        元素之心建筑配置
  HeartOfElements.cs    无限存储 / 内部电力 / 同元素气液温度合并
  HeartSolidCollector.cs  固体的 StorageFetch 搬运（像箱子一样）
  HeartProcessor.cs     四个配方面板 + 温控处理：分解速率 / 转化数量 / 恒温
  HeartDeconstructor.cs 拆除时保留物质、清空电力
  HeartProcessUI.cs     终端配方面板（食物压制器风格配方列表 + 配方详情 + 温控选择）
  HeartTerminal.cs      终端连接元素之心 / 选择目标 / 温控元素与目标温度
  HeartTerminalConfig.cs 五种 2x2 元素终端机配置
  HeartAnimator.cs      循环播放宝石心动画
  HeartLight.cs         轻微呼吸光 / 颜色漂移
  PortConfigs.cs        五种 1x1 元素接口配置
  HeartPort.cs          接口连接目标、输入/输出元素过滤、底部切换按钮
  HeartFilterSideScreenPatch.cs  让原版过滤侧边栏支持输出接口/装瓶器
  PortTransfer.cs       输入接口独立缓冲 → 元素之心
  PowerTransfer.cs      电力双向同步
  ManualBottleConfig.cs 1x2 装瓶器/卸瓶器/装载器配置
  ManualBottleLoader.cs 手动装瓶/卸瓶/装载逻辑与循环防护

anim/assets/oniheart/   元素之心的 PNG 图集 + build/anim 字节
anim/assets/oniheart_*  元素接口、手动装瓶器与元素终端机的 PNG 图集 + build/anim 字节
tools/generate_heart_anim.py     重新生成宝石心动画
tools/building_art.py            元素接口 / 手动装瓶器 / 元素终端机的实体化绘制代码
tools/generate_building_anims.py 重新生成上述建筑外观并打包 KAnim
```

## 说明

- 元素之心使用自定义 `oniheart_kanim`：折线心形宝石、下半部连续圆弧底（底部两侧轮廓与尖端被磨掉，侧边描边和切缝在下部渐隐）、冠部刻面、多面菱形中心切面、锐利棱线与动态五彩折射，16 帧、6 FPS、循环。中心不是镂空或圆形台面，而是由内菱形和周围四个风筝面组成的真实打磨切面。
- 元素接口是实体化的铸造接口座：方形安装板、六角螺栓、圆形法兰视窗、顶部带阀门的接管、压力表、状态灯和压铸流向箭头；气体/液体/电力分别使用青绿、蓝、琥珀色，进出口通过箭头和指示灯区分。
- 手动装瓶器是 1x2 立式装瓶机：铆接机柜、玻璃填充舱、瓶身与液面、填充臂、压力表、手柄、按钮、通风格栅和侧面检修管。
- 五种元素终端机是 2x2 实体科学仪器：左侧反应罐（水/冰晶/冷凝/沸腾/冷热分区画面）、右侧带金属边框的屏幕、物理键盘、旋钮、手柄、指示灯和对应的顶部装置（双出气管 / 散热器 / 冷凝盘管 / 锅炉烟囱 / 冷热换热器）。
- 所有自定义建筑外观通过 `tools/building_art.py` 绘制，再由 `tools/generate_building_anims.py` 打包；可修改后重新生成 `anim/assets/`。
- 宝石心由 `tools/generate_heart_anim.py` 生成，可修改脚本后重新生成 `anim/assets/oniheart/`。
- 气液接口的输出物质选择使用原版 `FilterSideScreen` 单选列表；元素之心切换统一放在属性面板下方的标准按钮区。
- 同一种液体/气体进入元素之心时会合并为一个堆，温度按质量加权平均计算；温控终端再把这个堆调整到目标温度。
- 温控终端的目标温度会被限制在元素稳定相区内，并与相变点保持 2K 余量，避免把水冻成冰或烧成蒸汽。
- 每个电力接口是一个双向电池连接；所有电力接口共享元素之心的电力池，不再区分输入/输出建筑。
- 元素接口的“无视障碍”通过为每种接口分配独立的 `ObjectLayer` 实现：普通建筑不阻挡接口，同种接口共用一层因此不能叠加。
- 接口和装瓶器只挂 `Filterable`，不挂 `ElementFilter`，避免 `ElementFilter` 作为 `ISecondaryOutput` 多出一个管道口；过滤侧栏通过 Harmony patch 适配。
- 原版 `PlanScreen` 与 Codex 均以建筑 ID 作为唯一键，同一栋建筑无法同时出现在两个建造分类中（会抛出重复键异常并导致无法进入游戏）。
- 按需求已移除“自动吸收散落物品”；固体可通过元素之心的材料列表搬入，或使用手动装载器搬入。
- 若需要完全自定义的下拉菜单，可以在 `HeartFilterSideScreenPatch` 和 `HeartPort` 的输出过滤逻辑上扩展。

---

<a id="english"></a>

# ONI-Heart (English)

An Oxygen Not Included mod that adds a 4x4 building, the **Heart of Elements**, plus gas/liquid input and output interfaces and a bidirectional power interface.

## Features

### Heart of Elements
- 4x4 building; construction material is raw mineral (sandstone, granite, igneous rock, etc.) or metal ore (copper ore, iron ore, etc.).
- Normal building: occupies `ObjectLayer.Building`, requires a foundation, and is blocked by normal buildings and terrain.
- Provides a storage-bin-like material checklist covering all solid item categories such as seeds, clothing, eggs, and food. Gases/liquids are not in the checklist and must enter/leave through element interfaces.
- Has work priority; once materials are checked, duplicants haul the matching solids into the heart like they would into a storage bin.
- Can be submerged in liquid without entering a "flooded" shutdown state.
- Visuals are a low-poly, faceted natural heart-shaped gem: a folded heart silhouette, a continuous rounded lower section, crown/star/diamond facet layers, dark seams and lit edges, slow rainbow refraction, and fine polishing grain. Custom 16-frame KAnim, 6 FPS loop, 512 px frames; the art fills the 4x4 footprint while the build-menu icon stays at normal size.
- Infinite storage for solids, liquids, and gases; each element stacks separately.
- Food does not spoil (`Preserve`).
- Solids do not off-gas/sublimate (`Seal`).
- Eggs do not hatch (hatching is suppressed when stored in normal Storage).
- Stored items do not exchange heat (`Insulate`); the building itself uses `UseStructureTemperature = false`.
- Cannot be melted or destroyed; only the "Deconstruct" command removes it.
- On deconstruction: solids drop directly, gases/liquids are preserved as bottled/canned items, and internal power is cleared.
- Soft light: about 600 Lux of warm-white breathing light with a slowly drifting color; no power use and no heat.
- High decor value: base decor +100, decor radius 10 cells.
- No heat generation, no power consumption, no power loss.

### Element terminals

Adds five 2x2 science buildings under the **Stations** category:

- **Element Terminal (Decompose)**
- **Element Terminal (Solidify)**
- **Element Terminal (Liquefy)**
- **Element Terminal (Vaporize)**
- **Element Terminal (Thermostat)**

When a terminal is selected, a button showing the connected heart's name appears in the bottom button area. Click it to cycle through same-world hearts. A terminal only controls its connected heart's matching operation; the heart itself no longer shows a processing panel.

### Recipe processing

After selecting an **Element Terminal** and connecting a heart, the recipe list appears in the **Config** tab of the right details panel, in a food-press style:

- **Decompose terminal**: only water. Open the recipe detail and drag the slider to adjust the rate (kg/s); rate 0 disables it.
- **Solidify terminal**: solidifies **liquids or gases** into the corresponding solid. Each recipe has its own amount (kg); 0 disables it.
- **Liquefy terminal**: liquefies **gases or solids** into the corresponding liquid. Each recipe has its own amount (kg); 0 disables it.
- **Vaporize terminal**: vaporizes **liquids or solids** into the corresponding gas. Each recipe has its own amount (kg); 0 disables it.

Each recipe has an independent target, and multiple recipes can run simultaneously. Decomposition uses a rate; conversions use an amount.

The panel uses food-press-style recipe rows: element icon + recipe name + current quantity/rate. The row itself has no increment/decrement buttons; clicking a row opens the **Recipe Detail** panel on the left. Decomposition rate and thermostat target temperature use a slider styled like the game's native temperature bar; solidify/liquefy/vaporize keep the original quantity input field and increment/decrement buttons. The vanilla ingredient/effect placeholder areas and recipe-cycle buttons are hidden. The recipe list no longer shows a recipe/heart header row; that information is shown as a status item in the building's properties panel.

Conversion recipes only accept **phase changes of one pure substance**: the output must actually exist and be spawnable, and the source and target must belong to the same substance family. Combustion/oxidation, refining, cracking, smelting, ore processing, polymerization/decomposition, and solutions/mixtures such as salt water, polluted water, milk, or resin are not listed.

Typical recipes that remain include water/ice/steam, the three phases of CO2/O2/H2/CH4, phase changes of chlorine/phosphorus/sulfur/ethanol, and the solid/liquid changes of pure oils, inks, resins and sugar. Animal fat -> biodiesel/CO2, wood/rubber/bitumen -> CO2, crude oil -> petroleum, ore -> molten metal, and plastic -> naphtha are excluded. Only recipes whose actual phase-change temperature is within 10-1000 K are shown; magma, molten metal and other extreme-temperature conversions are excluded.

Recipe visibility is further limited to **input materials that have already appeared**: discovered in the current save, or already delivered to the heart / configured with a pending amount. Materials that have never appeared stay hidden; new outputs unlock after their first production. The decomposition terminal is not affected by this filter.

Processing rules:

- **Decompose water**: processed at the configured rate; 1 kg/s water -> 0.888 kg/s oxygen + 0.112 kg/s hydrogen, 120 W, 1.25 kW heat. Power and heat scale with rate. The decomposition terminal is not affected by the phase-change whitelist.
- **Solidify / Liquefy / Vaporize**: processed at the transition point actually crossed by the source element; the output temperature is placed on the stable side to avoid immediate re-phasing. Released heat is converted into internal power, and absorbed heat is paid for from internal power. Heat is calculated per path segment, using each phase's own specific heat. Conversion throughput is 0.5 kg/s.
- Conversion recipes run toward the target amount and stop when reached; if input is insufficient, the remaining amount is kept until more material arrives.
- All outputs go directly into the heart's internal Storage.
- Decomposition consumes the heart's internal power; if power is insufficient, throughput is reduced to the available power.

### Thermostat terminal

A single **Element Terminal (Thermostat)** can control several liquid or gas elements at the same time. Each element has its own target temperature and its own switch.

- The Config tab lists every controllable liquid/gas element. Click an element to open its thermostat detail, set the target with the slider, and enable it with the separate switch.
- The target is clamped to the element's stable phase range with a 2 K margin from both phase-change points. Water, for example, can only be set to roughly 2-98 °C, so it will not freeze or boil.
- Below the target, the heart's internal power is consumed for heating; above the target, the excess heat is converted 100% into internal power.
- If power is limited, only part of the required heating is applied. Cooling power goes directly into the heart's shared power pool.
- One element can only be controlled by one thermostat terminal per heart. Enabling the same element on another terminal automatically turns it off on the previous terminal to avoid conflicts.

When a liquid/gas element enters the heart, portions of the same element are merged into one stack with a mass-weighted average temperature instead of being stored as two stacks at different temperatures. For example, 20 °C water plus incoming 50 °C water becomes one water stack at the mass-weighted temperature. Without a thermostat, no active temperature control is performed; only the input-temperature merge happens.

### Manual bottler / unbottler / loader

Adds five 1x2 vertical buildings, all made from raw mineral:

- **Manual Bottler (Gas)**: output only, HVAC category
- **Manual Bottler (Liquid)**: output only, Plumbing category
- **Manual Unbottler (Gas)**: input only, HVAC category
- **Manual Unbottler (Liquid)**: input only, Plumbing category
- **Manual Loader**: solid/item input only, Base category

The bottler works like a pitcher pump and is used on demand: the building keeps one 200 kg element source ready, and a duplicant comes to operate it only when needed, taking that portion. It does not continuously produce bottles. The unbottler receives bottled gas/liquid delivered by duplicants and transfers the contents into the heart. The manual loader receives solids and items delivered by duplicants and transfers them directly into the heart. The bottler is "on-demand manual output"; the unbottler/loader are "continuous input receivers". Their roles and operation differ.

When selected:

- **Bottom button area**: shows the connected heart's name; click to switch targets
- **Config tab**: the bottler uses a single-select filter list to choose the element to extract; the unbottler and loader use checklists to choose which bottles/solid items to receive

Loop prevention: bottles produced by a bottler carry a source tag, so an unbottler for the same heart will not pull those bottles back. The unbottler and loader also never take items directly from their connected heart.

### Element interfaces (built separately, all 1x1; all named "Element Interface" in the build menu)
- Element Interface (Gas Input)
- Element Interface (Gas Output)
- Element Interface (Liquid Input)
- Element Interface (Liquid Output)
- Element Interface (Power; one building handles both input and output)

Only element interfaces use separate placeholder layers and can be placed ignoring normal buildings and terrain. **Two interfaces of the same kind cannot overlap**, but different kinds can share a cell. The heart itself still follows normal building placement rules.

All interface pipe/power connection points are inside the building's own 1 cell (offset `0,0`) and do not occupy adjacent cells. Art is aligned to the cell center.

Interface construction materials:

- Gas/liquid interfaces: raw mineral (sandstone, granite, igneous rock, etc.).
- Power interface: metal ore (copper ore, iron ore, etc.) or refined metal.

When an element interface is selected, the properties panel shows the connected heart, direction, and output element. The bottom button area shows the current heart's name; click to cycle same-kind hearts.

- **Gas/liquid input interfaces**: do not restrict element type; whatever gas/liquid the pipe delivers is stored into the heart. Input interfaces have no filter list and no "substance" option.
- **Gas/liquid output interfaces**: have only one output pipe port; you must select exactly one output element in the filter list (single-select). Nothing is output when no element is selected.
- **Power interface**: only has the heart switch button; it is a bidirectional connection that stores surplus grid power and supplies power when the grid is short.

Interfaces automatically connect to the nearest same-world heart; you can also switch manually. Changes take effect immediately.

Each input interface has its own Storage buffer, and `PortTransfer` moves items into the heart, so multiple interfaces with the same settings do not share capacity or input quota (for example, two 10 kg water input interfaces can each input 10 kg). Output interfaces each draw from the heart's infinite stock without competing for capacity; actual flow is limited only by their pipe capacity.

For power, all power interfaces are connected through the heart's shared power pool, so a generator connected to interface A can power the grid at interface B and vice versa. Each interface battery is only a local buffer: with no local demand, all power is sent to the shared pool; with local demand, only about two frames of demand is kept locally and the rest is pulled from the shared pool, preventing one interface from hoarding power.

## Usage flow

1. **Collect solids**: after building the Heart of Elements, check the solid categories to collect in the material list; duplicants haul them in like a storage bin.
2. **Configure processing**: build an Element Terminal; it automatically connects to the nearest heart. Select the terminal, and the Config tab lists valid recipes whose input materials have already appeared (current inventory or pending amounts count as appeared); materials not yet encountered unlock after their first appearance. Open a recipe detail and set the target amount/rate in the bottom control row. For a thermostat, choose one liquid/gas element, enable temperature control, and set the target temperature. The properties panel shows the current recipe type and connected heart.
3. **Switch targets**: select a terminal, element interface, bottler, unbottler, or loader, then click the heart-name button in the bottom button area to cycle through same-world hearts. The recipe panel refreshes immediately.
4. **Connect gas/liquid/power**: build gas/liquid input and output interfaces and power interfaces. Input interfaces need no substance selection and automatically accept pipe contents; output interfaces use a single-select filter list. Input interfaces first write to their own buffer and then send to the heart; output interfaces draw directly from the heart.
5. **Manual bottling/unbottling/loading**: the bottler takes material from the heart and produces bottles/element chunks on demand; the unbottler transfers delivered bottles into the heart; the manual loader transfers solids/items into the heart. Bottler/unbottler use source tags to prevent same-heart loops.
6. **Check status**: the heart's description shows stored mass, internal power, and connected terminal/interface/bottler counts; the terminal description shows "Running / Waiting for material / Idle / Disconnected".

## Build locations

- **Heart of Elements**: Base category
- **Gas Element Interface**: HVAC category
- **Liquid Element Interface**: Plumbing category
- **Power Element Interface**: Power category
- **Manual Bottler (Gas)**: HVAC category
- **Manual Bottler (Liquid)**: Plumbing category
- **Manual Unbottler (Gas)**: HVAC category
- **Manual Unbottler (Liquid)**: Plumbing category
- **Manual Loader**: Base category
- **Element Terminals (Decompose / Solidify / Liquefy / Vaporize / Thermostat)**: Stations category

All buildings are placed under the **Advanced Research** tech and require that research to be completed before they can be built.

## DLC compatibility

- No DLC dependency: `requiredDlcIds` and `forbiddenDlcIds` are empty.
- Loads with the base game, with only some DLCs enabled, or with multiple DLCs mixed.
- No DLC-specific buildings, elements, or animation assets are used; interface filter lists automatically use the elements loaded by the current game.

## Building

Environment: macOS + .NET SDK (installed at `~/.dotnet` on this machine).

```bash
./build.sh
```

By default it references the local Steam game directory:

```
~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/Resources/Data/Managed
```

If the game is installed elsewhere, override it:

```bash
dotnet build -c Release /p:ManagedDir="/path/to/Managed"
```

The build automatically copies to the local mod directory:

```
~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local/ONI-Heart
```

Enable **元素之心** in the game's main-menu **Mods** screen.

## Layout

```
src/
  ONIHeartMod.cs        Mod entry + build-menu registration
  STRINGS.cs            Chinese text
  HeartConfig.cs        Heart of Elements building config
  HeartOfElements.cs    Infinite storage / internal power / same-element gas-liquid temperature merge
  HeartSolidCollector.cs  Solid StorageFetch hauling (like a storage bin)
  HeartProcessor.cs     Four recipe panels + thermostat processing: decomposition rate / conversion amount / constant temperature
  HeartDeconstructor.cs Preserves contents and clears power on deconstruction
  HeartProcessUI.cs     Terminal recipe panel (food-press-style list + recipe detail + thermostat selection)
  HeartTerminal.cs      Terminal connection to heart / target selection / thermostat element and target
  HeartTerminalConfig.cs Five 2x2 element terminal configs
  HeartAnimator.cs      Loops the gem-heart animation
  HeartLight.cs         Soft breathing light / color drift
  PortConfigs.cs        Five 1x1 element interface configs
  HeartPort.cs          Interface target, input/output element filtering, bottom switch
  HeartFilterSideScreenPatch.cs  Adapts the vanilla filter side screen for output interfaces/bottlers
  PortTransfer.cs       Input interface buffer -> heart
  PowerTransfer.cs      Bidirectional power synchronization
  ManualBottleConfig.cs 1x2 bottler/unbottler/loader configs
  ManualBottleLoader.cs Manual bottling/unbottling/loading logic and loop protection

anim/assets/oniheart/   Heart PNG atlas + build/anim bytes
anim/assets/oniheart_*  Element interface, manual bottler, and terminal PNG atlases + build/anim bytes
tools/generate_heart_anim.py     Regenerates the gem-heart animation
tools/building_art.py            Painted art for interfaces / manual bottler / terminals
tools/generate_building_anims.py Regenerates the above building art and packs KAnim
```

## Notes

- The heart uses a custom `oniheart_kanim`: a folded-heart gem with a continuous rounded lower section (the lower side outline and tip are ground away, with side outline and seams fading out on the lower half), crown facets, a faceted diamond center, sharp edges, and animated rainbow refraction. 16 frames, 6 FPS, loop. The center is not a hole or round table; it is a real polished facet made of an inner diamond and four surrounding kite facets.
- Element interfaces are solid cast interface bases: square mounting plate, hex bolts, round flange window, top valve connection, pressure gauge, status light, and die-cast flow arrows; gas/liquid/power use teal, blue, and amber respectively, with arrows and indicator lights distinguishing input/output.
- The manual bottler is a 1x2 vertical bottling machine: riveted cabinet, glass fill chamber, bottle and liquid level, fill arm, pressure gauge, handle, buttons, ventilation grille, and side service pipe.
- The five element terminals are 2x2 solid science instruments: left reaction chamber (water/ice crystal/condensation/boiling/hot-cold split visuals), right metal-framed screen, physical keyboard, knobs, handles, indicator lights, and matching top hardware (dual exhaust pipes / radiator / condenser coil / boiler chimney / hot-cold heat exchanger).
- All custom building art is painted by `tools/building_art.py` and packed by `tools/generate_building_anims.py`; edit and regenerate `anim/assets/`.
- The gem heart is generated by `tools/generate_heart_anim.py`; edit the script and regenerate `anim/assets/oniheart/`.
- Gas/liquid interface output selection uses the vanilla `FilterSideScreen` single-select list; heart switching is unified in the standard button area below the properties panel.
- The same liquid/gas element is merged into one stack when it enters the heart, with a mass-weighted average temperature; a thermostat then drives that stack to its target temperature.
- Thermostat targets are clamped inside the element's stable phase range with a 2 K margin from phase-change points, so water cannot be frozen or boiled by the thermostat.
- Each power interface is a bidirectional battery connection; all power interfaces share the heart's power pool and no longer distinguish input/output buildings.
- Interface obstacle-ignoring is implemented by assigning each interface kind its own `ObjectLayer`: normal buildings do not block interfaces, and same-kind interfaces share a layer so they cannot stack.
- Interfaces and bottlers only attach `Filterable`, not `ElementFilter`, avoiding the extra pipe port that `ElementFilter` creates as `ISecondaryOutput`; the filter side screen is adapted with a Harmony patch.
- The vanilla `PlanScreen` and Codex use the building ID as a unique key; one building cannot appear in two build categories (duplicate keys throw and prevent the build menu from loading).
- Automatic absorption of loose items has been removed as requested; solids can be hauled in through the heart's material list or loaded with the manual loader.
- If a fully custom dropdown is needed, extend `HeartFilterSideScreenPatch` and the output filtering logic in `HeartPort`.
