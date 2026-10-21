using System;
using System.IO;
using UnityEngine;
namespace MeteorDefenseVR.PcInput
{
    // QA-only listener tap. No clip generation, playback or processing of the output samples.
    public sealed class CampaignOutputRecorder : MonoBehaviour
    {
        private float[] samples;private int written,channels=2,rate;private volatile bool recording;
        public void Begin(){rate=AudioSettings.outputSampleRate;samples=new float[rate*180*2];written=0;recording=true;}
        private void OnAudioFilterRead(float[] data,int channelCount)
        {
            if(!recording||samples==null)return;
            channels=channelCount;int position=System.Threading.Volatile.Read(ref written);int count=Math.Min(data.Length,samples.Length-position);
            if(count<=0)return;Array.Copy(data,0,samples,position,count);System.Threading.Volatile.Write(ref written,position+count);
        }
        public string Save(string path)
        {
            recording=false;int count=System.Threading.Volatile.Read(ref written);if(count==0)return "Listener output tap received no samples; no final-output recording available";
            double energy=0;float peak=0;int clipped=0;
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                int bytes=count*2;stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));stream.Write(36+bytes);stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));stream.Write(16);stream.Write((short)1);stream.Write((short)channels);stream.Write(rate);stream.Write(rate*channels*2);stream.Write((short)(channels*2));stream.Write((short)16);stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));stream.Write(bytes);
                for(int i=0;i<count;i++){float x=samples[i];peak=Math.Max(peak,Math.Abs(x));energy+=x*x;if(Math.Abs(x)>1)clipped++;stream.Write((short)(Mathf.Clamp(x,-1,1)*32767));}
            }
            return $"Listener output WAV: samples={count} rate={rate} channels={channels} seconds={(double)count/rate/channels:F2} peak={peak} RMS={Math.Sqrt(energy/count)} samplesAboveUnity={clipped}; not human listening or OS/speaker capture";
        }
    }
}
