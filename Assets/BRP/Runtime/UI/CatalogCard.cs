using UnityEngine;
using UnityEngine.EventSystems;

namespace BRP
{
    public sealed class CatalogCard : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
    {
        public PartDefinition definition;
        bool dragged;
        public void OnBeginDrag(PointerEventData e) {dragged=true;LaboratoryGame.Instance.BeginCatalogDrag(definition);}
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) {LaboratoryGame.Instance.EndDrag();}
        public void OnPointerEnter(PointerEventData e) {LaboratoryGame.Instance.UI.ShowDefinition(definition);}
        public void OnPointerExit(PointerEventData e) { }
        public void OnPointerClick(PointerEventData e)
        {
            if(!dragged) LaboratoryGame.Instance.Spawn(definition);
            dragged=false;
        }
    }
}
