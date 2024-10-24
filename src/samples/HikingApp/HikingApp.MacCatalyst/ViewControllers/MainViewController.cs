// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UIKit;

namespace HikingApp.MacCatalyst.ViewControllers
{
    public class MainViewController : UITabBarController
    {
        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var exploreViewController = new ExploreViewController();
            var myTrailsViewController = new MyTrailsViewController();

            var exploreNavigationController = new UINavigationController(exploreViewController)
            {
                TabBarItem = new UITabBarItem("Explore", null, 0)
            };

            var myTrailsNavigationController = new UINavigationController(myTrailsViewController)
            {
                TabBarItem = new UITabBarItem("My Trails", null, 1)
            };

            ViewControllers = new UIViewController[]
            {
                exploreNavigationController,
                myTrailsNavigationController,
            };
        }
    }
}
