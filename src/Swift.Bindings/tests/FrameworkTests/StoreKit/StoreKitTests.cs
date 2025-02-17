using System;
using Xunit;
using Xunit.Abstractions;

public static class MauiProgram
{
	public class StoreKitTests
	{
		readonly ITestOutputHelper _output;

		public StoreKitTests(ITestOutputHelper output)
		{
			_output = output;
		}

        // Tracking issue: https://github.com/dotnet/runtimelab/issues/2850
		[Fact]
		public async Task Test1()
		{
            Assert.True(true);
		}
	}
}
