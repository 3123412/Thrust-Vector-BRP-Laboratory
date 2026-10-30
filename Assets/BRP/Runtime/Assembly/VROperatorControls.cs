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
        void Update()
        {
            if(game==null||!game.VREnabled) return;
            var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(CommonUsages.primaryButton,out bool lp);right.TryGetFeatureValue(CommonUsages.primaryButton,out bool rp);
            left.TryGetFeatureValue(CommonUsages.secondaryButton,out bool ls);right.TryGetFeatureValue(CommonUsages.secondaryButton,out bool rs);
            if(game.IsFlying)
            {
                if(ls&&!previousLeftSecondary) game.UI.ShowFlightToolbar();
                if(!game.UI.ModalOpen)
                {
                    if(lp&&!previousLeftPrimary) game.ToggleMap();
                    if(rp&&!previousRightPrimary) {game.NextStage();right.SendHapticImpulse(0,.25f,.08f);}
                    if(rs&&!previousRightSecondary) game.DeployParachute();
                    right.TryGetFeatureValue(CommonUsages.trigger,out float rotate);right.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 look);
                    left.TryGetFeatureValue(CommonUsages.trigger,out float translate);left.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 move);
                    left.TryGetFeatureValue(CommonUsages.gripButton,out bool vertical);right.TryGetFeatureValue(CommonUsages.gripButton,out bool throttle);
                    if(rotate>.5f&&look.sqrMagnitude>.04f) game.Flight.guidance=GuidanceMode.Manual;
                    game.Flight.manualInput=rotate>.5f?new DVector3(-look.y,0,-look.x):new DVector3();
                    game.Flight.translationInput=translate>.5f?new DVector3(move.x,vertical?move.y:0,vertical?0:move.y):new DVector3();
                    if(throttle) game.Flight.throttle=System.Math.Clamp(game.Flight.throttle+look.y*Time.unscaledDeltaTime*.4,0,1);
                }
            }
            else
            {
                if(ls&&!previousLeftSecondary) game.UI.ShowHome();
                if(lp&&rp&&left.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 a)&&right.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 b))
                {
                    float distance=Vector3.Distance(a,b);
                    if(!scaling) {scaling=true;initialHandDistance=Mathf.Max(.1f,distance);initialScale=game.Assembly.assemblyRoot.localScale.x;}
                    float desired=initialScale*distance/initialHandDistance;
                    game.ScaleAssembly(desired/game.Assembly.assemblyRoot.localScale.x);
                }
                else scaling=false;
            }
            previousLeftPrimary=lp;previousRightPrimary=rp;previousLeftSecondary=ls;previousRightSecondary=rs;
        }
    }
}
