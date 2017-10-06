// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script.WebHost.Extensions;
using Microsoft.Azure.WebJobs.Script.WebHost.Management;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Middleware
{
    public class VirtualFileSystemMiddleware : IMiddleware
    {
        private readonly VirtualFileSystemBase _vfs;
        private readonly VirtualFileSystemBase _zip;

        public VirtualFileSystemMiddleware(VirtualFileSystem vfs, ZipFileSystem zip)
        {
            _vfs = vfs;
            _zip = zip;
        }

        /// <summary>
        /// A request is a vfs request if it starts with /admin/zip or /admin/vfs
        /// </summary>
        /// <param name="context">Current HttpContext</param>
        /// <returns>IsVirtualFileSystemRequest</returns>
        public static bool IsVirtualFileSystemRequest(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments("/admin/vfs") ||
                context.Request.Path.StartsWithSegments("/admin/zip");
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate _)
        {
            // choose the right instance to use.
            var handler = context.Request.Path.StartsWithSegments("/admin/vfs") ? _vfs : _zip;
            HttpResponseMessage response = null;
            try
            {
                switch (context.Request.Method.ToLowerInvariant())
                {
                    case "get":
                        response = await handler.GetItem(context.Request);
                        break;

                    case "put":
                        response = await handler.PutItem(context.Request);
                        break;

                    case "delete":
                        response = await handler.DeleteItem(context.Request);
                        break;

                    default:
                        // VFS only supports GET, PUT, and DELETE
                        response = new HttpResponseMessage(System.Net.HttpStatusCode.MethodNotAllowed);
                        break;
                }

                context.Response.StatusCode = (int)response.StatusCode;

                // write response headers
                context.Response.Headers.AddRange(response.Headers.ToCoreHeaders());

                // This is to handle NullContent which != null, but has ContentLength of null.
                if (response.Content != null && response.Content.Headers.ContentLength != null)
                {
                    // Exclude content length to let ASP.NET Core take care of setting that based on the stream size.
                    context.Response.Headers.AddRange(response.Content.Headers.ToCoreHeaders("Content-Length"));
                    await response.Content.CopyToAsync(context.Response.Body);
                }
                response.Dispose();
            }
            catch (Exception e)
            {
                if (response != null)
                {
                    response.Dispose();
                }

                await context.Response.WriteAsync(e.Message);
            }
        }
    }
}