using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// 元素之心拆除时：
    /// - 固体直接掉在建筑位置；
    /// - 气体/液体保持为瓶装/罐装物品掉出（Storage.Drop 不会 vent/dump）；
    /// - 内部电力清空。
    /// </summary>
    public sealed class HeartDeconstructor : KMonoBehaviour
    {
        private HeartOfElements heart;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            heart = GetComponent<HeartOfElements>();
            Subscribe(-702296337, OnDeconstructComplete);
        }

        private void OnDeconstructComplete(object data)
        {
            if (heart == null)
            {
                heart = GetComponent<HeartOfElements>();
            }

            if (heart != null)
            {
                heart.ClearPower();
            }

            Storage storage = GetComponent<Storage>();
            if (storage == null)
            {
                return;
            }

            // 不 vent/dump：气体和液体作为瓶装/罐装物品掉出，固体作为普通物品掉出。
            storage.DropAll(vent_gas: false, dump_liquid: false);
        }
    }
}
