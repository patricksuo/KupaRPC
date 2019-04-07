
Remove-Item  .\coverage.opencover.xml  -Force -ErrorAction SilentlyContinue
Remove-Item  .\coveragereport  -Force -Recurse -ErrorAction SilentlyContinue
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:Exclude="[xunit.*]*"
reportgenerator  "-reports:coverage.opencover.xml"   "-targetdir:coveragereport"
Remove-Item  .\coverage.opencover.xml  -Force -ErrorAction SilentlyContinue
