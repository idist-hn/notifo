﻿// ==========================================================================
//  Notifo.io
// ==========================================================================
//  Copyright (c) Sebastian Stehle
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using TestSuite.Utils;

namespace TestSuite.ApiTests
{
    public sealed class WebhookCatcherFixture
    {
        internal WebhookCatcherClient Client { get; }

        public WebhookCatcherFixture()
        {
            Client = new WebhookCatcherClient(
                TestHelpers.GetAndPrintValue("webhookcatcher:host:api", "localhost"), 1026,
                TestHelpers.GetAndPrintValue("webhookcatcher:host:endpoint", "localhost"), 1026);
        }
    }
}
