using System.Linq;
using UnityEditor;

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
    /// モデルを読み込む前に呼ばれる。リグを Humanoid にし、拡大率を 1 にそろえる。
    /// </summary>
    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(CharacterFolder)) return;

        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human; // 人型(Humanoid)にする
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; // このモデルから骨の割り当てを作る
        importer.globalScale = 1f;            // Blender 側で拡大率 1 で書き出しているので 1 のまま
        importer.useFileScale = true;         // FBX に書かれた単位(メートル)を使う
        importer.importAnimation = true;      // Blender で作ったアニメを読み込む
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; // Blender の色をそのまま使う
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
