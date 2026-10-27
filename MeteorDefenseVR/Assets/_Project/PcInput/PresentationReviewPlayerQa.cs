using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using UnityEngine;
namespace MeteorDefenseVR.PcInput {
public sealed class PresentationReviewPlayerQa:MonoBehaviour {
 static string Argument(string prefix)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(prefix))?.Substring(prefix.Length);
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Argument("--pc-presentation-qa=")==null||FindAnyObjectByType<PresentationReviewPlayerQa>()!=null)return;var root=new GameObject("OptInPresentationQA");DontDestroyOnLoad(root);root.AddComponent<PresentationReviewPlayerQa>();}
 IEnumerator Start(){string scenario=Argument("--pc-presentation-qa=");string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/Presentation",scenario));var stack=new Stack<IEnumerator>();stack.Push(scenario=="replay-menu"?ReplayMenuProof.Run(output,true):scenario=="threat-transition"?ThreatTransitionProof.Run(output,true):scenario=="threat-direction"?ThreatDirectionProof.Run(output):scenario=="threat-safe"?ThreatSafeAreaProof.Run(output,true):scenario=="loading-boss"?LoadingBossReviewProof.Run(output,true):PresentationReviewProof.Run(output,true,scenario=="screens"));Exception failure=null;while(stack.Count>0){object next=null;bool more=false;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){failure=e;break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"runtime.txt"),"passed="+(failure==null)+"\nGPU="+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\n"+failure);Application.Quit(failure==null?0:2);}
}}
