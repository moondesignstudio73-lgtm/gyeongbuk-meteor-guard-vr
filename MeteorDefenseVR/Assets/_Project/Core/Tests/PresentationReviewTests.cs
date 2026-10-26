using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class PresentationReviewTests {
 [UnityTest,Timeout(360000)] public IEnumerator SequenceAndGallery()
 {
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Pilot();
 }
 private static IEnumerator Pilot()
 {
  while(Object.FindAnyObjectByType<GazeInputRouter>()==null)yield return null;
  bool done=false;System.Exception failure=null;
  IEnumerator Drive()
  {
   var stack=new System.Collections.Generic.Stack<IEnumerator>();
   stack.Push(PresentationReviewProof.Run(System.Environment.GetEnvironmentVariable("PRESENTATION_OUTPUT")??"Docs/DeliveryQA/cockpit-redesign-20261026/Before",System.Environment.GetEnvironmentVariable("PRESENTATION_VALIDATE")=="1"));
   while(stack.Count>0)
   {
    object next=null;bool more=false;
    try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(System.Exception e){failure=e;break;}
    if(!more){(stack.Pop() as System.IDisposable)?.Dispose();continue;}
    if(next is IEnumerator nested)stack.Push(nested);else yield return next;
   }
   while(stack.Count>0)(stack.Pop() as System.IDisposable)?.Dispose();done=true;
  }
  // Use the PlayerLoop, as the release-player fixture does, for relative mouse events.
  Object.FindAnyObjectByType<GazeInputRouter>().StartCoroutine(Drive());
  while(!done)yield return null;
  if(failure!=null)throw failure;
 }
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
}}
