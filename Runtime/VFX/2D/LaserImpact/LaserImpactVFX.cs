using UnityEngine;

namespace VFX
{
    // 레이저 명중 VFX의 조절 가능한 파라미터 묶음. 에디터 툴/런타임 양쪽에서 공유한다.
    [System.Serializable]
    public class LaserImpactParams
    {
        public Color color = new Color(0.35f, 0.9f, 1f);

        [Header("Flash (섬광)")]
        public float flashSize = 0.95f;
        public float flashLifetime = 0.18f;

        [Header("Sparks (불똥)")]
        public int sparkCount = 28;
        public float sparkSpeedMin = 4f;
        public float sparkSpeedMax = 9f;
        public float sparkSizeMin = 0.06f;
        public float sparkSizeMax = 0.16f;
        public float sparkLifetimeMin = 0.2f;
        public float sparkLifetimeMax = 0.5f;
        public float sparkGravity = 0.4f;
        public float sparkStretch = 2.5f;

        [Header("Embers (잔광)")]
        public int emberCount = 12;
        public float emberSpeedMin = 0.3f;
        public float emberSpeedMax = 1.2f;
        public float emberSizeMin = 0.08f;
        public float emberSizeMax = 0.2f;
        public float emberLifetimeMin = 0.4f;
        public float emberLifetimeMax = 0.8f;

        [Header("Render")]
        public int sortingOrder = 100;   // 2D에서 다른 스프라이트와의 앞뒤 순서

        public LaserImpactParams Clone()
        {
            return (LaserImpactParams)MemberwiseClone();
        }
    }


    // 2D 레이저 명중 VFX. 섬광(Flash)+불똥(Sparks)+잔광(Embers) 3겹 파티클을 코드로 생성한다.
    // 2D(직교 카메라)용으로 Sprites/Default 셰이더를 써서 알파 falloff를 보존한다(URP 파티클 셰이더의
    // '흰 네모' 함정 회피). 색은 파티클 startColor로 입히므로 머티리얼은 흰색 1개를 공유한다.
    public static class LaserImpactVFX
    {
        static Material glowMat;

        // 이펙트를 생성해 반환한다(자동 삭제 없음). 에디터 미리보기/직접 제어용.
        public static GameObject Build(Vector3 pos, LaserImpactParams p)
        {
            if (p == null) p = new LaserImpactParams();
            EnsureMaterial();

            var root = new GameObject("LaserImpact");
            root.transform.position = pos;

            BuildFlash(root.transform, p);
            BuildSparks(root.transform, p);
            BuildEmbers(root.transform, p);
            return root;
        }


        // 런타임 편의: 생성 후 life초 뒤 자동 삭제(플레이 모드에서만 삭제 동작).
        public static GameObject Spawn(Vector3 pos, LaserImpactParams p, float life = 2f)
        {
            var go = Build(pos, p);
            if (Application.isPlaying) Object.Destroy(go, life);
            return go;
        }


        // === ① 섬광 ===
        static void BuildFlash(Transform parent, LaserImpactParams p)
        {
            var ps = AddChild(parent, "Flash");
            var main = ps.main;
            main.loop = false; main.playOnAwake = true;
            main.startLifetime = p.flashLifetime;
            main.startSpeed = 0f;
            main.startSize = p.flashSize;
            main.startColor = Color.Lerp(p.color, Color.white, 0.7f);
            main.maxParticles = 10;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });

            var sh = ps.shape; sh.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.Lerp(p.color, Color.white, 0.8f), p.color, 1f));

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            var cv = new AnimationCurve();
            cv.AddKey(0f, 0.5f); cv.AddKey(0.3f, 1f); cv.AddKey(1f, 1.3f);
            sz.size = new ParticleSystem.MinMaxCurve(1f, cv);

            SetupRenderer(ps.gameObject, ParticleSystemRenderMode.Billboard, p.sortingOrder + 1);
        }


        // === ② 불똥 ===
        static void BuildSparks(Transform parent, LaserImpactParams p)
        {
            var ps = AddChild(parent, "Sparks");
            var main = ps.main;
            main.loop = false; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(p.sparkLifetimeMin, p.sparkLifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(p.sparkSpeedMin, p.sparkSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(p.sparkSizeMin, p.sparkSizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, p.color);
            main.gravityModifier = p.sparkGravity;
            main.maxParticles = Mathf.Max(10, p.sparkCount * 2);

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(p.sparkCount, 0, 1000)) });

            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, p.color, 0.6f));

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            var cv = new AnimationCurve(); cv.AddKey(0f, 1f); cv.AddKey(1f, 0f);
            sz.size = new ParticleSystem.MinMaxCurve(1f, cv);

            var r = SetupRenderer(ps.gameObject, ParticleSystemRenderMode.Stretch, p.sortingOrder + 2);
            r.lengthScale = p.sparkStretch;
            r.velocityScale = 0.08f;
        }


        // === ③ 잔광 ===
        static void BuildEmbers(Transform parent, LaserImpactParams p)
        {
            var ps = AddChild(parent, "Embers");
            var main = ps.main;
            main.loop = false; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(p.emberLifetimeMin, p.emberLifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(p.emberSpeedMin, p.emberSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(p.emberSizeMin, p.emberSizeMax);
            main.startColor = p.color;
            main.gravityModifier = -0.05f;
            main.maxParticles = Mathf.Max(10, p.emberCount * 2);

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(p.emberCount, 0, 1000)) });

            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;

            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(p.color, 0f), new GradientColorKey(p.color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            SetupRenderer(ps.gameObject, ParticleSystemRenderMode.Billboard, p.sortingOrder);
        }


        static ParticleSystem AddChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<ParticleSystem>();
        }


        static ParticleSystemRenderer SetupRenderer(GameObject go, ParticleSystemRenderMode mode, int order)
        {
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = glowMat;
            r.renderMode = mode;
            r.sortingOrder = order;
            return r;
        }


        static void EnsureMaterial()
        {
            if (glowMat != null) return;
            glowMat = new Material(Shader.Find("Sprites/Default"));
            glowMat.SetTexture("_MainTex", MakeSoftDot(128));
        }


        // 가운데 불투명 -> 가장자리 투명한 둥근 점 텍스처
        static Texture2D MakeSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }


        // 색 from->to, 알파는 peak 유지 후 끝에서 0으로 페이드
        static Gradient FadeGradient(Color from, Color to, float peakAlpha)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
