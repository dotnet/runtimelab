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
		public async Task TestProductProducts()
		{
            var productIdentifiers = new SwiftArray<SwiftString>();
            productIdentifiers.Append(new SwiftString("p1"));
            productIdentifiers.Append(new SwiftString("p2"));
			var productsTask = Product.products<SwiftArray<SwiftString>>(productIdentifiers);
            SwiftArray<Product> products = await productsTask;
            Assert.NotNull(products);
            // StoreKit configuration is not set
            Assert.Equal(0, products.Count);
		}

        [Fact]
        public void TestProductTypes()
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

        [Fact(Skip = "https://github.com/dotnet/runtimelab/issues/2850")]
        public void TestPurchaseOptionCustomMethodsBool()
        {
            var opt3 = Product.PurchaseOption.custom(new SwiftString("key3"), true);
            Assert.NotNull(opt3);
        }

        [Fact]
        public void TestSubscriptionOfferNestedTypes()
        {
            var intro = Product.SubscriptionOffer.OfferType.introductory;
            var promo = Product.SubscriptionOffer.OfferType.promotional;
            var winBack = Product.SubscriptionOffer.OfferType.winBack;
            Assert.NotNull(intro);
            Assert.NotNull(promo);
            Assert.NotNull(winBack);

            var payAsYouGo = Product.SubscriptionOffer.PaymentMode.payAsYouGo;
            var payUpFront = Product.SubscriptionOffer.PaymentMode.payUpFront;
            var freeTrial = Product.SubscriptionOffer.PaymentMode.freeTrial;
            Assert.NotNull(payAsYouGo);
            Assert.NotNull(payUpFront);
            Assert.NotNull(freeTrial);
        }

        [Fact(Skip = "https://github.com/dotnet/runtimelab/issues/2850")]
        public void TestExternalPurchaseProperties()
        {
            bool canPresent = ExternalPurchase.canPresent;
            Assert.False(canPresent);
        }
	}
}
