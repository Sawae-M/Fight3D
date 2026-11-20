using UnityEngine;

/// <summary>
/// 必要なパッケージ(Input System / Cinemachine / LitMotion)が入っているかを確認する。
/// ゲーム本体(Assets/Scripts)は、3つ全部が入るまでコンパイルされない設定にしてあるので、
/// 足りないときはここで画面と Console に案内を出す。
/// </summary>
public static class Fight3DPackageCheck
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Check()
    {
        if (System.Type.GetType("Fight3D.GameBootstrap, Fight3D") != null) return; // OK

        const string msg =
            "[Fight3D] 必要なパッケージが足りないため、ゲーム本体が読み込まれていません。\n" +
            "Window → Package Management → Package Manager から次の3つを入れてください:\n" +
            "・Input System\n・Cinemachine (3.x)\n・LitMotion (Install package from git URL)\n" +
            "詳しくはセットアップ手順書を見てください。";
        Debug.LogError(msg);
        new GameObject("Fight3D Package Warning").AddComponent<Warning>().Message = msg;
    }

    class Warning : MonoBehaviour
    {
        public string Message;

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = Mathf.RoundToInt(28 * Screen.height / 1080f),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(30, 30, 20, 20)
            };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width * 0.1f, Screen.height * 0.3f, Screen.width * 0.8f, Screen.height * 0.4f), Message, style);
        }
    }
}
