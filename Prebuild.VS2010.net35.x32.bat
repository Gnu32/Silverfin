@ECHO OFF

Tools\Prebuild.exe /target vs2010 /targetframework v3_5
del Compile.*.bat
echo C:\WINDOWS\Microsoft.NET\Framework\v3.5\msbuild Aurora.sln > Compile.VS2010.net35.x32.bat /p:DefineConstants=ISWIN
pause