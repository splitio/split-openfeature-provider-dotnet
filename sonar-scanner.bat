@echo off

SET "JAVA_HOME=C:/Program Files/Java/jdk17"
setlocal EnableDelayedExpansion
GOTO :main

:sonar_scanner
SonarScanner.MSBuild.exe begin ^
  /n:"split-openfeature-provider-dotnet" ^
  /k:"split-openfeature-provider-dotnet" ^
  /v:"%APPVEYOR_BUILD_VERSION%" ^
  /d:sonar.host.url="https://sonarqube.split-internal.com" ^
  /d:sonar.login="%SONAR_LOGIN%" ^
  /d:sonar.ws.timeout="300" ^
  /d:sonar.cs.opencover.reportsPaths="**\TestResults\*\*.xml" ^
  /d:sonar.coverage.exclusions="**-tests.cs" ^
  /d:sonar.links.ci="https://ci.appveyor.com/project/SplitDevOps/split-openfeature-provider-dotnet" ^
  /d:sonar.links.scm="https://github.com/splitio/split-openfeature-provider-dotnet" ^
  %*
EXIT /B 0

:main
IF NOT "%APPVEYOR_PULL_REQUEST_NUMBER%"=="" (
  echo Pull Request number %APPVEYOR_PULL_REQUEST_NUMBER%
  CALL :sonar_scanner ^
    "/d:sonar.pullrequest.provider="GitHub"" ^
    "/d:sonar.pullrequest.github.repository="splitio/split-openfeature-provider-dotnet"" ^
    "/d:sonar.pullrequest.key="%APPVEYOR_PULL_REQUEST_NUMBER%"" ^
    "/d:sonar.pullrequest.branch="%APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH%"" ^
    "/d:sonar.pullrequest.base="%APPVEYOR_REPO_BRANCH%""
) ELSE (
    IF "%APPVEYOR_REPO_BRANCH%"=="main" (
      echo "Main branch."
      CALL :sonar_scanner ^
        "/d:sonar.branch.name="%APPVEYOR_REPO_BRANCH%""
    ) ELSE (
        IF "%APPVEYOR_REPO_BRANCH%"=="development" (
          echo "Development branch."
          SET "TARGET_BRANCH=main"
          ) ELSE (
              echo "Feature branch."
              SET "TARGET_BRANCH=development"
            )
      echo Branch Name is %APPVEYOR_REPO_BRANCH%
      echo Target Branch is !TARGET_BRANCH!
      CALL :sonar_scanner ^
        "/d:sonar.branch.name="%APPVEYOR_REPO_BRANCH%"" ^
        "/d:sonar.branch.target="!TARGET_BRANCH!""
      )
  )
