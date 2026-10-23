using System.Collections.Generic;
using MeteorDefenseVR.Meteor;
using UnityEngine;
namespace MeteorDefenseVR.PcInput {
 public sealed partial class ThreatIndicatorView {
  private SfPresentationDirector noticeDirector;
  private readonly Dictionary<MeteorController,int> noticeSides=new Dictionary<MeteorController,int>();
  private readonly Vector3[] noticeCorners=new Vector3[4];
  private Rect noticeRegion;
  private bool noticeReserved;
  private void RefreshCriticalNoticeRegion() {
   var bounds=noticeDirector!=null?noticeDirector.CriticalNoticeBounds:null;
   noticeReserved=bounds!=null&&bounds.gameObject.activeInHierarchy;
   if(!noticeReserved){noticeSides.Clear();return;}
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
  private Vector2 KeepClearOfCriticalNotice(Vector2 point,MeteorController meteor) {
   if(!noticeReserved||meteor==null)return point;
   if(!noticeSides.TryGetValue(meteor,out int side)) {
    if(!noticeRegion.Contains(point))return point;
    side=point.x<noticeRegion.center.x?-1:1;noticeSides.Add(meteor,side);
   }
   // Keep this side for the warning's lifetime, even when the target crosses the
   // centre line. Only the label moves; bearing, range, target and aiming stay real.
   point.x=side<0?Mathf.Min(point.x,noticeRegion.xMin):Mathf.Max(point.x,noticeRegion.xMax);
   return point;
  }
 }
}
