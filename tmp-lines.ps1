$i=0
Get-Content "d:/dev/aabs/ActorSrcGen/tests/ActorSrcGen.Tests/Unit/GeneratorTests.cs" | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }
