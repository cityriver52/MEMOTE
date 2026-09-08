# MEMOTE

**MEMOTE** は、**MEMO + MOTE**（漂う小さな粒）から名付けた、「今だけ頭に置いておきたいこと」を泡のように浮かべ、終わったら弾いて消す超短期メモです。

> MEMOTE is RAM, not storage.

MEMOTE は **ローカルHTML版のみ**です。ネイティブEXEやWebサーバーは使いません。

## 特徴

- `MEMOTE.html` 1ファイルだけで動作
- Chromeでローカルファイルとして直接実行
- Webサーバー不要
- インストーラー不要
- 管理者権限不要
- GitHub Pages不使用
- 外部API不使用
- `fetch` / WebSocket / XMLHttpRequest 不使用
- メモ本文をネットワークへ送信しない
- Content Security Policy でネットワーク接続を禁止
- メモはChromeの `localStorage` のみに保存
- 最大10件
- Enterで追加
- ↓で一番上の泡を選択
- ↑ / ↓で移動
- Delete または Enter で泡を弾いて削除
- 泡の浮遊、生成、破裂、小泡パーティクルのアニメーション

## 入手

リポジトリ直下の `MEMOTE.html` を保存してください。

直接保存用:

`https://raw.githubusercontent.com/cityriver52/MEMOTE/main/MEMOTE.html`

GitHubへのアクセスが必要なのは **ファイルを取得するときだけ**です。保存後のMEMOTEはローカルファイルだけで完結し、GitHubへ通信しません。

## 一番簡単な起動方法

`MEMOTE.html` をChromeへドラッグするか、右クリックしてChromeで開きます。

通常のブラウザタブではなくアプリ風の専用ウィンドウとして使う場合は、WindowsショートカットからChromeを `--app` モードで起動します。

## Chromeをアプリ風ウィンドウとして起動する

まず `MEMOTE.html` を削除・移動しない固定フォルダへ置きます。

例:

```text
C:\Users\<ユーザー名>\Documents\MEMOTE\MEMOTE.html
```

Windowsで新しいショートカットを作り、リンク先を次の形式にします。

```text
"C:\Program Files\Google\Chrome\Application\chrome.exe" --app="file:///C:/Users/<ユーザー名>/Documents/MEMOTE/MEMOTE.html"
```

Chromeが別の場所にインストールされている場合は、`chrome.exe` の部分だけ実際のパスへ変更してください。

これで、アドレスバーやタブのないMEMOTE専用ウィンドウとして起動します。

## キーボードショートカットで起動する

上で作ったWindowsショートカットを右クリックし、**プロパティ → ショートカット キー** に任意のキーを設定します。

例:

```text
Ctrl + Alt + M
```

この方式ではMEMOTE自身がグローバルキーを監視しません。Windowsが普通のショートカットを起動し、そのショートカットがChromeを開きます。

会社PCで使う場合も、ネイティブ常駐アプリ、キーボードフック、`RegisterHotKey`、タスクスケジューラなどは使用しません。

## データ保存

メモはJavaScriptの `localStorage` に保存します。現在の保存キーは `MEMOTE.local.v1` です。

旧memoNOW版を同じブラウザ保存領域で開いた場合は、旧キー `memoNOW.local.v1` も読み取り、MEMOTEの保存キーへ自動移行します。

サーバー、GitHub、Google Driveなどには保存しません。

`file://` のローカルページに対するストレージはブラウザ管理なので、次の場合はメモが消える可能性があります。

- Chromeのサイトデータを削除した場合
- 組織ポリシー等でブラウザデータが消去された場合
- 別ブラウザで開いた場合
- 環境によってローカルファイルの保存領域の扱いが変わった場合
- HTMLファイルの保存場所やファイル名を変更した場合

MEMOTEは短期記憶用なので、重要な長期情報は別の保存先へ移してください。

## ネットワークについて

`MEMOTE.html` には外部リソースへのURL参照や通信コードを含めていません。

さらにHTML内のContent Security Policyで次を指定しています。

```text
connect-src 'none'
```

そのためアプリ動作中に `fetch`、WebSocket等を使って外部へ接続する設計にはなっていません。

CSSとJavaScriptもすべて `MEMOTE.html` 内に埋め込んでいます。

## ファイル構成

```text
MEMOTE.html   # アプリ本体。これだけで動く
README.md     # 説明
```

ビルド工程はありません。
