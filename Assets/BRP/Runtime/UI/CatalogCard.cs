using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using System.Collections.Generic;

namespace BRP
{
    public sealed class CatalogCard : MonoBehaviour,IInitializePotentialDragHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
    {
        public PartDefinition definition;
        bool dragged;
        int dragEndedFrame=-1;
        readonly HashSet<XRBaseInputInteractor> hovering=new HashSet<XRBaseInputInteractor>();
        static readonly HashSet<XRBaseInputInteractor> latched=new HashSet<XRBaseInputInteractor>();
        XRBaseInputInteractor holding;
        PartView heldView;
        float lastGrabTime=-10;
        public void OnInitializePotentialDrag(PointerEventData e)
        {
            if(LaboratoryGame.Instance.VREnabled) GetComponentInParent<UnityEngine.UI.ScrollRect>()?.OnInitializePotentialDrag(e);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            dragged=true;
            if(LaboratoryGame.Instance.VREnabled) GetComponentInParent<UnityEngine.UI.ScrollRect>()?.OnBeginDrag(e);
            else LaboratoryGame.Instance.BeginCatalogDrag(definition);
        }
        public void OnDrag(PointerEventData e) {if(LaboratoryGame.Instance.VREnabled) GetComponentInParent<UnityEngine.UI.ScrollRect>()?.OnDrag(e);}
        public void OnEndDrag(PointerEventData e)
        {
            if(LaboratoryGame.Instance.VREnabled) GetComponentInParent<UnityEngine.UI.ScrollRect>()?.OnEndDrag(e);
            else LaboratoryGame.Instance.EndDrag();
            dragged=false;dragEndedFrame=Time.frameCount;
        }
        public void OnPointerEnter(PointerEventData e)
        {
            LaboratoryGame.Instance.Sound.Hover();
            if(e is TrackedDeviceEventData tracked&&tracked.interactor is XRBaseInputInteractor interactor) hovering.Add(interactor);
        }
        public void OnPointerExit(PointerEventData e)
        {
            if(e is TrackedDeviceEventData tracked&&tracked.interactor is XRBaseInputInteractor interactor) hovering.Remove(interactor);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if(holding==null&&!dragged&&Time.frameCount!=dragEndedFrame&&Time.unscaledTime-lastGrabTime>.5f) LaboratoryGame.Instance.Spawn(definition);
            dragged=false;
        }
        void Update()
        {
            var game=LaboratoryGame.Instance;
            if(game==null||!game.VREnabled) return;
            latched.RemoveWhere(interactor=>interactor==null||!interactor.selectInput.ReadIsPerformed());
            if(holding!=null)
            {
                if(!holding.isActiveAndEnabled||!holding.selectInput.ReadIsPerformed())
                {
                    ReleaseHeld();
                    lastGrabTime=Time.unscaledTime;
                }
                return;
            }
            if(game.IsFlying||game.UI.ModalOpen) return;
            foreach(var interactor in hovering)
            {
                if(interactor==null||interactor.interactionManager==null||interactor.hasSelection) continue;
                if(latched.Contains(interactor)||!interactor.selectInput.ReadIsPerformed()) continue;
                latched.Add(interactor);
                var view=game.Assembly.Spawn(definition);
                if(view==null) {game.Toast("Предел: 128 деталей");return;}
                game.Select(view);game.Sound.Play(SoundCue.Click);
                view.transform.position=interactor.transform.position+interactor.transform.forward*.15f;
                view.transform.rotation=game.Assembly.assemblyRoot.rotation;
                interactor.StartManualInteraction((IXRSelectInteractable)view.Grab);
                lastGrabTime=Time.unscaledTime;
                if(view.Grab.isSelected) {holding=interactor;heldView=view;}
                else
                {
                    view.transform.position=game.Assembly.assemblyRoot.position+new Vector3(-.85f,.2f,-.12f);
                    game.Toast("Не удалось взять деталь. Наведите луч и повторите хват.");
                }
                break;
            }
        }
        void ReleaseHeld()
        {
            if(holding!=null&&heldView!=null&&holding.IsSelecting(heldView.Grab)) holding.EndManualInteraction();
            holding=null;heldView=null;
        }
        void OnDisable() {ReleaseHeld();hovering.Clear();}
    }
}
