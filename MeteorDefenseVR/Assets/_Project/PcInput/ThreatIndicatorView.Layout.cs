using System.Collections.Generic;
using MeteorDefenseVR.Meteor;
using UnityEngine;
namespace MeteorDefenseVR.PcInput {
 public sealed partial class ThreatIndicatorView {
  private const float LabelHalfWidth=100,LabelHalfHeight=40,LabelGap=10;
  private GameplayHudOverlayView protectedHud;
  private readonly Rect[] protectedRegions=new Rect[9],occupiedRegions=new Rect[5];
  private int protectedCount,occupiedCount;
  private readonly Dictionary<MeteorController,Vector2> parkedLabels=new();
  private readonly Dictionary<MeteorController,Vector2> trackedLabels=new();
  private Rect LabelSafeArea => Rect.MinMaxRect(-root.rect.width*.5f+LabelHalfWidth+LabelGap,-root.rect.height*.5f+LabelHalfHeight+LabelGap,root.rect.width*.5f-LabelHalfWidth-LabelGap,root.rect.height*.5f-LabelHalfHeight-LabelGap);
  private Rect ProjectPanel(RectTransform panel) {
   panel.GetWorldCorners(panelCorners);
   Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
   foreach(var corner in panelCorners){var p=view.Camera.WorldToViewportPoint(corner);var q=new Vector2((p.x-.5f)*root.rect.width,(p.y-.5f)*root.rect.height);min=Vector2.Min(min,q);max=Vector2.Max(max,q);}
   return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
  }
  private void Protect(Rect rect){protectedRegions[protectedCount++]=Rect.MinMaxRect(rect.xMin-LabelHalfWidth-LabelGap,rect.yMin-LabelHalfHeight-LabelGap,rect.xMax+LabelHalfWidth+LabelGap,rect.yMax+LabelHalfHeight+LabelGap);}
  private void RefreshProtectedLayout(){
   protectedCount=occupiedCount=0;
   foreach(var panel in telemetryPanels)if(panel!=null&&panel.gameObject.activeInHierarchy)Protect(ProjectPanel(panel));
   if(protectedHud==null)protectedHud=FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
   if(protectedHud!=null){
    foreach(string name in TopPanelNames){var panel=protectedHud.TelemetryRoot.transform.Find(name) as RectTransform;if(panel!=null)Protect(ProjectPanel(panel));}
    // Reserve this lane even between rewards; appearing messages must not make labels hop.
    var feedback=ProjectPanel((RectTransform)protectedHud.FeedbackPanel.transform);feedback.yMin-=24;feedback.yMax+=24;Protect(feedback);
   }
   Protect(new Rect(-root.rect.width*.15f,-root.rect.height*.15f,root.rect.width*.3f,root.rect.height*.3f));
   if(noticeReserved)protectedRegions[protectedCount++]=noticeRegion;
  }
  private static readonly string[] TopPanelNames={"HealthPanel","StagePanel","ScorePanel"};
  private bool ClearLabelPoint(Vector2 point){
   if(!LabelSafeArea.Contains(point))return false;
   for(int i=0;i<protectedCount;i++)if(protectedRegions[i].Contains(point))return false;
   for(int i=0;i<occupiedCount;i++)if(occupiedRegions[i].Contains(point))return false;
   return true;
  }
  private Vector2 PlaceProtectedLabel(Vector2 desired,MeteorController meteor){
   Vector2 point;
   if(parkedLabels.TryGetValue(meteor,out point)&&ClearLabelPoint(point))return Occupy(point,meteor);
   if(!parkedLabels.ContainsKey(meteor)&&ClearLabelPoint(desired)){
    point=trackedLabels.TryGetValue(meteor,out var previous)?Vector2.Lerp(previous,desired,1-Mathf.Exp(-Time.unscaledDeltaTime*18)):desired;
    if(ClearLabelPoint(point))return Occupy(point,meteor);
   }
   var safe=LabelSafeArea;float best=float.PositiveInfinity;Vector2 chosen=desired;
   void Candidate(Vector2 p){
    if(!ClearLabelPoint(p))return;
    float cost=(p-desired).sqrMagnitude;
    if(trackedLabels.TryGetValue(meteor,out var previous))cost+=(p-previous).sqrMagnitude*2;
    if(cost<best){best=cost;chosen=p;}
   }
   // Bounded perimeter search, outside the aim region. A relocated label keeps its
   // slot until its target disappears, so rank changes and moving targets cannot oscillate it.
   for(float y=Mathf.Max(safe.yMin+1,-root.rect.height*.06f);y<Mathf.Min(safe.yMax,root.rect.height*.28f);y+=2*LabelHalfHeight+LabelGap){
    Candidate(new Vector2(safe.xMin+1,y));Candidate(new Vector2(safe.xMax-1,y));
   }
   parkedLabels[meteor]=chosen;return Occupy(chosen,meteor);
  }
  private Vector2 Occupy(Vector2 point,MeteorController meteor){
   trackedLabels[meteor]=point;
   occupiedRegions[occupiedCount++]=new Rect(point.x-2*LabelHalfWidth-LabelGap,point.y-2*LabelHalfHeight-LabelGap,4*LabelHalfWidth+2*LabelGap,4*LabelHalfHeight+2*LabelGap);
   return point;
  }
  private void ClearProtectedLabels(){parkedLabels.Clear();trackedLabels.Clear();}
 }
}
