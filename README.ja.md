# STS2 Mods

**言語 / Language / 语言**: [简体中文](./README.md) | [English](./README.en.md) | **日本語**

*Slay the Spire 2* 向け mod のソースコードリポジトリです。主な目的は、マルチプレイ連携、戦闘情報の可視化、そしてプレイ中のコミュニケーション改善です。

## 現在のアクティブ mod

- [CombatQuill](./CombatQuill): 戦闘中と関連画面で戦術的な注釈と即時共有を行う mod
- [DefeatBGM](./DefeatBGM): 敗北時の既定 BGM を mod フォルダ内のローカル曲に差し替える mod
- [FrozenEye](./FrozenEye): 山札プレビューを実際のドロー順で表示する mod
- [PartyObserver](./PartyObserver): 同期済みの味方選択肢と現在の画面状態を確認できる mod
- [RandomVision](./RandomVision): Crystal Sphere とイベント画面のプレビュー情報をより分かりやすくする mod
- [RelicRpsChoice](./RelicRpsChoice): 共通レリックの競合を可視化されたじゃんけんフローで解決する mod

## アーカイブ済み mod

- [legacy/DamageMeter](./legacy/DamageMeter): ソース履歴の保存用です。現在の主力 mod としては扱いません

## リリース版のバージョン規約

- 共通のバージョン規約は [RELEASE_VERSIONING.md](./RELEASE_VERSIONING.md) にあります
- バージョン番号は `x.y.z` 形式です
- `x` は大きなアーキテクチャ変更、`y` は機能追加、`z` は不具合修正と最適化を表します

## ローカルビルドについて

- このリポジトリはソースコードとドキュメントのみを追跡し、`.tools`、`_workspace`、`_release`、`mods/` などのローカル作業ディレクトリは含みません
- `build-combatquill.ps1` と `build-partyobserver.ps1` は、まずリポジトリ内の `.tools` を探し、見つからない場合はゲーム配下の `modding/.tools`、最後にシステムの `dotnet 9` を試します
- リポジトリがゲームのルート直下にない場合は、`-Sts2Path` で *Slay the Spire 2* のインストール先を指定してください
- そのほかの mod は各ディレクトリ内の `*.csproj` または `*.sln` から直接ビルドできます

## リポジトリ構成

- ルートには現在保守中のソース、ドキュメント、共有スクリプトのみを置きます
- `legacy/` には履歴を残したいアーカイブ mod を置きます
- ビルド成果物、キャッシュ、エディタ用フォルダ、ゲームの実行用ディレクトリはバージョン管理しません
