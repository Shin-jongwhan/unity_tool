using UnityEditor;
using UnityEngine;

namespace VFX
{
    // 라이트닝 구체 VFX 툴: 파라미터를 조절하고 창 안의 독립 미리보기에서 재생한다.
    // PreviewRenderUtility로 메인 씬과 분리된 씬/카메라를 만들어 렌더 → 씬을 오염시키지 않는다.
    // 파티클이 아니라 LineRenderer 기반이라 드라이버의 Tick(elapsed)으로 시간을 진행시킨다.
    // 메뉴: Tools > VFX > 2D > Lightning Orb
    public class LightningOrbTool : EditorWindow
    {
        [SerializeField] LightningOrbParams param = new LightningOrbParams();

        Vector2 scroll;
        PreviewRenderUtility preview;
        GameObject effect;
        LightningOrbDriver driver;
        bool playing = true;
        float elapsed;
        double lastUpdate;
        LightningOrbPreset presetField;
        static readonly Color bgColor = new Color(0.03f, 0.03f, 0.09f);

        [MenuItem("Tools/VFX/2D/Lightning Orb")]
        static void Open()
        {
            GetWindow<LightningOrbTool>("Lightning Orb").minSize = new Vector2(340, 620);
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
            cam.orthographicSize = 1.6f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bgColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
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
                presetField = (LightningOrbPreset)EditorGUILayout.ObjectField(presetField, typeof(LightningOrbPreset), false);
                using (new EditorGUI.DisabledScope(presetField == null))
                    if (GUILayout.Button("불러오기", GUILayout.Width(70))) LoadPreset();
            }
            if (GUILayout.Button("프리셋 내보내기 (.asset)")) ExportPreset();
            EditorGUILayout.Space(6);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("색상", EditorStyles.boldLabel);
            param.color = EditorGUILayout.ColorField("번개 색", param.color);

            EditorGUILayout.LabelField("Core (코어 발광)", EditorStyles.boldLabel);
            param.coreStyle = (CoreStyle)EditorGUILayout.EnumPopup("표현 방식", param.coreStyle);
            param.coreSize = EditorGUILayout.FloatField("크기", param.coreSize);
            param.coreAlpha = EditorGUILayout.Slider("불투명도", param.coreAlpha, 0f, 1f);
            if (param.coreStyle == CoreStyle.Ring)
                param.coreThickness = EditorGUILayout.Slider("Ring 두께", param.coreThickness, 0.05f, 1f);
            param.corePulse = EditorGUILayout.Slider("펄스 진폭", param.corePulse, 0f, 1f);
            param.pulseSpeed = EditorGUILayout.FloatField("펄스 속도", param.pulseSpeed);

            EditorGUILayout.LabelField("Arcs (표면 번개)", EditorStyles.boldLabel);
            param.arcOrigin = (ArcOrigin)EditorGUILayout.EnumPopup("시작 위치", param.arcOrigin);
            param.arcCount = EditorGUILayout.IntField("아크 개수", param.arcCount);
            MinMaxRow("아크 길이", ref param.arcLengthMin, ref param.arcLengthMax);
            param.coreWidth = EditorGUILayout.FloatField("코어 라인 폭", param.coreWidth);
            param.glowWidthMul = EditorGUILayout.FloatField("글로우 폭 배수", param.glowWidthMul);
            param.generations = EditorGUILayout.IntSlider("지그재그 분할", param.generations, 1, 7);
            param.displace = EditorGUILayout.FloatField("흔들림 폭", param.displace);
            param.damp = EditorGUILayout.Slider("세대 감쇠", param.damp, 0.1f, 0.9f);

            EditorGUILayout.LabelField("Flicker (깜빡임)", EditorStyles.boldLabel);
            param.flickerInterval = EditorGUILayout.FloatField("깜빡임 간격(초)", param.flickerInterval);

            EditorGUILayout.LabelField("기타", EditorStyles.boldLabel);
            param.sortingOrder = EditorGUILayout.IntField("정렬 순서", param.sortingOrder);

            if (EditorGUI.EndChangeCheck())
                Rebuild();

            EditorGUILayout.EndScrollView();
        }


        void MinMaxRow(string label, ref float min, ref float max)
        {
            EditorGUILayout.BeginHorizontal();
            min = EditorGUILayout.FloatField(label, min);
            max = EditorGUILayout.FloatField(max);
            EditorGUILayout.EndHorizontal();
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
            effect = LightningOrbVFX.Build(Vector3.zero, param.Clone());
            driver = effect.GetComponent<LightningOrbDriver>();
            preview.AddSingleGO(effect);
            // 아크가 닿는 최대 반경 기준으로 프레이밍(코어만 키워도 화면이 안 흔들리게).
            float flStart = param.arcOrigin == ArcOrigin.Surface ? param.coreSize * 0.5f : 0f;
            float flReach = Mathf.Max(param.coreSize * 0.5f, flStart + param.arcLengthMax);
            preview.camera.orthographicSize = Mathf.Max(0.6f, flReach * 1.2f);
        }


        void CleanupEffect()
        {
            if (effect != null) { Object.DestroyImmediate(effect); effect = null; driver = null; }
        }


        void Randomize()
        {
            param.color = Color.HSVToRGB(Random.value, Random.Range(0.4f, 0.9f), 1f);
            param.coreStyle = (CoreStyle)Random.Range(0, 3);
            param.coreSize = Random.Range(0.4f, 1f);
            param.coreAlpha = Random.Range(0.6f, 1f);
            param.coreThickness = Random.Range(0.1f, 0.8f);
            param.corePulse = Random.Range(0f, 0.4f);
            param.pulseSpeed = Random.Range(3f, 10f);
            param.arcCount = Random.Range(5, 14);
            param.arcLengthMin = Random.Range(0.4f, 0.8f);
            param.arcLengthMax = param.arcLengthMin + Random.Range(0.2f, 0.7f);
            param.coreWidth = Random.Range(0.03f, 0.08f);
            param.glowWidthMul = Random.Range(2.5f, 4.5f);
            param.generations = Random.Range(3, 6);
            param.displace = Random.Range(0.1f, 0.3f);
            param.damp = Random.Range(0.4f, 0.7f);
            param.flickerInterval = Random.Range(0.03f, 0.09f);
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
                "프리셋 내보내기", "LightningOrbPreset", "asset", "저장 위치를 선택하세요");
            if (string.IsNullOrEmpty(path)) return;

            var preset = CreateInstance<LightningOrbPreset>();
            preset.param = param.Clone();
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            presetField = preset;
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
            Debug.Log($"[LightningOrb] 프리셋 저장: {path}");
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
