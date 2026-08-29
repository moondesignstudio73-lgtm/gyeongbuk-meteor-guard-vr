using System;
using System.Collections.Generic;
using System.Globalization;
using MeteorDefenseVR.Difficulty;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // One prewarmed panel; tabs only change text/visibility, never the game state or ranking data.
    public sealed class HangarRecordView
    {
        public enum RecordTab { Recent, TopTen, Best }
        private GameObject root,recent,top,best;
        private TextMeshPro score,rank,timestamp,status,pageText,bestRank;
        private readonly TextMeshPro[] metrics=new TextMeshPro[6];
        private readonly TextMeshPro[,] rows=new TextMeshPro[5,5];
        private readonly TextMeshPro[] bestRows=new TextMeshPro[4];
        private readonly TextMeshPro[] tabLabels=new TextMeshPro[3];
        private HangarMenuButton next;
        private int page;
        private bool? shownPersistence;
        public RecordTab Tab { get; private set; }
        public HangarMenuButton[] TabButtons { get; private set; }
        public HangarMenuButton BackButton { get; private set; }
        public HangarMenuButton NextButton => next;
        public bool IsVisible => root.activeInHierarchy;
        public string DisplayedScore => score.text;

        public void Build(Transform panel,TMP_FontAsset font,Action<HangarLobbyController.MenuAction> action)
        {
            root=HangarLobbyView.Group("MissionRecords",panel);
            TabButtons=new HangarMenuButton[3];
            string[] labels={"최근 기록","TOP 10","난이도별 최고"};
            var actions=new[] { HangarLobbyController.MenuAction.RecentRecords,HangarLobbyController.MenuAction.TopRecords,HangarLobbyController.MenuAction.BestRecords };
            for(int i=0;i<3;i++)
            {
                var command=actions[i];
                TabButtons[i]=HangarLobbyView.Button(root.transform,"RecordTab"+i,labels[i],new Vector2((i-1)*196,92),font,()=>action(command),new Vector2(186,44));
                tabLabels[i]=TabButtons[i].GetComponentInChildren<TextMeshPro>();
            }
            recent=HangarLobbyView.Group("RecentRecord",root.transform);
            top=HangarLobbyView.Group("TopTen",root.transform); best=HangarLobbyView.Group("DifficultyBests",root.transform);
            score=Text(recent.transform,"Score","SCORE —",new Vector2(-95,42),340,385,font);
            rank=Text(recent.transform,"Rank","RANK —",new Vector2(215,42),310,155,font); rank.color=new Color(1,.8f,.43f);
            for(int i=0;i<6;i++) metrics[i]=Text(recent.transform,"Metric"+i,"—",new Vector2(i%2==0?-150:150,1-(i/2)*36),190,280,font);
            timestamp=Text(recent.transform,"Timestamp","플레이 후 기록을 확인하세요",new Vector2(0,-107),155,590,font);
            string[] columns={"순위","SCORE","난이도","명중률","HP"}; float[] x={-268,-157,-21,112,249}; float[] widths={55,160,100,100,84};
            for(int c=0;c<5;c++)
            {
                Text(top.transform,"Column"+c,columns[c],new Vector2(x[c],57),145,widths[c],font);
                for(int r=0;r<5;r++) rows[r,c]=Text(top.transform,"Row"+r+"Cell"+c,"—",new Vector2(x[c],29-r*29),185,widths[c],font);
            }
            pageText=Text(top.transform,"Page","TOP 10 / 1–5",new Vector2(0,-111),145,570,font);
            bestRank=Text(best.transform,"HighestRank","HIGHEST RANK —",new Vector2(0,40),300,570,font); bestRank.color=new Color(1,.8f,.43f);
            for(int i=0;i<bestRows.Length;i++) bestRows[i]=Text(best.transform,"Best"+i,"—",new Vector2(0,1-i*33),190,570,font);
            status=Text(root.transform,"StorageNotice","",new Vector2(0,-179),120,580,font);
            BackButton=HangarLobbyView.Button(root.transform,"RecordsBack","뒤로가기",new Vector2(-145,-148),font,()=>action(HangarLobbyController.MenuAction.Back),new Vector2(260,48));
            next=HangarLobbyView.Button(root.transform,"RecordsNext","6–10위 보기",new Vector2(145,-148),font,()=>action(HangarLobbyController.MenuAction.NextRecordPage),new Vector2(260,48));
            root.SetActive(false);
        }
        public void SetVisible(bool visible) => root.SetActive(visible);
        public void NextPage(LocalRankingStore store) { page=1-page; Draw(store); }
        public void Present(LocalRankingStore store,RecordTab tab) { Tab=tab; page=0; Draw(store); }
        private void Draw(LocalRankingStore store)
        {
            recent.SetActive(Tab==RecordTab.Recent); top.SetActive(Tab==RecordTab.TopTen); best.SetActive(Tab==RecordTab.Best);
            for(int i=0;i<3;i++) tabLabels[i].color=i==(int)Tab?new Color(.4f,1,.75f):new Color(.8f,.97f,1);
            shownPersistence=null; RefreshPersistenceNotice(store);
            next.gameObject.SetActive(Tab==RecordTab.TopTen && store.Records.Count>5);
            next.GetComponentInChildren<TextMeshPro>(true).text=page==0?"6–10위 보기":"1–5위 보기";
            if(Tab==RecordTab.Recent) DrawRecent(store.Latest);
            if(Tab==RecordTab.TopTen) DrawTop(store.TopTen());
            if(Tab==RecordTab.Best)
            {
                bestRank.text="HIGHEST RANK  "+store.HighestRank;
                for(int i=0;i<bestRows.Length;i++)
                {
                    var level=(DifficultyLevel)i; var item=store.Best(level);
                    bestRows[i].text=DifficultyConfig.Label(level)+"   /   "+(item==null?"아직 기록이 없습니다":item.Score.ToString("N0")+"   /   RANK "+item.Rank);
                }
            }
        }
        private void DrawRecent(LocalMissionRecord item)
        {
            score.text=item==null?"아직 기록이 없습니다":"SCORE "+item.Score.ToString("N0"); rank.text="RANK "+(item?.Rank??"—");
            string[] values=item==null?new[] {"파괴 —","명중률 —","최종 HP —","난이도 —","플레이 시간 —","최고 Combo —"}
                :new[] {"파괴 "+item.Destroyed,"명중률 "+Percent(item.Accuracy),"최종 HP "+item.RemainingHP.ToString("0.#"),
                    "난이도 "+DifficultyConfig.Label(item.Difficulty)+(item.MaxStage>0?" · STAGE "+item.MaxStage.ToString("D2"):""),"플레이 시간 "+FormatPlayTime(item.PlayTime),"최고 Combo "+item.Combo};
            for(int i=0;i<6;i++) metrics[i].text=values[i];
            timestamp.text=item==null?"임무를 완료하면 이곳에 기록됩니다":DateTimeOffset.ParseExact(item.Timestamp,"O",CultureInfo.InvariantCulture).ToLocalTime().ToString("yyyy.MM.dd  HH:mm:ss");
        }
        private void DrawTop(List<LocalMissionRecord> items)
        {
            pageText.text=items.Count==0?"임무를 완료하고 첫 기록에 도전하세요":"TOP 10   /   "+(page==0?"1–5":"6–10");
            for(int row=0;row<5;row++)
            {
                int index=page*5+row; var item=index<items.Count?items[index]:null;
                string[] values=item==null?new[] {"—","—","—","—","—"}:new[] {(index+1).ToString(),item.Score.ToString("N0"),DifficultyConfig.Label(item.Difficulty)+(item.MaxStage>0?" S"+item.MaxStage:""),Percent(item.Accuracy),item.RemainingHP.ToString("0.#")};
                for(int c=0;c<5;c++) rows[row,c].text=values[c];
            }
        }
        private static string Percent(float value) => Mathf.RoundToInt(value*100)+"%";
        public static string FormatPlayTime(float seconds)
        {
            int total=Mathf.Max(0,(int)seconds);
            return total>=3600?$"{total/3600:00}:{total/60%60:00}:{total%60:00}":$"{total/60:00}:{total%60:00}";
        }
        public void RefreshPersistenceNotice(LocalRankingStore store)
        {
            if(shownPersistence==store.PersistenceAvailable) return;
            shownPersistence=store.PersistenceAvailable;
            status.text=store.PersistenceAvailable?"이 기기의 최근 "+store.Capacity+"개 기록 / 이름·영상 저장 없음":"저장할 수 없어 이번 실행에서만 기록을 확인할 수 있습니다";
        }
        private static TextMeshPro Text(Transform parent,string name,string content,Vector2 position,float size,float width,TMP_FontAsset font)
        { var label=HangarLobbyView.Label(parent,name,content,position,size,font); label.rectTransform.sizeDelta=new Vector2(width,36); return label; }
    }
}
