using System.Collections;
using UnityEngine;

namespace Wardrobe.Services
{
    // RefreshScrollContent rebuilds the tab, jumps to the top and replays the slide-in.
    // For a folder change, a trash move or an undo that is noise, so the content is put back
    // at the same pixel offset it had before.
    internal static class ScrollKeeper
    {
        public static void Refresh(MenuPageCosmetics? page)
        {
            if (!page)
            {
                return;
            }
            MenuScrollBox box = page!.menuScrollBox;
            float offset = box && box.scroller ? box.scroller.localPosition.y : float.NaN;
            page.RefreshScrollContent();
            if (!float.IsNaN(offset))
            {
                WardrobeUI.Ensure().StartCoroutine(Restore(page, offset));
            }
        }

        private static IEnumerator Restore(MenuPageCosmetics page, float offset)
        {
            float deadline = Time.unscaledTime + 2f;
            while (page && !page.subCategoriesReady && Time.unscaledTime < deadline)
            {
                yield return null;
            }
            yield return new WaitForFixedUpdate();
            yield return null;
            MenuScrollBox? box = page ? page.menuScrollBox : null;
            for (int frame = 0; frame < 4; frame++)
            {
                if (!page || !box || !box!.scrollHandle || !box.scrollBarBackground || !box.scroller)
                {
                    yield break;
                }
                float amount = Mathf.Clamp01(Mathf.InverseLerp(box.scrollerStartPosition, box.scrollerEndPosition, offset));
                if (box.scrollerStartPosition <= box.scrollerEndPosition)
                {
                    amount = 0f;
                }
                float half = box.scrollHandle.sizeDelta.y / 2f;
                float top = box.scrollBarBackground.rect.height - half;
                float y = Mathf.Lerp(top, half, amount);
                box.isAnimating = false;
                box.scrollHandleTargetPosition = y;
                box.scrollHandle.localPosition = new Vector3(box.scrollHandle.localPosition.x, y, box.scrollHandle.localPosition.z);
                box.scrollAmount = amount;
                float target = Mathf.Lerp(box.scrollerStartPosition, box.scrollerEndPosition, amount);
                box.scroller.localPosition = new Vector3(page.scrollerRestingPosition.x, target, box.scroller.localPosition.z);
                page.scrollerLerp = 1f;
                yield return null;
            }
        }
    }
}
