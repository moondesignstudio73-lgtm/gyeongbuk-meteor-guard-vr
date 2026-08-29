using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    // Deterministic authored geometry. All meshes are saved assets, never generated per frame.
    public static class PremiumMeshBuilder
    {
        public static Mesh Save(string name, Mesh mesh)
        {
            string path = "Assets/Art/Premium/Meshes/" + name + ".asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }
        public static Vector2[] Outline(float width, float height, float cut)
        {
            float x = width * .5f, y = height * .5f, c = Mathf.Min(cut, Mathf.Min(x, y) * .8f);
            return new[] { new Vector2(-x+c,-y), new Vector2(x-c,-y), new Vector2(x,-y+c), new Vector2(x,y-c),
                new Vector2(x-c,y), new Vector2(-x+c,y), new Vector2(-x,y-c), new Vector2(-x,-y+c) };
        }
        public static Mesh Panel(string name, float w, float h, float depth, float cut, float bevel)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            Vector2[] outer = Outline(w, h, cut), inner = Outline(w-bevel*2, h-bevel*2, Mathf.Max(.001f,cut-bevel*.5f));
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(a.x/w+.5f,a.y/h+.5f)); uv.Add(new Vector2(b.x/w+.5f,b.y/h+.5f)); uv.Add(new Vector2(c.x/w+.5f,c.y/h+.5f));
                indices.Add(n); indices.Add(n+1); indices.Add(n+2);
            }
            for (int i=0;i<8;i++)
            {
                int j=(i+1)%8;
                Vector3 a=new Vector3(inner[i].x,inner[i].y,-depth*.5f), b=new Vector3(inner[j].x,inner[j].y,-depth*.5f);
                Vector3 c=new Vector3(outer[i].x,outer[i].y,-depth*.5f+bevel), d=new Vector3(outer[j].x,outer[j].y,-depth*.5f+bevel);
                Vector3 e=new Vector3(outer[i].x,outer[i].y,depth*.5f), f=new Vector3(outer[j].x,outer[j].y,depth*.5f);
                Triangle(new Vector3(0,0,-depth*.5f),b,a); Triangle(a,b,c); Triangle(b,d,c);
                Triangle(c,d,e); Triangle(d,f,e); Triangle(new Vector3(0,0,depth*.5f),e,f);
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return Save(name,mesh);
        }
        public static Mesh Screen(string name, float w, float h, int tile)
        {
            // Match the generated atlas's measured module bounds, excluding adjacent headers.
            float x=(tile%3)/3f+.005f,y=tile/3==0?.01f:.567f,dx=1f/3f-.01f,dy=tile/3==0?.541f:.421f;
            var mesh=new Mesh { vertices=new[]{new Vector3(-w/2,-h/2,0),new Vector3(-w/2,h/2,0),new Vector3(w/2,h/2,0),new Vector3(w/2,-h/2,0)},
                uv=new[]{new Vector2(x,y),new Vector2(x,y+dy),new Vector2(x+dx,y+dy),new Vector2(x+dx,y)},triangles=new[]{0,1,2,0,2,3}};
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return Save(name,mesh);
        }
        public static Mesh Rock(string name, int seed, int columns, int rows, Vector3 elongation, bool sphere=false)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int j=0;j<=rows;j++) for(int i=0;i<=columns;i++)
            {
                float t=Mathf.PI*j/rows,p=2*Mathf.PI*i/columns;
                Vector3 n=new Vector3(Mathf.Sin(t)*Mathf.Cos(p),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(p));
                float noise=Mathf.PerlinNoise(n.x*2.7f+seed,n.y*2.7f+n.z*1.8f+seed*.17f);
                float micro=Mathf.PerlinNoise(n.z*9+seed,n.x*9+n.y*4);
                float radius=.41f+noise*.16f+micro*.045f+Mathf.Sin(n.y*8+n.x*6+seed)*.023f;
                if(sphere)radius=.5f;
                vertices.Add(Vector3.Scale(n*radius,elongation));uv.Add(new Vector2((float)i/columns,sphere?1f-(float)j/rows:(float)j/rows));
            }
            for(int j=0;j<rows;j++) for(int i=0;i<columns;i++)
            {int a=j*(columns+1)+i,b=a+columns+1;indices.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
            // Smooth the UV seam without smoothing away the fractured silhouette.
            var normals=mesh.normals;
            for(int j=0;j<=rows;j++){int a=j*(columns+1),b=a+columns;Vector3 n=(normals[a]+normals[b]).normalized;normals[a]=normals[b]=n;}
            mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();return Save(name,mesh);
        }
    }
}
