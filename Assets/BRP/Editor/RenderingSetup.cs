using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BRP.Editor
{
    public static class RenderingSetup
    {
        public static void Configure()
        {
            System.IO.Directory.CreateDirectory("Assets/BRP/Rendering");AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Project Configuration/Quality URP Config.asset");
            var pipelines=new UniversalRenderPipelineAsset[3];
            string[] names={"Performance","Balanced","Quality"};
            for(int i=0;i<3;i++)
            {
                string path="Assets/BRP/Rendering/"+names[i]+".asset";
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if(pipeline==null) {pipeline=Object.Instantiate(source);pipeline.name="BRP "+names[i];AssetDatabase.CreateAsset(pipeline,path);}
                pipeline.supportsHDR=i>0;pipeline.msaaSampleCount=4;pipeline.renderScale=1;
                pipeline.mainLightShadowmapResolution=i==0?1024:i==1?2048:4096;
                pipeline.maxAdditionalLightsCount=i==2?8:i==1?4:2;
                pipeline.shadowDistance=i==0?25:80;pipeline.shadowCascadeCount=i==2?4:2;
                var properties=new SerializedObject(pipeline);
                properties.FindProperty("m_MainLightShadowsSupported").boolValue=true;
                properties.FindProperty("m_AdditionalLightShadowsSupported").boolValue=i==2;
                properties.FindProperty("m_SoftShadowsSupported").boolValue=i>0;
                properties.FindProperty("m_ReflectionProbeBlending").boolValue=i>0;
                properties.FindProperty("m_ReflectionProbeBoxProjection").boolValue=i>0;
                properties.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);pipelines[i]=pipeline;
            }
            var quality=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels=quality.FindProperty("m_QualitySettings");
            for(int i=0;i<levels.arraySize;i++)
            {
                var level=levels.GetArrayElementAtIndex(i);level.FindPropertyRelative("customRenderPipeline").objectReferenceValue=pipelines[Mathf.Min(2,i/2)];
                level.FindPropertyRelative("vSyncCount").intValue=0;level.FindPropertyRelative("shadows").intValue=2;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();GraphicsSettings.defaultRenderPipeline=pipelines[1];QualitySettings.SetQualityLevel(3,true);
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");layers.GetArrayElementAtIndex(29).stringValue="BRP Flight";layers.GetArrayElementAtIndex(30).stringValue="BRP Preview";
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
