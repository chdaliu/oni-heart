using HarmonyLib;
using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// 让原版 FilterSideScreen 接受我们自己的输出接口和手动装瓶器。
    /// 这些建筑只挂 Filterable，不挂 ElementFilter，避免 ElementFilter
    /// 作为 ISecondaryOutput 在输出接口上多出一个管道口。
    /// </summary>
    [HarmonyPatch(typeof(FilterSideScreen), nameof(FilterSideScreen.IsValidForTarget))]
    public static class HeartFilterSideScreenPatch
    {
        public static bool Prefix(FilterSideScreen __instance, GameObject target, ref bool __result)
        {
            if (target == null || __instance == null || __instance.isLogicFilter)
            {
                return true;
            }

            HeartPort port = target.GetComponent<HeartPort>();
            bool isOurFilterTarget =
                (port != null && port.IsOutputPort) ||
                target.GetComponent<ManualBottleOutput>() != null;

            if (!isOurFilterTarget)
            {
                return true;
            }

            __result = target.GetComponent<Filterable>() != null;
            return false;
        }
    }

    /// <summary>
    /// 原版 FilterSideScreen 会显示“过滤输出 / 其他全部”两栏。
    /// 我们的接口和装瓶器只有一个输出，只保留元素选择列表和当前选择。
    /// </summary>
    [HarmonyPatch(typeof(FilterSideScreen), nameof(FilterSideScreen.SetTarget))]
    public static class HeartFilterSideScreenSetTargetPatch
    {
        public static void Postfix(FilterSideScreen __instance, GameObject target)
        {
            if (__instance == null)
            {
                return;
            }

            HeartPort port = target != null ? target.GetComponent<HeartPort>() : null;
            bool isOurs = target != null &&
                ((port != null && port.IsOutputPort) ||
                 target.GetComponent<ManualBottleOutput>() != null);

            Transform outputHeader = __instance.transform.Find("OutputElementHeader");
            if (outputHeader != null)
            {
                outputHeader.gameObject.SetActive(!isOurs);
            }

            Transform everythingElse = __instance.transform.Find("EverthingElse");
            if (everythingElse != null)
            {
                everythingElse.gameObject.SetActive(!isOurs);
            }
        }
    }
}
