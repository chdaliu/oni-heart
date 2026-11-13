namespace ONIHeart
{
    /// <summary>
    /// Independent buffer for input interfaces: ConduitConsumer first writes into the interface's own Storage,
    /// then periodically transfers into the heart. This prevents multiple interfaces with the same settings from sharing capacity or input quota.
    /// </summary>
    public sealed class PortTransfer : KMonoBehaviour, ISim200ms
    {
        private HeartPort port;
        private Storage source;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            port = GetComponent<HeartPort>();
            source = GetComponent<Storage>();
        }

        public void Sim200ms(float dt)
        {
            if (port == null)
            {
                port = GetComponent<HeartPort>();
            }

            if (source == null)
            {
                source = GetComponent<Storage>();
            }

            if (port == null || source == null || source.items.Count == 0)
            {
                return;
            }

            HeartOfElements heart = port.Heart;
            if (heart == null || heart.Storage == null)
            {
                return;
            }

            // Merge into the heart's existing gas/liquid stacks so input temperatures are
            // averaged by mass instead of creating a second stack of the same element.
            heart.AbsorbStorage(source);
        }
    }
}
