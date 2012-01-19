Tools\Prebuild.exe /target vs2008 /targetframework v3_5 
del Compile.*.bat
echo C:\WINDOWS\Microsoft.NET\Framework\v3.5\msbuild Aurora.sln /p:Platform="x64" > Compile.VS2008.net35.x64.bat /p:DefineConstants=ISWIN
pause