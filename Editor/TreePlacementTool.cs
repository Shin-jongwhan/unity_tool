using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkyTools
{
    // 나무 배치 에디터 툴: 절차 나무(ProceduralTree) + 임의 프리팹 나무를 파라미터를 조절하며
    // 씬에 흩뿌려 배치한다. 절차 나무 메시/머티리얼/텍스처는 에셋으로 저장되어 씬 저장 후에도 유지된다.
    // 메뉴: Tools > Sky Tools > Tree Placement Tool
    public class TreePlacementTool : EditorWindow
    {
        const string GenBase = "Assets/SkyTools/Generated";
        const string GenDir = GenBase + "/Tree";

        [SerializeField] bool includeProcedural = true;
        [SerializeField] List<GameObject> prefabTrees = new List<GameObject>();
        [SerializeField] int count = 40;
        [SerializeField] float xMin = -50f, xMax = 50f;
        [SerializeField] float zMin = -50f, zMax = 50f;
        [SerializeField] float procScaleMin = 1.2f, procScaleMax = 2.2f;
        [SerializeField] float prefabScaleMin = 0.8f, prefabScaleMax = 1.4f;
        [SerializeField, Range(0f, 1f)] float proceduralRatio = 0.6f;
        [SerializeField] int seed = 12345;
        [SerializeField] string parentName = "AuthoredTrees";
        [SerializeField] bool snapToGround = true;     // 레이캐스트로 콜라이더 지면에 스냅
        [SerializeField] float placeY = 0f;            // snap 꺼졌을 때 고정 Y
        [SerializeField] bool disablePrefabColliders = true;

        Vector2 scroll;
        Mesh canopyAsset, trunkAsset;
        Material leafMatAsset, trunkMatAsset;

        [MenuItem("Tools/Sky Tools/Tree Placement Tool")]
        static void Open()
        {
            GetWindow<TreePlacementTool>("Tree Placement").minSize = new Vector2(340, 460);
        }


        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("나무 소스", EditorStyles.boldLabel);
            includeProcedural = EditorGUILayout.Toggle("절차 나무 포함", includeProcedural);
            DrawPrefabList();
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("배치 파라미터", EditorStyles.boldLabel);
            count = Mathf.Max(0, EditorGUILayout.IntField("개수", count));
            EditorGUILayout.BeginHorizontal();
            xMin = EditorGUILayout.FloatField("X 범위", xMin);
            xMax = EditorGUILayout.FloatField(xMax);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            zMin = EditorGUILayout.FloatField("Z 범위", zMin);
            zMax = EditorGUILayout.FloatField(zMax);
            EditorGUILayout.EndHorizontal();
            proceduralRatio = EditorGUILayout.Slider("절차 비율", proceduralRatio, 0f, 1f);
            EditorGUILayout.BeginHorizontal();
            procScaleMin = EditorGUILayout.FloatField("절차 스케일", procScaleMin);
            procScaleMax = EditorGUILayout.FloatField(procScaleMax);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            prefabScaleMin = EditorGUILayout.FloatField("프리팹 스케일", prefabScaleMin);
            prefabScaleMax = EditorGUILayout.FloatField(prefabScaleMax);
            EditorGUILayout.EndHorizontal();
            seed = EditorGUILayout.IntField("시드", seed);
            snapToGround = EditorGUILayout.Toggle("지면에 스냅(레이캐스트)", snapToGround);
            using (new EditorGUI.DisabledScope(snapToGround))
                placeY = EditorGUILayout.FloatField("고정 Y", placeY);
            disablePrefabColliders = EditorGUILayout.Toggle("프리팹 콜라이더 끄기", disablePrefabColliders);
            parentName = EditorGUILayout.TextField("부모 오브젝트", parentName);

            EditorGUILayout.Space(8);
            DrawUsageInfo();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("랜덤 시드")) seed = UnityEngine.Random.Range(0, 999999);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.6f, 0.85f, 0.6f);
                if (GUILayout.Button("배치(추가)", GUILayout.Height(30))) Place(false);
                if (GUILayout.Button("다시 배치(지우고)", GUILayout.Height(30))) Place(true);
                GUI.backgroundColor = Color.white;
            }
            if (GUILayout.Button("배치 전부 지우기")) ClearAll();

            EditorGUILayout.EndScrollView();
        }


        void DrawPrefabList()
        {
            int valid = prefabTrees.Count(p => p != null);
            EditorGUILayout.LabelField($"프리팹 나무 ({valid}종)");
            int removeAt = -1;
            for (int i = 0; i < prefabTrees.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                prefabTrees[i] = (GameObject)EditorGUILayout.ObjectField(prefabTrees[i], typeof(GameObject), false);
                if (GUILayout.Button("-", GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) prefabTrees.RemoveAt(removeAt);
            if (GUILayout.Button("+ 프리팹 슬롯 추가")) prefabTrees.Add(null);
        }


        void DrawUsageInfo()
        {
            var prefabs = prefabTrees.Where(p => p != null).ToList();
            bool canProc = includeProcedural;
            bool canPrefab = prefabs.Count > 0;
            string src;
            if (canProc && canPrefab) src = $"절차 {Mathf.RoundToInt(proceduralRatio * 100)}% + 프리팹 {prefabs.Count}종 혼합";
            else if (canProc) src = "절차 나무만";
            else if (canPrefab) src = $"프리팹 {prefabs.Count}종만";
            else src = "⚠ 소스 없음 (절차 켜거나 프리팹 추가)";
            EditorGUILayout.HelpBox(
                $"사용 소스: {src}\n" +
                (canPrefab ? "프리팹: " + string.Join(", ", prefabs.Select(p => p.name)) + "\n" : "") +
                "배치는 '" + parentName + "' 오브젝트 아래에 생성됩니다.",
                canProc || canPrefab ? MessageType.Info : MessageType.Warning);
        }


        void Place(bool clearFirst)
        {
            var prefabs = prefabTrees.Where(p => p != null).ToList();
            bool canProc = includeProcedural;
            bool canPrefab = prefabs.Count > 0;
            if (!canProc && !canPrefab)
            {
                EditorUtility.DisplayDialog("Tree Placement", "나무 소스가 없습니다. 절차 나무를 켜거나 프리팹을 추가하세요.", "확인");
                return;
            }
            if (canProc) EnsureProcAssets();

            var parent = GameObject.Find(parentName);
            if (parent == null)
            {
                parent = new GameObject(parentName);
                Undo.RegisterCreatedObjectUndo(parent, "Create tree parent");
            }
            if (clearFirst) ClearChildren(parent);

            var rnd = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(xMin, xMax, (float)rnd.NextDouble());
                float z = Mathf.Lerp(zMin, zMax, (float)rnd.NextDouble());
                float y = SampleHeight(x, z);
                float yaw = (float)rnd.NextDouble() * 360f;

                bool proc = canProc && (!canPrefab || rnd.NextDouble() < proceduralRatio);
                GameObject go;
                if (proc)
                {
                    go = BuildProceduralInstance();
                    go.transform.localScale = Vector3.one * Mathf.Lerp(procScaleMin, procScaleMax, (float)rnd.NextDouble());
                }
                else
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rnd.Next(prefabs.Count)]);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(prefabScaleMin, prefabScaleMax, (float)rnd.NextDouble());
                    if (disablePrefabColliders)
                        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                }
                go.name = "Tree";
                go.transform.SetParent(parent.transform, true);
                go.transform.position = new Vector3(x, y, z);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Undo.RegisterCreatedObjectUndo(go, "Place tree");
            }
            Debug.Log($"[TreePlacementTool] '{parentName}' 아래에 나무 {count}그루 배치 완료.");
        }


        GameObject BuildProceduralInstance()
        {
            var root = new GameObject("Tree");
            var canopy = new GameObject("Canopy", typeof(MeshFilter), typeof(MeshRenderer));
            canopy.transform.SetParent(root.transform, false);
            canopy.GetComponent<MeshFilter>().sharedMesh = canopyAsset;
            canopy.GetComponent<MeshRenderer>().sharedMaterial = leafMatAsset;
            var trunk = new GameObject("Trunk", typeof(MeshFilter), typeof(MeshRenderer));
            trunk.transform.SetParent(root.transform, false);
            trunk.GetComponent<MeshFilter>().sharedMesh = trunkAsset;
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMatAsset;
            return root;
        }


        // 절차 나무 메시/텍스처/머티리얼을 에셋으로 저장(이미 있으면 재사용) → 씬 저장 후에도 유지.
        void EnsureProcAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SkyTools")) AssetDatabase.CreateFolder("Assets", "SkyTools");
            if (!AssetDatabase.IsValidFolder(GenBase)) AssetDatabase.CreateFolder("Assets/SkyTools", "Generated");
            if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder(GenBase, "Tree");

            canopyAsset = LoadOrSave(GenDir + "/TreeCanopy.asset", () => Instantiate(ProceduralTree.CanopyMesh));
            trunkAsset = LoadOrSave(GenDir + "/TreeTrunk.asset", () => Instantiate(ProceduralTree.TrunkMesh));
            var leafTex = LoadOrSave(GenDir + "/TreeLeafGradient.asset",
                () => Instantiate((Texture2D)ProceduralTree.LeafMaterial.GetTexture("_BaseMap")));
            leafMatAsset = LoadOrSave(GenDir + "/TreeLeaf.mat", () =>
            {
                var m = Instantiate(ProceduralTree.LeafMaterial);
                m.SetTexture("_BaseMap", leafTex);
                return m;
            });
            trunkMatAsset = LoadOrSave(GenDir + "/TreeTrunk.mat", () => Instantiate(ProceduralTree.TrunkMaterial));
            AssetDatabase.SaveAssets();
        }


        static T LoadOrSave<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var obj = create();
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }


        float SampleHeight(float x, float z)
        {
            if (snapToGround && Physics.Raycast(new Vector3(x, 1000f, z), Vector3.down, out var hit, 4000f))
                return hit.point.y;
            return placeY;
        }


        void ClearAll()
        {
            var parent = GameObject.Find(parentName);
            if (parent != null) ClearChildren(parent);
        }


        void ClearChildren(GameObject parent)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(parent.transform.GetChild(i).gameObject);
        }
    }
}
