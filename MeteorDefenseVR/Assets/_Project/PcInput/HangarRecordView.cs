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
            root=HangarLobbyView.Group("MissionRecords",panel); root.transform.localPosition=new Vector3(0,50,0);
            TabButtons=new HangarMenuButton[3];
            string[] labels={"최근 기록","TOP 10","난이도별 최고"};
            var actions=new[] { HangarLobbyController.MenuAction.RecentRecords,HangarLobbyController.MenuAction.TopRecords,HangarLobbyController.MenuAction.BestRecords };
            for(int i=0;i<3;i++)
            {
                var command=actions[i];
                TabButtons[i]=HangarLobbyView.Button(root.transform,"RecordTab"+i,labels[i],new Vector2((i-1)*295,92),font,()=>action(command),new Vector2(280,70));
                tabLabels[i]=TabButtons[i].GetComponentInChildren<TextMeshPro>();
            }
            recent=HangarLobbyView.Group("RecentRecord",root.transform);
            top=HangarLobbyView.Group("TopTen",root.transform); best=HangarLobbyView.Group("DifficultyBests",root.transform);
            score=Text(recent.transform,"Score","점수 —",new Vector2(-95,42),340,385,font);
            rank=Text(recent.transform,"Rank","등급 —",new Vector2(215,42),310,155,font); rank.color=new Color(1,.8f,.43f);
            for(int i=0;i<6;i++) metrics[i]=Text(recent.transform,"Metric"+i,"—",new Vector2(i%2==0?-235:235,1-(i/2)*36),240,430,font);
            timestamp=Text(recent.transform,"Timestamp","플레이 후 기록을 확인하세요",new Vector2(0,-135),260,900,font);
            string[] columns={"순위","점수","난이도","명중률","방어막"}; float[] x={-400,-250,-50,175,375}; float[] widths={90,200,170,160,120};
            for(int c=0;c<5;c++)
            {
                Text(top.transform,"Column"+c,columns[c],new Vector2(x[c],40),220,widths[c],font);
                for(int r=0;r<5;r++) rows[r,c]=Text(top.transform,"Row"+r+"Cell"+c,"—",new Vector2(x[c],12-r*29),240,widths[c],font);
            }
            pageText=Text(top.transform,"Page","TOP 10 / 1–5",new Vector2(0,-135),260,900,font);
            bestRank=Text(best.transform,"HighestRank","최고 등급 —",new Vector2(0,40),300,570,font); bestRank.color=new Color(1,.8f,.43f);
            for(int i=0;i<bestRows.Length;i++) bestRows[i]=Text(best.transform,"Best"+i,"—",new Vector2(0,1-i*33),240,900,font);
            status=Text(root.transform,"StorageNotice","",new Vector2(0,-290),260,900,font);
            BackButton=HangarLobbyView.Button(root.transform,"RecordsBack","뒤로가기",new Vector2(-145,-190),font,()=>action(HangarLobbyController.MenuAction.Back),new Vector2(260,70));
            next=HangarLobbyView.Button(root.transform,"RecordsNext","6–10위 보기",new Vector2(145,-190),font,()=>action(HangarLobbyController.MenuAction.NextRecordPage),new Vector2(260,70));
            status.textWrappingMode=TextWrappingModes.Normal; status.rectTransform.sizeDelta=new Vector2(900,90);
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
                bestRank.text="최고 등급  "+store.HighestRank;
                for(int i=0;i<bestRows.Length;i++)
                {
                    var level=(DifficultyLevel)i; var item=store.Best(level);
                    bestRows[i].text=DifficultyConfig.Label(level)+"   /   "+(item==null?"아직 기록이 없어요":item.Score.ToString("N0")+"점   /   등급 "+item.Rank);
                }
            }
        }
        private void DrawRecent(LocalMissionRecord item)
        {
            score.text=item==null?"아직 기록이 없어요":"점수 "+item.Score.ToString("N0"); rank.text="등급 "+(item?.Rank??"—");
            string[] values=item==null?new[] {"파괴 —","명중률 —","남은 방어막 —","난이도 —","플레이 시간 —","최고 연속 성공 —"}
                :new[] {"파괴 "+item.Destroyed,"명중률 "+Percent(item.Accuracy),"남은 방어막 "+item.RemainingHP.ToString("0.#"),
                    "난이도 "+DifficultyConfig.Label(item.Difficulty)+(item.MaxStage>0?" · 구역 "+item.MaxStage.ToString("D2"):""),"플레이 시간 "+FormatPlayTime(item.PlayTime),"최고 연속 성공 "+item.Combo};
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
            status.text=store.PersistenceAvailable?"이 기기에 최근 "+store.Capacity+"번의 도전을 기록해요. 이름과 영상은 저장하지 않아요.":"기록을 저장하지 못했어요. 게임을 닫기 전에 확인해 주세요.";
        }
        private static TextMeshPro Text(Transform parent,string name,string content,Vector2 position,float size,float width,TMP_FontAsset font)
        { var label=HangarLobbyView.Label(parent,name,content,position,size,font); label.rectTransform.sizeDelta=new Vector2(width,36); return label; }
    }
}
