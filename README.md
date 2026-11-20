# Fight3D(仮タイトル)

「月蝕の闘技場」を舞台にした 3D 対戦格闘ゲーム。Unity 6.3 LTS で作っています。

## いまできること

- 半径10mの円形ステージ(円の外には出られない見えない壁)
- カゲツ(人形の仮モデル)を WASD で動かせる
- カメラはカゲツの後ろから、サンドバッグの方を向いて追いかける
- U / I / O で 弱 / 中 / 強 攻撃。サンドバッグに当たるとダメージ数字が出る
- 弱 → 中 → 強 の連続技(当たっているとつながる)、パッシブ「連刃」のダメージアップ
- R キーで位置とサンドバッグをリセット
- ガード(J)と回避ステップ(K、無敵あり)
- カゲツのスキル: 影穿ち(P、3連続目は踏み込み+よろけ)、三日月払い(W+P、ガード崩し)、仮面解放(S+P、速度UP+呪印)、ウルト冥月斬(O+P、ゲージ100)
- ウルトゲージ(当てたダメージの10%、ガードされると3)
- サンドバッグ: よろけ・浮き(追い打ち0.7倍)、F1でガードする設定、F2でゲージ満タン

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
| スキル1 影穿ち / スキル2 三日月払い / スキル3 仮面解放 | P / W+P / S+P | RB / 上+RB / 下+RB |
| ウルト 冥月斬 | O+P 同時押し | B+RB |
| ガード / 回避 | J / K | LB / A(×) |
| リセット | R | START |
| (トレーニング)サンドバッグのガード / ゲージ満タン | F1 / F2 | — |

キー配置は `Assets/Resources/FightControls.inputactions` をダブルクリックすると変更できます。

## ファイルの説明(Assets/Scripts)

| ファイル | 役割 |
| --- | --- |
| GameBootstrap.cs | ▶ を押すと場面(ステージ・キャラ・カメラ・表示)を自動で組み立てる |
| FighterController.cs | カゲツの移動・攻撃・ガード・回避 |
| MoveData.cs | 技のダメージやフレーム数(数値を変えたいときはここ) |
| KagetsuSkills.cs | カゲツのスキルの回数・使えない時間・仮面解放 |
| Sandbag.cs | サンドバッグ(体力・連続技の記録・揺れ) |
| CameraRig.cs | 背後カメラ(Cinemachine) |
| TrainingHUD.cs | ダメージ数字と右上・左上の表示 |
| DollBuilder.cs | 人形の仮モデルを図形で作る |
| Arena.cs | ステージの大きさと見えない壁 |
| FightInput.cs | 入力(Input System) |

シーンに何も置かなくても、▶ を押せば動きます。
