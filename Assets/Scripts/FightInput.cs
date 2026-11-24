using UnityEngine;
using UnityEngine.InputSystem;

namespace Fight3D
{
    /// <summary>
    /// 入力(Input System)。キーボードとコントローラーの両方で操作できる。
    ///
    /// キー配置は Assets/Resources/FightControls.inputactions に書いてある。
    /// Unity でこのファイルをダブルクリックすると、画面上でキー配置を変更できる。
    ///
    /// | 操作       | キーボード | コントローラー(仮)        |
    /// | 移動       | W A S D    | 左スティック / 十字キー   |
    /// | 弱/中/強   | U / I / O  | X(□) / Y(△) / B(○)       |
    /// | スキル     | P          | RB(R1) / RT(R2)           |
    /// | ガード     | J          | LB(L1) / LT(L2)           |
    /// | 回避       | K          | A(×)                      |
    /// | リセット   | R          | START / OPTIONS           |
    ///
    /// トレーニング設定や切り替え(F1〜F6)は ShortcutInput を見る(コントローラーは SELECT を押しながらボタン)。
    /// </summary>
    public class FightInput : System.IDisposable
    {
        public const string ResourceName = "FightControls";  // Assets/Resources にあるキー配置ファイルの名前(拡張子なし)

        readonly InputActionAsset asset;  // 読み込んだキー配置(複製したもの)
        readonly bool ownsAsset;  // 自分で作った/複製したキー配置か(片付けるときに消す)

        public readonly InputAction Move, Light, Medium, Heavy, Skill, Guard, Dodge, Reset;  // 各操作。名前は .inputactions の Action 名と同じ

        /// <summary>キー配置ファイルを読み込んで、入力を受け付け始める</summary>
        public FightInput()
        {
            var loaded = Resources.Load<InputActionAsset>(ResourceName);
            if (loaded != null)
            {
                // 同じファイルを複数人で使っても影響し合わないように複製して使う
                asset = Object.Instantiate(loaded);
            }
            else
            {
                Debug.LogWarning("[Fight3D] Resources/FightControls.inputactions が見つからないので、初期のキー配置を使います。");
                asset = BuildDefault();
            }
            ownsAsset = true;

            var map = asset.FindActionMap("Fight", true);
            Move = map.FindAction("Move", true);
            Light = map.FindAction("Light", true);
            Medium = map.FindAction("Medium", true);
            Heavy = map.FindAction("Heavy", true);
            Skill = map.FindAction("Skill", true);
            Guard = map.FindAction("Guard", true);
            Dodge = map.FindAction("Dodge", true);
            Reset = map.FindAction("Reset", true);
            map.Enable();
        }

        /// <summary>移動の入力。y = 前(相手に近づく)/後ろ、x = 右/左</summary>
        public Vector2 MoveValue => Move.ReadValue<Vector2>();

        /// <summary>入力の受け付けをやめて、複製したキー配置を片付ける</summary>
        public void Dispose()
        {
            asset.Disable();
            if (ownsAsset) Object.Destroy(asset);
        }

        /// <summary>ファイルが無かったときの予備(内容は .inputactions と同じ)</summary>
        static InputActionAsset BuildDefault()
        {
            var a = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = a.AddActionMap("Fight");

            var move = map.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            AddButton(map, "Light", "<Keyboard>/u", "<Gamepad>/buttonWest");
            AddButton(map, "Medium", "<Keyboard>/i", "<Gamepad>/buttonNorth");
            AddButton(map, "Heavy", "<Keyboard>/o", "<Gamepad>/buttonEast");
            AddButton(map, "Skill", "<Keyboard>/p", "<Gamepad>/rightShoulder");
            map.FindAction("Skill").AddBinding("<Gamepad>/rightTrigger");
            AddButton(map, "Guard", "<Keyboard>/j", "<Gamepad>/leftShoulder");
            map.FindAction("Guard").AddBinding("<Gamepad>/leftTrigger");
            AddButton(map, "Dodge", "<Keyboard>/k", "<Gamepad>/buttonSouth");
            AddButton(map, "Reset", "<Keyboard>/r", "<Gamepad>/start");
            return a;
        }

        /// <summary>ボタン1つ分の操作(キーボードのキーとコントローラーのボタン)を追加する</summary>
        static void AddButton(InputActionMap map, string name, string key, string pad)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(key);
            action.AddBinding(pad);
        }
    }

    /// <summary>
    /// トレーニング設定・切り替え・やり直しの操作(キーボードとコントローラー)。
    /// コントローラーは SELECT(VIEW / SHARE)を押しながら、もう1つのボタンを押す。
    /// SELECT を押している間は、攻撃などのボタンは出ない(FighterController が MenuHeld を見る)。
    ///
    /// | 操作                         | キーボード | コントローラー            |
    /// | サンドバッグのガード ON/OFF   | F1         | SELECT + X(□)            |
    /// | ゲージ満タン                 | F2         | SELECT + Y(△)            |
    /// | サンドバッグが殴ってくる ON/OFF | F3       | SELECT + B(○)            |
    /// | 自分の体力を減らす           | F4         | SELECT + A(×)            |
    /// | キャラ切り替え               | F5         | SELECT + RB(R1)           |
    /// | トレーニング ⇔ CPU対戦       | F6         | SELECT + LB(L1)           |
    /// | 試合をやり直す(CPU対戦)      | R          | START(OPTIONS)           |
    /// </summary>
    public static class ShortcutInput
    {
        /// <summary>コントローラーの SELECT を押しているか(押している間は、攻撃などの代わりに設定の操作になる)</summary>
        public static bool MenuHeld => Gamepad.current != null && Gamepad.current.selectButton.isPressed;

        public static bool ToggleSandbagGuard => Pressed(k => k.f1Key, p => p.buttonWest);   // F1
        public static bool FillGauge => Pressed(k => k.f2Key, p => p.buttonNorth);           // F2
        public static bool ToggleSandbagAttack => Pressed(k => k.f3Key, p => p.buttonEast);  // F3
        public static bool CycleHp => Pressed(k => k.f4Key, p => p.buttonSouth);             // F4
        public static bool SwitchCharacter => Pressed(k => k.f5Key, p => p.rightShoulder);   // F5
        public static bool SwitchMode => Pressed(k => k.f6Key, p => p.leftShoulder);         // F6

        /// <summary>試合をやり直す(R / START)。START は SELECT を押していなくてよい</summary>
        public static bool RestartMatch =>
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        /// <summary>キーボードのキーが押された、または SELECT を押しながらコントローラーのボタンが押された瞬間か</summary>
        static bool Pressed(System.Func<Keyboard, UnityEngine.InputSystem.Controls.KeyControl> key,
            System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> pad)
        {
            var kb = Keyboard.current;
            if (kb != null && key(kb).wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.selectButton.isPressed && pad(gp).wasPressedThisFrame;
        }
    }
}