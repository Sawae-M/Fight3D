using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Fight3D.Tests
{
    /// <summary>
    /// コントローラーで操作できるかを確かめるプレイテスト。
    /// 本物のコントローラーの代わりに、Input System のテスト用の仮のコントローラーでボタンを押す。
    /// </summary>
    public class GamepadTest : InputTestFixture
    {
        /// <summary>X で弱攻撃が出る / SELECT + X でサンドバッグのガードが切り替わり、技は出ない / SELECT + RB でキャラが切り替わる</summary>
        [UnityTest]
        public IEnumerator GamepadButtonsWork()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            yield return null;

            // 仮のコントローラーを付けた後で、トレーニングの場面を作り直す(入力の準備をやり直すため)
            if (GameBootstrap.Mode != GameMode.Training) GameBootstrap.SwitchMode();
            else GameBootstrap.SwitchCharacter();
            yield return null;
            yield return null;
            var fighter = FindPlayer();
            Assert.IsNotNull(fighter, "自分のキャラがいない");

            // X(□)= 弱攻撃
            Press(pad.buttonWest);
            yield return WaitFrames(4);
            Release(pad.buttonWest);
            Assert.IsTrue(fighter.IsAttacking, "X を押したのに技が出ていない");
            Assert.AreEqual("弱攻撃", fighter.CurrentMoveName);
            yield return new WaitForSeconds(1f);

            // SELECT + X = サンドバッグのガード ON/OFF(技は出ない)
            var sandbag = Object.FindAnyObjectByType<Sandbag>();
            Assert.IsNotNull(sandbag, "サンドバッグがいない");
            bool guardBefore = sandbag.GuardEnabled;
            Press(pad.selectButton);
            yield return null;
            Press(pad.buttonWest);
            yield return WaitFrames(4);
            Release(pad.buttonWest);
            Assert.AreNotEqual(guardBefore, sandbag.GuardEnabled, "SELECT + X でサンドバッグのガードが切り替わっていない");
            Assert.IsFalse(fighter.IsAttacking, "SELECT を押しているのに技が出た");

            // SELECT + RB = キャラ切り替え
            var before = GameBootstrap.Selected;
            Press(pad.rightShoulder);
            yield return WaitFrames(3);
            Release(pad.rightShoulder);
            Release(pad.selectButton);
            yield return null;
            Assert.AreNotEqual(before, GameBootstrap.Selected, "SELECT + RB でキャラが切り替わっていない");
        }

        /// <summary>トレーニングの自分のキャラを探す</summary>
        static FighterController FindPlayer()
        {
            foreach (var f in Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None))
                if (f.TrainingMode) return f;
            return null;
        }

        /// <summary>n フレーム待つ</summary>
        static IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }
    }
}
