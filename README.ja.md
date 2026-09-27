# Lumen v1.0.4

**Lumen** は、KDE Gwenviewで使いやすかった写真閲覧・選別体験をWindows上で再構成した、軽量な写真ビューア／カリングアプリです。

Gwenviewのフォークではなく、ソースコードも含みません。大きなサムネイル一覧、一枚表示との素早い切替、連写写真の前後比較、選択式EXIF表示を中心に、写真整理の流れを止めないことを目的としています。

> English: [README.md](README.md)

## 主な使い方

1. **Browse** — 大きなサムネイルで明らかに不要な写真を選別します。
2. **View** — `Enter` またはダブルクリックで一枚表示します。
3. **連写比較** — `←` / `→` を連続して押し、最も良いカットを残します。画像切替アニメーションはありません。
4. **削除** — `Delete` でWindowsのごみ箱へ送ります。`Ctrl+Z` でLumen直前削除分の復元を試みます。
5. **露出補正** — 左ペインで -3.0～+3.0 EV を調整します。原本JPEGは書き換えません。
6. **一括Resize/Export** — Browseで `Ctrl+A` → `Ctrl+E`。既定は長辺3840px、縦横比維持、拡大なしです。

## EXIF / 撮影情報

左ペインの **Choose EXIF fields...** から表示項目を選択できます。

標準情報として、カメラ、レンズ、撮影日時、シャッター速度、絞り値、ISO、焦点距離、露出補正、色空間、ホワイトバランス、彩度、シャープネス、フラッシュ、ソフトウェア等を表示できます。画像サイズはBrowse中でもInformation欄に表示します。

FUJIFILM JPEGではMakerNoteを直接解析し、取得できる場合は画質、シャープネス、ホワイトバランス、彩度、ダイナミックレンジ、フィルムモード／フィルムシミュレーションも表示します。外部ExifToolは不要です。

## キーボード

| キー | 操作 |
|---|---|
| `Enter` | Browse → View |
| `Esc` | View → Browse / Browseでは選択解除 |
| `Space` | Browse / View切替 |
| `←` / `→` | Viewで前後の写真 |
| `Delete` | 選択写真／表示写真をごみ箱へ |
| `Ctrl+Z` | Lumen直前削除分の復元を試行 |
| `Ctrl+A` | Browseですべて選択 |
| `Ctrl+E` | Export |
| `[` / `]` | 露出 -0.1 / +0.1 EV |
| `Ctrl+ホイール` | Viewでズーム |
| `F11` | 全画面 |

## 画像品質について

画像の縮小は画素を捨てるため、数学的な意味でロスレスにはできません。またJPEG自体も非可逆圧縮です。

Lumenでは原本を変更せず、**Original → orientation / exposure / resize → 最終encode 1回**の順で処理し、中間JPEG保存による不要な世代劣化を避けます。JPEG品質の既定値は100です。

## 対応形式

JPEG/JPG、PNG、BMP、TIFF/TIF、GIF（静止画として）。

HEIC/HEIF、WebP、RAW、動画、アニメーション画像編集はv1.0の対象外です。

## 動作環境

- Windows 11 x64
- .NET Framework 4.8 / WPF

GitHub Release版ZIPにはビルド済み`Lumen.exe`が入るため、通常利用時にコンパイルは不要です。

## ライセンス

MIT License。詳細は [LICENSE.txt](LICENSE.txt) を参照してください。
