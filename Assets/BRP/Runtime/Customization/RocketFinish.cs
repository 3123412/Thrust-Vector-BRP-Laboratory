using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BRP
{
    public sealed class RocketFinish : MonoBehaviour
    {
        GameObject marking;
        static readonly Color White=new Color(.95f,.96f,.94f);

        public static void Apply(GameObject model,PlacedPart placement,PartDefinition definition,TMP_FontAsset font=null)
        {
            var finish=model.GetComponent<RocketFinish>();
            if(finish==null) finish=model.AddComponent<RocketFinish>();
            finish.Refresh(placement,definition,font==null?UiTheme.Font:font);
        }

        public static void Copy(PlacedPart source,PlacedPart target)
        {
            target.color=source.color;target.accentColor=source.accentColor;target.paintSmoothness=source.paintSmoothness;
            target.decalId=source.decalId;target.decalText=source.decalText;target.decalAngle=source.decalAngle;target.decalScale=source.decalScale;
        }

        void Refresh(PlacedPart placement,PartDefinition definition,TMP_FontAsset font)
        {
            var renderers=GetComponentsInChildren<MeshRenderer>();
            var owner=GetComponent<PartView>();
            bool hasBody=false;
            foreach(var renderer in renderers)
                if(renderer.sharedMaterial!=null&&(renderer.sharedMaterial.name.Contains("Silver")||renderer.sharedMaterial.name.Contains("Ceramic"))) hasBody=true;
            var block=new MaterialPropertyBlock();
            foreach(var renderer in renderers)
            {
                var material=renderer.sharedMaterial;if(material==null) continue;
                if(owner!=null&&renderer.GetComponentInParent<PartView>()!=owner) continue;
                bool body=material.name.Contains("Silver")||material.name.Contains("Ceramic");
                bool accent=material.name.Contains("Blue")||material.name.Contains("Orange");
                if(!body&&!accent) continue;
                block.Clear();block.SetColor("_BaseColor",body||!hasBody?placement.color:placement.accentColor);
                block.SetFloat("_Smoothness",Mathf.Clamp01(placement.paintSmoothness));renderer.SetPropertyBlock(block);
            }
            if(marking!=null)
            {
                marking.SetActive(false);
                if(Application.isPlaying) Destroy(marking);else DestroyImmediate(marking);
            }
            if(string.IsNullOrEmpty(placement.decalId)&&string.IsNullOrWhiteSpace(placement.decalText)) return;
            marking=new GameObject("Module markings",typeof(RectTransform),typeof(Canvas));marking.layer=gameObject.layer;
            var rect=marking.GetComponent<RectTransform>();rect.SetParent(transform,false);rect.sizeDelta=new Vector2(400,240);
            var normal=Quaternion.Euler(0,placement.decalAngle,0)*Vector3.forward;
            float radius=Mathf.Max(.08f,definition.radius);
            rect.localPosition=normal*(radius+.015f)+Vector3.up*definition.height*.54f;
            rect.localRotation=Quaternion.LookRotation(-normal,Vector3.up);
            float width=Mathf.Clamp(radius*1.35f*Mathf.Clamp(placement.decalScale,.3f,1.2f),.08f,definition.height*.8f);
            rect.localScale=Vector3.one*width/400;
            var canvas=marking.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var oldFont=UiTheme.Font;UiTheme.Font=font;
            BuildEmblem(rect,placement.decalId,placement.accentColor);
            string label=placement.decalText??"";
            if(label.Length>24) label=label.Substring(0,24);
            var title=UiTheme.Text(rect,label.ToUpperInvariant(),0,158,400,75,50,White);title.alignment=TextAlignmentOptions.Center;title.richText=false;
            UiTheme.Font=oldFont;
        }

        static void BuildEmblem(RectTransform root,string id,Color accent)
        {
            if(string.IsNullOrEmpty(id)) return;
            if(id=="brp")
            {
                var bar=UiTheme.Panel("BRP Accent",root,60,24,16,100,accent);bar.GetComponent<Image>().raycastTarget=false;
                var text=UiTheme.Text(root,"БРП",92,14,260,110,88,White);text.alignment=TextAlignmentOptions.Center;
                return;
            }
            if(id=="flag")
            {
                Color[] colors={White,new Color(.1f,.3f,.65f),new Color(.8f,.12f,.12f)};
                for(int i=0;i<3;i++) {var stripe=UiTheme.Panel("Flag stripe",root,85,16+i*40,230,40,colors[i]);stripe.GetComponent<Image>().raycastTarget=false;}
                return;
            }
            if(id=="hazard")
            {
                for(int i=0;i<5;i++)
                {
                    var stripe=UiTheme.Panel("Safety band",root,50+i*60,20,30,110,accent);
                    stripe.localRotation=Quaternion.Euler(0,0,-20);stripe.GetComponent<Image>().raycastTarget=false;
                }
                return;
            }
            if(id=="orbit")
            {
                var text=UiTheme.Text(root,"О Р Б И Т А",0,24,400,100,54,White);text.alignment=TextAlignmentOptions.Center;
                var line=UiTheme.Panel("Orbital underline",root,50,130,300,8,accent);line.GetComponent<Image>().raycastTarget=false;
                return;
            }
            var number=UiTheme.Text(root,id=="01"?"01":"СССР",0,10,400,130,id=="01"?112:76,White);number.alignment=TextAlignmentOptions.Center;
        }
    }
}
