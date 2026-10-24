
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Release-player opt-in QA; never runs without its explicit launch flag.
    public sealed class DifficultyLockInstructionPlayerQa : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("--pc-difficulty-lock-qa") ||
                FindAnyObjectByType<DifficultyLockInstructionPlayerQa>() != null) return;
            var root = new GameObject("OptInDifficultyLockQA");
            DontDestroyOnLoad(root);
            root.AddComponent<DifficultyLockInstructionPlayerQa>();
        }
        private IEnumerator Start()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath,"../QA/DifficultyLock"));
            Directory.CreateDirectory(output);
            float deadline=Time.realtimeSinceStartup+30;
            while (FindAnyObjectByType<HangarLobbyController>() == null && Time.realtimeSinceStartup<deadline) yield return null;
            var stack=new Stack<IEnumerator>();
            stack.Push(DifficultyLockInstructionProof.Run(output));
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
            File.WriteAllText(Path.Combine(output,"runtime.txt"),"Opt-in difficulty lock QA; inspect enclosing build identity\nGPU="+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\npassed="+(failure==null)+"\n"+failure);
            Application.Quit(failure==null?0:2);
        }
    }
}



