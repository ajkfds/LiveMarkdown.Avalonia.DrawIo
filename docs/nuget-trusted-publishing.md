# NuGet.org Trusted Publishing セットアップ

このリポジトリは NuGet.org への公開に **Trusted Publishing (OIDC)** を使用します。
長期間有効な API キーをリポジトリの Secrets に保存する必要はありません。
GitHub Actions が短命の OIDC トークンを発行し、`NuGet/login` アクションが
それを 1 時間有効な一時 API キーと交換して `dotnet nuget push` に渡します。

## 仕組み

```
GitHub Actions (id-token: write)
   │  OIDC token (sub = repo:OWNER/REPO:ref:refs/tags/v1.0.0 etc.)
   ▼
NuGet/login@v1 ──► nuget.org (Trusted Publishing policy と照合)
   │  一時 API キー (1h)
   ▼
dotnet nuget push --api-key <temp key>
```

## 1. nuget.org 側で Trusted Publishing ポリシーを作成

1. <https://www.nuget.org/> にサインイン
2. 右上のアカウント名 → **Trusted Publishing**（または
   `https://www.nuget.org/account/trustedpublishing`）を開く
3. **Create** を押し、以下を入力する

| 項目 | 値 |
| --- | --- |
| Policy Name | 任意（例: `LiveMarkdown.Avalonia.DrawIo release`） |
| Repository Owner | `ajkfds` |
| Repository | `LiveMarkdown.Avalonia.DrawIo` |
| Workflow File | `release.yml`（ファイル名のみ。パスは不要） |
| Environment | 空欄（このワークフローでは Environment を使いません） |

> **重要**: Workflow File は `.github/workflows/` 内のファイル名と完全一致させて
> ください。リポジトリ名・ワークフロー名を変更した場合はポリシーも更新が必要です。

## 2. リポジトリ側の設定

nuget.org の **ユーザー名**（プロフィールの `Account name`）を、
GitHub リポジトリの **Variables** か **Secrets** に `NUGET_USER` という名前で登録します。

- Settings → Secrets and variables → Actions → **Variables** → New repository variable
  - Name: `NUGET_USER`
  - Value: nuget.org のアカウント名（メールアドレス不可。`Account name` を使用）
- もしくは **Secrets** に `NUGET_USER` として登録しても動作します。

API キー（`NUGET_API_KEY`）の登録は **不要** です。Trusted Publishing へ移行したので、
既存の `NUGET_API_KEY` Secret は削除して構いません。

取得した一時 API キーは `.nupkg` と `.snupkg`（シンボルパッケージ）の両方の push に
使用されます。

> **`Error: Input required and not supplied: user` が出た場合**
> `NUGET_USER` が未登録（または空）です。上記のとおり Variables か Secrets に
> nuget.org の **Account name** を `NUGET_USER` として登録してください。
> 値にメールアドレスを使うと `NuGet/login` が失敗します。
> ワークフローは未設定時に分かりやすいエラーメッセージで早期に失敗するようにしています。

## 3. ワークフローの権限

`.github/workflows/release.yml` には以下が必要です（設定済み）。

```yaml
permissions:
  contents: write
  id-token: write   # OIDC トークン発行に必須
```

## 4. リリース

タグ push で自動実行されます。

```bash
git tag v1.0.0
git push origin v1.0.0
```

手動実行する場合は **Actions → Release → Run workflow** でバージョンを指定します
（`publish` を `false` にすると NuGet.org への push をスキップし、
パッケージ生成と GitHub Release のみ行います）。

## トラブルシューティング

| 症状 | 原因 / 対処 |
| --- | --- |
| `NuGet/login` が 401 / policy 不一致 | Repository Owner / Repository / Workflow File が実際の OIDC `sub` と一致しているか確認。`workflow_dispatch` 実行時の `ref` はブランチ名になる点にも注意。 |
| `NUGET_USER` 未設定 | Variables か Secrets に nuget.org のアカウント名を登録する。 |
| `Permission ... id-token` エラー | `permissions: id-token: write` が無い、または Organization 側で制限されている。 |
| 403 Forbidden (push) | 一時 API キーのスコープ不足。ポリシーが対象パッケージ ID (`LiveMarkdown.Avalonia.DrawIo`) を許可しているか確認。 |
| `--skip-duplicate` で無視される | 同一バージョンが既に存在。バージョンを上げてタグを切り直す。 |

## 参考

- NuGet Trusted Publishing: <https://learn.microsoft.com/nuget/nuget-org/trusted-publishing>
- `NuGet/login` アクション: <https://github.com/NuGet/login>
