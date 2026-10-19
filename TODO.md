# LiveMarkdown.Avalonia.DrawIo TODO

draw.io (mxGraph) ネイティブ描画プラグインの未対応項目一覧。
`[x]` = 対応済み、`[~]` = 簡易対応（近似）、`[ ]` = 未対応。

## サマリ

- 主要機能（基本図形・マーカー・変形/効果・ラベル・エッジ配線・構造/ページ・画像）は
  **おおむね実装済み**。
- 残件は (a) 網羅性の追加（特殊ステンシル・追加スタイル）、(b) 高度な機能
  （手描き風・障害物回避・リモート画像）、(c) 付随機能（リンク・黄金画像テスト）。
- 推奨着手順: **1（プリセットステンシル）→ 4（リッチ HTML）→ 7（その他）**。
  表現力を最も広げるのは 1。

進捗（おおよそ）: 項目 1〜6 は 80〜95% 実装、項目 7 は未着手。

---

## 1. 基本図形の拡充

対応済み:
- [x] 矩形 (`rounded=0`) / 角丸矩形 (`rounded=1`, `arcSize`)
- [x] 楕円 (`ellipse`) / 三角形 (`triangle`, `direction`)
- [x] 汎用ポリゴン (`mxgraph.basic.polygon`, `polyCoords`/`polyCurves`)
- [x] ひし形 (`rhombus`) / 六角形 (`hexagon`) / 平行四辺形 (`parallelogram`)
      / 台形 (`trapezoid`) / ステップ (`step`)
- [x] 円柱 (`cylinder`/`cylinder3`) / 雲 (`cloud`) / アクター (`umlActor`)
- [x] 内部記憶 (`internalStorage`) / 書類 (`document`) / 手動入力 (`manualInput`)
- [x] カード (`card`) / 注記 (`note`) / 吹き出し (`callout`)
- [x] 二重矢印/片矢印 (`doubleArrow`/`singleArrow`) / 二重楕円 (`doubleEllipse`) / 線 (`line`)
- [x] プロセス (`process`) / ループリミット (`loopLimit`) / オフページコネクタ (`offPageConnector`)
- [x] テープ (`tape`) / 遅延 (`delay`) / キューブ (`cube`) / ディスプレイ (`display`)
      / データストレージ (`dataStorage`)

追加対応（純ジオメトリで表現可能なもの）:
- [x] フローチャート/汎用: `terminator`(stadium), `predefinedProcess`, `sequential_data`,
      `multi-document`, `stored_data`, `manual_operation`, `merge_or_storage`, `sumEllipse`,
      `cross2`, `star`, `octagon`, `pentagon`, `orthogonal_triangle`, `tee`, `folder`
- [x] 論理ゲート: `or` / `and` / `xor`
- [x] `mxgraph.flowchart.*` は最後のセグメント（`decision`/`terminator`/`predefined_process`
      等）で基本図形へ解決

未対応（優先度高）:
- [x] `mxStencil` XML（`<shape>`/`<foreground>`/`<background>`/`<path>` の描画 DSL）の
      汎用パーサ `DrawIoStencil` を実装。`RegisterStencilSet`/`Register` で任意のステンシル
      セットを登録すると、presenter が矩形フォールバックの代わりに描画する。
- [ ] ステンシルセット（`mxgraph.flowchart.*` / `mxgraph.aws4.*` / `mxgraph.azure.*` 等）の
      XML ファイル同梱・自動登録 — 現状は API 提供のみで組み込みデータなし
- [ ] UML 系 (`umlFrame`, `umlLifeline`, `umlState`, `swimlane` のタイトル区画)

## 2. 矢印マーカーの拡充

対応済み:
- [x] `none` / `classic` / `classicThin` / `open` / `openThin`
- [x] `block` / `blockThin` / `async` / `oval` / `diamond` / `diamondThin`
- [x] `box` / `halfCircle` / `dash` / `cross` / `circle` / `circlePlus`
- [x] ER 系 (`ERone` / `ERmandOne` / `ERmany` / `ERoneToMany` / `ERzeroToOne` / `ERzeroToMany`)
      / `baseDash`
- [x] `doubleBlock`
- [x] `startFill` / `endFill`（塗り/枠の切り替え）、`startSize` / `endSize`

未対応:
- [ ] `openAsync` / `manyOptional` などの補助マーカー
- [ ] `startArrow`/`endArrow` に複数マーカーを連結する記法（`classic;open` 等）

## 3. 図形の変形・効果

対応済み:
- [x] `rotation`（セル中心まわり）
- [x] `flipH` / `flipV`（両方で 180 度）
- [x] `gradientColor`（縦方向の線形グラデーション）
- [x] `opacity` / `fillOpacity` / `strokeOpacity`（0..1 / 0..100 両対応）
- [x] `shadow`（既定オフセットの影）
- [x] `glass`（上半分のハイライト）
- [x] `dashPattern`（カスタム破線）
- [x] `shape=image` / `image=data:image/...`（data URI の画像セル）

未対応（その他）:
- [ ] `gradientDirection` の全方向（現状は上→下のみ）
- [ ] 影の細かい指定（`shadowOffset`, `shadowOpacity`, `shadowColor`）
- [ ] `sketch` / `comic`（手描き風、rough.js）
- [ ] `rotate=1` のラベル側回転やグループ化セルの合成変形

## 4. テキスト・ラベル

対応済み:
- [x] `<b>` / `<i>` / `<span style="color">` / `<br>`
- [x] `%Placeholder%`（親チェーン解決）
- [x] `fontStyle` の太字(1)・斜体(2)・下線(4)
- [x] `align` / `verticalAlign` / `labelPosition` / `verticalLabelPosition`
- [x] `fontSize` / `fontColor` / `fontFamily`（簡易）
- [x] `whiteSpace=wrap` の自動折り返し（長い単語は強制分割）
- [x] 縦書き (`horizontal=0`)
- [x] ラベル背景 (`labelBackgroundColor`)

未対応:
- [ ] リッチ HTML の残り (`<font>`, `<sup>`/`<sub>`, `<hr>`, リスト, 表, リンク)
- [ ] スパン単位の描画（現状は色以外のスパン書式はプレーンテキストへ落ちる）
- [ ] ラベル枠線 (`labelBorderColor`)
- [ ] `spacingTop/Bottom/Right` の全網羅、`textDirection` / `writing-mode`

## 5. エッジ（コネクタ）

対応済み:
- [x] `exitX/exitY` / `entryX/entryY` 接続点
- [x] waypoint 折れ線
- [x] `orthogonalEdgeStyle`（水平/垂直/混合アンカー対応）
- [x] `entityRelationEdgeStyle`（`mxEdgeStyle.EntityRelation` 準拠）
- [~] `elbowEdgeStyle`（直交ルーティングで近似）
- [x] `rounded=1`（角丸配線、`arcSize`）、`curved=1`（曲線配線）
- [x] `jumpStyle=arc` / `jumpSize`（交差ジャンプ、`TryIntersect` で交点検出）
- [x] `strokeWidth` / `dashed` / `dashPattern`
- [x] 全マーカー（classic/block/open/oval/diamond/... ER 系）
- [x] エッジラベル位置 (`mxGraphView.getPoint` 準拠)

未対応（優先度中）:
- [ ] フローティング接続（source/target なし、終端 `mxPoint` のみ）
- [ ] `exitPerimeter` / `entryPerimeter`（周囲射影）

未対応（低優先 / 高度）:
- [ ] `segmentEdgeStyle` / `loopEdgeStyle` / `sideToSide` / `topToBottom` / `isometricEdgeStyle`
- [ ] 障害物回避（libavoid）と `jettySize` の精密な再現
- [ ] `elbow=horizontal|vertical` 指定の厳密な反映

## 6. 構造・ページ

対応済み:
- [x] 複数ページ（`PageIndex` プロパティで 0 始まりのページを選択）
- [x] 非表示セル (`visible="0"`) のスキップ
- [x] 折りたたみコンテナ (`collapsed=1`) の子孫を非表示
- [x] レイヤー検出（`LayerIds` / セルの `LayerId` を解決）
- [x] `<mxGraphModel>` の `dx`/`dy`（ビュー原点）を読み取り

未対応:
- [ ] レイヤーの可視/非表示トグル UI・描画順の並べ替え
- [ ] `pageWidth` / `pageHeight` / `pageScale` によるスケーリング

備考:
- `dx`/`dy` は draw.io エディタのスクロール位置であり、図の内容には影響しないため
  レンダリングには適用しない（データとしては保持済み）。

## 7. その他

- [ ] カスタムリンク (`link`)・ツールチップ (`tooltip`)
- [ ] `mxStencil` の `include-shape`（サブステンシル合成）
- [ ] グループセルの `group` 変形（子への affine 継承）
- [ ] 描画結果（見た目）の黄金画像テスト / Avalonia Headless スナップショット
- [ ] パフォーマンス最適化（ブラシ/ペンのキャッシュ、図形ジオメトリの再利用）
