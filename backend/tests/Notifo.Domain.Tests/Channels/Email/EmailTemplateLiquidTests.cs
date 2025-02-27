﻿// ==========================================================================
//  Notifo.io
// ==========================================================================
//  Copyright (c) Sebastian Stehle
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Mjml.Net;
using Notifo.Domain.Channels.Email.Formatting;
using Notifo.Domain.Utils;

namespace Notifo.Domain.Channels.Email
{
    public class EmailTemplateLiquidTests : EmailTemplateTestsBase
    {
        protected override IEmailFormatter CreateFormatter(IEmailUrl url, IImageFormatter imageFormatter)
        {
            return new EmailFormatterLiquid(imageFormatter, url, new MjmlRenderer());
        }
    }
}
