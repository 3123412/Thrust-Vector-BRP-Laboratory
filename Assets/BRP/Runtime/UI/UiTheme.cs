using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BRP
{
    public static class UiTheme
    {
        public static readonly Color Ink=new Color(.027f,.037f,.043f,.97f);
        public static readonly Color PanelColor=new Color(.045f,.058f,.064f,.94f);
        public static readonly Color Line=new Color(.25f,.31f,.32f,.5f);
        public static readonly Color TextColor=new Color(.91f,.91f,.86f);
        public static readonly Color Muted=new Color(.54f,.62f,.63f);
        public static readonly Color Orange=new Color(.96f,.37f,.13f);
        public static readonly Color Cyan=new Color(.45f,.79f,.75f);
        public static TMP_FontAsset Font;

        public static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect;
        }
        public static RectTransform Panel(string name,Transform parent,float x,float y,float w,float h,Color? color=null)
        {
            var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<PanelGraphic>();
            image.color=color??PanelColor;return r;
        }
        public static TextMeshProUGUI Text(Transform parent,string text,float x,float y,float w,float h,float size=18,Color? color=null)
        {
            var rect=Rect("Text",parent,x,y,w,h);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=Font;label.text=text;label.fontSize=size;label.color=color??TextColor;
            label.raycastTarget=false;label.fontStyle=FontStyles.Normal;label.textWrappingMode=TextWrappingModes.Normal;
            label.overflowMode=TextOverflowModes.Ellipsis;return label;
        }
        public static UnityEngine.UI.Button Button(Transform parent,string text,float x,float y,float w,float h,UnityAction action,bool primary=false)
        {
            var r=Panel(text,parent,x,y,w,h,primary?Orange:new Color(.075f,.12f,.15f));
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.75f,.9f,.95f);colors.pressedColor=new Color(.5f,.65f,.72f);colors.disabledColor=new Color(.35f,.4f,.44f);button.colors=colors;
            if(action!=null) button.onClick.AddListener(action);
            var label=Text(r,text,6,0,w-12,h,w<90?12:17,primary?Ink:TextColor);label.alignment=TextAlignmentOptions.Midline;
            return button;
        }
        public static UnityEngine.UI.Button Command(Transform parent,string text,float x,float y,float w,float h,string command,int argument=0,bool primary=false)
        {
            var button=Button(parent,text,x,y,w,h,null,primary);
            var binding=button.gameObject.AddComponent<LabButton>();binding.command=command;binding.argument=argument;
            var accent=Panel("HoverLine",button.transform,0,h-2,w,2,new Color(Cyan.r,Cyan.g,Cyan.b,0));
            accent.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;binding.accent=accent.GetComponent<UnityEngine.UI.Image>();
            return button;
        }
        public static RectTransform Scroll(Transform parent,string name,float x,float y,float w,float h,out ScrollRect scroll)
        {
            var root=Rect(name,parent,x,y,w,h);scroll=root.gameObject.AddComponent<ScrollRect>();
            var viewport=Panel("Viewport",root,0,0,w,h,new Color(.04f,.07f,.09f,.05f));
            var mask=viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var content=Rect("Content",viewport,0,0,w-10,0);
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
            return content;
        }
        public static void Divider(Transform parent,float x,float y,float w) => Panel("Rule",parent,x,y,w,1,Line);
    }
}
