using System;
using Xunit;
using Xunit.Abstractions;
using Swift;
using Swift.Runtime;
using Swift.StoreKit;

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
		public async Task Test()
		{
            var productIdentifiers = new SwiftArray<SwiftString>();
            productIdentifiers.Append(new SwiftString("com.example.product1"));
            productIdentifiers.Append(new SwiftString("com.example.product2"));
            productIdentifiers.Append(new SwiftString("com.example.product3"));
			var productsTask = Product.products<SwiftArray<SwiftString>>(productIdentifiers);
            SwiftArray<Product> products = await productsTask;
            Assert.NotNull(products);
            // TODO: Add .storekit config
            Assert.Equal(0, products.Count);
		}
	}
}
