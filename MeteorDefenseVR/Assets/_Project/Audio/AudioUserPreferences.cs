using System;
using UnityEngine;
namespace MeteorDefenseVR.Audio
{
    public enum UserAudioChannel { Music, Effects, Radio, Ai }
    // Independent user gain after the authored mix. Defaults preserve every existing source level.
    public static class AudioUserPreferences
    {
        private static readonly float[] volumes={1,1,1,1};
        private static readonly bool[] muted=new bool[4];
        private static string prefix="MeteorDefense.Audio.v1.";
        private static bool loaded;
        public static event Action Changed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){loaded=false;prefix="MeteorDefense.Audio.v1.";Changed=null;}
        private static string Key(int channel,string field)=>prefix+channel+"."+field;
        public static void Reload()
        {for(int i=0;i<4;i++){float v=PlayerPrefs.GetFloat(Key(i,"volume"),1);volumes[i]=float.IsNaN(v)||float.IsInfinity(v)?1:Mathf.Clamp01(v);muted[i]=PlayerPrefs.GetInt(Key(i,"mute"),0)!=0;}loaded=true;Changed?.Invoke();}
        private static void Ensure(){if(!loaded)Reload();}
        public static float Volume(UserAudioChannel channel){Ensure();return volumes[(int)channel];}
        public static bool Muted(UserAudioChannel channel){Ensure();return muted[(int)channel];}
        public static float Gain(UserAudioChannel channel)=>Muted(channel)?0:Volume(channel);
        public static void SetVolume(UserAudioChannel channel,float value)
        {Ensure();if(float.IsNaN(value)||float.IsInfinity(value))return;volumes[(int)channel]=Mathf.Clamp01(value);PlayerPrefs.SetFloat(Key((int)channel,"volume"),volumes[(int)channel]);PlayerPrefs.Save();Changed?.Invoke();}
        public static void SetMuted(UserAudioChannel channel,bool value)
        {Ensure();muted[(int)channel]=value;PlayerPrefs.SetInt(Key((int)channel,"mute"),value?1:0);PlayerPrefs.Save();Changed?.Invoke();}
        // Isolated verification profile: never overwrites a player's actual preferences.
        public static void UseVerificationProfile(bool active){prefix=active?"MeteorDefense.Audio.QA.v1.":"MeteorDefense.Audio.v1.";loaded=false;Reload();}
        public static void ClearVerificationProfile(){for(int i=0;i<4;i++){PlayerPrefs.DeleteKey("MeteorDefense.Audio.QA.v1."+i+".volume");PlayerPrefs.DeleteKey("MeteorDefense.Audio.QA.v1."+i+".mute");}PlayerPrefs.Save();}
    }
}
