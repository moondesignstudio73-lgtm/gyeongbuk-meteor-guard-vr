using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.Meteor
{
    // Eight pieces of the existing rock mesh; no timer may advance the damage stage.
    // Built once with the prepared boss slot, then reused on every retry.
    public sealed class BossShellPresentation : IDisposable
    {
        private readonly Transform owner, root, core;
        private readonly Renderer[] intact;
        private readonly bool[] originalHidden;
        private readonly Transform[] pieces=new Transform[8];
        private readonly Mesh[] meshes=new Mesh[8];
        private readonly Vector3[] directions=new Vector3[8];
        private readonly float[] flight=new float[8];
        private readonly int[] order=new int[8];
        private readonly Material coreMaterial;
        private bool ordered;
        public int Stage {get;private set;}
        public int ShedCount {get;private set;}
        public int ActiveFragments {get {int n=0;foreach(float age in flight)if(age>=0)n++;return n;}}
        public bool CoreVisible=>core!=null&&core.gameObject.activeInHierarchy;
        public Transform Root=>root;
        public const float FragmentSeconds=.65f;

        public BossShellPresentation(Transform owner,MeshFilter source,Renderer[] intact)
        {
            this.owner=owner;this.intact=intact;originalHidden=new bool[intact.Length];
            for(int i=0;i<intact.Length;i++)originalHidden[i]=intact[i].forceRenderingOff;
            var mesh=source.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            var indices=new List<int>[8];for(int i=0;i<8;i++)indices[i]=new List<int>();
            var center=mesh.bounds.center;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 c=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3-center;
                int part=(c.x>=0?1:0)|(c.y>=0?2:0)|(c.z>=0?4:0);
                indices[part].Add(triangles[i]);indices[part].Add(triangles[i+1]);indices[part].Add(triangles[i+2]);
            }
            root=new GameObject("BossShellBreakup").transform;root.SetParent(owner,false);
            Material shellMaterial=source.GetComponent<Renderer>().sharedMaterial;
            for(int i=0;i<8;i++)
            {
                var shard=new Mesh{name="Boss shell sector "+i};shard.vertices=vertices;shard.normals=mesh.normals;shard.uv=mesh.uv;
                shard.SetTriangles(indices[i],0);shard.RecalculateBounds();meshes[i]=shard;
                pieces[i]=Part("ShellSector"+i,shard,shellMaterial);
                directions[i]=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1).normalized;
            }
            coreMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Boss exposed core"};
            coreMaterial.SetColor("_BaseColor",new Color(2.2f,.48f,.06f));
            core=Part("ExposedCore",mesh,coreMaterial);core.localScale=Vector3.one*.48f;
            Reset();
        }
        private Transform Part(string name,Mesh mesh,Material material)
        {
            var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(root,false);
            part.GetComponent<MeshFilter>().sharedMesh=mesh;var r=part.GetComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return part.transform;
        }
        public void ApplyHealth(float current,float maximum)
        {
            if(!Core.RuntimeValueGuard.IsFinite(current)||!Core.RuntimeValueGuard.IsFinite(maximum)||maximum<=0||current<=0)return;
            float fraction=current/maximum;int next=fraction<=1f/3+.00001f?2:fraction<=2f/3+.00001f?1:0;
            if(next<=Stage)return;
            if(!ordered)
            {
                Vector3 view=Camera.main!=null?owner.InverseTransformDirection(Camera.main.transform.position-owner.position).normalized:Vector3.back;
                for(int i=0;i<8;i++)order[i]=i;
                // The first breach faces the pilot at impact, then remains attached to the rotating rock.
                for(int i=0;i<8;i++)for(int j=i+1;j<8;j++)if(Vector3.Dot(directions[order[j]],view)>Vector3.Dot(directions[order[i]],view))
                {int swap=order[i];order[i]=order[j];order[j]=swap;}
                ordered=true;
            }
            root.gameObject.SetActive(true);core.gameObject.SetActive(true);
            foreach(var r in intact)if(r!=null)r.forceRenderingOff=true;
            for(int i=ShedCount;i<next*2;i++)flight[order[i]]=0;
            ShedCount=next*2;Stage=next;core.localScale=Vector3.one*(next==1?.48f:.58f);
        }
        public void Tick(float delta)
        {
            if(!Core.RuntimeValueGuard.IsFinite(delta)||delta<=0||Stage==0)return;
            for(int i=0;i<8;i++)if(flight[i]>=0)
            {
                flight[i]+=delta;float t=Mathf.Clamp01(flight[i]/FragmentSeconds);
                pieces[i].localPosition=directions[i]*(t*.9f);
                pieces[i].localScale=Vector3.one*Mathf.Lerp(1,.15f,t);
                if(t>=1){pieces[i].gameObject.SetActive(false);flight[i]=-1;}
            }
        }
        public void Reset()
        {
            Stage=ShedCount=0;ordered=false;
            for(int i=0;i<8;i++){flight[i]=-1;if(pieces[i]==null)continue;pieces[i].localPosition=Vector3.zero;pieces[i].localScale=Vector3.one;pieces[i].gameObject.SetActive(true);}
            for(int i=0;i<intact.Length;i++)if(intact[i]!=null)intact[i].forceRenderingOff=originalHidden[i];
            if(core!=null)core.gameObject.SetActive(false);if(root!=null)root.gameObject.SetActive(false);
        }
        public void SetEmission(MaterialPropertyBlock block)
        {foreach(var piece in pieces)if(piece!=null)piece.GetComponent<Renderer>().SetPropertyBlock(block);}
        public void Dispose()
        {
            Reset();foreach(var mesh in meshes)Release(mesh);Release(coreMaterial);if(root!=null)Release(root.gameObject);
        }
        private static void Release(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
