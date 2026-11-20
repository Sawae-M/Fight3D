using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Fight3D
{
    /// <summary>
    /// キーボード入力をまとめて扱うクラス。
    /// Unity の「新しい入力システム(Input System)」と「古い入力(Input Manager)」の
    /// どちらの設定でも動くようにしてある。
    /// </summary>
    public static class GameInput
    {
        public enum Key { W, A, S, D, U, I, O, P, J, K, R }

        /// <summary>押している間ずっと true</summary>
        public static bool Held(Key key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            var k = ToNew(kb, key);
            return k != null && k.isPressed;
#else
            return Input.GetKey(ToOld(key));
#endif
        }

        /// <summary>押した瞬間だけ true</summary>
        public static bool Pressed(Key key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            var k = ToNew(kb, key);
            return k != null && k.wasPressedThisFrame;
#else
            return Input.GetKeyDown(ToOld(key));
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Controls.KeyControl ToNew(Keyboard kb, Key key)
        {
            switch (key)
            {
                case Key.W: return kb.wKey;
                case Key.A: return kb.aKey;
                case Key.S: return kb.sKey;
                case Key.D: return kb.dKey;
                case Key.U: return kb.uKey;
                case Key.I: return kb.iKey;
                case Key.O: return kb.oKey;
                case Key.P: return kb.pKey;
                case Key.J: return kb.jKey;
                case Key.K: return kb.kKey;
                case Key.R: return kb.rKey;
            }
            return null;
        }
#else
        static KeyCode ToOld(Key key)
        {
            switch (key)
            {
                case Key.W: return KeyCode.W;
                case Key.A: return KeyCode.A;
                case Key.S: return KeyCode.S;
                case Key.D: return KeyCode.D;
                case Key.U: return KeyCode.U;
                case Key.I: return KeyCode.I;
                case Key.O: return KeyCode.O;
                case Key.P: return KeyCode.P;
                case Key.J: return KeyCode.J;
                case Key.K: return KeyCode.K;
                case Key.R: return KeyCode.R;
            }
            return KeyCode.None;
        }
#endif
    }
}
