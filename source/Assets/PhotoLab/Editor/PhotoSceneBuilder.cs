using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using LiquidVolumeFX;

namespace PhotoLab.EditorTools
{
    /// <summary>
    /// Builds the photosynthesis (leaf disk assay) lab scene from code. Menu: Photo Lab > Rebuild Scene.
    ///
    /// The fume hood and the room are copied from the density lab's scene, as in the other labs.
    /// Everything on the bench is, for now, a placeholder made of primitives at the right size and
    /// in the right place: the lamp on its post, the filter rack, the four cups with their disks,
    /// the syringes, the hole punch, the specimen shelf, the seagrass tank, the PAR meter, the timer
    /// and the stock bottles. Real meshes replace these one at a time without changing the layout,
    /// which lives in Bench.cs. Materials are regenerated each time.
    /// </summary>
    public static class PhotoSceneBuilder
    {
        const string Root = "Assets/PhotoLab";
        const string MeshDir = Root + "/Meshes";
        const string MatDir = Root + "/Materials";
        public const string ScenePath = Root + "/PhotoLab.unity";
        const string DensityScene = "Assets/DensityLab/DensityLab.unity";
        const string DensityMeshDir = "Assets/DensityLab/Meshes/";
        const string Environment = "Assets/DensityLab/Textures/env_studio.png";
        const float Cm = Bench.Cm;

        static Font labelFont;

        [MenuItem("Photo Lab/Rebuild Scene")]
        public static void Build()
        {
            foreach (string dir in new[] { MeshDir, MatDir })
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, Path.GetFileName(dir));
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var environment = AssetDatabase.LoadAssetAtPath<Cubemap>(Environment);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Lab");
            var lab = root.AddComponent<PhotoManager>();
            var drag = root.AddComponent<PhotoDrag>();

            CopyHoodAndRoom(scene, root.transform);
            RaiseSash(root.transform, 1.0f);
            var m = new Materials(environment);

            BuildLamp(root.transform, m, lab);
            BuildFilterRack(root.transform, m, lab);
            BuildCups(root.transform, m, lab);
            BuildSyringesAndPunch(root.transform, m, lab);
            BuildShelf(root.transform, m, lab);
            BuildTank(root.transform, m, lab);
            BuildMeterAndTimer(root.transform, m, lab);
            BuildBottles(root.transform, m, lab);
            BuildOverlays(root.transform, lab);

            // Light: as in the other labs, from the upper left and in through the open sash.
            var sun = new GameObject("Sun");
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(52f, 52f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.95f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.73f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.53f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.20f, 0.21f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = environment;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.sun = light;

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.90f, 0.90f, 0.87f);
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 120f;
            cam.allowMSAA = true;
            cam.allowHDR = false;
            camGo.AddComponent<AudioListener>();
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = true;

            var rig = camGo.AddComponent<PhotoRig>();
            rig.cam = cam;
            Vector3 benchTarget = new Vector3(0.0f, 0.55f, -0.2f);
            Vector3 cupsTarget = new Vector3(Bench.CupRowX, 0.35f, Bench.CupRowZ);
            Vector3 lampTarget = new Vector3(Bench.LampX, 1.5f, 0.0f);
            Vector3 shelfTarget = Bench.ShelfSlot(1) + new Vector3(0f, 0.55f, 0f);
            Vector3 tankTarget = new Vector3(Bench.TankX, 0.9f, Bench.TankZ);
            Vector3 meterTarget = new Vector3(Bench.MeterX + 0.5f, 0.25f, Bench.MeterZ);
            Vector3 hoodTarget = new Vector3(-0.15f, 3.0f, 0f);
            rig.views = new[]
            {
                new PhotoRig.View { name = "hood", target = hoodTarget, position = new Vector3(-0.15f - 11.85f, 5.5f, -40f), halfWidth = 9f, halfHeight = 13.7f },
                new PhotoRig.View { name = "bench", target = benchTarget, position = benchTarget + Quaternion.Euler(34f, 0f, 0f) * Vector3.back * 13f, halfWidth = 5.4f, halfHeight = 2.7f },
                new PhotoRig.View { name = "cups", target = cupsTarget, position = cupsTarget + Quaternion.Euler(28f, 0f, 0f) * Vector3.back * 4.2f, halfWidth = 2.7f, halfHeight = 1.2f },
                new PhotoRig.View { name = "lamp", target = lampTarget, position = lampTarget + Quaternion.Euler(18f, 0f, 0f) * Vector3.back * 6.0f, halfWidth = 2.6f, halfHeight = 2.0f },
                new PhotoRig.View { name = "shelf", target = shelfTarget, position = shelfTarget + Quaternion.Euler(28f, 0f, 0f) * Vector3.back * 3.4f, halfWidth = 2.3f, halfHeight = 1.2f },
                new PhotoRig.View { name = "tank", target = tankTarget, position = tankTarget + Quaternion.Euler(20f, 0f, 0f) * Vector3.back * 3.6f, halfWidth = 1.7f, halfHeight = 1.3f },
                new PhotoRig.View { name = "meter", target = meterTarget, position = meterTarget + Quaternion.Euler(45f, 0f, 0f) * Vector3.back * 3.4f, halfWidth = 1.3f, halfHeight = 0.9f }
            };
            rig.Snap("bench");
            lab.rig = rig;
            drag.lab = lab;
            drag.cam = cam;

            // hover glow on everything that can be picked up or used
            foreach (BenchItem item in root.GetComponentsInChildren<BenchItem>(true)) PhotoPackages.AddHighlight(item.gameObject);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Photo lab: scene rebuilt at " + ScenePath);
        }

        // ---------------------------------------------------------------------------------------
        // The fume hood and the room, copied from the density lab
        // ---------------------------------------------------------------------------------------

        static void CopyHoodAndRoom(Scene into, Transform parent)
        {
            Scene density = EditorSceneManager.OpenScene(DensityScene, OpenSceneMode.Additive);
            try
            {
                GameObject densityRoot = null;
                foreach (GameObject go in density.GetRootGameObjects()) if (go.name == "Lab") densityRoot = go;
                if (densityRoot == null) throw new System.InvalidOperationException("The density lab scene has no \"Lab\" object.");
                SceneManager.SetActiveScene(into);
                var copiedMeshes = new Dictionary<Mesh, Mesh>();
                foreach (string name in new[] { "Fume Hood", "Room" })
                {
                    Transform original = densityRoot.transform.Find(name);
                    if (original == null) throw new System.InvalidOperationException("The density lab scene has no \"" + name + "\".");
                    GameObject copy = Object.Instantiate(original.gameObject, original.position, original.rotation, parent);
                    copy.name = name;
                    foreach (MeshFilter filter in copy.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh mesh = filter.sharedMesh;
                        if (mesh == null || !AssetDatabase.GetAssetPath(mesh).StartsWith(DensityMeshDir)) continue;
                        Mesh mine;
                        if (!copiedMeshes.TryGetValue(mesh, out mine))
                        {
                            mine = Object.Instantiate(mesh);
                            mine.name = mesh.name;
                            Save(mine);
                            copiedMeshes.Add(mesh, mine);
                        }
                        filter.sharedMesh = mine;
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(density, true);
            }
        }

        /// <summary>The sash is copied part-way open; raise it further so the camera's bench view looks under it, not through its bottom rail.</summary>
        static void RaiseSash(Transform root, float by)
        {
            Transform sash = root.Find("Fume Hood/Hood Model/Box2088");
            if (sash != null) sash.position += Vector3.up * by;
        }

        // ---------------------------------------------------------------------------------------
        // Materials
        // ---------------------------------------------------------------------------------------

        class Materials
        {
            public Material metal, darkPlastic, whitePlastic, clearPlastic, water, seawater, leaf, leafWhite, sand, glass, rubber, paper, lcd, filterGel, lampFace, marker, coleus, seagrass;

            public Materials(Cubemap environment)
            {
                metal = Lit("Metal", new Color(0.62f, 0.63f, 0.65f), 0.9f, 0.7f);
                darkPlastic = Lit("DarkPlastic", new Color(0.12f, 0.12f, 0.13f), 0f, 0.45f);
                whitePlastic = Lit("WhitePlastic", new Color(0.92f, 0.92f, 0.90f), 0f, 0.4f);
                rubber = Lit("Rubber", new Color(0.18f, 0.18f, 0.2f), 0f, 0.2f);
                paper = Lit("Paper", new Color(0.96f, 0.95f, 0.90f), 0f, 0.1f);
                leaf = Lit("Leaf", new Color(0.16f, 0.42f, 0.14f), 0f, 0.35f);
                leafWhite = Lit("LeafWhite", new Color(0.88f, 0.90f, 0.78f), 0f, 0.35f);
                sand = Lit("Sand", new Color(0.78f, 0.72f, 0.56f), 0f, 0.1f);
                lcd = Lit("Lcd", new Color(0.55f, 0.62f, 0.48f), 0f, 0.3f);
                lampFace = Lit("LampFace", new Color(1f, 0.98f, 0.92f), 0f, 0.6f);
                lampFace.EnableKeyword("_EMISSION");
                lampFace.SetColor("_EmissionColor", new Color(1.6f, 1.55f, 1.4f));
                marker = Lit("Marker", new Color(1f, 0.75f, 0.1f), 0f, 0.5f);
                coleus = Lit("Coleus", Color.white, 0f, 0.3f);
                seagrass = Lit("Seagrass", Color.white, 0f, 0.35f);
                seagrass.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PhotoLab/Models/seagrass_color.jpg"));
                seagrass.SetFloat("_Cull", 0f);   // blades show from both sides
                coleus.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PhotoLab/Models/coleus_color.jpg"));
                marker.EnableKeyword("_EMISSION");
                marker.SetColor("_EmissionColor", new Color(0.9f, 0.6f, 0.05f));
                clearPlastic = Glass("ClearPlastic", new Color(0.92f, 0.95f, 0.97f, 0.18f), 0.6f);
                glass = Glass("Glass", new Color(0.90f, 0.95f, 0.98f, 0.14f), 0.85f);
                water = Glass("Water", new Color(0.72f, 0.86f, 0.95f, 0.35f), 0.9f);
                seawater = Glass("Seawater", new Color(0.55f, 0.82f, 0.86f, 0.45f), 0.9f);
                filterGel = Glass("FilterGel", new Color(1f, 1f, 1f, 0.2f), 0.5f);
            }

            static Material Lit(string name, Color color, float metallic, float smoothness)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, MatDir + "/" + name + ".mat");
                return mat;
            }

            static Material Glass(string name, Color color, float smoothness)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AssetDatabase.CreateAsset(mat, MatDir + "/" + name + ".mat");
                return mat;
            }
        }

        // ---------------------------------------------------------------------------------------
        // Bench parts (placeholders at the right size and place)
        // ---------------------------------------------------------------------------------------

        static void BuildLamp(Transform root, Materials m, PhotoManager lab)
        {
            var lamp = Node("Lamp", root, new Vector3(Bench.LampX, 0f, Bench.LampPostZ));
            Part("Base", lamp.transform, PrimitiveType.Cylinder, m.darkPlastic, new Vector3(0f, 0.75f * Cm, 0f), new Vector3(9f * Cm, 0.75f * Cm, 9f * Cm));
            Part("Post", lamp.transform, PrimitiveType.Cylinder, m.metal, new Vector3(0f, Bench.LampPostHeightCm * 0.5f * Cm, 0f),
                new Vector3(Bench.LampPostRadiusCm * 2f * Cm, Bench.LampPostHeightCm * 0.5f * Cm, Bench.LampPostRadiusCm * 2f * Cm));

            // The head slides on the post and reaches forward over the cups.
            Vector3 face = Bench.LampFace(Bench.LampDefaultDistanceCm);
            var head = Node("Head", root, new Vector3(Bench.CupRowX, face.y + Bench.LampHeadHeightCm * 0.5f * Cm, Bench.CupRowZ));
            float armLength = Bench.LampPostZ - Bench.CupRowZ;
            Part("Arm", head.transform, PrimitiveType.Cube, m.metal, new Vector3(0f, 0f, armLength * 0.5f), new Vector3(2f * Cm, 2f * Cm, armLength));
            Part("Collar", head.transform, PrimitiveType.Cylinder, m.darkPlastic, new Vector3(0f, 0f, armLength), new Vector3(4.5f * Cm, 2.5f * Cm, 4.5f * Cm));
            Part("Housing", head.transform, PrimitiveType.Cube, m.darkPlastic, Vector3.zero,
                new Vector3(Bench.LampHeadWidthCm * Cm, Bench.LampHeadHeightCm * Cm, Bench.LampHeadDepthCm * Cm));
            Part("Face", head.transform, PrimitiveType.Cube, m.lampFace, new Vector3(0f, -Bench.LampHeadHeightCm * 0.5f * Cm - 0.1f * Cm, 0f),
                new Vector3((Bench.LampHeadWidthCm - 1.5f) * Cm, 0.2f * Cm, (Bench.LampHeadDepthCm - 1.5f) * Cm), false);
            // The slot the filter slides into, just under the face.
            var slot = Part("Filter In Slot", head.transform, PrimitiveType.Cube, m.filterGel, new Vector3(0f, -Bench.LampHeadHeightCm * 0.5f * Cm - 0.6f * Cm, 0f),
                new Vector3(Bench.FilterSizeCm * Cm, Bench.FilterThicknessCm * Cm, Bench.FilterSizeCm * Cm), false);

            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(head.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -Bench.LampHeadHeightCm * 0.5f * Cm - 1f * Cm, 0f);
            lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 70f;
            light.innerSpotAngle = 40f;
            light.range = 12f;
            light.intensity = 3f;
            light.shadows = LightShadows.Soft;

            lab.lampHead = head.transform;
            lab.lampLight = light;
            var beam = lightGo.AddComponent<VLB.VolumetricLightBeamSD>();
            beam.colorFromLight = true;
            beam.intensityFromLight = false;
            beam.intensityInside = 0.35f; beam.intensityOutside = 0.35f;
            beam.spotAngleFromLight = true;
            beam.coneRadiusStart = Bench.LampHeadWidthCm * 0.45f * Cm;
            beam.fallOffEndFromLight = false;
            beam.fallOffStart = 0.1f; beam.fallOffEnd = 2.6f;
            beam.noiseMode = VLB.NoiseMode.WorldSpace;
            beam.noiseIntensity = 0.35f;
            beam.trackChangesDuringPlaytime = true;
            lab.beam = beam;
            lab.filterInSlot = slot.GetComponent<Renderer>();
            Label(head.transform, "lamp", new Vector3(0f, Bench.LampHeadHeightCm * 0.5f * Cm + 1.5f * Cm, 0f));
        }

        static void BuildFilterRack(Transform root, Materials m, PhotoManager lab)
        {
            var rack = Node("Filter Rack", root, new Vector3(Bench.FilterRackX, 0f, Bench.FilterRackZ));
            var gels = new List<FilterGel>();
            Part("Base", rack.transform, PrimitiveType.Cube, m.darkPlastic, new Vector3(0f, 0.75f * Cm, 0f),
                new Vector3((PhotosynthesisModel.Filters.Length * Bench.FilterPitchCm + 2f) * Cm, 1.5f * Cm, 4f * Cm));
            for (int i = 0; i < PhotosynthesisModel.Filters.Length; i++)
            {
                PhotosynthesisModel.Filter f = PhotosynthesisModel.Filters[i];
                Vector3 at = Bench.FilterSlot(i) - rack.transform.position;
                var gel = Part("Filter " + f.id, rack.transform, PrimitiveType.Cube, m.filterGel, at + new Vector3(0f, 1.5f * Cm + Bench.FilterSizeCm * 0.5f * Cm, 0f),
                    new Vector3(Bench.FilterSizeCm * Cm, Bench.FilterSizeCm * Cm, Bench.FilterThicknessCm * Cm), false);
                if (f.id != "clear") { var fg = gel.AddComponent<FilterGel>(); fg.id = f.id; fg.home = gel.transform.position; gels.Add(fg); Highlightable(gel); }
                else gel.SetActive(false);   // "no filter" is the empty slot, not a gel in the rack
                var mat = new Material(m.filterGel) { name = "Filter_" + f.id };
                mat.SetColor("_BaseColor", new Color(f.tint.r, f.tint.g, f.tint.b, Mathf.Max(0.12f, f.tint.a * 0.6f)));
                AssetDatabase.CreateAsset(mat, MatDir + "/Filter_" + f.id + ".mat");
                gel.GetComponent<Renderer>().sharedMaterial = mat;
            }
            Label(rack.transform, "filters", new Vector3(0f, 13f * Cm, 0f));
            lab.filters = gels.ToArray();
        }

        static void BuildCups(Transform root, Materials m, PhotoManager lab)
        {
            var cupList = new List<Cup>();
            for (int i = 0; i < Bench.CupCount; i++)
            {
                var cup = Node("Cup " + (i + 1), root, Bench.CupSlot(i));
                float h = Bench.CupHeightCm * Cm;
                Part("Wall", cup.transform, PrimitiveType.Cylinder, m.clearPlastic, new Vector3(0f, h * 0.5f, 0f),
                    new Vector3(Bench.CupRadiusCm * 2f * Cm, h * 0.5f, Bench.CupRadiusCm * 2f * Cm), false);
                var water = Part("Water", cup.transform, PrimitiveType.Cylinder, m.water, new Vector3(0f, h * 0.5f, 0f),
                    new Vector3((Bench.CupRadiusCm * 2f - 0.3f) * Cm, h * 0.5f, (Bench.CupRadiusCm * 2f - 0.3f) * Cm), false);
                var cupLiquid = Liquid(water, TOPOLOGY.Cylinder, new Color(0.65f, 0.85f, 0.97f, 0.25f), 0f);
                var cc = cup.AddComponent<Cup>();
                cc.id = "cup" + (i + 1); cc.index = i; cc.home = cup.transform.position;
                cc.diskMaterial = m.leaf; cc.diskWhiteMaterial = m.leafWhite;
                cc.water = water.transform; cc.liquid = cupLiquid;
                Highlightable(cup);
                cupList.Add(cc);
                Label(cup.transform, "cup " + (i + 1), new Vector3(0f, h + 1.2f * Cm, 0f));
            }
            lab.cups = cupList.ToArray();
        }

        static void BuildSyringesAndPunch(Transform root, Materials m, PhotoManager lab)
        {
            var rack = Node("Syringe Rack", root, new Vector3(Bench.SyringeRackX, 0f, Bench.SyringeRackZ));
            lab.syringeRack = Part("Base", rack.transform, PrimitiveType.Cube, m.darkPlastic, new Vector3(0f, 1f * Cm, 0f),
                new Vector3((Bench.CupCount * Bench.SyringePitchCm + 2f) * Cm, 2f * Cm, 4f * Cm)).GetComponent<Renderer>();
            var syringes = new List<Syringe>();
            for (int i = 0; i < Bench.CupCount; i++)
            {
                Vector3 at = Bench.SyringeSlot(i) - rack.transform.position;
                var s = Node("Syringe " + (i + 1), rack.transform, at);
                var sy = s.AddComponent<Syringe>();
                sy.id = (i + 1).ToString(); sy.home = s.transform.position;
                sy.plungerRestY = 2.5f * Cm + 2f * Cm; sy.plungerFullY = Bench.SyringeLengthCm * Cm + 2f * Cm; sy.liquidTopY = Bench.SyringeLengthCm * 0.5f * Cm;
                var liq = Part("Liquid", s.transform, PrimitiveType.Cylinder, m.water, new Vector3(0f, 0.3f * Cm + Bench.SyringeLengthCm * 0.5f * Cm, 0f), new Vector3(Bench.SyringeRadiusCm * 1.7f * Cm, Bench.SyringeLengthCm * 0.5f * Cm, Bench.SyringeRadiusCm * 1.7f * Cm), false);
                sy.liquid = liq.transform;
                sy.liquidVolume = Liquid(liq, TOPOLOGY.Cylinder, new Color(0.65f, 0.85f, 0.97f, 0.3f), 0f, 0.22f);
                if (sy.liquidVolume == null) liq.SetActive(false);
                var din = Part("Disks", s.transform, PrimitiveType.Cylinder, m.leaf, new Vector3(0f, 0.5f * Cm, 0f), new Vector3(Bench.SyringeRadiusCm * 1.4f * Cm, 0.25f * Cm, Bench.SyringeRadiusCm * 1.4f * Cm), false);
                din.SetActive(false); sy.disksInside = din.transform;
                syringes.Add(sy);
                Highlightable(s);
                Part("Barrel", s.transform, PrimitiveType.Cylinder, m.clearPlastic, new Vector3(0f, Bench.SyringeLengthCm * 0.5f * Cm, 0f),
                    new Vector3(Bench.SyringeRadiusCm * 2f * Cm, Bench.SyringeLengthCm * 0.5f * Cm, Bench.SyringeRadiusCm * 2f * Cm), false);
                var plunger = Part("Plunger", s.transform, PrimitiveType.Cylinder, m.whitePlastic, new Vector3(0f, sy.plungerRestY, 0f),
                    new Vector3(Bench.SyringeRadiusCm * 1.2f * Cm, 2.5f * Cm, Bench.SyringeRadiusCm * 1.2f * Cm));
                sy.plunger = plunger.transform;
                Part("Tip", s.transform, PrimitiveType.Cylinder, m.whitePlastic, new Vector3(0f, 0.3f * Cm, 0f), new Vector3(0.5f * Cm, 0.4f * Cm, 0.5f * Cm));
            }
            Label(rack.transform, "syringes", new Vector3(0f, 16f * Cm, 0f));
            lab.syringes = syringes.ToArray();

            var punch = Node("Hole Punch", root, new Vector3(Bench.PunchX, 0f, Bench.PunchZ));
            var hp = punch.AddComponent<HolePunch>(); hp.id = "punch"; hp.home = punch.transform.position; lab.punch = hp;
            Highlightable(punch);
            var punchModel = Model("Assets/PhotoLab/Models/hole_punch.fbx", punch.transform, 10f, 0f, null);
            if (punchModel != null) punchModel.transform.localRotation = Quaternion.AngleAxis(-102f, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);   // lying flat, jaw aimed at the spinach
            else Part("Body", punch.transform, PrimitiveType.Cube, m.metal, new Vector3(0f, 1.5f * Cm, 0f), new Vector3(5f * Cm, 3f * Cm, 9f * Cm));
            Label(punch.transform, "hole punch", new Vector3(0f, 6f * Cm, 0f));

            // the pile of punched disks collects on the leaf
            var dish = Node("Dish", root, new Vector3(Bench.SyringeRackX + 11f * Cm, 0f, Bench.SyringeRackZ - 3f * Cm));
            Part("Base", dish.transform, PrimitiveType.Cylinder, m.whitePlastic, new Vector3(0f, 0.3f * Cm, 0f), new Vector3(6f * Cm, 0.3f * Cm, 6f * Cm));
            Label(dish.transform, "disks", new Vector3(0f, 3f * Cm, 0f));
            var pileGo = Node("Disk Pile", root, new Vector3(Bench.SyringeRackX + 11f * Cm, 0.6f * Cm, Bench.SyringeRackZ - 3f * Cm));
            var pile = pileGo.AddComponent<DiskStack>(); pile.id = "pile"; pile.home = pileGo.transform.position; pile.green = m.leaf; pile.white = m.leafWhite;
            var pileMesh = Part("Pile", pileGo.transform, PrimitiveType.Cylinder, m.leaf, Vector3.zero, new Vector3(3.2f * Cm, 0.01f, 3.2f * Cm), false);
            pile.pile = pileMesh.transform; pile.pileRenderer = pileMesh.GetComponent<Renderer>();
            Highlightable(pileGo);
            pileMesh.SetActive(false);
            lab.stack = pile;
        }

        static List<Specimen> shelfSpecimens;

        static void BuildShelf(Transform root, Materials m, PhotoManager lab)
        {
            var shelf = Node("Specimen Shelf", root, new Vector3(Bench.ShelfX, 0f, Bench.ShelfZ));
            Part("Plank", shelf.transform, PrimitiveType.Cube, m.whitePlastic, new Vector3(0f, Bench.ShelfHeightCm * 0.5f * Cm, 0f),
                new Vector3((2f * Bench.ShelfPitchCm + 2f) * Cm, Bench.ShelfHeightCm * Cm, Bench.ShelfDepthCm * Cm));
            string[] names = { "spinach", "coleus" };
            var markers = new List<Renderer>();
            var specs = new List<Specimen>();
            for (int k = 0; k < 2; k++)
            {
                Vector3 at = Bench.ShelfSlot(k) - shelf.transform.position;
                var station = Node(names[k], shelf.transform, at);
                var spc = station.AddComponent<Specimen>(); spc.id = names[k]; spc.specimenId = names[k]; spc.home = station.transform.position; specs.Add(spc);
                Highlightable(station);
                if (k == 0) { var sg = Model("Assets/PhotoLab/Models/spinach_plant.fbx", station.transform, 4.5f, 0f, null); if (sg != null) sg.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); }
                else if (k == 1) { var cg = Model("Assets/PhotoLab/Models/coleus.fbx", station.transform, 3.6f, 20f, m.coleus); if (cg != null) cg.transform.localRotation = Quaternion.Euler(-90f, 20f, 0f); }
                var marker = Part("Marker", station.transform, PrimitiveType.Sphere, m.marker, new Vector3(0f, 0.5f * Cm, -Bench.ShelfDepthCm * 0.5f * Cm + 1f * Cm), new Vector3(1.4f * Cm, 1.4f * Cm, 1.4f * Cm), false);
                markers.Add(marker.GetComponent<Renderer>());
                Label(station.transform, names[k], new Vector3(0f, 14f * Cm, 0f));
            }
            // markers line up with PhotosynthesisModel.Specimens: spinach, coleus, coleus-white, seagrass
            foreach (Renderer mk in markers) mk.enabled = false;
            shelfSpecimens = specs;
        }

        static void BuildTank(Transform root, Materials m, PhotoManager lab)
        {
            var tank = Node("Seagrass Tank", root, new Vector3(Bench.TankX, 0f, Bench.TankZ));
            var tsp = tank.AddComponent<Specimen>(); tsp.id = "seagrass"; tsp.specimenId = "seagrass"; tsp.home = tank.transform.position;
            var allSpecs = new List<Specimen>(shelfSpecimens); allSpecs.Add(tsp); lab.specimens = allSpecs.ToArray();
            float w = Bench.TankWidthCm * Cm, d = Bench.TankDepthCm * Cm, h = Bench.TankHeightCm * Cm, water = Bench.TankWaterCm * Cm;
            Part("Sand", tank.transform, PrimitiveType.Cube, m.sand, new Vector3(0f, 1.25f * Cm, 0f), new Vector3(w - 0.4f * Cm, 2.5f * Cm, d - 0.4f * Cm));
            var tankWater = Part("Water", tank.transform, PrimitiveType.Cube, m.seawater, new Vector3(0f, 2.5f * Cm + (h - 2.5f * Cm) * 0.5f, 0f), new Vector3(w - 0.4f * Cm, h - 2.5f * Cm, d - 0.4f * Cm), false);
            // lagoon water: blue-green with tannin brown lower down, a touch of murk, the blades still visible
            Liquid(tankWater, TOPOLOGY.Cube, new Color(0.66f, 0.76f, 0.82f, 0.40f), (water - 2.5f * Cm) / (h - 2.5f * Cm), 0.26f, new Color(0.70f, 0.64f, 0.52f, 0.40f), 0.06f);
            Part("Glass", tank.transform, PrimitiveType.Cube, m.glass, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), false);
            // three clumps of turtle grass on the sand, each scaled from a 1.9 m clump to about 12 cm
            // upright (the scan's up axis needs the same turn as the plants), stretched a little taller than wide so the blades read as turtle grass
            float[] cx = { -6.5f, 0f, 6.5f }; float[] cz = { 0.5f, -0.5f, 0.5f }; float[] cs = { 0.05f, 0.058f, 0.05f };
            for (int j = 0; j < 3; j++)
            {
                var clumpRoot = Node("Clump " + j, tank.transform, new Vector3(cx[j] * Cm, 2.5f * Cm, cz[j] * Cm));
                var clump = Model("Assets/PhotoLab/Models/seagrass.fbx", clumpRoot.transform, cs[j] * 10f, 0f, m.seagrass);
                if (clump != null) { clump.transform.localRotation = Quaternion.Euler(-90f, 0f, j * 70f); clump.transform.localScale = new Vector3(cs[j] * 10f, cs[j] * 10f, cs[j] * 16f); }
            }
            // a juvenile blue crab on the sand, about 4.5 cm across
            var crabRoot = Node("Crab", tank.transform, new Vector3(-4.5f * Cm, 2.5f * Cm, -3.5f * Cm));
            var crabModel = Model("Assets/PhotoLab/Models/blue_crab.fbx", crabRoot.transform, 0.193f, 0f, null);
            if (crabModel != null) crabModel.transform.localRotation = Quaternion.Euler(-90f, 0f, 160f);
            // its own small lamp, fixed, so the tank is lit whatever the bench lamp does
            var lightGo = new GameObject("Tank Light");
            lightGo.transform.SetParent(tank.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, h + 12f * Cm, 0f);
            lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 60f;
            light.range = 6f;
            light.intensity = 2f;
            light.color = new Color(0.85f, 0.95f, 1f);
            Label(tank.transform, "seagrass tank", new Vector3(0f, h + 3f * Cm, 0f));
        }

        static void BuildMeterAndTimer(Transform root, Materials m, PhotoManager lab)
        {
            var meter = Node("PAR Meter", root, new Vector3(Bench.MeterX, 0f, Bench.MeterZ));
            Part("Body", meter.transform, PrimitiveType.Cube, m.darkPlastic, new Vector3(0f, Bench.MeterHeightCm * 0.5f * Cm, 0f),
                new Vector3(Bench.MeterWidthCm * Cm, Bench.MeterHeightCm * Cm, Bench.MeterDepthCm * Cm));
            Part("Screen", meter.transform, PrimitiveType.Cube, m.lcd, new Vector3(0f, Bench.MeterHeightCm * Cm + 0.05f * Cm, 2f * Cm),
                new Vector3((Bench.MeterWidthCm - 2f) * Cm, 0.1f * Cm, 4f * Cm), false);
            // the quantum sensor on its lead, lying beside the meter
            var sensor = Part("Sensor", meter.transform, PrimitiveType.Cylinder, m.whitePlastic, new Vector3(Bench.MeterWidthCm * 0.5f * Cm + 2.5f * Cm, 0.6f * Cm, 0f), new Vector3(2.4f * Cm, 0.6f * Cm, 2.4f * Cm));
            Label(sensor.transform, "sensor", new Vector3(0f, 2.5f, 0f));
            lab.parDisplay = Display(meter.transform, new Vector3(0f, Bench.MeterHeightCm * Cm + 0.15f * Cm, 2f * Cm), 0.2f * Cm, "0\nµmol/m²/s");
            Label(meter.transform, "PAR meter", new Vector3(0f, 7f * Cm, 0f));

            var timer = Node("Timer", root, new Vector3(Bench.TimerX, 0f, Bench.TimerZ));
            lab.timerBody = Part("Body", timer.transform, PrimitiveType.Cube, m.whitePlastic, new Vector3(0f, 1.5f * Cm, 0f), new Vector3(7f * Cm, 3f * Cm, 6f * Cm)).GetComponent<Renderer>();
            Part("Screen", timer.transform, PrimitiveType.Cube, m.lcd, new Vector3(0f, 3.05f * Cm, 0f), new Vector3(5.5f * Cm, 0.1f * Cm, 2.5f * Cm), false);
            lab.timerDisplay = Display(timer.transform, new Vector3(0f, 3.15f * Cm, 0f), 0.22f * Cm, "00:00");
            Label(timer.transform, "timer", new Vector3(0f, 6f * Cm, 0f));
        }

        static void BuildBottles(Transform root, Materials m, PhotoManager lab)
        {
            var list = new List<Bottle>();
            for (int i = 0; i < Bench.BottleIds.Length; i++)
            {
                var b = Node("Bottle " + Bench.BottleIds[i], root, Bench.BottleSlot(i));
                var bt = b.AddComponent<Bottle>(); bt.id = Bench.BottleIds[i]; bt.home = b.transform.position;
                bt.solution = i == 0 ? Solution.Bicarbonate : i == 1 ? Solution.Water : Solution.None; bt.detergent = i == 2;
                Highlightable(b);
                list.Add(bt);
                float r = i == 2 ? Bench.BottleRadiusCm * 0.6f : Bench.BottleRadiusCm;
                float h = i == 2 ? Bench.BottleHeightCm * 0.6f : Bench.BottleHeightCm;
                Part("Body", b.transform, PrimitiveType.Cylinder, i == 0 ? m.water : m.clearPlastic, new Vector3(0f, h * 0.5f * Cm, 0f), new Vector3(r * 2f * Cm, h * 0.5f * Cm, r * 2f * Cm), false);
                Part("Cap", b.transform, PrimitiveType.Cylinder, m.darkPlastic, new Vector3(0f, (h + 1f) * Cm, 0f), new Vector3(r * 1.3f * Cm, 1f * Cm, r * 1.3f * Cm));
                Label(b.transform, Bench.BottleLabels[i], new Vector3(0f, (h + 3.5f) * Cm, 0f));
            }
            lab.bottles = list.ToArray();
        }

        static void BuildOverlays(Transform root, PhotoManager lab)
        {
            var holder = Node("Overlays", root, Vector3.zero);
            var list = new List<GameObject>();
            foreach (string lens in PhotoManager.Lenses)
            {
                if (lens == "bench") continue;
                var o = Node("Overlay " + lens, holder.transform, Vector3.zero);
                o.SetActive(false);
                list.Add(o);
            }
            lab.overlays = list.ToArray();
        }

        // ---------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------

        static Mesh Save(Mesh mesh)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(MeshDir + "/" + mesh.name + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Liquid Volume Pro on a mesh, with no flask of its own (the cup, barrel or glass is already there). Returns null when the package is absent.</summary>
        static Component Liquid(GameObject go, TOPOLOGY topology, Color color, float level, float alpha = 0.18f, Color? color2 = null, float murk = 0f)
        {
            var lv = go.AddComponent<LiquidVolume>();
            lv.topology = topology;
            lv.detail = DETAIL.DefaultNoFlask;
            lv.level = level;
            lv.liquidColor1 = color;
            lv.liquidColor2 = color2 ?? color;
            lv.liquidScale1 = 1f; lv.liquidScale2 = 1f;
            lv.alpha = alpha;
            lv.murkiness = murk;
            lv.turbulence1 = 0.05f; lv.turbulence2 = 0.05f;
            lv.foamThickness = 0f; lv.foamDensity = 0f;
            lv.smokeEnabled = false;
            lv.bubblesAmount = 0;
            lv.bubblesSizeMin = 2; lv.bubblesSizeMax = 4;
            lv.bubblesVerticalSpeed = 1.2f;
            lv.bubblesBrightness = 0.8f;
            lv.depthAware = false;
            return lv;
        }

        /// <summary>An imported model under a station: scaled from metres to bench units (10 cm), turned, and optionally re-materialed.</summary>
        static GameObject Model(string path, Transform parent, float scale, float turnDegrees, Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("Photo lab: no model at " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, turnDegrees, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                if (material != null) r.sharedMaterial = material;
            }
            return go;
        }

        /// <summary>Highlight Plus on an item the student can point at: a soft outline and glow, off until hovered.</summary>
        static void Highlightable(GameObject go)
        {
            var h = go.AddComponent<HighlightPlus.HighlightEffect>();
            h.outline = 0.9f; h.outlineWidth = 0.25f; h.outlineColor = new Color(1f, 0.85f, 0.3f, 1f);
            h.glow = 0.5f; h.glowWidth = 0.4f;
            h.innerGlow = 0f; h.overlay = 0f;
            h.highlighted = false;
        }

        static GameObject Node(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        static GameObject Part(string name, Transform parent, PrimitiveType type, Material material, Vector3 localPosition, Vector3 localScale, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A small floating label so the placeholder bench reads at a glance. Removed when the real meshes arrive.</summary>
        static TextMesh Label(Transform parent, string text, Vector3 localPosition)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = labelFont;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = 0.019f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var r = go.GetComponent<MeshRenderer>();
            if (labelFont != null) r.sharedMaterial = labelFont.material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }

        /// <summary>A readout lying flat on a screen, read from the camera's side.</summary>
        static TextMesh Display(Transform parent, Vector3 localPosition, float size, string text)
        {
            var go = new GameObject("Readout");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = labelFont;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = size * 0.42f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.02f, 0.03f, 0.02f);
            var r = go.GetComponent<MeshRenderer>();
            if (labelFont != null) r.sharedMaterial = labelFont.material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }
    }
}
