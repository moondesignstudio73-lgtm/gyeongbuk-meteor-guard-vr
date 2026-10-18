using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class AsteroidTextureRotationTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator RockGrainStaysAttachedWhenViewingRigRotates()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var rig = new GameObject("Rotation proof");
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.transform.SetParent(rig.transform, false);
            rock.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/AsteroidNormal.mat");
            var camera = new GameObject("Proof camera").AddComponent<Camera>();
            camera.transform.SetParent(rig.transform, false);
            camera.transform.localPosition = new Vector3(0, 0, -2);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            var light = new GameObject("Proof key").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.SetParent(rig.transform, false);
            light.transform.localRotation = Quaternion.Euler(25, -35, 0);
            RenderSettings.sun = light;
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            Texture2D before = null, after = null;
            try
            {
                yield return null;
                before = Capture(camera, target);
                rig.transform.rotation = Quaternion.Euler(37, 71, 19);
                yield return null;
                after = Capture(camera, target);
                Directory.CreateDirectory("Docs/DeliveryQA/RotationProof");
                File.WriteAllBytes("Docs/DeliveryQA/RotationProof/before.png", before.EncodeToPNG());
                File.WriteAllBytes("Docs/DeliveryQA/RotationProof/rotated.png", after.EncodeToPNG());
                var a = before.GetPixels32(); var b = after.GetPixels32();
                double delta = 0, brightness = 0; int count = 0;
                for (int y = 100; y < 156; y++) for (int x = 100; x < 156; x++)
                {
                    int i = y * 256 + x;
                    delta += Mathf.Abs(a[i].r-b[i].r) + Mathf.Abs(a[i].g-b[i].g) + Mathf.Abs(a[i].b-b[i].b);
                    brightness += a[i].r + a[i].g + a[i].b; count += 3;
                }
                Debug.Log($"[RotationProof] mean channel delta={delta/count:F4}, brightness={brightness/count:F4}");
                Assert.That(brightness/count, Is.GreaterThan(5), "Proof must render visible rock, not an empty frame");
                Assert.That(delta/count, Is.LessThan(1.5), "Co-rotating rock, key and camera must preserve surface grain");
            }
            finally
            {
                camera.targetTexture = null;
                target.Release(); Object.Destroy(target);
                if (before != null) Object.Destroy(before);
                if (after != null) Object.Destroy(after);
                Object.Destroy(rig);
            }
        }

        private static Texture2D Capture(Camera camera, RenderTexture target)
        {
            camera.Render();
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                return image;
            }
            finally { RenderTexture.active = previous; }
        }
    }
}
