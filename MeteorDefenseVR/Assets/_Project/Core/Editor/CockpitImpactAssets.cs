using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    // Editor-only, deterministic assets. Nothing is generated on a player's first impact.
    internal static class CockpitImpactAssets
    {
        private const string Root="Assets/Art/CockpitDamage/";
        public static Material SharedMaterial(string name,string shader)
        {
            string path=Root+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(material,path);}
            else material.shader=Shader.Find(shader);
            EditorUtility.SetDirty(material);return material;
        }
        public static Mesh Fracture(int point,int stage)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uvs=new List<Vector2>();var colors=new List<Color>();
            var rng=new System.Random(763+point*127);
            void Ribbon(Vector2 a,Vector2 b,float width)
            {
                Vector2 n=new Vector2(a.y-b.y,b.x-a.x).normalized*width*.5f;int k=vertices.Count;
                vertices.Add(a-n);vertices.Add(a+n);vertices.Add(b+n);vertices.Add(b-n);
                uvs.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});
                for(int j=0;j<4;j++)colors.Add(Color.white);
                triangles.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            }
            void Facet(Vector2 a,Vector2 b,Vector2 c,float alpha)
            {
                int k=vertices.Count;vertices.Add(new Vector3(a.x,a.y,-.003f));vertices.Add(b);vertices.Add(new Vector3(c.x,c.y,.003f));
                uvs.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one});
                for(int j=0;j<3;j++)colors.Add(new Color(1,1,1,alpha));triangles.AddRange(new[]{k,k+1,k+2});
            }
            // Concentric stages use one continuous seeded fracture pattern, with displaced translucent facets.
            float[] bounds={.018f,.21f,.34f,.47f,.59f};
            int rays=11;
            for(int ray=0;ray<rays;ray++)
            {
                float angle=(ray+(float)rng.NextDouble()*.48f)*Mathf.PI*2/rays;
                Vector2 d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),side=new Vector2(-d.y,d.x);
                Vector2 previous=d*bounds[stage];
                for(int s=1;s<=4;s++)
                {
                    Vector2 next=d*Mathf.Lerp(bounds[stage],bounds[stage+1],s/4f)+side*((float)rng.NextDouble()-.5f)*.035f;
                    Ribbon(previous,next,Mathf.Lerp(.009f,.003f,(stage+s/4f)/4f));
                    if(s==2 || s==3)
                    {
                        var branch=next+d*.025f+side*(.035f+stage*.015f);Ribbon(next,branch,.003f);
                        if(ray%2==0)Facet(previous,next,branch,.13f);
                    }
                    previous=next;
                }
                if(stage==0)
                {
                    var center=d*.027f;var adjacent=new Vector2(Mathf.Cos(angle+.55f),Mathf.Sin(angle+.55f))*.022f;
                    Facet(Vector2.zero,center,adjacent,.82f);Ribbon(center,adjacent,.0035f);
                }
                else if(ray%2==0) Ribbon(d*bounds[stage],(d*.88f+side*.3f)*bounds[stage],.003f);
            }
            string path=Root+"BeveledCrack_"+point+"_"+stage+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="BeveledCrack"};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uvs);mesh.SetColors(colors);
            mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);return mesh;
        }
        public static AudioClip Sound(string name,int kind)
        {
            string path=Root+name+".wav";
            if(!File.Exists(path))
            {
                const int rate=44100;int count=kind==2?rate*2:rate/2;
                var rng=new System.Random(349+kind);double filtered=0;
                using(var file=new BinaryWriter(File.Create(path)))
                {
                    file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+count*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(rate);file.Write(rate*2);file.Write((short)2);file.Write((short)16);file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(count*2);
                    for(int i=0;i<count;i++)
                    {
                        double t=(double)i/rate,n=rng.NextDouble()*2-1;filtered=filtered*.72+n*.28;
                        double value;
                        if(kind==0) value=(Math.Sin(2*Math.PI*(72*t+1.7*(1-Math.Exp(-t*25))))*.5+Math.Sin(t*2*Math.PI*173)*.18+filtered*.65)*Math.Exp(-t*12);
                        else if(kind==1) value=(Math.Sin(t*2*Math.PI*2397)*.20+Math.Sin(t*2*Math.PI*3713)*.12+(n-filtered)*.4)*Math.Exp(-t*14);
                        else {double fade=Math.Min(1,Math.Min(i,count-1-i)/441.0);value=(n-filtered)*.32*fade;}
                        file.Write((short)(Math.Max(-.95,Math.Min(.95,value))*short.MaxValue));
                    }
                }
            }
            AssetDatabase.ImportAsset(path);var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
