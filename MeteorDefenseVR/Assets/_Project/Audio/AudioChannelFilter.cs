using System;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MeteorDefenseVR.Audio
{
    // Source DSP gain avoids fighting existing fades, spatial attenuation and authored volumes.
    // Audio thread reads only scalar fields; no Unity API, allocation, preferences or events there.
    [DisallowMultipleComponent]
    public sealed class AudioChannelFilter : MonoBehaviour
    {
        public UserAudioChannel Channel { get; private set; }
        private volatile float gain=1;
        public volatile bool Measure;
        public volatile float InputRms,OutputRms,PeakInputRms,PeakOutputRms;
        private int blocks;
        public int MeasuredBlocks=>System.Threading.Volatile.Read(ref blocks);
        public float AppliedGain=>gain;
        public static AudioChannelFilter Attach(AudioSource source,UserAudioChannel channel)
        {
            if(source==null)return null;
            var filter=source.GetComponent<AudioChannelFilter>()??source.gameObject.AddComponent<AudioChannelFilter>();
            filter.Channel=channel;filter.Refresh();return filter;
        }
        private void OnEnable(){AudioUserPreferences.Changed+=Refresh;Refresh();}
        private void OnDisable(){AudioUserPreferences.Changed-=Refresh;gain=1;}
        private void Refresh()=>gain=AudioUserPreferences.Gain(Channel);
        public void ResetMeasurement(){InputRms=OutputRms=PeakInputRms=PeakOutputRms=0;System.Threading.Interlocked.Exchange(ref blocks,0);}
        private void OnAudioFilterRead(float[] data,int channels)
        {
            float level=gain;bool measure=Measure;double before=0,after=0;
            for(int i=0;i<data.Length;i++){float sample=data[i];data[i]=sample*level;if(measure){before+=sample*sample;after+=data[i]*data[i];}}
            if(measure&&data.Length>0){InputRms=(float)Math.Sqrt(before/data.Length);OutputRms=(float)Math.Sqrt(after/data.Length);PeakInputRms=Math.Max(PeakInputRms,InputRms);PeakOutputRms=Math.Max(PeakOutputRms,OutputRms);System.Threading.Interlocked.Increment(ref blocks);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install(){SceneManager.sceneLoaded-=SceneReady;SceneManager.sceneLoaded+=SceneReady;}
        private static void SceneReady(Scene scene,LoadSceneMode mode)
        {
            var manager=UnityEngine.Object.FindAnyObjectByType<AudioManager>();
            foreach(var root in scene.GetRootGameObjects())foreach(var source in root.GetComponentsInChildren<AudioSource>(true))
                Attach(source,manager!=null&&manager.OwnsBgmSource(source)?UserAudioChannel.Music:UserAudioChannel.Effects);
        }
    }
}
