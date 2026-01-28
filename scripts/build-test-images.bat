@echo off
echo Building test Docker images...

echo Building API image...
docker build -f src\CrewTech.Notify.SenderApi\Dockerfile.test -t crewtech-notify-api:test .
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%
echo API image built

echo Building Worker image...
docker build -f src\CrewTech.Notify.Worker\Dockerfile.test -t crewtech-notify-worker:test .
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%
echo Worker image built

echo.
echo Test images ready!
docker images | findstr crewtech-notify
