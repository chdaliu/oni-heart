using UnityEngine;

namespace ONIHeart
{
    /// <summary>
    /// Soft breathing light for the heart: brightness rises and falls slowly, and the color drifts between warm white and very faint colors.
    /// Visual only; consumes no power and produces no heat.
    /// </summary>
    public sealed class HeartLight : KMonoBehaviour, ISim1000ms
    {
        private const float BASE_INTENSITY = 0.62f;

        private Light2D light;
        private Color baseColor;
        private float phase;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            light = GetComponent<Light2D>();
            if (light != null)
            {
                baseColor = light.Color;
            }
        }

        public void Sim1000ms(float dt)
        {
            if (light == null)
            {
                return;
            }

            phase += dt;
            float breath = 0.5f + 0.5f * Mathf.Sin(phase * 0.9f);
            light.IntensityAnimation = BASE_INTENSITY * (0.78f + 0.22f * breath);

            float hue = Mathf.Repeat(phase * 0.035f, 1f);
            Color tint = Color.HSVToRGB(hue, 0.32f, 1f);
            light.Color = Color.Lerp(baseColor, tint, 0.12f + 0.10f * breath);
        }
    }
}
