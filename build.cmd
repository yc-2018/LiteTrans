@echo off
setlocal
chcp 65001 >nul
rem ===== 轻译 LiteTrans 一键编译 =====
rem 只依赖 Windows 自带的 .NET Framework 编译器，无需安装任何 SDK

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [错误] 找不到 C# 编译器 csc.exe
  exit /b 1
)

set SPEECH=%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Speech\v4.0_4.0.0.0__31bf3856ad364e35\System.Speech.dll
set GAC=%WINDIR%\Microsoft.NET\assembly\GAC_MSIL
set UIA1=%GAC%\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll
set UIA2=%GAC%\UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll

echo 正在编译 …
"%CSC%" -nologo -codepage:65001 -target:winexe -optimize+ -platform:anycpu ^
  -out:"%~dp0LiteTrans.exe" ^
  -win32icon:"%~dp0src\app.ico" ^
  -win32manifest:"%~dp0src\app.manifest" ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:System.Web.Extensions.dll -r:"%SPEECH%" -r:"%UIA1%" -r:"%UIA2%" ^
  "%~dp0src\*.cs"

if errorlevel 1 (
  echo.
  echo [失败] 编译未通过
  exit /b 1
)

echo.
echo [完成] 已生成 %~dp0LiteTrans.exe
endlocal
