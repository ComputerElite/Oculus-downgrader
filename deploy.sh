cd bin/Debug/net6.0
rm msedgedriver_version.txt
rm msedgedriver
rm "Oculus Downgrader.zip"
7z a "Oculus Downgrader.zip" *.dll *.pdb "Oculus Downgrader.exe" *.runtimeconfig.json *.deps.json ref runtimes *.txt