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

rem ## Source folder to sync. Should be absolute path to your "Distribution" folder
set source=C:\Users\Gnu32\Documents\C#\Silverfin\Distribution

rem ## Destination to sync to. Can be any valid absolute path, including UNC (e.g. \\SIMSERVERBETA\Silverfin)
set target=\\SIMSERVERBETA\OpenSim\Silverfin

rem ## Files and folders to ignore, seperated by space. Uses standard Windows wildcards
set ignoredfiles=*.dat *.vshost.* *.so *.dylib *.pdb *.log .gitignore
set ignoredfolders=Databases Caches Logs .git

rem ## Pause at end of sync (y, n)
set pause_at_end=n

echo Now syncing the distribution directory to production/testing location:
echo 	%source%
echo	to %target%
if %pause_at_end%==y echo And I will pause at the end when sync is complete.
echo.
echo Note that you can change these settings by opening this batch file in a text editor.
echo Additionally, you can configure Visual Studio to automatically execute this batchfile
echo upon successful compile.

robocopy "%source%" "%target%" /S /Z /XD %ignoredfolders% /XF %ignoredfiles% /XA:SH /PURGE
if %pause_at_end%==y pause