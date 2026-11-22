using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Models/Characters の中に入れたキャラのモデル(FBX)を、
/// 読み込むときに自動で「Humanoid(人型)」の設定にし、アニメーションのクリップも整えるエディタ用スクリプト。
/// Blender から書き出した FBX を置くだけで、人型アニメーションが使える状態になる。
/// </summary>
public class CharacterModelPostprocessor : AssetPostprocessor
{
    /// <summary>この場所の中にあるモデルだけを人型にする</summary>
    private const string CharacterFolder = "Assets/Models/Characters/";

    /// <summary>ループ再生するクリップの名前(待機・歩き・ガード)</summary>
    private static readonly string[] LoopClips = { "Idle", "WalkForward", "WalkBack", "StrafeLeft", "StrafeRight", "Guard" };

    /// <summary>
    /// アセットの読み込みが終わるたびに呼ばれる。トゥーンシェーダーが新しく読み込まれたら、キャラのモデルを読み込み直す。
    /// (初めてプロジェクトを開いたとき、シェーダーより先にモデルが読み込まれると、トゥーンにならないため)
    /// </summary>
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!imported.Any(p => p.EndsWith("Fight3DToon.shader"))) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterFolder.TrimEnd('/') }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
    }

    /// <summary>
    /// モデルを読み込む前に呼ばれる。リグを Humanoid にし、拡大率を 1 にそろえる。
    /// </summary>
    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(CharacterFolder)) return;

        // トゥーンシェーダーが変わったり後から読み込まれたりしたら、このモデルも読み込み直す(マテリアルをトゥーンにするため)
        context.DependsOnSourceAsset(ToonShaderPath);

        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human; // 人型(Humanoid)にする
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; // このモデルから骨の割り当てを作る
        importer.globalScale = 1f;            // Blender 側で拡大率 1 で書き出しているので 1 のまま
        importer.useFileScale = true;         // FBX に書かれた単位(メートル)を使う
        importer.importAnimation = true;      // Blender で作ったアニメを読み込む
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; // Blender の色をそのまま使う
    }

    /// <summary>キャラに使うトゥーン(アニメ調)シェーダーの名前(Assets/Models/Shaders/Fight3DToon.shader)</summary>
    private const string ToonShaderName = "Fight3D/Toon";

    /// <summary>トゥーンシェーダーのファイルの場所</summary>
    private const string ToonShaderPath = "Assets/Models/Shaders/Fight3DToon.shader";

    /// <summary>
    /// モデルのマテリアルを読み込んだ後に呼ばれる。Blender の色を引き継いだまま、トゥーンシェーダーに切り替える。
    /// 名前に Metal / Gold / Blade が入るマテリアルには、金属の小さなハイライトを付ける。
    /// </summary>
    private void OnPostprocessMaterial(Material material)
    {
        if (!assetPath.StartsWith(CharacterFolder)) return;
        var toon = Shader.Find(ToonShaderName);
        if (toon == null) return; // シェーダーがまだ無いときは、今まで通りの見た目のまま

        // Blender で付けた色を先に取っておく(URP では _BaseColor、古い形式では _Color)
        Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;

        material.shader = toon;
        material.SetColor("_BaseColor", baseColor);
        // 輪郭線は、その物の色をうんと暗くした色にする(真っ黒より柔らかく見える)
        material.SetColor("_OutlineColor", baseColor * 0.25f);

        string n = material.name;
        bool shiny = n.Contains("Metal") || n.Contains("Gold") || n.Contains("Blade");
        material.SetFloat("_SpecStrength", shiny ? 0.6f : 0f); // [仮] 金属のハイライトの強さ
    }

    /// <summary>
    /// アニメを読み込む前に呼ばれる。クリップ名から「Armature|」を外し、ループ・その場再生の設定をする。
    /// </summary>
    private void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(CharacterFolder)) return;

        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            // Blender の書き出しで付く「Armature|」を外して、Idle や Light のような名前にする
            int bar = clip.name.LastIndexOf('|');
            if (bar >= 0) clip.name = clip.name.Substring(bar + 1);

            clip.loopTime = LoopClips.Contains(clip.name); // 待機・歩き・ガードはくり返す
            clip.loopPose = clip.loopTime;

            // 位置の移動はゲームのプログラムがするので、アニメは「その場」で再生する。
            // 「Bake Into Pose」をすべて ON にすると、ルートモーションを使わなくても
            // しゃがむ・倒れるなどの上下の動きは見た目に残り、キャラの位置は動かない。
            clip.lockRootRotation = true;        // 向き: ポーズに焼き込む
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;         // 上下: ポーズに焼き込む(倒れたときに体が浮かない)
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;      // 前後左右: ポーズに焼き込む
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
    }
}
