using System.Collections.Generic;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace MeteorDefenseVR.PcInput
{
    // An isolated, pooled miniature set filmed to a texture. Never moves the tracked camera.
    [DisallowMultipleComponent]
    public sealed class BaseFlightCinematic : MonoBehaviour
    {
        public enum Shot { None, Departure, Return }
        public Shot CurrentShot { get; private set; }
        public bool HasReturned { get; private set; }
        public float ReturnProgress => Mathf.Clamp01(elapsed / ReturnSeconds);
        public float DoorOpening { get; private set; }
        public float BlurStrength { get; private set; }
        public const float ReturnSeconds = 5.2f;
        private GameFlowManager flow;
        private LaunchSequenceController launch;
        private ResultController result;
        private Transform stage, ship, leftDoor, rightDoor, cameraRig, engines, damage, landingGear;
        private Camera filmCamera;
        private Canvas canvas;
        private CanvasGroup group;
        private UnityEngine.UI.RawImage picture;
        private RenderTexture film;
        private Material blur, hull, trim, lightBlue, orange, dark;
        private readonly List<Object> resources = new();
        private ParticleSystem smoke;
        private Vector3 cameraPosition = new(7, 4, -12);
        private Quaternion cameraRotation;
        private float elapsed, fade;
        private bool xr;
        private const int FilmLayer = 31;

        public void Initialize(GameFlowManager manager, LaunchSequenceController sequence, ResultController results)
        {
            flow = manager; launch = sequence; result = results;
            if (flow == null || launch == null || result == null || Camera.main == null) { enabled = false; return; }
            BuildSet();
            launch.StageChanged += StageChanged;
            flow.OnStateChanged += StateChanged;
            result.BeforeFailedResult = BeginReturn;
            StopShot();
        }

        private void StageChanged(LaunchStage next, string _)
        {
            if (!isActiveAndEnabled) return;
            if (next == LaunchStage.Hangar) Begin(Shot.Departure);
            if (next == LaunchStage.Idle) StopShot();
        }
        private void StateChanged(GameState state)
        {
            if (state == GameState.Boot || state == GameState.Reset) HasReturned = false;
            if (CurrentShot == Shot.None && state != GameState.Result) StopShot();
            if (CurrentShot == Shot.Return && state != GameState.Result) StopShot();
            if (CurrentShot == Shot.Departure && state != GameState.Launch && state != GameState.Countdown) StopShot();
        }
        private bool BeginReturn()
        {
            if (!isActiveAndEnabled || stage == null) return false;
            Begin(Shot.Return);
            return true;
        }
        private void Begin(Shot shot)
        {
            CurrentShot = shot; elapsed = fade = 0; HasReturned = false;
            xr = XRSettings.isDeviceActive || Camera.main.stereoEnabled;
            filmCamera.enabled = true; stage.gameObject.SetActive(true); canvas.gameObject.SetActive(true);
            canvas.sortingOrder = 270;
            group.alpha = 0;
            damage.gameObject.SetActive(shot == Shot.Return);
            landingGear.gameObject.SetActive(shot == Shot.Return);
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (shot == Shot.Return) smoke.Play();
            // In VR, a stationary cinema panel replaces forced camera motion.
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(Camera.main.transform, false);
            rect.sizeDelta = new Vector2(1280, 720);
            rect.localPosition = new Vector3(0, 0, xr ? 2.4f : .32f);
            rect.localRotation = Quaternion.identity;
            float height = xr ? 1.7f : 2f * .32f * Mathf.Tan(Camera.main.fieldOfView * Mathf.Deg2Rad * .5f);
            rect.localScale = Vector3.one * (height / 720f);
            if (!xr) rect.sizeDelta = new Vector2(720f * Camera.main.aspect, 720);
            filmCamera.aspect = rect.sizeDelta.x / rect.sizeDelta.y;
            AnimateShot(0);
        }

        private void LateUpdate()
        {
            if (CurrentShot == Shot.None || Time.timeScale == 0) return;
            elapsed += Time.unscaledDeltaTime;
            AnimateShot(Time.unscaledDeltaTime);
        }
        private void AnimateShot(float delta)
        {
            float speed = 0;
            Vector3 shotCamera = cameraPosition;
            float alpha = Mathf.Clamp01(elapsed / .2f);
            if (CurrentShot == Shot.Departure)
            {
                float t = launch.SpeedAmount;
                DoorOpening = launch.DoorOpenAmount;
                speed = launch.CurrentStage == LaunchStage.Launch ? t : 0;
                ship.localRotation = Quaternion.Euler(0, 0, -t * 4);
                ship.localPosition = Vector3.Lerp(new Vector3(0, .6f, 23), cameraPosition + new Vector3(-1, -.8f, -4), t * t);
                engines.localScale = new Vector3(1, 1, Mathf.Lerp(.25f, 3.5f, launch.EngineAmount * (.3f + .7f * t)));
                if (launch.CurrentStage >= LaunchStage.Space)
                {
                    fade += delta / .45f;
                    alpha = 1 - Mathf.Clamp01(fade);
                    if (fade >= 1) { StopShot(); return; }
                }
            }
            else
            {
                float t = ReturnProgress;
                float approach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .58f));
                float landing = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .58f) / .18f));
                ship.localPosition = Vector3.Lerp(new Vector3(4, 1.6f, 2), new Vector3(0, .9f, 23), approach) + Vector3.down * landing * 1.15f;
                ship.localRotation = Quaternion.Euler(0, 180, Mathf.Sin(t * 13) * 4 * (1 - landing));
                engines.localScale = new Vector3(1, 1, Mathf.Lerp(.6f, .08f, landing));
                DoorOpening = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .76f) / .2f));
                float dolly = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .25f) / .3f))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .71f) / .16f)));
                shotCamera = Vector3.Lerp(cameraPosition, new Vector3(4, 3, 6), dolly);
                if (t > .96f) alpha = 1 - (t - .96f) / .04f;
                if (t >= 1)
                {
                    HasReturned = true;
                    StopShot();
                    canvas.sortingOrder = 80;
                    canvas.gameObject.SetActive(true);
                    group.alpha = 1;
                    if (flow.CurrentState == GameState.Result && result.IsPresentationPending) result.RevealPreparedResult();
                    return;
                }
            }
            leftDoor.localPosition = new Vector3(-3.5f - DoorOpening * 7, 2, 10);
            rightDoor.localPosition = new Vector3(3.5f + DoorOpening * 7, 2, 10);
            group.alpha = Mathf.Clamp01(alpha);
            float shake = xr ? 0 : speed * speed;
            cameraRig.localPosition = shotCamera + new Vector3(Mathf.Sin(elapsed * 73), Mathf.Sin(elapsed * 57), 0) * (.055f * shake);
            cameraRig.localRotation = Quaternion.LookRotation(new Vector3(0, .5f, 20) - shotCamera)
                * Quaternion.Euler(Mathf.Sin(elapsed * 51) * shake * .35f, 0, Mathf.Sin(elapsed * 67) * shake * .22f);
            BlurStrength = xr ? 0 : speed * .024f;
            if (blur != null) blur.SetFloat("_Strength", BlurStrength);
        }

        private void StopShot()
        {
            CurrentShot = Shot.None; BlurStrength = 0;
            if (filmCamera != null) filmCamera.enabled = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (stage != null) stage.gameObject.SetActive(false);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void DismissDockedBackdrop() { if (CurrentShot == Shot.None) StopShot(); }
        private void OnDisable()
        {
            StopShot();
            if (result != null && result.IsPresentationPending) result.RevealPreparedResult();
        }
        private void OnDestroy()
        {
            if (launch != null) launch.StageChanged -= StageChanged;
            if (flow != null) flow.OnStateChanged -= StateChanged;
            if (result != null && result.BeforeFailedResult == BeginReturn) result.BeforeFailedResult = null;
            if (canvas != null) Destroy(canvas.gameObject);
            if (stage != null) Destroy(stage.gameObject);
            if (film != null) film.Release();
            foreach (Object resource in resources) if (resource != null) Destroy(resource);
        }

        private void BuildSet()
        {
            stage = Node("BaseFlightSet", null);
            stage.position = new Vector3(10000, 10000, 10000);
            hull = Paint("Hull", new Color(.19f, .27f, .36f), true);
            trim = Paint("Armor", new Color(.52f, .62f, .7f), true);
            dark = Paint("Hangar", new Color(.035f, .065f, .095f), true);
            lightBlue = Paint("Ion", new Color(.12f, .85f, 1), false);
            orange = Paint("Damage", new Color(1, .3f, .045f), false);
            Box("Floor", stage, new Vector3(0, -2, 23), new Vector3(18, .5f, 30), dark);
            Box("BackWall", stage, new Vector3(0, 3, 38), new Vector3(18, 10, .7f), dark);
            Box("Ceiling", stage, new Vector3(0, 7, 23), new Vector3(18, .5f, 30), dark);
            Box("UpperBaseArmor", stage, new Vector3(0, 10, 13), new Vector3(32, 7, 4), dark);
            Box("LowerBaseArmor", stage, new Vector3(0, -5, 13), new Vector3(32, 6, 4), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Wall", stage, new Vector3(side * 9, 2.5f, 23), new Vector3(.5f, 9, 30), dark);
                Box("DoorPillar", stage, new Vector3(side * 8, 2, 10), new Vector3(1, 9, 2), trim);
                Box("DoorLight", stage, new Vector3(side * 7.4f, 2, 8.9f), new Vector3(.12f, 8, .1f), lightBlue);
                Box("OuterArmor", stage, new Vector3(side * 15, 2, 14), new Vector3(9, 9, 4), dark);
                for (int i = 0; i < 4; i++)
                    Box("BaseBeacon", stage, new Vector3(side * (10 + i * 2), 7.2f, 10.8f), new Vector3(.7f, .08f, .1f), lightBlue);
                for (int i = 0; i < 9; i++)
                {
                    Box("RunwayLight", stage, new Vector3(side * 4, -1.7f, 11 + i * 3), new Vector3(.15f, .06f, 1.3f), lightBlue);
                    Box("WallRib", stage, new Vector3(side * 8.5f, 2.5f, 11 + i * 3), new Vector3(.45f, 8, .35f), hull);
                }
            }
            Box("Lintel", stage, new Vector3(0, 6.5f, 10), new Vector3(17, 1, 2), trim);
            leftDoor = Box("LeftDoor", stage, new Vector3(-3.5f, 2, 10), new Vector3(7, 8, .7f), hull);
            rightDoor = Box("RightDoor", stage, new Vector3(3.5f, 2, 10), new Vector3(7, 8, .7f), hull);
            foreach (Transform door in new[] { leftDoor, rightDoor })
                for (int i = 0; i < 5; i++)
                    Box("DoorPanel", door, new Vector3(0, -.4f + i * .2f, -.55f), new Vector3(.94f, .015f, .1f), trim);
            ship = Node("Interceptor", stage);
            Wedge("Fuselage", ship, new Vector3(0, 0, 0), new Vector3(1.4f, .8f, 4.6f), hull);
            Wedge("Wings", ship, new Vector3(0, -.16f, 1.2f), new Vector3(5.5f, .17f, 2.6f), trim);
            Wedge("Canopy", ship, new Vector3(0, .5f, -.3f), new Vector3(.7f, .45f, 1.7f), lightBlue);
            engines = Node("Exhaust", ship);
            engines.localPosition = new Vector3(0, 0, 2.5f);
            landingGear = Node("LandingGear", ship);
            damage = Node("DamagedArmor", ship);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Nacelle", ship, new Vector3(side * 1.3f, 0, 1.2f), new Vector3(.7f, .7f, 2.6f), hull);
                Box("EngineArmor", ship, new Vector3(side * 1.3f, .38f, 1.2f), new Vector3(.5f, .08f, 2), trim);
                Box("WingStripe", ship, new Vector3(side * 1.8f, -.05f, 1.7f), new Vector3(.5f, .04f, .12f), lightBlue);
                Wedge("TailFin", ship, new Vector3(side * 1.3f, .7f, 1.6f), new Vector3(.15f, 1.3f, 1.6f), trim);
                var plume = GameObject.CreatePrimitive(PrimitiveType.Sphere); plume.name = "IonPlume"; plume.layer = FilmLayer;
                plume.GetComponent<Collider>().enabled = false; Destroy(plume.GetComponent<Collider>());
                plume.transform.SetParent(engines, false); plume.transform.localPosition = new Vector3(side * 1.3f, 0, .7f);
                plume.transform.localScale = new Vector3(.4f, .4f, 1.6f); plume.GetComponent<Renderer>().sharedMaterial = lightBlue;
                Box("LandingStrut", landingGear, new Vector3(side * 1.2f, -.7f, 1), new Vector3(.15f, .8f, .2f), trim);
                Box("LandingSkid", landingGear, new Vector3(side * 1.2f, -1.1f, .8f), new Vector3(.35f, .15f, 1.5f), dark);
                var scar = Box("HotFracture", damage, new Vector3(side * 1.2f, .37f, .7f), new Vector3(.06f, .04f, 1.6f), orange);
                scar.localRotation = Quaternion.Euler(0, side * 30, 0);
            }
            var smokeNode = Node("DamageSmoke", damage); smokeNode.localPosition = new Vector3(1.3f, .5f, 1.5f);
            smoke = smokeNode.gameObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main; main.playOnAwake = false; main.maxParticles = 60; main.startLifetime = 1.4f;
            main.startSpeed = .7f; main.startSize = .65f; main.startColor = new Color(.28f, .3f, .32f, .3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = smoke.emission; emission.rateOverTime = 22;
            var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15; shape.radius = .12f;
            smokeNode.localRotation = Quaternion.Euler(-90, 0, 0);
            // A Resources shader survives player stripping even when no authored particle asset uses it.
            var smokeMaterial = new Material(Resources.Load<Shader>("FlightSmoke"));
            var puff = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float radius = Vector2.Distance(new Vector2(x,y), new Vector2(15.5f,15.5f)) / 15.5f;
                puff.SetPixel(x, y, new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius), 1.6f)));
            }
            puff.Apply(); smokeMaterial.SetTexture("_MainTex", puff); resources.Add(puff); resources.Add(smokeMaterial);
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
            cameraRig = Node("FlightFilmCamera", stage); cameraRig.localPosition = cameraPosition;
            cameraRotation = Quaternion.LookRotation(new Vector3(0, .5f, 20) - cameraPosition);
            cameraRig.localRotation = cameraRotation;
            filmCamera = cameraRig.gameObject.AddComponent<Camera>();
            filmCamera.clearFlags = CameraClearFlags.SolidColor; filmCamera.backgroundColor = new Color(.006f, .014f, .028f);
            filmCamera.cullingMask = 1 << FilmLayer; filmCamera.nearClipPlane = .1f; filmCamera.farClipPlane = 90;
            filmCamera.fieldOfView = 48; filmCamera.depth = -10;
            var data = filmCamera.GetUniversalAdditionalCameraData(); data.allowXRRendering = false; data.renderPostProcessing = false;
            film = new RenderTexture(1280, 720, 24) { name = "BaseFlightFilm" }; film.Create(); resources.Add(film);
            filmCamera.targetTexture = film;
            var key = Node("SetKey", stage).gameObject.AddComponent<Light>(); key.type = LightType.Point; key.range = 60; key.intensity = 12;
            key.color = new Color(.65f,.8f,1); key.cullingMask = 1 << FilmLayer; key.transform.localPosition = new Vector3(0,5,10);
            var fill = Node("SetFill", stage).gameObject.AddComponent<Light>(); fill.type = LightType.Point; fill.range = 30; fill.intensity = 8;
            fill.color = new Color(.25f,.7f,1); fill.cullingMask = 1 << FilmLayer; fill.transform.localPosition = new Vector3(0,4,30);
            var screen = new GameObject("BaseFlightScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvas = screen.GetComponent<Canvas>(); canvas.sortingOrder = 270;
            group = screen.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var image = new GameObject("Film", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            image.transform.SetParent(screen.transform, false);
            var imageRect = image.GetComponent<RectTransform>(); imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one; imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            picture = image.GetComponent<UnityEngine.UI.RawImage>(); picture.texture = film; picture.raycastTarget = false;
            Shader blurShader = Resources.Load<Shader>("FlightVelocityBlur");
            if (blurShader != null) { blur = new Material(blurShader); resources.Add(blur); picture.material = blur; }
        }
        private Transform Node(string label, Transform parent)
        {
            var item = new GameObject(label); item.layer = FilmLayer; item.transform.SetParent(parent, false); return item.transform;
        }
        private Material Paint(string label, Color color, bool lit)
        {
            var material = new Material(Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit"));
            material.name = "Flight_" + label; material.SetColor("_BaseColor", color);
            if (lit) { material.SetFloat("_Metallic", .5f); material.SetFloat("_Smoothness", .65f); }
            resources.Add(material); return material;
        }
        private Transform Box(string label, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = label; item.layer = FilmLayer;
            item.GetComponent<Collider>().enabled = false; Destroy(item.GetComponent<Collider>());
            item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material; return item.transform;
        }
        private void Wedge(string label, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = Node(label, parent); item.localPosition = position; item.localScale = scale;
            var mesh = new Mesh { name = label };
            mesh.vertices = new[] { new Vector3(0,0,-.5f), new Vector3(-.5f,-.3f,.5f), new Vector3(.5f,-.3f,.5f), new Vector3(0,.5f,.25f) };
            mesh.triangles = new[] { 0,2,1, 0,1,3, 0,3,2, 1,2,3 }; mesh.RecalculateNormals(); resources.Add(mesh);
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
