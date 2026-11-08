namespace ONIHeart
{
    /// <summary>
    /// Keeps the heart playing its slow pulsing idle loop.
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
