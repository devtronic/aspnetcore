// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Components.WebAssembly.Server
{
    internal class ContentEncodingNegotiator
    {
        // List of encodings by preference order with their associated extension so that we can easily handle "*".
        private static readonly StringSegment [] _preferredEncodings =
            new StringSegment [] { "br", "gzip" };

        private static readonly IDictionary<StringSegment, string> _encodingExtensionMap = new Dictionary<StringSegment, string>(StringSegmentComparer.OrdinalIgnoreCase)
        {
            ["br"] = ".br",
            ["gzip"] = ".gz"
        };

        private readonly RequestDelegate _next;

        public IWebHostEnvironment WebHostEnvironment { get; }

        public ContentEncodingNegotiator(RequestDelegate next, IWebHostEnvironment webHostEnvironment)
        {
            _next = next;
            WebHostEnvironment = webHostEnvironment;
        }

        public Task InvokeAsync(HttpContext context)
        {
            NegotiateEncoding(context);
            return _next(context);
        }

        private void NegotiateEncoding(HttpContext context)
        {
            var accept = context.Request.Headers[HeaderNames.AcceptEncoding];

            if (StringValues.IsNullOrEmpty(accept))
            {
                return;
            }

            if (!StringWithQualityHeaderValue.TryParseList(accept, out var encodings) || encodings.Count == 0)
            {
                return;
            }

            var selectedEncoding = StringSegment.Empty;
            var selectedEncodingQuality = .0;

            foreach (var encoding in encodings)
            {
                var encodingName = encoding.Value;
                var quality = encoding.Quality.GetValueOrDefault(1);

                if (quality < double.Epsilon)
                {
                    continue;
                }

                if (quality < selectedEncodingQuality)
                {
                    continue;
                }

                if (quality == selectedEncodingQuality)
                {
                    foreach (var preferredEncoding in _preferredEncodings)
                    {
                        if (preferredEncoding == selectedEncoding)
                        {
                            break;
                        }

                        if (preferredEncoding == encoding.Value && ResourceExists(context, _encodingExtensionMap[preferredEncoding]))
                        {
                            selectedEncoding = encoding.Value;
                            break;
                        }
                    }

                    continue;
                }

                if (_encodingExtensionMap.ContainsKey(encodingName) && ResourceExists(context, _encodingExtensionMap[encodingName]))
                {
                    selectedEncoding = encodingName;
                    selectedEncodingQuality = quality;
                }

                if (StringSegment.Equals("*", encodingName, StringComparison.Ordinal))
                {
                    foreach (var candidate in _preferredEncodings)
                    {
                        if (ResourceExists(context, _encodingExtensionMap[candidate]))
                        {
                            selectedEncoding = candidate;
                            break;
                        }
                    }

                    selectedEncodingQuality = quality;
                }

                if (StringSegment.Equals("identity", encodingName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedEncoding = StringSegment.Empty;
                    selectedEncodingQuality = quality;
                }
            }

            if (_encodingExtensionMap.TryGetValue(selectedEncoding, out var extension))
            {
                context.Request.Path = context.Request.Path + extension;
                context.Response.Headers[HeaderNames.ContentEncoding] = selectedEncoding.Value;
                context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.ContentEncoding);
            }

            return;
        }

        private bool ResourceExists(HttpContext context, string extension) =>
            WebHostEnvironment.WebRootFileProvider.GetFileInfo(context.Request.Path + extension).Exists;
    }
}
