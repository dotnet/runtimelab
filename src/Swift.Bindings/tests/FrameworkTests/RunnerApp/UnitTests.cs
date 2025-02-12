using System;
using Xunit;
using Xunit.Abstractions;

public static class MauiProgram
{
	public class UnitTests
	{
		readonly ITestOutputHelper _output;

		public UnitTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void SmokeTest()
		{
			Assert.True(true);
		}
	}
}