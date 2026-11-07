#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>创建缺失的默认视觉预置体和材质，已有资源交由使用者编辑维护。</summary>
    public static class SkillVisualAssets
    {
        public const string FireballPath = "Assets/Prefabs/火球.prefab";
        public const string ShieldPath = "Assets/Prefabs/护盾.prefab";
        public const string ImpactPath = "Assets/Prefabs/命中爆散.prefab";
        public const string SigilPath = "Assets/Prefabs/地面阵法.prefab";
        private const string MaterialRoot = "Assets/Materials/程序能量";
        private const string MeshPath = "Assets/Meshes/单位能量球.asset";
        private static Material ember, core, shell, cyan, cyanSoft, gold, fireParticle, cyanParticle;
        private static Mesh sphere;

        [MenuItem("Tools/NarutoVR/技能/创建缺失的华丽特效预置体")]
        public static void EnsureAssets()
        {
            Folder("Assets/Prefabs"); Folder(MaterialRoot); Folder("Assets/Meshes");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SkillEnergy.shader");
            if (!shader) throw new InvalidOperationException("请先等待技能能量着色器导入完成。");
            core = Material("日心白金", shader, new Color(3.2f, 2.1f, .65f), 0, .9f);
            ember = Material("焰轨橙金", shader, new Color(2.6f, .55f, .035f), 0, .9f);
            shell = Material("流火外壳", shader, new Color(2.4f, .3f, .025f), 1, .7f);
            cyan = Material("护盾青光", shader, new Color(.06f, 1.8f, 2.7f), 0, .85f);
            cyanSoft = Material("护盾薄雾", shader, new Color(.03f, .6f, 1.2f), 1, .4f);
            gold = Material("阵法刻金", shader, new Color(1.6f, .75f, .12f), 0, .7f);
            fireParticle = Material("火星柔光", shader, new Color(2.4f, .8f, .08f), 2, .9f);
            cyanParticle = Material("青色柔光", shader, new Color(.06f, 1.1f, 1.9f), 2, .7f);
            sphere = SphereMesh();
            SaveMissing(FireballPath, Fireball); SaveMissing(ShieldPath, Shield);
            SaveMissing(ImpactPath, Impact); SaveMissing(SigilPath, Sigil);
            AssetDatabase.SaveAssets();
            Debug.Log(Describe(FireballPath) + "\n" + Describe(ShieldPath) + "\n" + Describe(ImpactPath) + "\n" + Describe(SigilPath));
        }

        public static string Describe(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!root) return path + "：未生成";
            int particles = 0, triangles = 0;
            foreach (var p in root.GetComponentsInChildren<ParticleSystem>(true)) particles += p.main.maxParticles;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh) triangles += f.sharedMesh.triangles.Length / 3;
            return $"{root.name}：{root.GetComponentsInChildren<Renderer>(true).Length}个渲染器 / 粒子上限{particles} / 静态网格{triangles}三角形（尾迹与粒子另计）";
        }

        private static GameObject Fireball()
        {
            var root = Root("火球 · 日轮流焰");
            Transform heart = Mesh(root.transform, "白金内核", sphere, core, Vector3.one * .34f);
            Mesh(root.transform, "内层赤焰", sphere, shell, Vector3.one * .68f);
            Transform outer = Mesh(root.transform, "流动火壳", sphere, shell, Vector3.one * .94f);
            var orbits = new List<SkillVisualMotion.Orbit>();
            orbits.Add(Spin(outer, new Vector3(17, 29, -12)));
            for (int i = 0; i < 3; ++i)
            {
                var pivot = Child(root.transform, "火焰轨道" + (i + 1));
                pivot.localRotation = Quaternion.Euler(40 + i * 57, i * 66, i * 37);
                Arc(pivot, "断续能量弧", .42f + i * .015f, .016f, 18 + i * 25, 235 + i * 12, ember, 44);
                Arc(pivot, "高光短弧", .42f + i * .015f, .01f, 270, 325, core, 14);
                for (int j = 0; j < 2; ++j)
                {
                    float a = (j * 180 + i * 22) * Mathf.Deg2Rad;
                    Transform spark = Mesh(pivot, "轨道火花" + (j + 1), sphere, core, Vector3.one * .045f);
                    spark.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .44f;
                }
                orbits.Add(Spin(pivot, new Vector3(0, 0, 80 + i * 37)));
            }
            Particles(root.transform, "离散焰星 · 上限36", fireParticle, 36, 18, .65f, .08f, .32f, .12f, .3f, false);
            var broad = Trail(root.transform, "橙红主尾迹", ember, .95f, .18f, new Color(1, .45f, .08f), new Color(1, .05f, 0));
            var thin = Trail(root.transform, "白金芯尾迹", core, .3f, .11f, Color.white, new Color(1, .4f, .05f));
            var motion = root.AddComponent<SkillVisualMotion>(); motion.orbits = orbits.ToArray(); motion.pulseNode = heart;
            motion.flightTrails = new[] { broad, thin }; motion.pulseFrequency = 2.5f;
            root.SetActive(true); return root;
        }

        private static GameObject Shield()
        {
            var root = Root("护盾 · 青纹护腕");
            var orbits = new List<SkillVisualMotion.Orbit>();
            for (int i = 0; i < 3; ++i)
            {
                Transform ring = Child(root.transform, "护腕符环" + (i + 1));
                ring.localPosition = Vector3.forward * (i - 1) * .025f;
                ring.localRotation = Quaternion.Euler(i == 1 ? 14 : -8, i * 9, i * 37);
                Arc(ring, "青色环轨", .053f + i * .005f, .0024f, 0, 360, cyan, 64);
                Arc(ring, "外侧断弧", .063f + i * .005f, .0012f, 20, 285, cyan, 52);
                for (int j = 0; j < 8; ++j) Rune(ring, j * 45, .06f + i * .005f, .007f, .0012f, cyan, j % 3);
                orbits.Add(Spin(ring, new Vector3(0, 0, i % 2 == 0 ? 25 + i * 7 : -38)));
            }
            Transform aura = Mesh(root.transform, "薄层护盾辉光", sphere, cyanSoft, new Vector3(.12f, .12f, .06f));
            Particles(root.transform, "护腕浮光 · 上限12", cyanParticle, 12, 6, 1.2f, .006f, .018f, .025f, .048f, false);
            var motion = root.AddComponent<SkillVisualMotion>(); motion.orbits = orbits.ToArray(); motion.pulseNode = aura;
            motion.pulseAmount = .04f; motion.pulseFrequency = .7f;
            root.SetActive(true); return root;
        }

        private static GameObject Impact()
        {
            var root = Root("命中爆散 · 星火震环");
            Transform expansion = Child(root.transform, "爆散扩张环");
            Arc(expansion, "白金内震环", .075f, .012f, 0, 360, core, 56);
            Arc(expansion, "橙色外震环", .12f, .008f, 0, 360, ember, 64);
            for (int i = 0; i < 12; ++i)
            {
                float a = i * Mathf.PI / 6;
                Vector3 v = new(Mathf.Cos(a), Mathf.Sin(a), 0);
                Line(expansion, "放射火痕" + (i + 1), new[] { v * .1f, v * (.18f + (i % 3) * .035f) }, .006f, ember);
            }
            Particles(root.transform, "瞬时火星 · 上限48", fireParticle, 48, 0, .6f, .025f, .07f, .9f, .035f, true);
            var motion = root.AddComponent<SkillVisualMotion>(); motion.expansionNode = expansion;
            motion.expansionMultiplier = 4; motion.fadeSeconds = .75f;
            var lifetime = root.AddComponent<SkillVisualLifetime>(); lifetime.lifetime = 1.1f;
            root.SetActive(true); return root;
        }

        private static GameObject Sigil()
        {
            var root = Root("地面阵法 · 双层天衡印");
            // Root local XZ is the ground plane. All lines sit slightly above the floor.
            Transform plane = Child(root.transform, "阵法水平面");
            plane.localRotation = Quaternion.Euler(90, 0, 0); plane.localPosition = Vector3.up * .025f;
            Arc(plane, "外层金色封印", 1.15f, .012f, 0, 360, gold, 128);
            Arc(plane, "内侧青色细环", 1.08f, .006f, 0, 360, cyan, 128);
            Arc(plane, "中心阵眼", .32f, .01f, 0, 360, cyan, 64);
            var orbits = new List<SkillVisualMotion.Orbit>();
            for (int layer = 0; layer < 2; ++layer)
            {
                Transform rotor = Child(plane, "阵法旋转层" + (layer + 1));
                float r = layer == 0 ? .82f : .52f;
                for (int i = 0; i < 6; ++i)
                {
                    Arc(rotor, "分段符环" + (i + 1), r, .009f, i * 60 + 5, i * 60 + 45, layer == 0 ? cyan : gold, 18);
                    Rune(rotor, i * 60 + 25, r + .075f, .055f, .009f, layer == 0 ? gold : cyan, i % 3);
                }
                Polygon(rotor, "几何六芒", 6, r - .12f, .005f, cyan, 2);
                orbits.Add(Spin(rotor, new Vector3(0, 0, layer == 0 ? 6 : -11)));
            }
            for (int i = 0; i < 24; ++i) Rune(plane, i * 15, .99f, .035f, .006f, gold, i % 3);
            Polygon(plane, "中心菱印", 4, .22f, .01f, gold, 1);
            var p = Particles(root.transform, "阵眼浮尘 · 上限20", cyanParticle, 20, 5, 2.4f, .015f, .035f, .09f, .85f, false);
            var shape = p.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.rotation = new Vector3(-90, 0, 0);
            var velocity = p.velocityOverLifetime; velocity.space = ParticleSystemSimulationSpace.Local;
            // Unity requires all three linear velocity axes to use the same curve mode.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(.04f, .14f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.enabled = true;
            var motion = root.AddComponent<SkillVisualMotion>(); motion.orbits = orbits.ToArray();
            root.SetActive(true); return root;
        }

        private static Material Material(string name, Shader shader, Color color, float style, float opacity)
        {
            string path = MaterialRoot + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing) return existing;
            GuardMissing(path);
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Style", style); material.SetFloat("_Opacity", opacity);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void SaveMissing(string path, Func<GameObject> create)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            GuardMissing(path); GameObject root = null;
            try
            {
                root = create();
                BakeLines(root, Path.GetFileNameWithoutExtension(path));
                if (root.GetComponentsInChildren<Collider>(true).Length != 0) throw new InvalidOperationException("视觉预置体禁止携带碰撞体：" + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { if (root) Object.DestroyImmediate(root); }
        }
        private static void GuardMissing(string path)
        {
            if (File.Exists(path)) throw new InvalidOperationException("资源已存在但尚未成功导入，已保留原文件：" + path);
        }

        private sealed class LineBatch
        {
            public Transform parent;
            public Material material;
            public readonly List<Vector3> vertices = new();
            public readonly List<Vector3> normals = new();
            public readonly List<Color> colors = new();
            public readonly List<Vector2> uv = new();
            public readonly List<int> indices = new();
        }

        private static void BakeLines(GameObject root, string assetName)
        {
            // Join only within a single animation node/material. The rotating layers remain independent.
            var motion = root.GetComponent<SkillVisualMotion>();
            var batches = new List<LineBatch>();
            foreach (var line in root.GetComponentsInChildren<LineRenderer>(true))
            {
                Transform anchor = root.transform;
                if (motion)
                {
                    foreach (var orbit in motion.orbits)
                        if (orbit.node && line.transform.IsChildOf(orbit.node) && orbit.node.IsChildOf(anchor)) anchor = orbit.node;
                    if (motion.expansionNode && line.transform.IsChildOf(motion.expansionNode)) anchor = motion.expansionNode;
                }
                var batch = batches.Find(b => b.parent == anchor && b.material == line.sharedMaterial);
                if (batch == null) { batch = new LineBatch { parent = anchor, material = line.sharedMaterial }; batches.Add(batch); }
                Vector3 normal = anchor.InverseTransformDirection(line.transform.forward).normalized;
                int segments = line.positionCount - (line.loop ? 0 : 1);
                for (int i = 0; i < segments; ++i)
                {
                    Vector3 a = anchor.InverseTransformPoint(line.transform.TransformPoint(line.GetPosition(i)));
                    Vector3 b = anchor.InverseTransformPoint(line.transform.TransformPoint(line.GetPosition((i + 1) % line.positionCount)));
                    if ((b - a).sqrMagnitude < .00000001f) continue;
                    Vector3 side = Vector3.Cross(normal, (b - a).normalized) * (line.widthMultiplier * .5f);
                    int k = batch.vertices.Count;
                    batch.vertices.AddRange(new[] { a - side, a + side, b - side, b + side });
                    batch.normals.AddRange(new[] { normal, normal, normal, normal });
                    batch.colors.AddRange(new[] { Color.white, Color.white, Color.white, Color.white });
                    batch.uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one });
                    batch.indices.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 });
                }
                Object.DestroyImmediate(line.gameObject);
            }
            for (int i = 0; i < batches.Count; ++i)
            {
                var batch = batches[i]; string path = $"Assets/Meshes/{assetName}_符纹层{i + 1}.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh)
                {
                    GuardMissing(path); mesh = new Mesh { name = assetName + "合并符纹层" + (i + 1) };
                    mesh.SetVertices(batch.vertices); mesh.SetNormals(batch.normals); mesh.SetColors(batch.colors);
                    mesh.SetUVs(0, batch.uv); mesh.SetTriangles(batch.indices, 0); mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, path);
                }
                Mesh(batch.parent, "合并符纹网格" + (i + 1), mesh, batch.material, Vector3.one);
            }
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static GameObject Root(string name) { var root = new GameObject(name); root.SetActive(false); return root; }
        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }
        private static SkillVisualMotion.Orbit Spin(Transform node, Vector3 speed) => new() { node = node, angularVelocity = speed };
        private static Transform Mesh(Transform parent, string name, Mesh mesh, Material material, Vector3 scale)
        {
            Transform t = Child(parent, name); t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }
        private static LineRenderer Line(Transform parent, string name, Vector3[] points, float width, Material material, bool loop = false)
        {
            var line = Child(parent, name).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false; line.loop = loop;
            line.positionCount = points.Length; line.SetPositions(points); line.widthMultiplier = width;
            line.numCornerVertices = 0; line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.startColor = line.endColor = Color.white; return line;
        }
        private static void Arc(Transform parent, string name, float radius, float width, float from, float to, Material material, int segments)
        {
            bool loop = Mathf.Abs(to - from) >= 359.9f;
            var points = new Vector3[segments + (loop ? 0 : 1)];
            for (int i = 0; i < points.Length; ++i)
            { float a = Mathf.Lerp(from, to, (float)i / segments) * Mathf.Deg2Rad; points[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius; }
            Line(parent, name, points, width, material, loop);
        }
        private static void Polygon(Transform parent, string name, int sides, float radius, float width, Material material, int step)
        {
            // Separate triangles for the six-point star; no repeated disconnected bridge.
            int loops = step == 2 && sides == 6 ? 2 : 1;
            for (int l = 0; l < loops; ++l)
            {
                int count = sides / loops; var points = new Vector3[count];
                for (int i = 0; i < count; ++i)
                { float a = ((i * step + l) % sides) * Mathf.PI * 2 / sides; points[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius; }
                Line(parent, name + l, points, width, material, true);
            }
        }
        private static void Rune(Transform parent, float angle, float radius, float size, float width, Material material, int variant)
        {
            Transform rune = Child(parent, "几何符纹");
            float a = angle * Mathf.Deg2Rad; rune.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
            rune.localRotation = Quaternion.Euler(0, 0, angle - 90);
            if (variant == 0) Line(rune, "菱形", new[] { new Vector3(0, size, 0), new Vector3(size * .5f, 0, 0), new Vector3(0, -size, 0), new Vector3(-size * .5f, 0, 0) }, width, material, true);
            else if (variant == 1)
            {
                Line(rune, "三叉刻痕", new[] { new Vector3(-size * .5f, size * .4f, 0), Vector3.zero, new Vector3(size * .5f, size * .4f, 0) }, width, material);
                Line(rune, "中轴", new[] { Vector3.up * size, Vector3.down * size }, width, material);
            }
            else Line(rune, "折线", new[] { new Vector3(-size * .5f, size, 0), new Vector3(size * .5f, size * .4f, 0), new Vector3(-size * .5f, -size * .4f, 0), new Vector3(size * .5f, -size, 0) }, width, material);
        }
        private static TrailRenderer Trail(Transform parent, string name, Material material, float width, float seconds, Color first, Color last)
        {
            var trail = Child(parent, name).gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material; trail.widthMultiplier = width; trail.time = seconds; trail.minVertexDistance = .02f;
            trail.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0); trail.emitting = false; trail.autodestruct = false;
            trail.colorGradient = Gradient(first, last); trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            return trail;
        }
        private static ParticleSystem Particles(Transform parent, string name, Material material, int limit, float rate, float life,
            float minSize, float maxSize, float speed, float radius, bool burst)
        {
            var ps = Child(parent, name).gameObject.AddComponent<ParticleSystem>();
            var main = ps.main; main.playOnAwake = true; main.loop = !burst; main.duration = burst ? .1f : 3;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .6f, life); main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize); main.maxParticles = limit;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = Color.white; main.useUnscaledTime = false;
            var emission = ps.emission; emission.rateOverTime = rate;
            if (burst) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)limit) });
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
            var color = ps.colorOverLifetime; color.enabled = true;
            color.color = Gradient(Color.white, material == cyanParticle ? new Color(.2f, .8f, 1) : new Color(1, .35f, .05f));
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.maxParticleSize = .2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return ps;
        }
        private static Gradient Gradient(Color first, Color last)
        {
            var result = new Gradient();
            result.SetKeys(new[] { new GradientColorKey(first, 0), new GradientColorKey(last, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.6f, .65f), new GradientAlphaKey(0, 1) });
            return result;
        }
        private static Mesh SphereMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath); if (existing) return existing;
            GuardMissing(MeshPath);
            const int rings = 12, segments = 20;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
            for (int y = 0; y <= rings; ++y)
                for (int x = 0; x <= segments; ++x)
                {
                    float phi = Mathf.PI * y / rings, theta = 2 * Mathf.PI * x / segments;
                    Vector3 n = new(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    vertices.Add(n * .5f); normals.Add(n); uv.Add(new Vector2((float)x / segments, (float)y / rings)); colors.Add(Color.white);
                }
            for (int y = 0; y < rings; ++y)
                for (int x = 0; x < segments; ++x)
                { int i = y * (segments + 1) + x; triangles.AddRange(new[] { i, i + segments + 1, i + 1, i + 1, i + segments + 1, i + segments + 2 }); }
            var mesh = new Mesh { name = "单位直径低面数能量球" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath); return mesh;
        }
    }
}
#endif
