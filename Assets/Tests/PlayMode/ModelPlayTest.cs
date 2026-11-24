using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fight3D.Tests
{
    /// <summary>
    /// 本番の3Dモデルで試合が動くかを確かめるプレイテスト。
    /// CPU同士で試合をさせて、流れたアニメの名前を記録し、ゲーム画面をときどき撮影して Logs/PlayCheck に保存する。
    /// Unity の Test Runner(Window > General > Test Runner の PlayMode)か、batchmode の -runTests で動く。
    /// </summary>
    public class ModelPlayTest
    {
        const float PlaySeconds = 30f;     // 試合を動かす時間(秒) [仮]
        const float ShotInterval = 2f;     // 画面を撮る間隔(秒) [仮]
        const string OutFolder = "Logs/PlayCheck";  // 結果の置き場所(git には入らない)

        /// <summary>CPU同士の試合で、2人とも本番モデルになり、いろいろなアニメが流れることを確かめる</summary>
        [UnityTest]
        public IEnumerator BattleWithModelsPlaysAnimations()
        {
            Directory.CreateDirectory(OutFolder);
            yield return null;  // ▶ 直後の自動の組み立て(トレーニング)を待つ

            // CPU対戦に切り替え、自分のキャラも CPU に動かしてもらう(キーボードを使わずに試合を進めるため)
            GameBootstrap.Selected = CharacterId.Kagetsu;
            if (GameBootstrap.Mode != GameMode.CpuBattle) GameBootstrap.SwitchMode();
            var fighters = Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None);
            foreach (var f in fighters)
                if (f.Cpu == null) f.Cpu = new CpuBrain();
            yield return null;

            fighters = Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None);
            var animators = Object.FindObjectsByType<CharacterAnimator>(FindObjectsSortMode.None);
            Assert.AreEqual(2, fighters.Length, "キャラが2人いない");
            Assert.AreEqual(2, animators.Length, "本番モデル(アニメ付き)になっていないキャラがいる");

            var seen = animators.ToDictionary(a => a, a => new List<string>());
            float t = 0f, nextShot = 0f, nextFxShot = 0f;
            int shot = 0, fxShot = 0, sparkFrames = 0, trailFrames = 0;
            while (t < PlaySeconds)
            {
                foreach (var a in animators)
                {
                    var list = seen[a];
                    if (a.CurrentClip != null && (list.Count == 0 || list[list.Count - 1] != a.CurrentClip)) list.Add(a.CurrentClip);
                }
                // 攻撃の演出: 火花が出ている・剣の軌跡が出ているフレームを数え、火花はときどき撮る
                bool spark = GameObject.Find("HitSpark") != null;
                if (spark) sparkFrames++;
                if (Object.FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None).Any(tr => tr.emitting)) trailFrames++;
                if (spark && fxShot < 6 && t >= nextFxShot)
                {
                    Capture(Path.Combine(OutFolder, $"fx_{fxShot:00}.png"));
                    fxShot++;
                    nextFxShot = t + 1.5f;
                }
                if (t >= nextShot)
                {
                    Capture(Path.Combine(OutFolder, $"shot_{shot:00}.png"));
                    shot++;
                    nextShot += ShotInterval;
                }
                yield return null;
                t += Time.deltaTime;
            }

            // 結果を書き出す(流れた順のアニメの名前)
            var sb = new StringBuilder();
            foreach (var kv in seen)
            {
                var distinct = kv.Value.Distinct().ToList();
                sb.AppendLine($"== {kv.Key.name}: {distinct.Count} 種類 [{string.Join(", ", distinct)}]");
                sb.AppendLine("順番: " + string.Join(" > ", kv.Value.Take(80)));
            }
            sb.AppendLine($"演出: 火花が出ていたフレーム {sparkFrames} / 剣の軌跡が出ていたフレーム {trailFrames}");
            File.WriteAllText(Path.Combine(OutFolder, "clips.txt"), sb.ToString());

            Assert.Greater(sparkFrames, 0, "攻撃が当たったのに火花が一度も出ていない");
            Assert.Greater(trailFrames, 0, "カゲツの剣の軌跡が一度も出ていない");
            foreach (var kv in seen)
                Assert.GreaterOrEqual(kv.Value.Distinct().Count(), 3, $"{kv.Key.name} のアニメが3種類も流れていない");
        }

        /// <summary>メインカメラの画面を PNG に保存する</summary>
        static void Capture(string path)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rt = new RenderTexture(960, 540, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);
        }
    }
}
