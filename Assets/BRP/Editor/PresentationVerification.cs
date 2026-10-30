using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BRP.Editor
{
    public static class PresentationVerification
    {
        [Serializable] public sealed class RenderingReport
        {
            public string utc,context;
            public int frames,width,height,renderers,triangles,drawCalls;
            public double averageFrameMs,p95FrameMs;
        }
        static readonly List<double> times=new List<double>();
        static double until,last;
        static string label;
        public static void Capture(string context)
        {
            EditorApplication.update-=Sample;times.Clear();label=context;
            last=EditorApplication.timeSinceStartup;until=last+8;EditorApplication.update+=Sample;
        }
        static void Sample()
        {
            double now=EditorApplication.timeSinceStartup;
            if(Time.frameCount==lastFrame) return;
            lastFrame=Time.frameCount;
            times.Add((now-last)*1000);last=now;
            if(now<until) return;
            EditorApplication.update-=Sample;
            var sorted=times.OrderBy(t=>t).ToArray();
            var report=new RenderingReport
            {
                utc=DateTime.UtcNow.ToString("O"),context=label+" / Editor Game View; not headset timing",
                frames=times.Count,width=Screen.width,height=Screen.height,
                renderers=UnityEngine.Object.FindObjectsByType<MeshRenderer>().Length,
                triangles=UnityStats.triangles,drawCalls=UnityStats.drawCalls,
                averageFrameMs=times.Average(),p95FrameMs=sorted[(int)((sorted.Length-1)*.95)]
            };
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/rendering-"+label+".json",JsonUtility.ToJson(report,true));
            Debug.Log(JsonUtility.ToJson(report));
        }
        static int lastFrame;
    }
}
