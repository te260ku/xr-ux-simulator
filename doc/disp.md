2. 推奨するBATの修正
/LoadConfigで復元した後に、/SetMonitorsで6画面すべての解像度と配置を再適用する方法です。
@echo off
setlocal

set "TOOL=%~dp0MultiMonitorTool.exe"
set "CONFIG=%~dp0monitors6.cfg"

rem 現在のディスプレイ構成を保存
"%TOOL%" /scomma "%~dp0before.csv"

rem 保存済み構成を復元
"%TOOL%" /LoadConfig "%CONFIG%"

timeout /t 3 /nobreak >nul

rem 解像度と配置を再適用
call :ApplyMonitors

timeout /t 3 /nobreak >nul

rem もう一度適用
call :ApplyMonitors

rem 復元後のディスプレイ構成を保存
"%TOOL%" /scomma "%~dp0after.csv"

endlocal
exit /b 0

:ApplyMonitors
"%TOOL%" /SetMonitors ^
 "Name=MONITOR_ID_1 Primary=1 Width=1920 Height=1080 PositionX=0 PositionY=0" ^
 "Name=MONITOR_ID_2 Width=1920 Height=1080 PositionX=1920 PositionY=0" ^
 "Name=MONITOR_ID_3 Width=1920 Height=1080 PositionX=3840 PositionY=0" ^
 "Name=MONITOR_ID_4 Width=1920 Height=1080 PositionX=0 PositionY=1080" ^
 "Name=MONITOR_ID_5 Width=1920 Height=1080 PositionX=1920 PositionY=1080" ^
 "Name=MONITOR_ID_6 Width=2560 Height=1600 PositionX=3840 PositionY=1080"

exit /b


これは構成例です。実行前に6画面すべてのMonitor ID、解像度、座標、プライマリ指定を実際の値へ変更してください。

手入力するよりも、正しく設定した状態でMultiMonitorToolの以下の機能を使うことを推奨します。

Options → Copy /SetMonitors Command Mode → Use Monitor ID as Name

続いて6画面をすべて選択し、Edit → Copy /SetMonitors Commandを実行します。

生成されたコマンドを、BATの:ApplyMonitors以下にそのまま使用してください。これなら実際のリフレッシュレートや画面の向きなども含めて正確に復元できます。


3. MultiMonitorToolで確認する項目
正常な状態と異常な状態で、以下のコマンドを実行して情報を保存してください。
MultiMonitorTool.exe /scomma "monitors_before.csv"


復元に失敗した後は、
MultiMonitorTool.exe /scomma "monitors_after.csv"


CSVの次の項目を比較します。
- Monitor ID：物理ディスプレイの識別情報
- Monitor Name：\\.\DISPLAY1などのWindows上の番号
- Resolution：解像度
- Left-Top：配置座標
特にMonitor IDとDISPLAY番号の対応が変化しているかに注目してください。ドックの接続状態や画面の有効化によって番号が変わっていれば、DISPLAY番号ではなくMonitor IDを指定して復元する対策が有効です。