@ECHO OFF

echo        o 8                      ooooo  o       
echo          8                      8              
echo .oPYo. o8 8 o    o .oPYo. oPYo. o8oo   o8 odYo. 
echo Yb..    8 8 Y.  .P 8oooo8 8  `'  8      8 8' `8 
echo   'Yb.  8 8 `b..d' 8.     8      8      8 8   8 
echo `YooP'  8 8  `YP'  `Yooo' 8      8      8 8   8 
echo :.....::....::...:::.....:..:::::..:::::....::..
echo ::::::::::::::::::::::::Major Rasputin 2012:::::
echo.

rem ## Default Course of Action (aurora,server,config,quit)
set choice=aurora

rem ## Auto-restart on exit/crash (y,n)
set auto_restart=y

rem ## Pause on crash/exit (y,n)
set auto_pause=y

echo Welcome to the Silverfin launcher.
if %auto_restart%==y echo I am configured to automatically restart on exit.
if %auto_pause%==y echo I am configured to automatically pause on exit.
echo You can edit this batch file to change your default choices.
echo.
echo You have the following choices:
echo	- aurora: Launches Silverfin (Aurora) as standalone/region simulator
echo	- server: Launches Silverfin (Aurora) as server for specific roles
echo	- config: Launches the configurator to configure Silverfin
echo	- quit: Quits
echo.

:action
set /p choice="What would you like to do? (aurora, server, config, quit) [%choice%]: "
if %choice%==aurora (
	set app="Aurora.exe"
	goto launchcycle
)
if %choice%==server (
	set app="Aurora.Server.exe"
	goto launchcycle
)
if %choice%==config (
	set app="Aurora.Configurator.exe"
	goto launchcycle
)
if %choice%==quit goto eof
if %choice%==q goto eof
if %choice%==exit goto eof

echo "%choice%" isn't a valid choice!
goto action



:launchcycle
echo.
echo Launching %app%...
%app%
if %auto_pause%==y pause
if %auto_restart%==y goto launchcycle


:eof
