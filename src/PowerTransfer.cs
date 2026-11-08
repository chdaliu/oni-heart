namespace ONIHeart
{
    /// <summary>
    /// Bidirectional synchronization between a single power interface and the heart's internal power.
    /// The battery on the interface is only a buffer for the local grid; actual energy is shared through the heart,
    /// so a generator connected to one interface can power the grid at another interface.
    ///
    /// The local buffer follows local grid demand:
    /// - When the local grid has no demand, all battery power is sent to the heart (generator side).
    /// - When the local grid has demand, the battery keeps only about two frames of demand and pulls the rest from the heart.
    /// This prevents multiple power interfaces from hoarding power so generator output enters the shared pool.
    /// </summary>
    public sealed class PowerTransfer : KMonoBehaviour, ISim200ms
    {
        private const float TARGET_TICKS = 2f;

        private HeartPort port;
        private Battery battery;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            port = GetComponent<HeartPort>();
            battery = GetComponent<Battery>();
        }

        public void Sim200ms(float dt)
        {
            if (port == null)
            {
                port = GetComponent<HeartPort>();
            }

            if (battery == null)
            {
                battery = GetComponent<Battery>();
            }

            if (port == null || battery == null || port.kind != HeartPortKind.Power)
            {
                return;
            }

            HeartOfElements heart = port.Heart;
            if (heart == null)
            {
                return;
            }

            float target = 0f;
            if (battery.IsConnected)
            {
                float neededWatts = Game.Instance.circuitManager.GetWattsNeededWhenActive(battery.CircuitID);
                if (neededWatts > 0f)
                {
                    target = neededWatts * dt * TARGET_TICKS;
                }
            }

            float difference = battery.JoulesAvailable - target;
            if (difference > 0f)
            {
                battery.ConsumeEnergy(difference);
                heart.StorePower(difference);
            }
            else if (difference < 0f && heart.StoredJoules > 0f)
            {
                float supplied = heart.ConsumePower(-difference);
                if (supplied > 0f)
                {
                    battery.AddEnergy(supplied);
                }
            }
        }
    }
}
