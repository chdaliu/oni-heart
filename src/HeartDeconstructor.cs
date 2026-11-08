using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// When the heart is deconstructed:
    /// - Solids drop directly at the building position.
    /// - Gases/liquids drop as bottled/canned items (Storage.Drop does not vent/dump).
    /// - Internal power is cleared.
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

            // No vent/dump: gases and liquids drop as bottled/canned items; solids drop as normal items.
            storage.DropAll(vent_gas: false, dump_liquid: false);
        }
    }
}
