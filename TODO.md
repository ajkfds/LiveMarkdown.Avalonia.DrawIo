# LiveMarkdown.Avalonia.DrawIo TODO

draw.io (mxGraph) ネイティブ描画プラグインの未対応項目一覧。
優先度の高い順に並べている。`[x]` は対応済み。

## 1. 基本図形の拡充

対応済み:
- [x] 矩形 (`rounded=0`)
- [x] 角丸矩形 (`rounded=1`, `arcSize`)
- [x] 楕円 (`ellipse`)
- [x] 三角形 (`triangle`, `direction`)
- [x] 汎用ポリゴン (`mxgraph.basic.polygon`, `polyCoords`/`polyCurves`)

未対応（本項目で対応するもの）:
- [x] ひし形 (`rhombus`)
- [x] 六角形 (`hexagon`, `size`/`fixedSize`, `direction`)
- [x] 平行四辺形 (`parallelogram`, `size`/`fixedSize`, `flipH`)
- [x] 台形 (`trapezoid`, `size`/`fixedSize`, `direction`, `flipV`)
- [x] ステップ/シェブロン (`step`, `size`/`fixedSize`, `direction`)
- [x] 円柱/データベース (`cylinder` / `cylinder3`, `size`)
- [x] 雲 (`cloud`)
- [x] アクター (`umlActor`)
- [x] 内部記憶 (`internalStorage`, `dx`/`dy`)
- [x] 書類 (`document`, `size`)
- [x] 手動入力 (`manualInput`, `size`)
- [x] カード (`card`, `size`)
- [x] 注記 (`note`, `size`)
- [x] 吹き出し (`callout`, `base`/`size`)
- [x] 二重矢印 / 片矢印 (`doubleArrow` / `singleArrow`, `arrowWidth`/`arrowSize`)
- [x] 二重楕円 (`doubleEllipse`)
- [x] 線 (`line`)
- [x] プロセス (`process`, `size`, `backgroundOutline`)
- [x] ループリミット (`loopLimit`, `size`)
- [x] オフページコネクタ (`offPageConnector`, `size`)
- [x] テープ (`tape`, `size`)
- [x] 遅延 (`delay`, `size`)
- [x] キューブ (`cube`, `size`)

今後（アイコン系ステンシル）:
- [ ] プリセットステンシル全般 (`mxgraph.aws4.*`, `mxgraph.flowchart.*`,
      `mxgraph.uml.*`, `mxgraph.network.*` 等) — 現状は矩形へフォールバック

## 2. 矢印マーカーの拡充

対応済み:
- [x] `none` / `classic` / `classicThin` / `open` / `openThin`
- [x] `block` / `blockThin` / `async` / `oval` / `diamond` / `diamondThin`
- [x] `box` / `halfCircle` / `dash` / `cross` / `circle` / `circlePlus`
- [x] ER 系 (`ERone` / `ERmandOne` / `ERmany` / `ERoneToMany` /
      `ERzeroToOne` / `ERzeroToMany`) / `baseDash`
- [x] `doubleBlock`
- [x] `startFill` / `endFill`（塗り/枠の切り替え）
- [x] `startSize` / `endSize`（マーカーサイズ個別指定）

未対応:
- [ ] `openAsync` / `manyOptional` などの補助マーカー
- [ ] `startArrow`/`endArrow` に複数マーカーを連結する記法（`classic;open` 等）

## 3. 図形の変形・効果

対応済み:
- [x] `rotation`（図形の回転、セル中心まわり）
- [x] `flipH` / `flipV`（図形の反転、両方で 180 度）
- [x] `gradientColor`（縦方向の線形グラデーション）
- [x] `opacity` / `fillOpacity` / `strokeOpacity`（透明度、0..1 / 0..100 両対応）
- [x] `shadow`（影）
- [x] `glass`（ガラス効果、上半分のハイライト）
- [x] `dashPattern`（カスタム破線）

未対応:
- [ ] `gradientDirection` の全方向（現状は上→下のみ）
- [ ] 影の細かい指定（`shadowOffset`, `shadowOpacity`, `shadowColor`）
- [ ] `sketch` / `comic`（手描き風、rough.js）
- [ ] `shape=image` / `image=data:...`（画像セル・アイコン画像）

## 4. テキスト・ラベル

対応済み:
- [x] `<b>` / `<i>` / `<span style="color">` / `<br>`
- [x] `%Placeholder%`（親チェーン解決）
- [x] `fontStyle` の太字
- [x] `align` / `verticalAlign` / `labelPosition` / `verticalLabelPosition`
- [x] `fontSize` / `fontColor` / `fontFamily`（簡易）

未対応:
- [ ] `fontStyle` の斜体(2)・下線(4)
- [ ] `whiteSpace=wrap` の自動折り返し
- [ ] 縦書き・テキスト回転 (`horizontal=0`, `rotation`, `textDirection`, `writing-mode`)
- [ ] リッチ HTML の残り (`<font>`, `<sup>`/`<sub>`, `<hr>`, リスト, 表, リンク)
- [ ] ラベル背景/枠線 (`labelBackgroundColor`, `labelBorderColor`)
- [ ] `spacing` 系の全網羅 (`spacingTop/Bottom/Right`)

## 5. エッジ（コネクタ）

対応済み:
- [x] `exitX/exitY` / `entryX/entryY` 接続点
- [x] waypoint 折れ線
- [x] `orthogonalEdgeStyle`（簡易直交ベンド）
- [x] `strokeWidth` / `dashed`
- [x] `classic` / `open` 矢じり
- [x] エッジラベル位置 (`mxGraphView.getPoint` 準拠)

未対応:
- [ ] `rounded=1`（角丸配線）、`jettySize`、障害物回避
- [ ] 他エッジスタイル (`entityRelationEdgeStyle`, `elbowEdgeStyle`,
      `segmentEdgeStyle`, `loopEdgeStyle`, `sideToSide`, `topToBottom` 等)
- [ ] `curved=1` / ベジェ曲線配線
- [ ] `jumpStyle` / `jumpSize`（交差ジャンプ）
- [ ] フローティング接続（source/target なし、`exitX`/`entryY` のみ）
- [ ] `exitPerimeter` / `entryPerimeter`（周囲射影）

## 6. 構造・ページ

- [ ] 複数ページ (`<mxfile>` の 2 ページ目以降)
- [ ] レイヤーの可視/順序
- [ ] 折りたたみコンテナ (`collapsible`) の collapsed 状態
- [ ] `pageWidth` / `pageHeight` によるスケーリング

## 7. その他

- [ ] カスタムリンク・ツールチップ
- [ ] `mxStencil` の `include-shape`（サブステンシル合成）
- [ ] 描画結果（見た目）の黄金画像テスト
