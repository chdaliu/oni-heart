using System;

namespace ONIHeart
{
    /// <summary>
    /// All building and UI text. Chinese is kept as the default so that even without extra translation files,
    /// Chinese clients see the correct text.
    /// </summary>
    public static class STRINGS
    {
        public static class BUILDINGS
        {
            public static class PREFABS
            {
                public static class ONIHEART
                {
                    public static LocString NAME = "元素之心";
                    public static LocString DESC =
                        "一个来自元素裂隙的奇异建筑。它能够无限制地收容固体、液体和气体，并将它们分别保存。\n\n" +
                        "储存在其中的食物不会腐败，固体会被密封，蛋类不会孵化。内部电力同样可以无限储存且不会损耗。\n\n" +
                        "使用流程：直接用材料过滤列表让复制人搬入固体；用元素接口输入/输出气体、液体和电力；用元素终端机选择这个元素之心并配置分解/固化/液化/气化配方；用元素终端机底部按钮区的元素之心名称按钮切换目标。手动装瓶器用于瓶装气体/液体的装入与提取。\n\n" +
                        "元素之心不会被融化或破坏，只能通过拆除指令移除。拆除时固体直接掉落，气体和液体以瓶装形式保留，内部电力清空。";
                    public static LocString EFFECT =
                        "无限容量存储\n食物保鲜\n固体密封\n蛋类休眠\n轻微照明\n大量装饰\n终端配置分解/相变\n接口输入输出气液电力\n无热量产生/交换\n电力无损耗";
                    public static LocString FLAVOR = "\"它只是安静地跳动着，仿佛整个世界都可以被装进去。\"";
                }

                public static class ONIHEARTGASIN
                {
                    public static LocString NAME = "元素接口";
                    public static LocString DESC =
                        "将气体管道中的气体送入所选元素之心。\n\n" +
                        "输入接口不限制元素类型，管道送来什么气体就自动存入元素之心。\n" +
                        "底部按钮区显示当前连接的元素之心，点击可切换。";
                    public static LocString EFFECT = "气体输入\n自动接收所有气体\n无限吞吐\n无热量交换";
                    public static LocString FLAVOR = "\"吸气，然后永远不呼气。\"";
                }

                public static class ONIHEARTGASOUT
                {
                    public static LocString NAME = "元素接口";
                    public static LocString DESC =
                        "从所选元素之心向气体管道输出气体。\n\n" +
                        "在侧边过滤列表中勾选一种要输出的气体（只能单选）。\n" +
                        "底部按钮区显示当前连接的元素之心，点击可切换。";
                    public static LocString EFFECT = "气体输出\n单一元素输出\n无限供应\n无热量交换";
                    public static LocString FLAVOR = "\"它记得每一种气体的味道。\"";
                }

                public static class ONIHEARTLIQUIDIN
                {
                    public static LocString NAME = "元素接口";
                    public static LocString DESC =
                        "将液体管道中的液体送入所选元素之心。\n\n" +
                        "输入接口不限制元素类型，管道送来什么液体就自动存入元素之心。\n" +
                        "底部按钮区显示当前连接的元素之心，点击可切换。";
                    public static LocString EFFECT = "液体输入\n自动接收所有液体\n无限吞吐\n无热量交换";
                    public static LocString FLAVOR = "\"连海洋也装得下。\"";
                }

                public static class ONIHEARTLIQUIDOUT
                {
                    public static LocString NAME = "元素接口";
                    public static LocString DESC =
                        "从所选元素之心向液体管道输出液体。\n\n" +
                        "在侧边过滤列表中勾选一种要输出的液体（只能单选）。\n" +
                        "底部按钮区显示当前连接的元素之心，点击可切换。";
                    public static LocString EFFECT = "液体输出\n单一元素输出\n无限供应\n无热量交换";
                    public static LocString FLAVOR = "\"细水长流，永无止境。\"";
                }

                public static class ONIHEARTMANUALBOTTLELIQUID
                {
                    public static LocString NAME = "手动装瓶器（液体）";
                    public static LocString DESC =
                        "1x2 立式的手动液体装瓶建筑。\n\n" +
                        "从所选元素之心取出指定液体，生成瓶装物品放在建筑旁。\n" +
                        "只负责输出，不会把瓶子再送回元素之心。";
                    public static LocString EFFECT = "手动提取液体\n只输出\n1x2 立式\n无电力损耗";
                    public static LocString FLAVOR = "\"把元素装进瓶子，然后带走。\"";
                }

                public static class ONIHEARTMANUALBOTTLEGAS
                {
                    public static LocString NAME = "手动装瓶器（气体）";
                    public static LocString DESC =
                        "1x2 立式的手动气体装瓶建筑。\n\n" +
                        "从所选元素之心取出指定气体，生成罐装物品放在建筑旁。\n" +
                        "只负责输出，不会把罐子再送回元素之心。";
                    public static LocString EFFECT = "手动提取气体\n只输出\n1x2 立式\n无电力损耗";
                    public static LocString FLAVOR = "\"把元素装进罐子，然后带走。\"";
                }

                public static class ONIHEARTMANUALUNBOTTLERLIQUID
                {
                    public static LocString NAME = "手动卸瓶器（液体）";
                    public static LocString DESC =
                        "1x2 立式的手动液体卸瓶建筑。\n\n" +
                        "复制人把装有液体的瓶子送进建筑，建筑把瓶内液体转入所选元素之心。\n" +
                        "只负责输入，不会从元素之心取物。勾选列表决定接收哪些瓶子。";
                    public static LocString EFFECT = "手动装入液体\n只输入\n1x2 立式\n无电力损耗";
                    public static LocString FLAVOR = "\"把瓶子倒空，让心记住它。\"";
                }

                public static class ONIHEARTMANUALUNBOTTLERGAS
                {
                    public static LocString NAME = "手动卸瓶器（气体）";
                    public static LocString DESC =
                        "1x2 立式的手动气体卸瓶建筑。\n\n" +
                        "复制人把装有气体的罐子送进建筑，建筑把瓶内气体转入所选元素之心。\n" +
                        "只负责输入，不会从元素之心取物。勾选列表决定接收哪些罐子。";
                    public static LocString EFFECT = "手动装入气体\n只输入\n1x2 立式\n无电力损耗";
                    public static LocString FLAVOR = "\"把罐子倒空，让心记住它。\"";
                }

                public static class ONIHEARTMANUALLOADER
                {
                    public static LocString NAME = "手动装载器";
                    public static LocString DESC =
                        "1x2 立式的手动固体/物品装载建筑。\n\n" +
                        "复制人把固体或物品送进建筑，建筑再把它们转入所选元素之心。\n" +
                        "只负责输入，不会从元素之心取物。勾选列表决定接收哪些物品。";
                    public static LocString EFFECT = "手动装载固体/物品\n只输入\n1x2 立式\n无电力损耗";
                    public static LocString FLAVOR = "\"把东西搬进去，心会收好。\"";
                }

                public static class ONIHEARTPOWER
                {
                    public static LocString NAME = "元素接口";
                    public static LocString DESC =
                        "元素之心与电网之间的双向电力连接。\n\n" +
                        "所有电力接口共享元素之心的同一个电力池：一个接口接入的发电机可以给另一个接口的电网供电。\n\n" +
                        "电网有富余电力时存入元素之心；电网需要电力时从元素之心取出。\n\n" +
                        "底部按钮区显示当前连接的元素之心，点击可切换。";
                    public static LocString EFFECT = "双向电力连接\n无损耗\n无热量产生";
                    public static LocString FLAVOR = "\"心跳即为电流。\"";
                }

                public static class ONIHEARTTERMINALDECOMPOSE
                {
                    public static LocString NAME = "元素终端机（分解）";
                    public static LocString DESC =
                        "2x2 科学仪器。连接一个元素之心后，控制其中的水分解配方与速率。\n\n" +
                        "速率 0 表示停止分解。";
                    public static LocString EFFECT = "控制元素之心的分解\n选择连接的元素之心\n2x2 科学仪器";
                    public static LocString FLAVOR = "\"把水拆开，看看里面藏着什么。\"";
                }

                public static class ONIHEARTTERMINALSOLIDIFY
                {
                    public static LocString NAME = "元素终端机（固化）";
                    public static LocString DESC =
                        "2x2 科学仪器。连接一个元素之心后，分别设置其中各种液体的固化数量。\n\n" +
                        "数量 0 表示不工作。";
                    public static LocString EFFECT = "控制元素之心的固化\n选择连接的元素之心\n2x2 科学仪器";
                    public static LocString FLAVOR = "\"让流动的东西安静下来。\"";
                }

                public static class ONIHEARTTERMINALLIQUEFY
                {
                    public static LocString NAME = "元素终端机（液化）";
                    public static LocString DESC =
                        "2x2 科学仪器。连接一个元素之心后，分别设置其中各种气体的液化数量。\n\n" +
                        "数量 0 表示不工作。";
                    public static LocString EFFECT = "控制元素之心的液化\n选择连接的元素之心\n2x2 科学仪器";
                    public static LocString FLAVOR = "\"收紧，再收紧。\"";
                }

                public static class ONIHEARTTERMINALVAPORIZE
                {
                    public static LocString NAME = "元素终端机（气化）";
                    public static LocString DESC =
                        "2x2 科学仪器。连接一个元素之心后，分别设置其中各种液体的气化数量。\n\n" +
                        "数量 0 表示不工作。";
                    public static LocString EFFECT = "控制元素之心的气化\n选择连接的元素之心\n2x2 科学仪器";
                    public static LocString FLAVOR = "\"让沉重的东西飞起来。\"";
                }
            }
        }

        public static class UI
        {
            public static class ONIHEART
            {
                public static LocString PORT_TITLE = "元素接口";
                public static LocString PORT_TARGET = "{0}";
                public static LocString PORT_DIRECTION = "连接方向：{0}";
                public static LocString PORT_DIRECTION_INPUT = "输入";
                public static LocString PORT_DIRECTION_OUTPUT = "输出";
                public static LocString PORT_DIRECTION_BIDIRECTIONAL = "双向";
                public static LocString PORT_DIRECTION_TOOLTIP = "输入接口把管道内容送入元素之心；输出接口从元素之心抽取内容。";
                public static LocString PORT_ELEMENT = "输出物质：{0}";
                public static LocString PORT_INPUT_AUTO = "自动接收输入的气体/液体";
                public static LocString PORT_INPUT_AUTO_TOOLTIP = "输入接口不限制元素类型，管道送来什么就存入元素之心。";
                public static LocString TERMINAL_TITLE = "元素终端机";
                public static LocString TERMINAL_STATUS = "{0}  {1}";
                public static LocString TERMINAL_STATUS_TOOLTIP = "终端机只控制所连接元素之心的这一类操作。";
                public static LocString TERMINAL_STATE = "状态：{0}";
                public static LocString TERMINAL_STATE_RUNNING = "运行中";
                public static LocString TERMINAL_STATE_WAITING = "等待材料";
                public static LocString TERMINAL_STATE_IDLE = "待机";
                public static LocString TERMINAL_STATE_DISCONNECTED = "未连接元素之心";
                public static LocString TERMINAL_STATE_TOOLTIP =
                    "运行中表示配方正在处理；等待材料表示已设置了配方，但元素之心里没有对应输入；待机表示已连接但所有配方数量/速率都是 0。";
                public static LocString TERMINAL_NO_HEART = "未连接元素之心";
                public static LocString STOCK_AMOUNT = "当前库存：{0}";
                public static LocString CONNECTED_DEVICES = "已连接：{0} 个终端，{1} 个接口，{2} 个装卸建筑";
                public static LocString CONNECTED_DEVICES_TOOLTIP =
                    "终端机负责配置分解/固化/液化/气化；接口负责气体、液体和电力输入输出；装瓶/卸瓶/装载器负责手动装卸。";
                public static LocString MANUAL_TARGET = "目标：{0}";
                public static LocString TARGET = "元素之心：{0}";
                public static LocString TARGET_TOOLTIP = "点击切换连接的元素之心";
                public static LocString NO_TARGET = "未连接";
                public static LocString SUBSTANCE = "物质：{0}";
                public static LocString SUBSTANCE_TOOLTIP = "勾选要输出的物质（只能单选）";
                public static LocString NO_SUBSTANCE = "未选择";
                public static LocString POWER_STORED = "储存电力：{0}";
                public static LocString POWER_STORED_TOOLTIP = "元素之心内部储存的电力，不会随时间损耗；所有电力接口共享这个电力池。";
                public static LocString MANUAL_BOTTLE_TITLE = "手动装瓶器";
                public static LocString MANUAL_MODE = "模式：{0}";
                public static LocString MANUAL_MODE_OFF = "关闭";
                public static LocString MANUAL_MODE_LOAD = "装入";
                public static LocString MANUAL_MODE_EXTRACT = "提取";
                public static LocString MANUAL_MODE_TOOLTIP = "点击切换：关闭 / 装入 / 提取";
                public static LocString MANUAL_PHASE_GAS = "气体";
                public static LocString MANUAL_PHASE_LIQUID = "液体";
                public static LocString MANUAL_PHASE_SOLID = "固体";
                public static LocString MANUAL_OUTPUT_STATUS = "输出：{0}";
                public static LocString MANUAL_OUTPUT_TOOLTIP = "在过滤列表中勾选一种要提取的物质（只能单选）。";
                public static LocString MANUAL_INPUT_STATUS = "输入：{0}";
                public static LocString MANUAL_UNBOTTLE_TOOLTIP = "复制人会把勾选列表中的瓶装物送进建筑，再转入元素之心。";
                public static LocString MANUAL_LOADER_TOOLTIP = "复制人会把勾选列表中的固体/物品送进建筑，再转入元素之心。";
                public static LocString MANUAL_ELEMENT_TOOLTIP = "点击切换装瓶/提取的物质";
                public static LocString MANUAL_STATUS = "模式：{0}  类型：{1}  物质：{2}";
                public static LocString MANUAL_STATUS_TOOLTIP = "装入需要复制人送来瓶装气体/液体；提取会从元素之心生成瓶装物品。";

                public static LocString PROCESS_OFF = "关闭";
                public static LocString MATERIAL = "材料：{0}";
                public static LocString NO_MATERIAL = "无";
                public static LocString PROCESS_STATUS_OFF = "所有配方均已关闭";
                public static LocString PROCESS_STATUS_TOOLTIP =
                    "分解水：按设定速率处理，1kg/s 水 → 0.888kg/s 氧气 + 0.112kg/s 氢气，120W，1.25kW 热量。\n" +
                    "固化/液化/气化：按元素相变温度处理，输出温度落在稳定一侧，吸放热由环境承担。\n" +
                    "四个配方面板相互独立，可以同时运行；所有产物自动存入元素之心内部。";

                public static LocString MODULE_DECOMPOSE = "分解";
                public static LocString MODULE_SOLIDIFY = "固化";
                public static LocString MODULE_LIQUEFY = "液化";
                public static LocString MODULE_VAPORIZE = "气化";

                public static LocString PANEL_DECOMPOSE = "分解配方";
                public static LocString PANEL_SOLIDIFY = "固化配方";
                public static LocString PANEL_LIQUEFY = "液化配方";
                public static LocString PANEL_VAPORIZE = "气化配方";
                public static LocString RECIPE_DECOMPOSE = "{0} → 氧气 + 氢气";
                public static LocString RECIPE_CONVERSION = "{0} → {1}";
                public static LocString NO_RECIPES = "暂无可处理的材料";
                public static LocString RECIPE_DETAIL_TITLE = "配方详情";
                public static LocString RECIPE_ROW_TOOLTIP = "点击打开配方详情，调整数量/速率";
                public static LocString DECOMPOSE_RATE_TOOLTIP = "在此输入或调整分解速率（kg/s）；0 表示不工作";
                public static LocString CONVERSION_AMOUNT_TOOLTIP = "在此输入或调整要生成的数量（kg）；0 表示不工作";
                public static LocString DECOMPOSE_STATUS = "分解：{0} kg/s";
                public static LocString CONVERSION_STATUS = "{0}：{1} 个配方进行中";
                public static LocString MASS_STORED = "储存物质：{0}";
                public static LocString MASS_STORED_TOOLTIP = "元素之心内部储存的固体、液体与气体总质量。";
                public static LocString HEART_HELP = "配置：使用元素终端机";
                public static LocString HEART_HELP_TOOLTIP =
                    "选中元素终端机后，在“配置”页设置分解/固化/液化/气化配方；点击终端底部按钮区的元素之心名称按钮切换目标。气体、液体和电力由元素接口输入输出。";
            }
        }
    }
}
