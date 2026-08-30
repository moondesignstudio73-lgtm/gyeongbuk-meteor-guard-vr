using System;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.Editor
{
    public static class CommercialVisualPolishSetup
    {
        private const string ScenePath="Assets/_Project/Scenes/MeteorDefense.unity";
        private const string Root="Assets/Art/Premium";
        private const string CockpitVisualPrefab=Root+"/Prefabs/CockpitVisual.prefab";
        private static Material metal,dark,edge,cyan,amber,red,canopy,radarGlass,trail;
        private static TMP_FontAsset font;

        public static void EnsureCurrentScene()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Directory.CreateDirectory(Root+"/Materials"); Directory.CreateDirectory(Root+"/Meshes");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset");
            LoadMaterials();
            UpgradeCockpit(); UpgradeEarth(); UpgradeMeteors();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[CommercialVisualPolish] Cockpit, telemetry, canopy, Earth and meteor LOD integration complete.");
        }

        public static void RestoreClassicCockpitOnly()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Meshes");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset");
            LoadMaterials();UpgradeCockpit();
            Transform geometry=GameObject.Find("LaunchSystem")?.transform.Find("CockpitGeometry");
            Transform premium=geometry?.Find("PremiumCockpit");if(premium!=null)premium.gameObject.SetActive(true);
            Transform panoramic=geometry?.Find("PanoramicCanopy");
            if(panoramic!=null)
            {
                foreach(Transform child in panoramic)
                {
                    bool keep=child.name=="CanopyGlass360";
                    child.gameObject.SetActive(keep);
                }
            }
            SaveCockpitVisual(geometry?.Find("PremiumCockpit"));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[ClassicCockpit] Five-monitor visual restored, closed rear cockpit rebuilt, live radar retained, and status data moved to side holograms.");
        }

        public static void UpgradeRearPanoramaOnly()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Meshes");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset");
            LoadMaterials();
            Transform cockpit=GameObject.Find("LaunchSystem")?.transform.Find("CockpitGeometry/PremiumCockpit");
            if(cockpit==null)throw new InvalidOperationException("PremiumCockpit was not found.");
            Remove(cockpit,"RestoredRearCockpit");
            CreateRearCockpit(cockpit);
            SaveCockpitVisual(cockpit);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            CockpitDamageSetup.ApplyAndBake();
            Debug.Log("[RearPanorama] Existing cockpit preserved; rear/side solid walls reduced, observation glass expanded, and directional rear damage anchors baked.");
        }

        private static void LoadMaterials()
        {
            metal=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Titanium.mat");
            dark=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/AnodizedCarbon.mat");
            edge=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MachinedEdges.mat");
            cyan=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/InstrumentCyan.mat");
            amber=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/SafetyAmber.mat");
            red=Unlit("WarningRed",new Color(3.4f,.085f,.018f,1));
            radarGlass=Unlit("RadarBlackGlass",new Color(.002f,.016f,.021f,1));
            canopy=Material("CockpitCanopy","MeteorDefense/Cockpit Canopy");
            canopy.SetColor("_Tint",new Color(.018f,.09f,.12f,.032f));canopy.SetColor("_EdgeColor",new Color(.06f,.62f,.9f,.19f));canopy.SetFloat("_Dust",.055f);canopy.SetFloat("_Scratch",.07f);
            trail=Material("MeteorIonTrail","MeteorDefense/Soft Energy"); trail.SetColor("_BaseColor",new Color(.16f,.52f,1,.22f)); trail.SetFloat("_Ring",-1);
        }

        private static void UpgradeCockpit()
        {
            Transform cockpit=GameObject.Find("LaunchSystem")?.transform.Find("CockpitGeometry/PremiumCockpit");
            if(cockpit==null) throw new InvalidOperationException("PremiumCockpit was not found. Run the established visual integration first.");
            Remove(cockpit,"CommercialMilitaryDetail"); Remove(cockpit,"CommercialCanopy"); Remove(cockpit,"LiveAvionics"); Remove(cockpit,"RestoredRearCockpit");
            Transform oldSystems=cockpit.parent.Find("CockpitSystems");if(oldSystems!=null)UnityEngine.Object.DestroyImmediate(oldSystems.gameObject);
            Transform detail=Child(cockpit,"CommercialMilitaryDetail");

            // Dense peripheral silhouette; the central 38-degree gaze cone stays open.
            Panel(detail,"LowerKneePanel",new Vector3(0,-1.45f,1.8f),new Vector3(1.05f,.32f,.4f),metal,new Vector3(48,0,0),.035f);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"PORT":"STBD";
                Beam(detail,s+"OuterRib",new Vector3(side*2.12f,-.66f,2.15f),new Vector3(side*3.25f,2.7f,2.34f),.11f,.22f,dark);
                Beam(detail,s+"InnerRib",new Vector3(side*1.86f,-.57f,2.1f),new Vector3(side*2.88f,2.58f,2.25f),.035f,.07f,edge);
                Panel(detail,s+"ShoulderArmor",new Vector3(side*2.02f,-1.03f,1.95f),new Vector3(.72f,.52f,.46f),metal,new Vector3(22,side*-18,side*-4),.045f);
                Panel(detail,s+"AccessPanel",new Vector3(side*1.67f,-1.22f,1.41f),new Vector3(.55f,.22f,.06f),dark,new Vector3(27,side*-18,0),.018f);
                Panel(detail,s+"HazardMark",new Vector3(side*1.67f,-1.205f,1.371f),new Vector3(.34f,.018f,.008f),amber,new Vector3(27,side*-18,0),.002f);
                for(int i=0;i<6;i++)
                {
                    float x=side*(.65f+i*.17f); Panel(detail,s+"Breaker"+i,new Vector3(x,-1.30f,1.30f),new Vector3(.09f,.075f,.065f),i==5?red:edge,new Vector3(28,0,0),.008f);
                }
                for(int i=0;i<8;i++)
                {
                    float y=-1.31f+i*.095f; Panel(detail,s+"SeamBolt"+i,new Vector3(side*2.28f,y,1.68f),new Vector3(.024f,.024f,.012f),edge,new Vector3(0,side*15,0),.003f);
                }
                // Protected illumination: dark housing, inset emitter, and a broad low-alpha spill card.
                Panel(detail,s+"WashHousing",new Vector3(side*1.76f,-.73f,1.78f),new Vector3(.34f,.08f,.08f),dark,new Vector3(18,side*-18,0),.009f);
                Panel(detail,s+"WashEmitter",new Vector3(side*1.76f,-.708f,1.737f),new Vector3(.25f,.018f,.008f),cyan,new Vector3(18,side*-18,0),.001f);
                Beam(detail,s+"CableA",new Vector3(side*2.26f,-1.17f,1.53f),new Vector3(side*2.53f,-.56f,1.98f),.026f,.026f,dark);
                Beam(detail,s+"CableB",new Vector3(side*2.34f,-1.18f,1.51f),new Vector3(side*2.62f,-.50f,2.01f),.018f,.018f,edge);
            }
            Panel(detail,"OverheadArmor",new Vector3(0,2.72f,2.37f),new Vector3(2.9f,.24f,.23f),dark,new Vector3(0,0,0),.035f);
            for(int i=-3;i<=3;i++) Panel(detail,"OverheadKey"+(i+3),new Vector3(i*.28f,2.60f,2.24f),new Vector3(.12f,.05f,.055f),i==0?red:(Mathf.Abs(i)==3?amber:edge),default,.008f);
            Combine(detail,"CommercialCockpit_");

            Transform glassRoot=Child(cockpit,"CommercialCanopy");
            Mesh pane=PremiumMeshBuilder.Save("CanopySidePane",SidePaneMesh());
            Transform left=MeshObject(glassRoot,"CanopyGlass_Port",pane,canopy,Vector3.zero); left.localScale=new Vector3(1,1,1);
            Transform right=MeshObject(glassRoot,"CanopyGlass_Starboard",pane,canopy,Vector3.zero); right.localScale=new Vector3(-1,1,1);

            CreateRearCockpit(cockpit);
            CreateLiveAvionics(cockpit);
            SaveCockpitVisual(cockpit);
        }

        private static void CreateRearCockpit(Transform cockpit)
        {
            Transform rear=Child(cockpit,"RestoredRearCockpit");

            // Preserve the pressure-shell skeleton while turning its solid infill into a
            // panoramic canopy. The narrow metal bands remain readable from every heading.
            Panel(rear,"Floor_Port",new Vector3(-1.62f,-1.13f,-.72f),new Vector3(1.02f,.13f,3.25f),metal,default,.030f);
            Panel(rear,"Floor_Starboard",new Vector3(1.62f,-1.13f,-.72f),new Vector3(1.02f,.13f,3.25f),metal,default,.030f);
            Panel(rear,"Floor_AftBridge",new Vector3(0,-1.13f,-2.22f),new Vector3(2.22f,.13f,.28f),dark,default,.022f);
            Panel(rear,"Floor_ForwardBridge",new Vector3(0,-1.13f,.76f),new Vector3(2.22f,.13f,.28f),dark,default,.022f);
            Panel(rear,"FloorObservationGlass",new Vector3(0,-1.058f,-.74f),new Vector3(2.15f,.018f,2.84f),canopy,default,.006f);

            Panel(rear,"RearBulkhead_Port",new Vector3(-2.12f,-.69f,-2.47f),new Vector3(.26f,.55f,.20f),metal,default,.030f);
            Panel(rear,"RearBulkhead_Port_Upper",new Vector3(-2.12f,1.17f,-2.47f),new Vector3(.26f,.70f,.20f),metal,default,.030f);
            Panel(rear,"RearBulkhead_Starboard",new Vector3(2.12f,-.69f,-2.47f),new Vector3(.26f,.55f,.20f),metal,default,.030f);
            Panel(rear,"RearBulkhead_Starboard_Upper",new Vector3(2.12f,1.17f,-2.47f),new Vector3(.26f,.70f,.20f),metal,default,.030f);
            Panel(rear,"RearBulkhead_Lower",new Vector3(0,-.96f,-2.47f),new Vector3(4.42f,.30f,.20f),dark,default,.030f);
            Panel(rear,"RearBulkhead_Upper",new Vector3(0,1.55f,-2.47f),new Vector3(4.42f,.28f,.20f),dark,default,.030f);
            Panel(rear,"RearObservationGlass",new Vector3(0,.29f,-2.365f),new Vector3(4.02f,2.10f,.018f),canopy,default,.008f);
            Panel(rear,"RearWindowFrame_Top",new Vector3(0,1.39f,-2.34f),new Vector3(4.18f,.075f,.105f),edge,default,.008f);
            Panel(rear,"RearWindowFrame_Bottom",new Vector3(0,-.82f,-2.34f),new Vector3(4.18f,.075f,.105f),edge,default,.008f);
            // Corner uprights are split around eye level so a threat at exactly +/-135 degrees
            // cannot hide behind the structural seam. The upper/lower nodes remain connected.
            TaperedBeam(rear,"RearWindowFrame_Port_Lower",new Vector3(-2.02f,-.83f,-2.34f),new Vector3(-2.02f,-.24f,-2.34f),.050f,.030f,.052f,metal);
            TaperedBeam(rear,"RearWindowFrame_Port_Upper",new Vector3(-2.02f,.88f,-2.34f),new Vector3(-2.02f,1.41f,-2.34f),.030f,.050f,.052f,metal);
            TaperedBeam(rear,"RearWindowFrame_Starboard_Lower",new Vector3(2.02f,-.83f,-2.34f),new Vector3(2.02f,-.24f,-2.34f),.050f,.030f,.052f,metal);
            TaperedBeam(rear,"RearWindowFrame_Starboard_Upper",new Vector3(2.02f,.88f,-2.34f),new Vector3(2.02f,1.41f,-2.34f),.030f,.050f,.052f,metal);
            TaperedBeam(rear,"RearWindowSplitter_Port",new Vector3(-.73f,-.81f,-2.33f),new Vector3(-.62f,1.38f,-2.33f),.045f,.026f,.046f,edge);
            TaperedBeam(rear,"RearWindowSplitter_Starboard",new Vector3(.73f,-.81f,-2.33f),new Vector3(.62f,1.38f,-2.33f),.045f,.026f,.046f,edge);

            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"Port":"Starboard";
                Panel(rear,s+"SideLowerArmor",new Vector3(side*2.39f,-.96f,-.36f),new Vector3(.17f,.30f,3.55f),metal,default,.028f);
                Panel(rear,s+"SideUpperArmor",new Vector3(side*2.39f,1.52f,-.36f),new Vector3(.17f,.26f,3.55f),dark,default,.028f);
                Panel(rear,s+"ObservationGlass",new Vector3(side*2.295f,.28f,-.36f),new Vector3(.018f,2.12f,3.45f),canopy,default,.006f);
                TaperedBeam(rear,s+"AftFrameLower",new Vector3(side*2.24f,-1.03f,-2.12f),new Vector3(side*2.24f,-.28f,-2.12f),.048f,.028f,.052f,metal);
                TaperedBeam(rear,s+"AftFrameUpper",new Vector3(side*2.24f,.90f,-2.12f),new Vector3(side*2.24f,1.55f,-2.12f),.028f,.048f,.052f,metal);
                TaperedBeam(rear,s+"ForwardFrame",new Vector3(side*2.43f,-1.02f,1.43f),new Vector3(side*2.61f,1.68f,1.43f),.070f,.040f,.080f,metal);
                for(int i=0;i<2;i++)
                {
                    float z=-1.03f+i*1.43f;
                    TaperedBeam(rear,s+"ObservationRib_"+i,new Vector3(side*2.27f,-1.00f,z),new Vector3(side*2.32f,1.50f,z+.13f),.050f,.030f,.052f,edge);
                }
                TaperedBeam(rear,s+"CeilingRail",new Vector3(side*2.09f,1.47f,-2.17f),new Vector3(side*2.63f,2.27f,1.58f),.078f,.045f,.090f,dark);
                TaperedBeam(rear,s+"FloorRail",new Vector3(side*2.05f,-1.06f,-2.17f),new Vector3(side*2.45f,-.96f,1.43f),.078f,.045f,.090f,metal);
                Panel(rear,s+"MechanicalPanel",new Vector3(side*1.73f,-.74f,-2.32f),new Vector3(.46f,.24f,.07f),dark,default,.014f);
                for(int i=0;i<5;i++)Panel(rear,s+"MechanicalVent"+i,new Vector3(side*(1.45f+i*.09f),-.52f,-2.267f),new Vector3(.04f,.22f,.012f),edge,default,.002f);
                Panel(rear,s+"GuideHousing",new Vector3(side*1.35f,-.91f,-2.31f),new Vector3(.70f,.065f,.052f),dark,default,.007f);
                Panel(rear,s+"GuideLight",new Vector3(side*1.35f,-.905f,-2.275f),new Vector3(.56f,.018f,.01f),cyan,default,.001f);
            }
            Panel(rear,"CeilingSpine",new Vector3(0,1.76f,-.40f),new Vector3(.20f,.14f,3.78f),dark,default,.024f);
            Panel(rear,"CeilingObservationGlass_Port",new Vector3(-1.08f,1.67f,-.40f),new Vector3(1.91f,.018f,3.50f),canopy,default,.006f);
            Panel(rear,"CeilingObservationGlass_Starboard",new Vector3(1.08f,1.67f,-.40f),new Vector3(1.91f,.018f,3.50f),canopy,default,.006f);
            for(int i=0;i<3;i++)
            {
                float z=-1.83f+i*1.43f;
                TaperedBeam(rear,"CeilingRib_Port_"+i,new Vector3(-2.05f,1.49f,z),new Vector3(-.12f,1.79f,z),.082f,.045f,.10f,metal);
                TaperedBeam(rear,"CeilingRib_Starboard_"+i,new Vector3(.12f,1.79f,z),new Vector3(2.05f,1.49f,z),.045f,.082f,.10f,metal);
                Panel(rear,"CeilingGuide_"+i,new Vector3(0,1.665f,z),new Vector3(.19f,.026f,.14f),i==0?amber:cyan,default,.003f);
            }
            Combine(rear,"RestoredRearCockpit_");
        }

        private static void CreateLiveAvionics(Transform cockpit)
        {
            Transform systems=Child(cockpit.parent,"CockpitSystems");
            Transform root=Child(systems,"LiveAvionics");
            Transform leftHousing=cockpit.Find("PortPrimary"),rightHousing=cockpit.Find("StarboardPrimary"),nav=cockpit.Find("Navigation");
            Transform leftSecondary=cockpit.Find("PortSecondary"),rightSecondary=cockpit.Find("StarboardSecondary");
            if(leftHousing==null||rightHousing==null||nav==null||leftSecondary==null||rightSecondary==null)throw new InvalidOperationException("Premium avionics housings are missing.");
            Remove(leftHousing,"LiveShipStatus"); Remove(leftHousing,"LiveShipBacking"); Remove(rightHousing,"LiveThreatAnalysis"); Remove(rightHousing,"LiveThreatBacking"); Remove(nav,"LiveRadar"); Remove(nav,"LiveRadarText");
            Remove(leftSecondary,"LiveWeaponStatus");Remove(leftSecondary,"LiveWeaponBacking");Remove(rightSecondary,"LiveSystemsStatus");Remove(rightSecondary,"LiveSystemsBacking");
            EnableReadout(leftHousing); EnableReadout(rightHousing); EnableReadout(leftSecondary); EnableReadout(rightSecondary); DisableReadout(nav);
            var holograms=CreateHologramHud(systems);
            Transform radar=Child(nav,"LiveRadar"); radar.localPosition=new Vector3(0,0,-.069f);
            MeshObject(radar,"RadarBacking",PremiumMeshBuilder.Panel("LiveRadarBacking",.62f,.36f,.006f,.03f,.002f),radarGlass,Vector3.zero);
            Material line=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/HudHairline.mat");
            for(int i=1;i<=3;i++)Ring(radar,"RangeRing"+i,.055f*i,line,.0025f);
            Cross(radar,"Axis",line);
            Transform sweep=Child(radar,"Sweep"); var sweepLine=sweep.gameObject.AddComponent<LineRenderer>(); ConfigureLine(sweepLine,line,.004f,false);
            sweepLine.positionCount=2;sweepLine.SetPosition(0,Vector3.zero);sweepLine.SetPosition(1,new Vector3(0,.19f,-.004f));
            var blips=new Transform[16];
            for(int i=0;i<blips.Length;i++)
            {
                blips[i]=MeshObject(radar,"Contact_"+i,PremiumMeshBuilder.Panel("RadarContact",.015f,.015f,.002f,.003f,.001f),i<2?amber:cyan,Vector3.zero);
                blips[i].gameObject.SetActive(false);
            }
            TMP_Text radarText=Text(nav,"LiveRadarText",.135f,new Vector2(.59f,.37f),new Vector3(0,0,-.078f),TextAlignmentOptions.BottomLeft);
            radarText.color=new Color(.18f,.92f,1);
            var presentation=root.gameObject.AddComponent<CockpitTelemetryPresentation>();
            var spawner=UnityEngine.Object.FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            presentation.Configure(spawner,UnityEngine.Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include),
                UnityEngine.Object.FindAnyObjectByType<GameSessionStats>(FindObjectsInactive.Include),UnityEngine.Object.FindAnyObjectByType<DifficultyManager>(FindObjectsInactive.Include),
                spawner!=null?GameObject.Find("PlayerRig")?.transform:null,holograms.shipText,holograms.threatText,radarText,sweep,blips,holograms.weaponText,holograms.systemsText,holograms.shipGroup,holograms.threatGroup);
        }

        private static (TMP_Text shipText,TMP_Text threatText,TMP_Text weaponText,TMP_Text systemsText,CanvasGroup shipGroup,CanvasGroup threatGroup) CreateHologramHud(Transform systems)
        {
            Transform existing=systems.Find("CockpitHologramHUD");if(existing!=null)UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var go=new GameObject("CockpitHologramHUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));go.transform.SetParent(systems,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Camera.main;canvas.planeDistance=1.25f;canvas.sortingOrder=32;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var left=CreateHologramPanel(go.transform,"ShipStatusHologram",new Vector2(.115f,.47f));
            var right=CreateHologramPanel(go.transform,"ThreatStatusHologram",new Vector2(.885f,.47f));
            TMP_Text ship=HudText(left.root,"LiveShipStatus",new Vector2(-122,78),new Vector2(250,118),17,TextAlignmentOptions.TopLeft);
            TMP_Text threat=HudText(right.root,"LiveThreatAnalysis",new Vector2(-122,78),new Vector2(250,102),17,TextAlignmentOptions.TopLeft);
            TMP_Text weapon=HudText(right.root,"LiveWeaponStatus",new Vector2(-122,-34),new Vector2(250,80),14,TextAlignmentOptions.TopLeft);
            TMP_Text sensor=HudText(left.root,"LiveSystemsStatus",new Vector2(-122,-51),new Vector2(250,52),13,TextAlignmentOptions.TopLeft);
            return(ship,threat,weapon,sensor,left.group,right.group);
        }

        private static (RectTransform root,CanvasGroup group) CreateHologramPanel(Transform parent,string name,Vector2 anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasGroup),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Outline));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(286,228);rect.anchoredPosition=Vector2.zero;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.005f,.055f,.075f,.22f);image.raycastTarget=false;
            var outline=go.GetComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.08f,.88f,1f,.42f);outline.effectDistance=new Vector2(1.5f,-1.5f);outline.useGraphicAlpha=true;
            var group=go.GetComponent<CanvasGroup>();group.alpha=.46f;group.interactable=false;group.blocksRaycasts=false;
            HudLine(rect,"TopLine",new Vector2(0,106),new Vector2(250,2));HudLine(rect,"BottomLine",new Vector2(0,-106),new Vector2(250,2));
            return(rect,group);
        }

        private static void HudLine(RectTransform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.sizeDelta=size;rect.anchoredPosition=position;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.08f,.88f,1f,.58f);image.raycastTarget=false;
        }

        private static TMP_Text HudText(RectTransform parent,string name,Vector2 position,Vector2 size,float fontSize,TextAlignmentOptions alignment)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.pivot=new Vector2(0,1);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=fontSize;text.alignment=alignment;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;text.color=new Color(.2f,.95f,1f);text.overflowMode=TextOverflowModes.Overflow;return text;
        }

        private static void SaveCockpitVisual(Transform cockpit)
        {
            if(cockpit==null)return;Directory.CreateDirectory(Root+"/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(cockpit.gameObject,CockpitVisualPrefab);
        }

        private static void UpgradeEarth()
        {
            Transform earth=GameObject.Find("MissionCompleteSystem")?.transform.Find("EarthFocus"); if(earth==null)return;
            Renderer renderer=earth.GetComponent<Renderer>(); if(renderer==null)return;
            Material old=renderer.sharedMaterial; Material material=Material("OrbitalEarth","MeteorDefense/Orbital Earth");
            if(old!=null&&old.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",old.GetTexture("_BaseMap"));
            material.SetColor("_DayTint",new Color(.82f,.94f,1));material.SetColor("_NightColor",new Color(2.1f,.63f,.1f));
            material.SetColor("_AtmosphereColor",new Color(.04f,.42f,1));material.SetFloat("_CloudStrength",.26f);renderer.sharedMaterial=material;
        }

        private static void UpgradeMeteors()
        {
            string[] kinds={"Normal","Fast","Large","Boss","Tutorial"};
            for(int i=0;i<kinds.Length;i++)
            {
                string path="Assets/_Project/Meteor/Prefabs/Meteor_"+kinds[i]+".prefab"; GameObject root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Remove(root.transform,"LOD_Mid"); Remove(root.transform,"LOD_Far"); Remove(root.transform,"MotionTrail");
                    Renderer high=root.GetComponent<MeshRenderer>(); Vector3 shape=kinds[i]=="Fast"?new Vector3(.76f,.82f,1.28f):kinds[i]=="Large"?new Vector3(1.12f,.88f,1):Vector3.one;
                    Transform mid=MeshObject(root.transform,"LOD_Mid",PremiumMeshBuilder.Rock("Asteroid"+kinds[i]+"_Mid",31+i*11,24,14,shape),high.sharedMaterial,Vector3.zero);
                    Transform far=MeshObject(root.transform,"LOD_Far",PremiumMeshBuilder.Rock("Asteroid"+kinds[i]+"_Far",47+i*13,12,7,shape),high.sharedMaterial,Vector3.zero);
                    var lod=root.GetComponent<LODGroup>(); if(lod==null)lod=root.AddComponent<LODGroup>(); lod.fadeMode=LODFadeMode.CrossFade;lod.animateCrossFading=false;
                    lod.SetLODs(new[]{new LOD(.18f,new[]{high}),new LOD(.055f,new[]{mid.GetComponent<Renderer>()}),new LOD(.012f,new[]{far.GetComponent<Renderer>()})});lod.RecalculateBounds();
                    Transform motion=Child(root.transform,"MotionTrail");var tr=motion.gameObject.AddComponent<TrailRenderer>();tr.sharedMaterial=trail;tr.time=kinds[i]=="Fast"?.24f:.13f;
                    float width=kinds[i]=="Boss"?.12f:kinds[i]=="Large"?.065f:.035f;tr.startWidth=width;tr.endWidth=0;tr.minVertexDistance=.12f;tr.shadowCastingMode=ShadowCastingMode.Off;tr.receiveShadows=false;tr.emitting=false;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(kinds[i]=="Boss"?new Color(1,.28f,.04f):new Color(.15f,.72f,1),0),new GradientColorKey(new Color(.02f,.12f,.2f),1)},new[]{new GradientAlphaKey(.18f,0),new GradientAlphaKey(0,1)});tr.colorGradient=gradient;
                    var lifecycle=motion.gameObject.AddComponent<MeteorMotionTrail>();lifecycle.Configure(root.GetComponent<MeteorController>(),tr);
                    var boss=root.GetComponent<BossMeteorView>();if(boss!=null)boss.Configure(root.GetComponent<MeteorController>(),new[]{high,mid.GetComponent<Renderer>(),far.GetComponent<Renderer>()},root.GetComponentInChildren<Light>(true),root.GetComponent<AudioSource>());
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }

        private static Mesh SidePaneMesh()
        {
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(.38f,-.47f,2.2f),new Vector3(.46f,2.35f,2.27f),new Vector3(2.85f,2.58f,2.34f),new Vector3(1.88f,-.53f,2.13f)};
            mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static TMP_Text Text(Transform parent,string name,float size,Vector2 bounds,Vector3 position,TextAlignmentOptions alignment)
        {
            Transform t=Child(parent,name);t.localPosition=position;var text=t.gameObject.AddComponent<TextMeshPro>();text.font=font;text.fontSize=size;text.alignment=alignment;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;text.rectTransform.sizeDelta=bounds;text.fontStyle=FontStyles.Normal;text.lineSpacing=-18;text.overflowMode=TextOverflowModes.Truncate;return text;
        }
        private static void DisableReadout(Transform housing){var readout=housing.Find("Readout");if(readout!=null&&readout.TryGetComponent(out Renderer r))r.enabled=false;}
        private static void EnableReadout(Transform housing){var readout=housing.Find("Readout");if(readout!=null&&readout.TryGetComponent(out Renderer r))r.enabled=true;}
        private static void Cross(Transform root,string name,Material material)
        {
            Transform t=Child(root,name);var line=t.gameObject.AddComponent<LineRenderer>();ConfigureLine(line,material,.0015f,false);line.positionCount=5;
            line.SetPositions(new[]{new Vector3(-.18f,0,-.003f),new Vector3(.18f,0,-.003f),new Vector3(0,0,-.003f),new Vector3(0,-.18f,-.003f),new Vector3(0,.18f,-.003f)});
        }
        private static void Ring(Transform root,string name,float radius,Material material,float width)
        {Transform t=Child(root,name);var line=t.gameObject.AddComponent<LineRenderer>();ConfigureLine(line,material,width,true);line.positionCount=48;for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-.003f));}}
        private static void ConfigureLine(LineRenderer line,Material material,float width,bool loop){line.sharedMaterial=material;line.useWorldSpace=false;line.loop=loop;line.widthMultiplier=width;line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;}
        private static Material Material(string name,string shaderName){string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);Shader shader=Shader.Find(shaderName);if(shader==null)throw new InvalidOperationException("Shader not found: "+shaderName);if(m==null){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}else m.shader=shader;m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        private static Material Unlit(string name,Color color){var m=Material(name,"Universal Render Pipeline/Unlit");m.SetColor("_BaseColor",color);return m;}
        private static Transform Child(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;return t;}
        private static void Remove(Transform parent,string name){Transform t=parent.Find(name);if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);}
        private static Transform MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 position){Transform t=Child(parent,name);t.localPosition=position;var f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;}
        private static Transform Panel(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,Vector3 rot=default,float bevel=.015f){string key="Commercial_"+name+"_"+size.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);Transform t=MeshObject(parent,name,PremiumMeshBuilder.Panel(key,size.x,size.y,size.z,Mathf.Min(size.x,size.y)*.12f,Mathf.Min(bevel,Mathf.Min(size.x,size.y)*.06f)),mat,pos);t.localRotation=Quaternion.Euler(rot);return t;}
        private static void Beam(Transform root,string name,Vector3 a,Vector3 b,float w,float d,Material mat){var t=Panel(root,name,(a+b)*.5f,new Vector3(w,(b-a).magnitude,d),mat);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static void TaperedBeam(Transform root,string name,Vector3 a,Vector3 b,float startWidth,float endWidth,float depth,Material mat)
        {
            float length=(b-a).magnitude,sa=startWidth*.5f,sb=endWidth*.5f,sd=depth*.5f;
            string key="Tapered_"+name+"_"+startWidth.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+endWidth.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+length.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
            var mesh=new Mesh{name=key};
            mesh.vertices=new[]{new Vector3(-sa,-length*.5f,-sd),new Vector3(sa,-length*.5f,-sd),new Vector3(sa,-length*.5f,sd),new Vector3(-sa,-length*.5f,sd),new Vector3(-sb,length*.5f,-sd),new Vector3(sb,length*.5f,-sd),new Vector3(sb,length*.5f,sd),new Vector3(-sb,length*.5f,sd)};
            mesh.triangles=new[]{0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0,4,7,6,4,6,5,0,1,2,0,2,3};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            Transform t=MeshObject(root,name,PremiumMeshBuilder.Save(key,mesh),mat,(a+b)*.5f);
            t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        private static void Combine(Transform root,string prefix)
        {
            Transform combined=Child(root,"CombinedMeshes");var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&!r.transform.IsChildOf(combined)).ToArray();
            foreach(var group in renderers.GroupBy(r=>r.sharedMaterial))
            {var instances=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(instances,true,true);MeshObject(combined,group.Key.name,PremiumMeshBuilder.Save(prefix+group.Key.name,mesh),group.Key,Vector3.zero);foreach(var r in group)r.enabled=false;}
        }
    }
}
