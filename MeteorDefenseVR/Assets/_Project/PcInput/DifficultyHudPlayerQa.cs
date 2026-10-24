using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using UnityEngine;
namespace MeteorDefenseVR.PcInput {
public sealed class DifficultyHudPlayerQa:MonoBehaviour {
 static string Argument(string prefix)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(prefix))?.Substring(prefix.Length);
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Argument("--pc-difficulty-hud-qa=")==null||FindAnyObjectByType<DifficultyHudPlayerQa>()!=null)return;var root=new GameObject("OptInDifficultyHudQA");DontDestroyOnLoad(root);root.AddComponent<DifficultyHudPlayerQa>();}
 IEnumerator Start(){string scenario=Argument("--pc-difficulty-hud-qa=");int width=int.Parse(Argument("--hud-width=")??"800"),height=width==800?600:width==2560?1080:720;string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/DifficultyHud",width+"x"+height,scenario));var stack=new Stack<IEnumerator>();stack.Push(DifficultyHudProof.Run(output,width,height,true));Exception failure=null;while(stack.Count>0){object next=null;bool more=false;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){failure=e;break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"runtime.txt"),"passed="+(failure==null)+"\nGPU="+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\n"+failure);Application.Quit(failure==null?0:2);}
}}



