using UnityEngine;
using UnityEngine.EventSystems;

namespace BRP
{
    public sealed class TutorialPreviewInput : MonoBehaviour,IBeginDragHandler,IDragHandler,IScrollHandler
    {
        public TutorialFocusPreview preview;
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) {if(preview!=null) preview.Rotate(eventData.delta);}
        public void OnScroll(PointerEventData eventData) {if(preview!=null) preview.ChangeZoom(eventData.scrollDelta.y*.12f);}
    }
}
