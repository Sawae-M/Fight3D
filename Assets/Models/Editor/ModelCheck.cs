using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// キャラの3Dモデル(Blender で作った FBX)が正しく読み込めているかを調べるエディタ用の道具。
/// - メニュー「Fight3D/モデルをチェック」、または batchmode の -executeMethod ModelCheck.RunBatch で動く
/// - 調べること: Humanoid になっているか / アニメのクリップが全部あるか / シェーダーにエラーが無いか(ピンクにならないか)
/// - いくつかのアニメのポーズを画像に撮って Logs/ModelCheck に保存する(見た目の確認用)
/// </summary>
public static class ModelCheck
{
    /// <summary>モデルの置き場所</summary>
    const string ModelFolder = "Assets/Models/Characters/Resources/Fighters/";

    /// <summary>結果と画像を書き出す場所(プロジェクトの Logs の中。git には入らない。Temp は Unity を閉じると消えるので使わない)</summary>
    const string OutFolder = "Logs/ModelCheck";

    /// <summary>キャラごとの、必ずあるはずのクリップ名(Blender の kagetsu_anims.py / gouga_anims.py と同じ)</summary>
    static readonly Dictionary<string, string[]> RequiredClips = new Dictionary<string, string[]>
    {
        { "Kagetsu", new[] { "Idle", "WalkForward", "WalkBack", "StrafeLeft", "StrafeRight", "Light", "Medium", "Heavy",
            "KageUgachi", "KageUgachi3", "MikazukiBarai", "KamenKaihou", "MeigetsuZan", "MeigetsuZanRush",
            "Guard", "Dodge", "Hit", "Stagger", "Launch", "KO", "Win" } },
        { "Gouga", new[] { "Idle", "WalkForward", "WalkBack", "StrafeLeft", "StrafeRight", "Light", "Medium", "Heavy",
            "Gansai", "Gouwan", "Uketome", "UketomeCounter", "DaichiKuzushi", "DaichiKuzushiRush",
            "Guard", "Dodge", "Hit", "Stagger", "Launch", "KO", "Win" } },
    };

    /// <summary>画像に撮るポーズ(クリップ名, 何秒目) [仮]</summary>
    static readonly (string clip, float time)[] Shots =
    {
        ("Idle", 0f), ("Light", 0.13f), ("Heavy", 0.3f), ("Guard", 0f), ("Hit", 0.05f), ("KO", 1f),
    };

    /// <summary>メニューから動かす</summary>
    [MenuItem("Fight3D/モデルをチェック")]
    public static void RunFromMenu()
    {
        bool ok = Check(out string report);
        EditorUtility.DisplayDialog("モデルのチェック", (ok ? "問題なし\n\n" : "問題あり\n\n") + report, "OK");
    }

    /// <summary>batchmode から動かす。終わったら Unity を閉じる(問題なし 0 / 問題あり 1)</summary>
    public static void RunBatch()
    {
        bool ok = false;
        try { ok = Check(out _); }
        catch (System.Exception e) { File.WriteAllText(Path.Combine(OutFolder, "error.txt"), e.ToString()); }
        EditorApplication.Exit(ok ? 0 : 1);
    }

    /// <summary>全部のキャラを調べて、結果を report.txt に書く。問題が無ければ true</summary>
    static bool Check(out string report)
    {
        Directory.CreateDirectory(OutFolder);
        var sb = new StringBuilder();
        bool ok = true;
        if (EditorUtility.scriptCompilationFailed) { sb.AppendLine("NG: スクリプトのコンパイルに失敗している"); ok = false; }

        foreach (var kv in RequiredClips)
            ok &= CheckModel(kv.Key, kv.Value, sb);

        report = sb.ToString();
        File.WriteAllText(Path.Combine(OutFolder, "report.txt"), (ok ? "RESULT: OK\n" : "RESULT: NG\n") + report);
        return ok;
    }

    /// <summary>1体のモデルを調べる</summary>
    static bool CheckModel(string name, string[] required, StringBuilder sb)
    {
        bool ok = true;
        string path = ModelFolder + name + ".fbx";
        sb.AppendLine($"== {name} ({path})");
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (importer == null || prefab == null) { sb.AppendLine("NG: モデルが見つからない"); return false; }

        // Humanoid
        sb.AppendLine($"rig: {importer.animationType}");
        var animator = prefab.GetComponent<Animator>();
        bool human = animator != null && animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid;
        sb.AppendLine(human ? "OK: Humanoid のアバターがある" : "NG: Humanoid のアバターが無い・無効");
        ok &= human;

        // クリップ
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name, c => c);
        var missing = required.Where(r => !clips.ContainsKey(r)).ToList();
        sb.AppendLine($"clips: {clips.Count} 個" + (missing.Count == 0 ? "(全部ある)" : $" / NG: 足りない {string.Join(", ", missing)}"));
        ok &= missing.Count == 0;
        foreach (var loop in new[] { "Idle", "Guard", "WalkForward" })
            if (clips.TryGetValue(loop, out var lc) && !lc.isLooping) { sb.AppendLine($"NG: {loop} がループになっていない"); ok = false; }

        // マテリアルとシェーダー
        var shaders = new HashSet<Shader>();
        foreach (var r in prefab.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m != null) shaders.Add(m.shader);
        foreach (var s in shaders)
        {
            bool err = s == null || ShaderUtil.ShaderHasError(s) || s.name == "Hidden/InternalErrorShader";
            sb.AppendLine($"shader: {(s != null ? s.name : "null")} {(err ? "NG: エラーあり(ピンクになる)" : "OK")}");
            ok &= !err;
        }

        // ポーズの画像
        if (human) RenderShots(name, prefab, clips, sb);
        return ok;
    }

    /// <summary>いくつかのアニメのポーズを撮って PNG に保存する</summary>
    static void RenderShots(string name, GameObject prefab, Dictionary<string, AnimationClip> clips, StringBuilder sb)
    {
        var root = new GameObject("ModelCheckStage");
        try
        {
            var model = Object.Instantiate(prefab, root.transform);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, 150f, 0f);  // キャラの前の上から照らす
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(root.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.27f, 0.32f);
            cam.fieldOfView = 30f;
            cam.transform.position = new Vector3(2.6f, 1.4f, 5.2f);  // 斜め前から(キャラは +Z を向いている)
            cam.transform.LookAt(new Vector3(0f, 1.0f, 0f));
            var rt = new RenderTexture(512, 512, 24);
            cam.targetTexture = rt;

            var animator = model.GetComponent<Animator>();
            var graph = PlayableGraph.Create("ModelCheck");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
            foreach (var (clipName, time) in Shots)
            {
                if (!clips.TryGetValue(clipName, out var clip)) continue;
                var playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);
                playable.SetTime(time);
                graph.Evaluate(0f);
                // ポーズがクリップごとに変わっているかを数字でも見る(右手と腰の位置)
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hand != null && hips != null)
                    sb.AppendLine($"pose {clipName}@{time:0.00}s: 右手 {hand.position.ToString("F2")} 腰 {hips.position.ToString("F2")} (長さ {clip.length:0.00}s)");
                // 注意: batchmode のエディタでは、体の形(スキン)の更新が描画に間に合わず、
                // 画像のポーズが前のままになることがある。画像は「色・シェーダーが正しいか」を見る用で、
                // ポーズが正しいかは上の数字(右手と腰の位置)で見る
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(OutFolder, $"{name}_{clipName}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                playable.Destroy();
            }
            graph.Destroy();
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            sb.AppendLine("shots: Logs/ModelCheck に保存");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
