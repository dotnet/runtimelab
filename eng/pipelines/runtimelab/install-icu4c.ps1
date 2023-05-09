Invoke-WebRequest -Uri https://github.com/unicode-org/icu/releases/download/release-73-1/icu4c-73_1-src.zip -OutFile icu4c-73_1-src.zip

unzip icu4c-73_1-src.zip

cd icu

# build the host icu first
msbuild source\allinone\allinone.sln /p:Configuration=Debug /p:Platform=Win64 /p:SkipUWP=true

# then cross compile

