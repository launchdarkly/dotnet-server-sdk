LaunchDarkly SDK for .NET
===========================

[![Build status](https://ci.appveyor.com/api/projects/status/wjj4xipn4xpniu8c?svg=true)](https://ci.appveyor.com/project/jkodumal/net-client)

Quick setup
-----------

0. Use [NuGet](http://docs.nuget.org/docs/start-here/using-the-package-manager-console) to add thet .NET SDK to your project

        Install-Package LaunchDarkly.Client

1. Import the LaunchDarkly package:

        using LaunchDarkly.Client;

2. Create a new LDClient with your API key:

        LdClient ldClient = new LdClient("YOUR_API_KEY");

Your first feature flag
-----------------------

1. Create a new feature flag on your [dashboard](https://app.launchdarkly.com)
2. In your application code, use the feature's key to check wthether the flag is on for each user:

        User user = User.WithKey(username);
        bool showFeature = await ldClient.toggle("your.feature.key", user, false);
        if (showFeature) {
          // application code to show the feature 
        }
        else {
          // the code to run if the feature is off
        }

Learn more
-----------

Check out our [documentation](http://docs.launchdarkly.com) for in-depth instructions on configuring and using LaunchDarkly. You can also head straight to the [complete reference guide for this SDK](http://docs.launchdarkly.com/v1.0/docs/dotnet-sdk-reference).

Contributing
------------

We encourage pull-requests and other contributions from the community. We've also published an [SDK contributor's guide](http://docs.launchdarkly.com/v1.0/docs/sdk-contributors-guide) that provides a detailed explanation of how our SDKs work.
