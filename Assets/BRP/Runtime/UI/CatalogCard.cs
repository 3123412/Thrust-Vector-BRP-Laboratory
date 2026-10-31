using UnityEngine;
using UnityEngine.EventSystems;

namespace BRP
{
    public sealed class CatalogCard : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
    {
        public PartDefinition definition;
        bool dragged;
        int dragEndedFrame=-1;
        public void OnBeginDrag(PointerEventData e) {dragged=true;LaboratoryGame.Instance.BeginCatalogDrag(definition);}
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) {LaboratoryGame.Instance.EndDrag();dragged=false;dragEndedFrame=Time.frameCount;}
        public void OnPointerEnter(PointerEventData e) {LaboratoryGame.Instance.Sound.Hover();}
        public void OnPointerExit(PointerEventData e) { }
        public void OnPointerClick(PointerEventData e)
        {
            if(!dragged&&Time.frameCount!=dragEndedFrame) LaboratoryGame.Instance.Spawn(definition);
            dragged=false;
        }
    }
}
