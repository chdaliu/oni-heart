namespace ONIHeart
{
    /// <summary>
    /// 单个电力接口与元素之心内部电力的双向同步。
    /// 接口上的电池只是与本地电网交换的缓冲；实际能量通过元素之心共享，
    /// 所以一个接口接入的发电机可以给另一个接口所在的电网供电。
    ///
    /// 本地缓冲按本地电网的实际需求确定：
    /// - 本地没有用电需求时，电池里的电全部送入元素之心（发电机侧）；
    /// - 本地有用电需求时，电池只保留约两帧需求量，不足部分从元素之心补回。
    /// 这样多个电力接口不会互相囤电，发电机产生的电会进入共享池。
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
