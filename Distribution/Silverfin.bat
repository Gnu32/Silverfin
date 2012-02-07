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

rem ## Default Course of Action (aurora,server,wipe,quit)
set choice=aurora

rem ## Auto-restart on exit/crash (y,n)
set auto_restart=y

rem ## Pause on crash/exit (y,n)
set auto_pause=y

:start
echo Welcome to the Silverfin launcher.
if %auto_restart%==y echo I am configured to automatically restart on exit.
if %auto_pause%==y echo I am configured to automatically pause on exit.
echo You can edit this batch file to change your default choices.
echo.
echo You have the following choices:
echo	- aurora: Launches Silverfin (Aurora) as standalone/region simulator
echo	- server: Launches Silverfin (Aurora) as server for specific roles
echo	- wipe: Wipes cache, databases or logs
echo	- quit: Quits
echo.

:action
set /p choice="What would you like to do? (aurora, server, wipe, quit) [%choice%]: "
if %choice%==aurora (
	set app="Aurora.exe"
	goto launchcycle
)
if %choice%==server (
	set app="Aurora.exe /server"
	goto launchcycle
)
if %choice%==wipe goto wipe
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

:wipe
set wchoice=back
echo.
echo You have the following choices:
echo	- temp: Wipes logs and caches
echo	- full: Wipes logs, caches and databases (!!!DESTRUCTIVE!!!)
echo	- back: Goes back to main menu 
echo.
set /p wchoice="What would you like to wipe? (temp, full) [back]: "
if %wchoice%==temp goto wipe_temp
if %wchoice%==full goto wipe_full
if %wchoice%==quit goto eof
if %wchoice%==q goto eof
if %wchoice%==exit goto eof
goto action

	:wipe_temp
	set wipe=n
	echo.
	set /p wipe="Are you ABSOLUTELY SURE you want to wipe all caches and logs? (y,n) [n]"
	if %wipe%==y (
		rd /q /s Logs
		rd /q /s Caches
		echo Temporary files wiped.
		goto action
	)
	goto wipe
	
	:wipe_full
	set wipe=n
	echo.
	echo ################# W
	echo ######## ######## A
	echo #######/ \####### R   You are about to perform a full wipe.
	echo ######/ ! \###### N   If your Silverfin installation is configured to
	echo #####/ !!! \##### I   use SQLite, you WILL LOSE EVERYTHING.
	echo ####/   !   \#### N   
	echo ###/         \### G   You will lose your ASSETS, ACCOUNT INFORMATION,
	echo ##/     !     \## !   REGION DATA, EVERYTHING YOU SAVED!
	echo #/_____________\# !
	echo ################# !
	set /p wipe="Are you ABSOLUTELY SURE you want to nuke everything? (makeitso, n) [n]"
	if %wipe%==makeitso (
		rd /q /s Databases
		rd /q /s Logs
		rd /q /s Caches
		echo The universe is new again. All data wiped.
		goto action
	)
	goto wipe


:eof
pause
