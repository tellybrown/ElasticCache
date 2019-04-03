@echo Off

set config=%1
if "%config%" == "" (
   set config=Release
)
 
set version=0.1.0
if not "%PackageVersion%" == "" (
   set version=%PackageVersion%
)

mkdir Build
dotnet build ElasticCache.sln -c %config%
dotnet pack ElasticCache.StrongName\ElasticCache.StrongName.csproj -p:PackageVersion=%version% -o .\..\Build /p:NuspecFile=.\..\ElasticCache.StrongName.nuspec

rem dotnet pack ElasticCache\ElasticCache.csproj -p:PackageVersion=%version% -o .\..\Build /p:NuspecFile=.\..\ElasticCache.nuspec
