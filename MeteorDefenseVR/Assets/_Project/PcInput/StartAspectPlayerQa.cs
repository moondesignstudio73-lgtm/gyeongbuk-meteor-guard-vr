#if START_ASPECT_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Compiled only by the separate instrumented builder; explicit launch flag also required.
    public sealed class StartAspectPlayerQa : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("--pc-start-aspect-qa") ||
                FindAnyObjectByType<StartAspectPlayerQa>() != null) return;
            var root = new GameObject("InstrumentedStartAspectQA");
            DontDestroyOnLoad(root);
            root.AddComponent<StartAspectPlayerQa>();
        }
        private IEnumerator Start()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/StartAspect"));
            Directory.CreateDirectory(output);
            float deadline=Time.realtimeSinceStartup+30;
            while (FindAnyObjectByType<HangarLobbyController>() == null && Time.realtimeSinceStartup<deadline) yield return null;
            var stack=new Stack<IEnumerator>();
            stack.Push(StartAspectPlayerProof.Run(output));
            Exception failure=null;
            while(stack.Count>0)
            {
                object yielded=null;bool more=false;
                try { more=stack.Peek().MoveNext();if(more)yielded=stack.Peek().Current; }
                catch(Exception error){failure=error;break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(yielded is IEnumerator nested)stack.Push(nested);
                else yield return yielded;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.WriteAllText(Path.Combine(output,"runtime.txt"),"Instrumented START_ASPECT_QA build, not public release\nGPU="+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\npassed="+(failure==null)+"\n"+failure);
            Application.Quit(failure==null?0:2);
        }
    }
}
#endif

