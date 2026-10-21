using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace MeteorDefenseVR.Audio
{
    // Explicit QA-only separate-process probe; never writes production preference keys.
    public sealed class AudioPreferenceRestartProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--audio-prefs-write")>=0||Array.IndexOf(args,"--audio-prefs-read")>=0)
                new GameObject("AudioPreferenceRestartProbe").AddComponent<AudioPreferenceRestartProbe>();
        }
        private IEnumerator Start()
        {
            bool write=Array.IndexOf(Environment.GetCommandLineArgs(),"--audio-prefs-write")>=0;
            AudioUserPreferences.UseVerificationProfile(true);
            if(write) for(int i=0;i<4;i++){AudioUserPreferences.SetVolume((UserAudioChannel)i,(i+1)*.2f);AudioUserPreferences.SetMuted((UserAudioChannel)i,i%2==1);}
            yield return null;
            bool passed=true;
            for(int i=0;i<4;i++)passed &= Mathf.Abs(AudioUserPreferences.Volume((UserAudioChannel)i)-(i+1)*.2f)<.00001f&&AudioUserPreferences.Muted((UserAudioChannel)i)==(i%2==1);
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","QA"));Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,write?"audio-prefs-write.json":"audio-prefs-read.json"),"{\"passed\":"+(passed?"true":"false")+",\"profile\":\"QA only\",\"processId\":"+System.Diagnostics.Process.GetCurrentProcess().Id+"}");
            if(!write)AudioUserPreferences.ClearVerificationProfile();
            AudioUserPreferences.UseVerificationProfile(false);Application.Quit(passed?0:1);
        }
    }
}
