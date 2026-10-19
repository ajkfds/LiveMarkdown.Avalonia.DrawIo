# LiveMarkdown.Avalonia.DrawIo TODO

draw.io (mxGraph) ネイティブ描画プラグインの未対応項目一覧。
`[x]` = 対応済み、`[~]` = 簡易対応（近似）、`[ ]` = 未対応。

## サマリ

- 主要機能（基本図形・マーカー・変形/効果・ラベル・エッジ配線）は**おおむね実装済み**。
- 残件は (a) 網羅性の追加（特殊形状・追加スタイル）、(b) 高度な機能（画像・手描き風・
  障害物回避）、(c) **構造/ページ系がほぼ未着手**、の 3 種類。
- 推奨着手順: **6（構造・ページ）→ 3（画像 `shape=image`）→ 1（プリセットステンシル）**。
  表現力を最も広げるのは 1 と 3、実用上の欠落が大きいのは 6。

進捗（おおよそ）: 項目 1〜5 は 80〜90% 実装、項目 6・7 はほぼ 0%。

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

未対応（優先度高）:
- [ ] プリセットステンシル全般 (`mxgraph.aws4.*`, `mxgraph.flowchart.*`, `mxgraph.uml.*`,
      `mxgraph.network.*` 等) — 現状は矩形へフォールバック。**表現力への影響が最も大きい**
- [ ] UML 系 (`umlFrame`, `umlLifeline`, `umlState`, `swimlane` のタイトル区画)
- [ ] 論理ゲート (`xor` / `or` / `and`)・その他の基本図形 (`rhombus` 系の派生,
      `card`/`tape` などの `direction` バリエーション)

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

未対応（優先度高）:
- [ ] `shape=image` / `image=data:...`（画像セル・アイコン画像）— **ステンシルと並ぶ主要欠落**

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

## 6. 構造・ページ（未着手 / 優先度高）

- [ ] 複数ページ (`<mxfile>` の 2 ページ目以降の選択)
- [ ] レイヤーの可視/非表示・描画順
- [ ] 折りたたみコンテナ (`collapsible`) の collapsed 状態
- [ ] `pageWidth` / `pageHeight` / `pageScale` によるスケーリング
- [ ] `<mxGraphModel>` の `dx`/`dy`（ビュー原点）の反映

## 7. その他

- [ ] カスタムリンク (`link`)・ツールチップ (`tooltip`)
- [ ] `mxStencil` の `include-shape`（サブステンシル合成）
- [ ] グループセルの `group` 変形（子への affine 継承）
- [ ] 描画結果（見た目）の黄金画像テスト / Avalonia Headless スナップショット
- [ ] パフォーマンス最適化（ブラシ/ペンのキャッシュ、図形ジオメトリの再利用）
