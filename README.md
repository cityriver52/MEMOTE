# memoNOW

**memoNOW** は、「今だけ頭に置いておきたいこと」を泡のように浮かべ、終わったら弾いて消す超短期メモです。

> memoNOW is RAM, not storage.

現在の memoNOW は **ローカルHTML版のみ**です。旧WPF / EXE版は廃止しました。

## 特徴

- `memoNOW.html` 1ファイルだけで動作
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

リポジトリ直下の `memoNOW.html` を保存してください。

直接保存用:

`https://raw.githubusercontent.com/cityriver52/memoNOW/main/memoNOW.html`

GitHubへのアクセスが必要なのは **ファイルを取得するときだけ**です。保存後のmemoNOWはローカルファイルだけで完結し、GitHubへ通信しません。

## 一番簡単な起動方法

`memoNOW.html` をChromeへドラッグするか、右クリックしてChromeで開きます。

ただし通常のブラウザタブではなくアプリ風の専用ウィンドウとして使う場合は、WindowsショートカットからChromeを `--app` モードで起動します。

## Chromeをアプリ風ウィンドウとして起動する

まず `memoNOW.html` を削除・移動しない固定フォルダへ置きます。

例:

```text
C:\Users\<ユーザー名>\Documents\memoNOW\memoNOW.html
```

Windowsで新しいショートカットを作り、リンク先を次の形式にします。

```text
"C:\Program Files\Google\Chrome\Application\chrome.exe" --app="file:///C:/Users/<ユーザー名>/Documents/memoNOW/memoNOW.html"
```

Chromeが別の場所にインストールされている場合は、`chrome.exe` の部分だけ実際のパスへ変更してください。

これで、アドレスバーやタブのないmemoNOW専用ウィンドウとして起動します。

## キーボードショートカットで起動する

上で作ったWindowsショートカットを右クリックし、**プロパティ → ショートカット キー** に任意のキーを設定します。

例:

```text
Ctrl + Alt + M
```

この方式ではmemoNOW自身がグローバルキーを監視しません。Windowsが普通のショートカットを起動し、そのショートカットがChromeを開きます。

会社PCで使う場合も、ネイティブ常駐アプリ、キーボードフック、RegisterHotKey、タスクスケジューラなどは使用しません。

## データ保存

メモはJavaScriptの `localStorage` に保存します。

サーバー、GitHub、Google Driveなどには保存しません。

`file://` のローカルページに対するストレージはブラウザ管理なので、次の場合はメモが消える可能性があります。

- Chromeのサイトデータを削除した場合
- 組織ポリシー等でブラウザデータが消去された場合
- 別ブラウザで開いた場合
- 環境によってローカルファイルの保存領域の扱いが変わった場合

memoNOWは短期記憶用なので、重要な長期情報は別の保存先へ移してください。

## ネットワークについて

`memoNOW.html` には外部リソースへのURL参照や通信コードを含めていません。

さらにHTML内のContent Security Policyで次を指定しています。

```text
connect-src 'none'
```

そのためアプリ動作中に `fetch`、WebSocket等を使って外部へ接続する設計にはなっていません。

CSSとJavaScriptもすべて `memoNOW.html` 内に埋め込んでいます。

## ファイル構成

```text
memoNOW.html   # アプリ本体。これだけで動く
README.md      # 説明
```

ビルド工程はありません。
