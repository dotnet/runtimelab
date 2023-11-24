MONO_CONFIG=Debug
MONO_ARCH?=arm64
DOTNET := ../../../../dotnet.sh
DEPLOY_AND_RUN?=true
TOOLS_DIR=../../../tasks

appbuilder:
	$(DOTNET) build -c Debug $(TOOLS_DIR)/AndroidAppBuilder/AndroidAppBuilder.csproj

run: clean appbuilder
	$(DOTNET) publish \
	/p:TargetArchitecture=$(MONO_ARCH) \
	/p:TargetOS=linux-bionic \
	/p:Configuration=$(MONO_CONFIG) \
	/p:DeployAndRun=$(DEPLOY_AND_RUN) \
	/bl

clean:
	rm -rf ../../../../artifacts/bin/AndroidSampleApp
