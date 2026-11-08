[中文](README.md) | [English](README.en.md)

# 元素之心 (ONI-Heart)

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

### Debug: preload all elements

To make recipe testing easy, the heart preloads 1000 kg of every enabled element into its internal Storage when it first enters simulation, including **gases, liquids, and solids**, and selects discovered solid resources in the hauling list. This lets you test decomposition, solidification, liquefaction, and vaporization without connecting gas/liquid pipes or hauling solids manually.

- Debug seeding is tracked by `HeartDebugSeeder.SeedVersion`; increment `SeedVersion` and rebuild to seed again.
- Normal consumption is not refilled automatically. To refill, increment `SeedVersion` in `src/HeartDebugSeeder.cs` and rebuild.
- To disable: set the `Enabled` constant in `src/HeartDebugSeeder.cs` to `false` and rebuild.
- Debug seeding only affects the Heart of Elements and does not change normal building logic.

### Element terminals

Adds four 2x2 science buildings under the **Stations** category:

- **Element Terminal (Decompose)**
- **Element Terminal (Solidify)**
- **Element Terminal (Liquefy)**
- **Element Terminal (Vaporize)**

When a terminal is selected, a button showing the connected heart's name appears in the bottom button area. Click it to cycle through same-world hearts. A terminal only controls its connected heart's matching operation; the heart itself no longer shows a processing panel.

### Recipe processing

After selecting an **Element Terminal** and connecting a heart, the recipe list appears in the **Config** tab of the right details panel, in a food-press style:

- **Decompose terminal**: only water. Open the recipe detail and adjust the rate (kg/s); rate 0 disables it.
- **Solidify terminal**: solidifies **liquids or gases** into the corresponding solid. Each recipe has its own amount (kg); 0 disables it.
- **Liquefy terminal**: liquefies **gases or solids** into the corresponding liquid. Each recipe has its own amount (kg); 0 disables it.
- **Vaporize terminal**: vaporizes **liquids or solids** into the corresponding gas. Each recipe has its own amount (kg); 0 disables it.

Each recipe has an independent target, and multiple recipes can run simultaneously. Decomposition uses a rate; conversions use an amount.

The panel uses food-press-style recipe rows: element icon + recipe name + current quantity/rate. The row itself has no increment/decrement buttons; clicking a row opens the **Recipe Detail** panel on the left, with the same bottom control row as the food press for direct input or adjustment. The vanilla ingredient/effect placeholder areas are hidden, leaving only the recipe name, current stock, and the amount control row. The recipe list no longer shows a recipe/heart header row; that information is shown as a status item in the building's properties panel.

Only recipes whose output actually exists and is spawnable are shown. Elements without a spawnable output item do not appear. Only elements within the normal temperature range (about 10-1000 K) are kept; extreme elements such as gaseous rock, magma, molten metal, and solid hydrogen are excluded.

Processing rules:

- **Decompose water**: processed at the configured rate; 1 kg/s water -> 0.888 kg/s oxygen + 0.112 kg/s hydrogen, 120 W, 1.25 kW heat. Power and heat scale with rate.
- **Solidify / Liquefy / Vaporize**: processed at the element's own phase-change temperature; the output temperature is placed on the stable side to avoid immediate re-phasing. Heat is absorbed/released by changing the surrounding environment cell temperature. Conversion throughput is 0.5 kg/s.
- Conversion recipes run toward the target amount and stop when reached; if input is insufficient, the remaining amount is kept until more material arrives.
- All outputs go directly into the heart's internal Storage.
- Decomposition consumes the heart's internal power; if power is insufficient, throughput is reduced to the available power.

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
2. **Configure processing**: build an Element Terminal; it automatically connects to the nearest heart. Select the terminal, and the Config tab lists all valid recipes for that operation (regardless of current heart inventory). Open a recipe detail and set the target amount/rate in the bottom control row. The properties panel shows the current recipe type and connected heart.
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
- **Element Terminals (Decompose / Solidify / Liquefy / Vaporize)**: Stations category

None of the buildings are added to the tech tree. **No research required; available from the start.**

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
  HeartOfElements.cs    Infinite storage / internal power
  HeartSolidCollector.cs  Solid StorageFetch hauling (like a storage bin)
  HeartProcessor.cs     Four recipe panels: decomposition rate / conversion amount
  HeartDeconstructor.cs Preserves contents and clears power on deconstruction
  HeartProcessUI.cs     Terminal recipe panel (food-press-style list + recipe detail)
  HeartTerminal.cs      Terminal connection to heart / target selection
  HeartTerminalConfig.cs Four 2x2 element terminal configs
  HeartDebugSeeder.cs   Debug: preload all elements on first simulation
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
- The four element terminals are 2x2 solid science instruments: left reaction chamber (water/ice crystal/condensation/boiling visuals), right metal-framed screen, physical keyboard, knobs, handles, indicator lights, and matching top hardware (dual exhaust pipes / radiator / condenser coil / boiler chimney).
- All custom building art is painted by `tools/building_art.py` and packed by `tools/generate_building_anims.py`; edit and regenerate `anim/assets/`.
- The gem heart is generated by `tools/generate_heart_anim.py`; edit the script and regenerate `anim/assets/oniheart/`.
- Gas/liquid interface output selection uses the vanilla `FilterSideScreen` single-select list; heart switching is unified in the standard button area below the properties panel.
- Each power interface is a bidirectional battery connection; all power interfaces share the heart's power pool and no longer distinguish input/output buildings.
- Interface obstacle-ignoring is implemented by assigning each interface kind its own `ObjectLayer`: normal buildings do not block interfaces, and same-kind interfaces share a layer so they cannot stack.
- Interfaces and bottlers only attach `Filterable`, not `ElementFilter`, avoiding the extra pipe port that `ElementFilter` creates as `ISecondaryOutput`; the filter side screen is adapted with a Harmony patch.
- The vanilla `PlanScreen` and Codex use the building ID as a unique key; one building cannot appear in two build categories (duplicate keys throw and prevent the build menu from loading).
- Automatic absorption of loose items has been removed as requested; solids can be hauled in through the heart's material list or loaded with the manual loader.
- If a fully custom dropdown is needed, extend `HeartFilterSideScreenPatch` and the output filtering logic in `HeartPort`.
