using UnityEngine;
using UnityEngine.XR;

namespace BRP
{
    [DefaultExecutionOrder(-50)]
    public sealed class VROperatorControls : MonoBehaviour
    {
        public LaboratoryGame game;
        bool previousLeftPrimary,previousRightPrimary,previousLeftSecondary,previousRightSecondary;
        float initialHandDistance,initialScale;
        bool scaling;
        bool controlsFocused=true;
        bool leftButtonsReady,rightButtonsReady;
        bool smoothMovement=true,smoothTurn;
        float moveSpeed=1.1f,nextSnapTurn;
        public bool SmoothMovement=>smoothMovement;
        public bool SmoothTurn=>smoothTurn;
        public float MoveSpeed=>moveSpeed;
        void Start()
        {
            smoothMovement=PlayerPrefs.GetInt("BRP.VRSmoothMovement",1)!=0;
            smoothTurn=PlayerPrefs.GetInt("BRP.VRSmoothTurn",0)!=0;
            moveSpeed=Mathf.Clamp(PlayerPrefs.GetFloat("BRP.VRMoveSpeed",1.1f),.8f,1.6f);
        }
        public void ToggleSmoothMovement()
        {
            smoothMovement=!smoothMovement;PlayerPrefs.SetInt("BRP.VRSmoothMovement",smoothMovement?1:0);
        }
        public void ToggleTurnMode()
        {
            smoothTurn=!smoothTurn;PlayerPrefs.SetInt("BRP.VRSmoothTurn",smoothTurn?1:0);
        }
        public void CycleMoveSpeed()
        {
            moveSpeed=moveSpeed<.95f?1.1f:moveSpeed<1.4f?1.6f:.8f;PlayerPrefs.SetFloat("BRP.VRMoveSpeed",moveSpeed);
        }
        void OnDisable() {ReleaseFlightInput();scaling=false;}
        void OnApplicationFocus(bool focused)
        {
            controlsFocused=focused;
            if(!focused) {ReleaseFlightInput();scaling=false;leftButtonsReady=rightButtonsReady=false;previousLeftPrimary=previousRightPrimary=previousLeftSecondary=previousRightSecondary=true;}
        }
        void ReleaseFlightInput()
        {
            if(game?.Flight==null) return;
            game.Flight.manualInput=new DVector3();game.Flight.translationInput=new DVector3();
        }
        void Update()
        {
            if(game==null||!game.VREnabled||!controlsFocused) return;
            var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool leftTracked=left.isValid&&(!left.TryGetFeatureValue(CommonUsages.isTracked,out bool lt)||lt);
            bool rightTracked=right.isValid&&(!right.TryGetFeatureValue(CommonUsages.isTracked,out bool rt)||rt);
            left.TryGetFeatureValue(CommonUsages.primaryButton,out bool lp);right.TryGetFeatureValue(CommonUsages.primaryButton,out bool rp);
            left.TryGetFeatureValue(CommonUsages.secondaryButton,out bool ls);right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool rs);
            if(!leftTracked) leftButtonsReady=false;else if(!lp&&!ls) leftButtonsReady=true;
            if(!rightTracked) rightButtonsReady=false;else if(!rp&&!rs) rightButtonsReady=true;
            if(!leftButtonsReady) {lp=false;ls=false;}
            if(!rightButtonsReady) {rp=false;rs=false;}
            if(game.IsFlying)
            {
                ReleaseFlightInput();scaling=false;
                if(ls&&!previousLeftSecondary) game.UI.ShowFlightToolbar();
                if(!game.UI.ModalOpen)
                {
                    if(lp&&!previousLeftPrimary) game.ToggleMap();
                    if(rp&&!previousRightPrimary) {if(game.UI.MapView) game.CycleMapView();else game.NextStage();right.SendHapticImpulse(0,.25f,.08f);}
                    if(rs&&!previousRightSecondary) {if(game.UI.MapView) game.FocusNextMapPlanet();else game.DeployParachute();}
                    right.TryGetFeatureValue(CommonUsages.trigger,out float rotate);right.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 look);
                    left.TryGetFeatureValue(CommonUsages.trigger,out float translate);left.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 move);
                    left.TryGetFeatureValue(CommonUsages.gripButton,out bool vertical);right.TryGetFeatureValue(CommonUsages.gripButton,out bool throttle);
                    if(!rightTracked) {look=Vector2.zero;rotate=0;throttle=false;}
                    if(!leftTracked) {move=Vector2.zero;translate=0;vertical=false;}
                    if(game.UI.MapView)
                    {
                        game.OrbitMap(look*Time.unscaledDeltaTime*180,move.y*Time.unscaledDeltaTime*500);
                        game.Flight.manualInput=new DVector3();game.Flight.translationInput=new DVector3();
                    }
                    else
                    {
                        if(rotate>.5f&&look.sqrMagnitude>.04f) game.Flight.guidance=GuidanceMode.Manual;
                        game.Flight.manualInput=rotate>.5f?new DVector3(-look.y,0,-look.x):new DVector3();
                        game.Flight.translationInput=translate>.5f?new DVector3(move.x,vertical?move.y:0,vertical?0:move.y):new DVector3();
                        if(throttle) game.Flight.throttle=System.Math.Clamp(game.Flight.throttle+look.y*Time.unscaledDeltaTime*.4,0,1);
                    }
                }
            }
            else
            {
                if(ls&&!previousLeftSecondary) game.UI.ShowHome();
                bool holding=false;
                foreach(var view in game.Assembly.views) if(view.Grab!=null&&view.Grab.isSelected) {holding=true;break;}
                left.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 walk);
                right.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 turn);
                if(!game.UI.ModalOpen&&!holding&&!scaling)
                {
                    if(smoothMovement) Move(walk);
                    Turn(turn.x);
                }
                if(!game.UI.ModalOpen&&!holding&&lp&&rp&&left.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 a)&&right.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 b))
                {
                    float distance=Vector3.Distance(a,b);
                    if(!scaling) {scaling=true;initialHandDistance=Mathf.Max(.1f,distance);initialScale=game.Assembly.assemblyRoot.localScale.x;}
                    float desired=initialScale*distance/initialHandDistance;
                    if(Mathf.Abs(desired-game.Assembly.assemblyRoot.localScale.x)>.001f) game.ScaleAssembly(desired/game.Assembly.assemblyRoot.localScale.x);
                }
                else scaling=false;
            }
            previousLeftPrimary=lp;previousRightPrimary=rp;previousLeftSecondary=ls;previousRightSecondary=rs;
        }
        void Move(Vector2 input)
        {
            if(input.sqrMagnitude<.04f) return;
            var forward=Vector3.ProjectOnPlane(game.mainCamera.transform.forward,Vector3.up).normalized;
            var right=Vector3.ProjectOnPlane(game.mainCamera.transform.right,Vector3.up).normalized;
            var movement=(forward*input.y+right*input.x)*moveSpeed*Time.unscaledDeltaTime;
            var controller=game.mainCamera.GetComponentInParent<CharacterController>();
            if(controller!=null&&controller.enabled) controller.Move(movement);
            else game.mainCamera.transform.root.position+=movement;
        }
        void Turn(float input)
        {
            if(Mathf.Abs(input)<.2f) return;
            var rig=game.mainCamera.transform.root;
            if(smoothTurn) rig.RotateAround(game.mainCamera.transform.position,Vector3.up,input*75*Time.unscaledDeltaTime);
            else if(Time.unscaledTime>=nextSnapTurn)
            {
                rig.RotateAround(game.mainCamera.transform.position,Vector3.up,Mathf.Sign(input)*30);
                nextSnapTurn=Time.unscaledTime+.45f;
            }
        }
    }
}
