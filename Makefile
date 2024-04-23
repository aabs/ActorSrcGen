VER=0.3.1
CONFIG=Release
build:
	dotnet.exe build -c $(CONFIG)
deploy:
	dotnet.exe nuget push ./ActorSrcGen.Abstractions/bin/$(CONFIG)/ActorSrcGen.Abstractions.$(VER).nupkg --api-key ${ACTORSRCGEN_NUGET_APIKEY} --source https://api.nuget.org/v3/index.json
	dotnet.exe nuget push ./ActorSrcGen/bin/$(CONFIG)/ActorSrcGen.$(VER).nupkg --api-key ${ACTORSRCGEN_NUGET_APIKEY} --source https://api.nuget.org/v3/index.json
	