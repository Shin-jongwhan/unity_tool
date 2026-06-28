using UnityEditor;
using UnityEngine;

namespace VFX
{
    // 레이저 명중 VFX 툴: 색·각종 파라미터를 조절하고, 창 안의 독립 미리보기 화면에서 재생한다.
    // PreviewRenderUtility로 메인 씬과 분리된 씬/카메라를 만들어 렌더 → 씬을 전혀 오염시키지 않는다.
    // 메뉴: Tools > VFX > 2D > Laser Impact
    public class LaserImpactTool : EditorWindow
    {
        [SerializeField] LaserImpactParams param = new LaserImpactParams();

        Vector2 scroll;
        PreviewRenderUtility preview;
        GameObject effect;
        bool playing = true;
        float elapsed;
        double lastUpdate;
        LaserImpactPreset presetField;
        static readonly Color bgColor = new Color(0.04f, 0.04f, 0.10f);

        [MenuItem("Tools/VFX/2D/Laser Impact")]
        static void Open()
        {
            GetWindow<LaserImpactTool>("Laser Impact").minSize = new Vector2(340, 660);
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
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }


        void OnGUI()
        {
            // === 상단: 독립 미리보기 화면 ===
            Rect previewRect = GUILayoutUtility.GetRect(position.width, 240f);
            if (Event.current.type == EventType.Repaint)
                RenderPreview(previewRect);

            // === 재생 컨트롤 ===
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

            // === 내보내기 / 불러오기 ===
            EditorGUILayout.LabelField("프리셋", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                presetField = (LaserImpactPreset)EditorGUILayout.ObjectField(presetField, typeof(LaserImpactPreset), false);
                using (new EditorGUI.DisabledScope(presetField == null))
                    if (GUILayout.Button("불러오기", GUILayout.Width(70))) LoadPreset();
            }
            if (GUILayout.Button("프리셋 내보내기 (.asset)")) ExportPreset();
            EditorGUILayout.Space(6);

            // === 파라미터 (변경 시 즉시 반영) ===
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("색상", EditorStyles.boldLabel);
            param.color = EditorGUILayout.ColorField("레이저 색", param.color);

            EditorGUILayout.LabelField("Flash (섬광)", EditorStyles.boldLabel);
            param.flashSize = EditorGUILayout.FloatField("크기", param.flashSize);
            param.flashLifetime = EditorGUILayout.FloatField("지속시간", param.flashLifetime);

            EditorGUILayout.LabelField("Sparks (불똥)", EditorStyles.boldLabel);
            param.sparkCount = EditorGUILayout.IntField("개수", param.sparkCount);
            MinMaxRow("속도", ref param.sparkSpeedMin, ref param.sparkSpeedMax);
            MinMaxRow("크기", ref param.sparkSizeMin, ref param.sparkSizeMax);
            MinMaxRow("수명", ref param.sparkLifetimeMin, ref param.sparkLifetimeMax);
            param.sparkGravity = EditorGUILayout.FloatField("중력", param.sparkGravity);
            param.sparkStretch = EditorGUILayout.FloatField("늘림(streak)", param.sparkStretch);

            EditorGUILayout.LabelField("Embers (잔광)", EditorStyles.boldLabel);
            param.emberCount = EditorGUILayout.IntField("개수", param.emberCount);
            MinMaxRow("속도", ref param.emberSpeedMin, ref param.emberSpeedMax);
            MinMaxRow("크기", ref param.emberSizeMin, ref param.emberSizeMax);
            MinMaxRow("수명", ref param.emberLifetimeMin, ref param.emberLifetimeMax);

            EditorGUILayout.LabelField("기타", EditorStyles.boldLabel);
            param.sortingOrder = EditorGUILayout.IntField("정렬 순서", param.sortingOrder);

            if (EditorGUI.EndChangeCheck())
                Rebuild();   // 값 바꾸면 미리보기 즉시 갱신

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
            if (preview == null || effect == null) return;

            // 시간 진행
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.05f, (float)(now - lastUpdate));
            lastUpdate = now;
            if (playing)
            {
                float loop = Mathf.Max(param.sparkLifetimeMax, param.emberLifetimeMax) + 0.5f;
                elapsed += dt;
                if (elapsed > loop) elapsed = 0f;
            }
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
                ps.Simulate(Mathf.Max(0.0001f, elapsed), false, true);

            // 렌더
            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.Render();
            Texture tex = preview.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }


        void Rebuild()
        {
            CleanupEffect();
            effect = LaserImpactVFX.Build(Vector3.zero, param.Clone());
            preview.AddSingleGO(effect);   // 미리보기 씬으로 이동(메인 씬 오염 X)
            elapsed = 0f;
        }


        void CleanupEffect()
        {
            if (effect != null) { Object.DestroyImmediate(effect); effect = null; }
        }


        void Randomize()
        {
            param.color = Color.HSVToRGB(Random.value, Random.Range(0.55f, 1f), 1f);

            param.flashSize = Random.Range(0.6f, 1.6f);
            param.flashLifetime = Random.Range(0.1f, 0.3f);

            param.sparkCount = Random.Range(12, 60);
            param.sparkSpeedMin = Random.Range(2f, 6f);
            param.sparkSpeedMax = param.sparkSpeedMin + Random.Range(2f, 6f);
            param.sparkSizeMin = Random.Range(0.04f, 0.12f);
            param.sparkSizeMax = param.sparkSizeMin + Random.Range(0.04f, 0.16f);
            param.sparkLifetimeMin = Random.Range(0.15f, 0.4f);
            param.sparkLifetimeMax = param.sparkLifetimeMin + Random.Range(0.1f, 0.4f);
            param.sparkGravity = Random.Range(-0.2f, 1f);
            param.sparkStretch = Random.Range(1f, 4f);

            param.emberCount = Random.Range(6, 24);
            param.emberSpeedMin = Random.Range(0.2f, 1f);
            param.emberSpeedMax = param.emberSpeedMin + Random.Range(0.3f, 1.2f);
            param.emberSizeMin = Random.Range(0.06f, 0.16f);
            param.emberSizeMax = param.emberSizeMin + Random.Range(0.05f, 0.2f);
            param.emberLifetimeMin = Random.Range(0.3f, 0.6f);
            param.emberLifetimeMax = param.emberLifetimeMin + Random.Range(0.2f, 0.6f);

            Rebuild();
            Repaint();
        }


        void OnEditorUpdate()
        {
            if (playing) Repaint();   // 재생 중이면 매 에디터 프레임 다시 그림
        }


        void ExportPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "프리셋 내보내기", "LaserImpactPreset", "asset", "저장 위치를 선택하세요");
            if (string.IsNullOrEmpty(path)) return;

            var preset = CreateInstance<LaserImpactPreset>();
            preset.param = param.Clone();
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            presetField = preset;
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
            Debug.Log($"[LaserImpact] 프리셋 저장: {path}");
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
