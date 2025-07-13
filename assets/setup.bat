@echo off

echo Waiting 3 seconds before starting...
timeout /t 3 /nobreak

echo Deleting old files...
del 4Term.exe
del AutoUpdater.exe
del CHANGES
del LICENSE
del version.txt

echo Copying new files from 4Term_1.4.0_x64...
xcopy "%cd%\4Term_1.4.0_x64\*" "%cd%" /E /I /Y

echo Removing source folder 4Term_1.4.0_x64...
rmdir /s /q "%cd%\4Term_1.4.0_x64"

start "" /b cmd /c "timeout /t 2 > nul & del "%~f0""
