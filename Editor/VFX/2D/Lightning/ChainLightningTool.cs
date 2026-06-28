using UnityEditor;
using UnityEngine;

namespace VFX
{
    // 체인 라이트닝 VFX 툴: 파라미터를 조절하고 창 안의 독립 미리보기에서 재생한다.
    // PreviewRenderUtility로 메인 씬과 분리된 씬/카메라를 만들어 렌더 → 씬을 오염시키지 않는다.
    // 드라이버의 Tick(elapsed)으로 시간을 진행시키며, 카메라는 체인 전체 폭에 맞춰 자동 프레이밍한다.
    // 메뉴: Tools > VFX > 2D > Chain Lightning
    public class ChainLightningTool : EditorWindow
    {
        [SerializeField] ChainLightningParams param = new ChainLightningParams();

        Vector2 scroll;
        PreviewRenderUtility preview;
        GameObject effect;
        ChainLightningDriver driver;
        bool playing = true;
        float elapsed;
        double lastUpdate;
        ChainLightningPreset presetField;
        static readonly Color bgColor = new Color(0.03f, 0.03f, 0.09f);

        [MenuItem("Tools/VFX/2D/Chain Lightning")]
        static void Open()
        {
            GetWindow<ChainLightningTool>("Chain Lightning").minSize = new Vector2(340, 640);
        }


        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EnsurePreview();
            Rebuild();
            lastUpdate = EditorApplication.timeSinceStartup;
        }


        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupEffect();
            if (preview != null) { preview.Cleanup(); preview = null; }
        }


        void EnsurePreview()
        {
            if (preview != null) return;
            preview = new PreviewRenderUtility();
            var cam = preview.camera;
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bgColor;
            cam.transform.rotation = Quaternion.identity;
        }


        void OnGUI()
        {
            Rect previewRect = GUILayoutUtility.GetRect(position.width, 240f);
            if (Event.current.type == EventType.Repaint)
                RenderPreview(previewRect);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = playing ? new Color(1f, 0.55f, 0.55f) : new Color(0.6f, 0.85f, 1f);
                if (GUILayout.Button(playing ? "■ 정지" : "▶ 재생", GUILayout.Height(30)))
                    playing = !playing;
                GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
                if (GUILayout.Button("↻ 다시", GUILayout.Height(30), GUILayout.Width(70)))
                    elapsed = 0f;
                GUI.backgroundColor = new Color(1f, 0.9f, 0.5f);
                if (GUILayout.Button("🎲 랜덤", GUILayout.Height(30), GUILayout.Width(90)))
                    Randomize();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("프리셋", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                presetField = (ChainLightningPreset)EditorGUILayout.ObjectField(presetField, typeof(ChainLightningPreset), false);
                using (new EditorGUI.DisabledScope(presetField == null))
                    if (GUILayout.Button("불러오기", GUILayout.Width(70))) LoadPreset();
            }
            if (GUILayout.Button("프리셋 내보내기 (.asset)")) ExportPreset();
            EditorGUILayout.Space(6);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("색상", EditorStyles.boldLabel);
            param.color = EditorGUILayout.ColorField("번개 색", param.color);

            EditorGUILayout.LabelField("Chain (노드 배치)", EditorStyles.boldLabel);
            param.nodeCount = EditorGUILayout.IntSlider("노드 수", param.nodeCount, 2, 10);
            param.segmentLength = EditorGUILayout.FloatField("노드 간격", param.segmentLength);
            param.spread = EditorGUILayout.FloatField("좌우 퍼짐", param.spread);

            EditorGUILayout.LabelField("Bolt (줄기)", EditorStyles.boldLabel);
            param.coreWidth = EditorGUILayout.FloatField("코어 라인 폭", param.coreWidth);
            param.glowWidthMul = EditorGUILayout.FloatField("글로우 폭 배수", param.glowWidthMul);
            param.generations = EditorGUILayout.IntSlider("지그재그 분할", param.generations, 1, 7);
            param.displace = EditorGUILayout.FloatField("흔들림 폭", param.displace);
            param.damp = EditorGUILayout.Slider("세대 감쇠", param.damp, 0.1f, 0.9f);

            EditorGUILayout.LabelField("Branches (곁가지)", EditorStyles.boldLabel);
            param.branchPerSegment = EditorGUILayout.IntSlider("구간당 곁가지", param.branchPerSegment, 0, 5);
            param.branchChance = EditorGUILayout.Slider("출현 확률", param.branchChance, 0f, 1f);
            param.branchLength = EditorGUILayout.FloatField("곁가지 길이", param.branchLength);

            EditorGUILayout.LabelField("Nodes (노드 섬광)", EditorStyles.boldLabel);
            param.nodeFlashSize = EditorGUILayout.FloatField("섬광 크기", param.nodeFlashSize);
            param.nodePulse = EditorGUILayout.Slider("펄스 진폭", param.nodePulse, 0f, 1f);
            param.pulseSpeed = EditorGUILayout.FloatField("펄스 속도", param.pulseSpeed);

            EditorGUILayout.LabelField("Flicker (깜빡임)", EditorStyles.boldLabel);
            param.flickerInterval = EditorGUILayout.FloatField("깜빡임 간격(초)", param.flickerInterval);

            EditorGUILayout.LabelField("기타", EditorStyles.boldLabel);
            param.sortingOrder = EditorGUILayout.IntField("정렬 순서", param.sortingOrder);

            if (EditorGUI.EndChangeCheck())
                Rebuild();

            EditorGUILayout.EndScrollView();
        }


        void RenderPreview(Rect rect)
        {
            if (preview == null || driver == null) return;

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.05f, (float)(now - lastUpdate));
            lastUpdate = now;
            if (playing) elapsed += dt;

            driver.Tick(elapsed);

            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.Render();
            Texture tex = preview.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }


        void Rebuild()
        {
            CleanupEffect();
            effect = ChainLightningVFX.Build(Vector3.zero, param.Clone());
            driver = effect.GetComponent<ChainLightningDriver>();
            preview.AddSingleGO(effect);

            // 체인은 +X로 뻗으므로 카메라를 가운데로 옮기고 폭에 맞춰 줌.
            float flWidth = Mathf.Max(1f, (param.nodeCount - 1) * param.segmentLength);
            preview.camera.transform.position = new Vector3(flWidth * 0.5f, 0f, -10f);
            preview.camera.orthographicSize = Mathf.Max(2f, flWidth * 0.6f);
        }


        void CleanupEffect()
        {
            if (effect != null) { Object.DestroyImmediate(effect); effect = null; driver = null; }
        }


        void Randomize()
        {
            param.color = Color.HSVToRGB(Random.value, Random.Range(0.4f, 0.9f), 1f);
            param.nodeCount = Random.Range(3, 7);
            param.segmentLength = Random.Range(1.1f, 2.2f);
            param.spread = Random.Range(0.3f, 1.1f);
            param.coreWidth = Random.Range(0.04f, 0.09f);
            param.glowWidthMul = Random.Range(2.5f, 4.5f);
            param.generations = Random.Range(4, 7);
            param.displace = Random.Range(0.2f, 0.5f);
            param.damp = Random.Range(0.4f, 0.7f);
            param.branchPerSegment = Random.Range(0, 4);
            param.branchChance = Random.Range(0.3f, 0.8f);
            param.branchLength = Random.Range(0.3f, 0.9f);
            param.flickerInterval = Random.Range(0.03f, 0.08f);
            Rebuild();
            Repaint();
        }


        void OnEditorUpdate()
        {
            if (playing) Repaint();
        }


        void ExportPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "프리셋 내보내기", "ChainLightningPreset", "asset", "저장 위치를 선택하세요");
            if (string.IsNullOrEmpty(path)) return;

            var preset = CreateInstance<ChainLightningPreset>();
            preset.param = param.Clone();
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            presetField = preset;
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
            Debug.Log($"[ChainLightning] 프리셋 저장: {path}");
        }


        void LoadPreset()
        {
            if (presetField == null) return;
            param = presetField.param.Clone();
            Rebuild();
            Repaint();
        }
    }
}
