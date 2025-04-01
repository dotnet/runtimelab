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

        [Fact]
        public async Task TestProductsWithInvalidIdsReturnsEmpty()
        {
            var productIdentifiers = new SwiftArray<SwiftString>();
            productIdentifiers.Append(new SwiftString("p1"));
            productIdentifiers.Append(new SwiftString("p2"));
			var productsTask = Product.products<SwiftArray<SwiftString>>(productIdentifiers);
            SwiftArray<Product> products = await productsTask;
            Assert.NotNull(products);
            Assert.Equal(0, products.Count);
		}

        [Fact]
        public void TestProductTypesEquality()
        {
            var productTypes = new SwiftArray<Product.ProductType>();
            productTypes.Append(Product.ProductType.consumable);
            productTypes.Append(Product.ProductType.nonConsumable);
            productTypes.Append(Product.ProductType.nonRenewable);
            productTypes.Append(Product.ProductType.autoRenewable);

            Assert.True (productTypes[0] == Product.ProductType.consumable);
            Assert.True (productTypes[1] == Product.ProductType.nonConsumable);
            Assert.True (productTypes[2] == Product.ProductType.nonRenewable);
            Assert.True (productTypes[3] == Product.ProductType.autoRenewable);
        }

        [Fact]
        public void TestAppStoreProperties()
        {
            bool canMakePayments = AppStore.canMakePayments;
            Assert.True(canMakePayments);

            SwiftString production = AppStore.Environment.production.rawValue;
            SwiftString sandbox = AppStore.Environment.sandbox.rawValue;
            SwiftString xcode = AppStore.Environment.xcode.rawValue;
            Assert.NotNull(production.ToString());
            Assert.NotNull(sandbox.ToString());
            Assert.NotNull(xcode.ToString());
        }

        [Fact]
        public void TestPurchaseOptionCustomMethods()
        {
            var opt1 = Product.PurchaseOption.custom(new SwiftString("key1"), new SwiftString("value1"));
            Assert.NotNull(opt1);
            var opt2 = Product.PurchaseOption.custom(new SwiftString("key2"), 9.99);
            Assert.NotNull(opt2);
        }

        [Fact]
        public async Task TestProductsPurchaseTypes()
        {
            var productIdentifiers = new SwiftArray<SwiftString>();
            productIdentifiers.Append(new SwiftString("p1"));
            productIdentifiers.Append(new SwiftString("p2"));
			var productsTask = Product.products<SwiftArray<SwiftString>>(productIdentifiers);
            SwiftArray<Product> products = await productsTask;

            for (int i = 0; i < products.Count; i++)
            {
                var product = products[i];
                Assert.NotNull(product);
                Assert.True(product.type == Product.ProductType.consumable || product.type == Product.ProductType.nonConsumable || product.type == Product.ProductType.nonRenewable || product.type == Product.ProductType.autoRenewable);
            }
        }

        [Fact]
        public async Task TestPurchaseVerified()
        {
            var productIdentifiers = new SwiftArray<SwiftString>();
            productIdentifiers.Append(new SwiftString("validID"));
			var productsTask = Product.products<SwiftArray<SwiftString>>(productIdentifiers);
            SwiftArray<Product> products = await productsTask;

            if (products.Count > 0)
            {
                var product = products[0];
                var result = await product.purchase(new SwiftSet<Product.PurchaseOption>());

                // Assert.True(result is Product.PurchaseResult.verified);
            }
        }

        [Fact]
        public async Task TestPurchaseWhenCancelledOrPending()
        {
            var productIdentifiers = new SwiftArray<SwiftString>();
            productIdentifiers.Append(new SwiftString("invalidID"));
			var productsTask = Product.products<SwiftArray<SwiftString>>(productIdentifiers);
            SwiftArray<Product> products = await productsTask;

            if (products.Count > 0)
            {
                var product = products[0];
                var result = await product.purchase(new SwiftSet<Product.PurchaseOption>());

                // Assert.True(result is Product.PurchaseResult.userCancelled or Product.PurchaseResult.pending);
            }
        }

        // [Fact]
        // public async Task CurrentEntitlements()
        // {
        //     await foreach (var result in Transaction.CurrentEntitlements)
        //     {
        //         switch (result)
        //         {
        //             case VerificationResult<Product> verification when verification.IsVerified:
        //                 TestProductsPurchaseTypes product = verification.Verified;
        //                 Assert.NotNull(product);
        //                 Assert.False(string.IsNullOrEmpty(product.ProductID));
        //                 break;

        //             case VerificationResult<Product> verification when !verification.IsVerified:
        //                 continue;
        //         }
        //     }
        // }
	}
}
