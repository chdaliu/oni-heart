using HarmonyLib;
using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// Make the vanilla FilterSideScreen accept our output interfaces and manual bottler.
    /// These buildings only have Filterable, not ElementFilter, avoiding an extra pipe port
    /// caused by ElementFilter implementing ISecondaryOutput.
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
    /// The vanilla FilterSideScreen shows two columns: "filtered output" and "everything else".
    /// Our interfaces and bottler have only one output, so keep only the element selection list and current selection.
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
