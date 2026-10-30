using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace BRP.Editor
{
    public static class ShowcaseRecorder
    {
        static RecorderController controller;
        static GameObject caption;
        public static bool Recording=>controller!=null&&controller.IsRecording();
        public static void Start()
        {
            if(!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first");
            if(Recording) Stop();
            Directory.CreateDirectory("Delivery");
            var settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name="BRP Gameplay";movie.Enabled=true;movie.CaptureAudio=true;movie.CaptureAlpha=false;
            movie.EncoderSettings=new CoreEncoderSettings {EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.High,Codec=CoreEncoderSettings.OutputCodec.MP4};
            movie.ImageInputSettings=new GameViewInputSettings {OutputWidth=1920,OutputHeight=1080};
            movie.OutputFile=Path.GetFullPath("Delivery/BRP_Gameplay");
            settings.AddRecorderSettings(movie);settings.SetRecordModeToManual();settings.FrameRate=30;
            controller=new RecorderController(settings);controller.PrepareRecording();controller.StartRecording();
        }
        public static void Caption(string heading,string detail)
        {
            if(caption!=null) Object.DestroyImmediate(caption);
            var ui=LaboratoryGame.Instance.UI;
            var panel=UiTheme.Panel("Video Caption",ui.root,570,782,780,104,UiTheme.Ink);caption=panel.gameObject;
            UiTheme.Panel("Caption Accent",panel,0,0,4,104,UiTheme.Orange);
            UiTheme.Text(panel,heading,24,15,734,32,22,UiTheme.TextColor);
            UiTheme.Text(panel,detail,24,57,734,34,15,UiTheme.Muted);
        }
        public static void Stop()
        {
            if(controller!=null) controller.StopRecording();controller=null;
            if(caption!=null) Object.DestroyImmediate(caption);
            Debug.Log("BRP gameplay video saved");
        }
    }
}
