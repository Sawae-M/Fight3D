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
    /// | スキル     | P          | RB(R1)                    |
    /// | ガード     | J          | LB(L1)                    |
    /// | 回避       | K          | A(×)                      |
    /// | リセット   | R          | START / OPTIONS           |
    /// </summary>
    public class FightInput : System.IDisposable
    {
        public const string ResourceName = "FightControls";

        readonly InputActionAsset asset;
        readonly bool ownsAsset;

        public readonly InputAction Move, Light, Medium, Heavy, Skill, Guard, Dodge, Reset;

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
            AddButton(map, "Guard", "<Keyboard>/j", "<Gamepad>/leftShoulder");
            AddButton(map, "Dodge", "<Keyboard>/k", "<Gamepad>/buttonSouth");
            AddButton(map, "Reset", "<Keyboard>/r", "<Gamepad>/start");
            return a;
        }

        static void AddButton(InputActionMap map, string name, string key, string pad)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(key);
            action.AddBinding(pad);
        }
    }
}
