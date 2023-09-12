.DEFAULT_GOAL := publish

TOP=../../../../
REPO_DIR=$(realpath $(TOP))
BUILD_SCRIPT=$(REPO_DIR)/build.sh
DOTNET:=$(TOP)dotnet.sh
DOTNET_Q_ARGS=--nologo -v:q -consoleloggerparameters:NoSummary

BUILD_CONFIG?=Debug
TARGET_ARCH?=$(shell . $(TOP)eng/native/init-os-and-arch.sh && echo $${arch})
TARGET_OS?=$(shell . $(TOP)eng/native/init-os-and-arch.sh && echo $${os})
USE_MINT?=true

world: runtime publish

naot:
	$(BUILD_SCRIPT) clr.aot -c $(BUILD_CONFIG)

runtime:
	$(BUILD_SCRIPT) clr.aot+libs -c $(BUILD_CONFIG) $(MSBUILD_ARGS)

publish: clean
	$(DOTNET) publish -c $(BUILD_CONFIG) -r $(TARGET_OS)-$(TARGET_ARCH) -p:UseInterpreter=$(USE_MINT) /bl

run: publish
	$(TOP)artifacts/bin/HelloMint/$(TARGET_ARCH)/$(BUILD_CONFIG)/$(TARGET_OS)-$(TARGET_ARCH)/publish/HelloMint

clean:
	rm -rf msbuild.binlog $(TOP)artifacts/bin/HelloMint/ $(TOP)artifacts/obj/coreclr/HelloMint/
