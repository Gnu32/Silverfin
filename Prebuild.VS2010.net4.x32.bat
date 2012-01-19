@ECHO OFF

Tools\Prebuild.exe /target vs2010 /targetframework v4_0 
del Compile.*.bat
echo C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\msbuild Aurora.sln > Compile.VS2010.net4.x32.bat /p:DefineConstants=ISWIN
pause