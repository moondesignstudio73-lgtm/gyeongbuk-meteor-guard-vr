using System.Collections.Generic;
using MeteorDefenseVR.Meteor;
using UnityEngine;
namespace MeteorDefenseVR.PcInput {
 public sealed partial class ThreatIndicatorView {
  private SfPresentationDirector noticeDirector;

  private readonly Vector3[] noticeCorners=new Vector3[4];
  private Rect noticeRegion;
  private bool noticeReserved;
  private void RefreshCriticalNoticeRegion() {
   var bounds=noticeDirector!=null?noticeDirector.CriticalNoticeBounds:null;
   noticeReserved=bounds!=null&&bounds.gameObject.activeInHierarchy;
   if(!noticeReserved)return;
   bounds.GetWorldCorners(noticeCorners);
   Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
   foreach(var corner in noticeCorners) {
    Vector3 p=view.Camera.WorldToViewportPoint(corner);
    Vector2 local=new Vector2((p.x-.5f)*root.rect.width,(p.y-.5f)*root.rect.height);
    min=Vector2.Min(min,local);max=Vector2.Max(max,local);
   }
   // Includes the full fixed-size arrow/distance label at its maximum urgency scale.
   noticeRegion=Rect.MinMaxRect(min.x-104,min.y-43,max.x+104,max.y+43);
  }
 }
}
