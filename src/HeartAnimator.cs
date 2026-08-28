namespace ONIHeart
{
    /// <summary>
    /// 保证元素之心始终播放缓慢闪动的 idle 循环。
    /// </summary>
    public sealed class HeartAnimator : KMonoBehaviour
    {
        private KBatchedAnimController anim;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            anim = GetComponent<KBatchedAnimController>();
            if (anim != null)
            {
                anim.Play("idle", KAnim.PlayMode.Loop);
            }
        }
    }
}
