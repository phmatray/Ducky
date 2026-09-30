@ECHO OFF
REM Runs the Fallout build through build.ps1 (SPEC 19).
powershell -ExecutionPolicy ByPass -NoProfile -File "%~dp0build.ps1" %*
