using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using UnityEngine;
namespace MeteorDefenseVR.PcInput {
public sealed class ExperienceTipPlayerQa:MonoBehaviour {
 static string Argument(string prefix)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(prefix))?.Substring(prefix.Length);
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Configure(){string scenario=Argument("--pc-experience-tip-qa=")??(Application.isEditor?Environment.GetEnvironmentVariable("TIP_QA_SCENARIO"):null);if(string.IsNullOrEmpty(scenario))return;var settings=Resources.Load<DifficultySettings>("DifficultySettings");settings.defaultDifficulty=scenario=="locked-default"?DifficultyLevel.Experience:DifficultyLevel.Normal;settings.lockDifficultySelection=scenario=="locked-default";}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Argument("--pc-experience-tip-qa=")==null||FindAnyObjectByType<ExperienceTipPlayerQa>()!=null)return;var root=new GameObject("OptInExperienceTipQA");DontDestroyOnLoad(root);root.AddComponent<ExperienceTipPlayerQa>();}
 IEnumerator Start(){string scenario=Argument("--pc-experience-tip-qa=");int width=int.Parse(Argument("--tip-width=")??"800"),height=width==800?600:720;string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/ExperienceTip",width+"x"+height,scenario));var stack=new Stack<IEnumerator>();stack.Push(ExperienceTipProof.Run(output,scenario,width,height,true));Exception failure=null;while(stack.Count>0){object next=null;bool more=false;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){failure=e;break;}if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(next is IEnumerator nested)stack.Push(nested);else yield return next;}while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"runtime.txt"),"passed="+(failure==null)+"\nGPU="+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\n"+failure);Application.Quit(failure==null?0:2);}
}}
