namespace ONIHeart
{
    /// <summary>
    /// 输入接口的独立缓冲：ConduitConsumer 先写入接口自己的 Storage，
    /// 再定期转入元素之心。这样多个同参数接口不会共享容量或输入额度。
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

            source.Transfer(heart.Storage, block_events: false, hide_popups: true);
        }
    }
}
