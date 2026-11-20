# Fight3D(仮タイトル)

「月蝕の闘技場」を舞台にした 3D 対戦格闘ゲーム。Unity 6.3 LTS で作っています。

## いまできること(マイルストーン1)

- 半径10mの円形ステージ(円の外には出られない見えない壁)
- カゲツ(人形の仮モデル)を WASD で動かせる
- カメラはカゲツの後ろから、サンドバッグの方を向いて追いかける
- U / I / O で 弱 / 中 / 強 攻撃。サンドバッグに当たるとダメージ数字が出る
- 弱 → 中 → 強 の連続技(当たっているとつながる)、パッシブ「連刃」のダメージアップ
- R キーで位置とサンドバッグをリセット

## 使っているパッケージ

| パッケージ | 用途 | 入れ方 |
| --- | --- | --- |
| Input System | 入力(キーボード・コントローラー) | Package Manager → Unity Registry |
| Cinemachine 3.x | 背後カメラ | Package Manager → Unity Registry |
| LitMotion 2.x | 見た目の動き(揺れ・ダメージ数字など) | Package Manager → + → Install package from git URL → `https://github.com/annulusgames/LitMotion.git?path=src/LitMotion/Assets/LitMotion` |

3つ全部が入るまでゲーム本体(`Assets/Scripts`)はコンパイルされません(`Fight3D.asmdef` の設定)。足りないときは ▶ を押すと案内が出ます。

## 操作

| 操作 | キーボード | コントローラー(仮) |
| --- | --- | --- |
| 近づく / 離れる / 回る | W / S / A・D | 左スティック・十字キー |
| 弱 / 中 / 強 攻撃 | U / I / O | X(□) / Y(△) / B(○) |
| リセット | R | START |

キー配置は `Assets/Resources/FightControls.inputactions` をダブルクリックすると変更できます。

## ファイルの説明(Assets/Scripts)

| ファイル | 役割 |
| --- | --- |
| GameBootstrap.cs | ▶ を押すと場面(ステージ・キャラ・カメラ・表示)を自動で組み立てる |
| FighterController.cs | カゲツの移動と攻撃 |
| AttackData.cs | 弱・中・強のダメージやフレーム数(数値を変えたいときはここ) |
| Sandbag.cs | サンドバッグ(体力・連続技の記録・揺れ) |
| CameraRig.cs | 背後カメラ(Cinemachine) |
| TrainingHUD.cs | ダメージ数字と右上・左上の表示 |
| DollBuilder.cs | 人形の仮モデルを図形で作る |
| Arena.cs | ステージの大きさと見えない壁 |
| FightInput.cs | 入力(Input System) |

シーンに何も置かなくても、▶ を押せば動きます。
