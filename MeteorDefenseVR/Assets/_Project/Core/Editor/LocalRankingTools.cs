using MeteorDefenseVR.PcInput;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class LocalRankingTools
    {
        [MenuItem("Meteor Defense/Local Ranking/Clear local records...")]
        public static void ClearRecords()
        {
            if(!EditorUtility.DisplayDialog("로컬 랭킹 초기화","이 PC의 보관 기록과 복구 사본을 모두 초기화합니다. 계속할까요?","초기화","취소")) return;
            var controller=Application.isPlaying?Object.FindAnyObjectByType<LocalRankingController>():null;
            bool success;
            if(controller!=null) success=controller.ClearHistory();
            else { var store=new LocalRankingStore(LocalRankingController.StoragePath); store.Load(); success=store.Clear(); }
            EditorUtility.DisplayDialog("로컬 랭킹",success?"기록과 복구 사본을 초기화했습니다.":"초기화하지 못했습니다. 게임 종료 후 운영 안내의 파일 정리 절차를 확인하세요.","확인");
        }
    }
}
