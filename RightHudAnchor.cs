using UnityEngine;
using UnityEngine.UI;

namespace SpiritOfTheSamuraiUltrawide
{
    // These sibling frames mix RectTransform and ordinary Transform; preserve their existing layout.
    internal sealed class RightHudAnchor : MonoBehaviour
    {
        private RectTransform parent;
        private Transform arrows, bag;
        private RectTransform arrow_rect;
        private float arrows_x, bag_x, reference_width;
        private float applied = float.NaN;

        private void Awake()
        {
            parent = transform as RectTransform;
            CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
            arrows = transform.Find("ArrowsFrame");
            bag = transform.Find("IncenseFrame");
            if (parent == null || scaler == null || arrows == null || bag == null)
            {
                enabled = false;
                return;
            }
            arrow_rect = arrows as RectTransform;
            arrows_x = arrow_rect != null ? arrow_rect.anchoredPosition.x : arrows.localPosition.x;
            bag_x = bag.localPosition.x;
            // StaticUI stretches across the canvas with an authored horizontal size inset.
            reference_width = scaler.referenceResolution.x + parent.sizeDelta.x;
        }

        private void LateUpdate()
        {
            float offset = Mathf.Max(0f, parent.rect.width - reference_width) * 0.5f;
            if (Mathf.Approximately(offset, applied)) return;
            applied = offset;
            SetArrowX(arrows_x + offset);
            if (bag != null)
            {
                Vector3 position = bag.localPosition;
                position.x = bag_x + offset;
                bag.localPosition = position;
            }
        }

        private void SetArrowX(float x)
        {
            if (arrow_rect != null)
            {
                Vector2 position = arrow_rect.anchoredPosition;
                position.x = x;
                arrow_rect.anchoredPosition = position;
            }
            else if (arrows != null)
            {
                Vector3 position = arrows.localPosition;
                position.x = x;
                arrows.localPosition = position;
            }
        }
    }
}
