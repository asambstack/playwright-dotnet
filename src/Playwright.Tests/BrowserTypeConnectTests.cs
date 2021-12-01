/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Microsoft.Playwright.Tests
{
    ///<playwright-file>browsertype-connect.spec.ts</playwright-file>
    public class BrowserTypeConnectTests : PlaywrightTestEx
    {
        private BrowserServer _browserServer;

        [SetUp]
        public void SetUpAsync()
        {
            try
            {
                DirectoryInfo assemblyDirectory = new(AppContext.BaseDirectory);
                BrowserServer browserServer = new();
                browserServer.Process = new()
                {
                    StartInfo =
                    {
                        FileName = GetExecutablePath(),
                        Arguments = $"launch-server {BrowserType.Name}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = false,
                        CreateNoWindow = true,
                    },
                };
                browserServer.Process.Start();
                browserServer.WSEndpoint = browserServer.Process.StandardOutput.ReadLine();

                if (browserServer.WSEndpoint != null && !browserServer.WSEndpoint.StartsWith("ws://"))
                {
                    throw new PlaywrightException("Invalid web socket address: " + browserServer.WSEndpoint);
                }
                _browserServer = browserServer;
            }
            catch (IOException ex)
            {
                throw new PlaywrightException("Failed to launch server", ex);
            }
        }

        [TearDown]
        public void TearDown()
        {
            _browserServer.Process.Kill(true);
            _browserServer = null;
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should be able to reconnect to a browser")]
        public async Task ShouldBeAbleToConnectToBrowserAsync()
        {
            var browser = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync(Server.EmptyPage);
            Assert.AreEqual(Server.EmptyPage, page.Url);
            //await browser.CloseAsync();
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should support slowmo option")]
        public async Task ShouldSupportSlowMo()
        {
            var browser = await BrowserType.ConnectAsync(_browserServer.WSEndpoint, new BrowserTypeConnectOptions { SlowMo = 200 });
            var context = await browser.NewContextAsync();
            var time1 = DateTime.Now;            
            var page = await context.NewPageAsync();
            await page.GotoAsync(Server.EmptyPage);
            var time2 = DateTime.Now;
            Assert.AreEqual(Server.EmptyPage, page.Url);
            Assert.Greater((time2-time1).TotalMilliseconds, 200);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should be able to connect two browsers at the same time")]
        public async Task ShouldBeAbleToConnectTwoBrowsersAtTheSameTime()
        {
            var browserOne = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var browserTwo = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var contextOne = await browserOne.NewContextAsync();
            var contextTwo = await browserTwo.NewContextAsync();
            var pageOne = await contextOne.NewPageAsync();
            var pageTwo = await contextTwo.NewPageAsync();
            await pageOne.GotoAsync(Server.EmptyPage);
            await pageTwo.GotoAsync(Server.EmptyPage);
            Assert.AreEqual(Server.EmptyPage, pageOne.Url);
            Assert.AreEqual(Server.EmptyPage, pageTwo.Url);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "disconnected event should be emitted when browser is closed or server is closed")]
        public async Task DisconnectedEventShouldBeEmittedWhenBrowserIsClosedOrServerIsClosed()
        {
            var browserOne = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            int browserOneCount = 0;
            browserOne.Disconnected += (_, e) => browserOneCount++;
            //await browserOne.CloseAsync();
            Assert.AreEqual(browserOneCount, 1);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should handle exceptions during connect")]
        void ShouldHandleExceptionsDuringConnect()
        {
            // This is an implementation detail test
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should set the browser connected state")]
        public async Task ShouldSetTheBrowserConnectedState()
        {
            var browser = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync(Server.EmptyPage);
            Assert.AreEqual(browser.IsConnected, true);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should throw when used after isConnected returns false")]
        public async Task ShouldThrowWhenUsedAfterIsConnectedReturnsFalse()
        {
            var browser = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await Task.Run(() =>
            {
                _browserServer.Process.Kill(true);
            });
            await Task.Run(() =>
            {
                _browserServer.Process.Kill(true);
            });
            Assert.AreEqual(browser.IsConnected, false);
            var exception = await PlaywrightAssert.ThrowsAsync<PlaywrightException>(async () => await page.GotoAsync(Server.EmptyPage));
            Assert.AreEqual("Browser closed", exception.Message);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should reject navigation when browser closes")]
         public async Task ShouldRejectNavigationWhenBrowserCloses()
        {
            var browser = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync(Server.EmptyPage);
            await browser.CloseAsync();
            Assert.AreEqual(browser.IsConnected, false);
            var exception = await PlaywrightAssert.ThrowsAsync<PlaywrightException>(async () => await page.GotoAsync(Server.EmptyPage));
            Assert.AreEqual("Browser closed", exception.Message);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should reject waitForSelector when browser closes")]
        public async Task ShouldRejectWaitForSelectorWhenBrowserCloses()
        {
            var browser = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync(Server.EmptyPage);
            //await browser.CloseAsync();
            Assert.AreEqual(browser.IsConnected, false);
            var exception = await PlaywrightAssert.ThrowsAsync<PlaywrightException>(async () => await page.GotoAsync(Server.EmptyPage));
            Assert.AreEqual("Browser closed", exception.Message);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should emit close events on pages and contexts")]
        [Ignore("SKIP WIRE")]
        public async Task ShouldEmitCloseEventsOnPagesAndContexts()
        {
            //var socketClosedTcs = new TaskCompletionSource<bool>();
            //var log = new List<string>();
            //IWebSocket webSocket = null;

            //Page.WebSocket += (_, e) =>
            //{
            //    log.Add($"open<{e.Url}>");
            //    webSocket = e;
            //    webSocket.Close += (_, _) =>
            //    {
            //        log.Add("close");
            //        socketClosedTcs.TrySetResult(true);
            //    };
            //};

            //await Page.EvaluateAsync(@"port => {
            //    const ws = new WebSocket('ws://localhost:' + port + '/ws');
            //    ws.addEventListener('open', () => ws.close());
            //}", Server.Port);

            //await socketClosedTcs.Task;
            //Assert.AreEqual($"open<ws://localhost:{Server.Port}/ws>:close", string.Join(":", log));
            //Assert.True(webSocket.IsClosed);
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should terminate network waiters")]
        [Ignore("SKIP WIRE")]
        public async Task ShouldTerminateNetworkWaiters()
        {
            var browser = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            //const results = await Promise.all([
            //  newPage.waitForRequest(server.EMPTY_PAGE).catch (e => e),
            //    newPage.waitForResponse(server.EMPTY_PAGE).catch (e => e),
            //    remoteServer.close(),
            //  ]);
            //for (let i = 0; i < 2; i++)
            //{
            //    const message = results[i].message;
            //    expect(message).toContain('Page closed');
            //    expect(message).not.toContain('Timeout');
            //}

            var taskList = new[]
            {
              page.WaitForRequestAsync(Server.EmptyPage)
            };

            _browserServer.Process.Kill(true);
            var completedTasks = await Task.WhenAll(taskList);


            foreach (var task in completedTasks)
            {
                Console.WriteLine(task);
            }
        }

        [PlaywrightTest("browsertype-connect.spec.ts", "should respect selectors")]
        public async Task ShouldRespectSelectors()
        {
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();

            string mycss = @"
({
    query(root, selector) {
        return root.querySelector(selector);
    },
    queryAll(root, selector) {
        return Array.from(root.querySelectorAll(selector));
    }
})";

            // Register one engine before connecting.
            await playwright.Selectors.RegisterAsync("mycss1", new() { Script = mycss });

            var browser1 = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            var context1 = await browser1.NewContextAsync();

            await playwright.Selectors.RegisterAsync("mycss2", new() { Script = mycss });

            var page1 = await context1.NewPageAsync();
            await page1.SetContentAsync("<div>hello</div>");
            Assert.AreEqual("hello", await page1.InnerHTMLAsync("css=div"));
            Assert.AreEqual("hello", await page1.InnerHTMLAsync("mycss1=div"));
            Assert.AreEqual("hello", await page1.InnerHTMLAsync("mycss2=div"));

            var browser2 = await BrowserType.ConnectAsync(_browserServer.WSEndpoint);
            await playwright.Selectors.RegisterAsync("mycss3", new() { Script = mycss });
            var page2 = await browser2.NewPageAsync();
            await page2.SetContentAsync("<div>hello</div>");

            Assert.AreEqual("hello", await page2.InnerHTMLAsync("css=div"));
            Assert.AreEqual("hello", await page2.InnerHTMLAsync("mycss1=div"));
            Assert.AreEqual("hello", await page2.InnerHTMLAsync("mycss2=div"));
            Assert.AreEqual("hello", await page2.InnerHTMLAsync("mycss3=div"));

            //await browser1.CloseAsync();
            //await browser2.CloseAsync();
        }


        private class BrowserServer
        {
            public Process Process { get; set; }
            public string WSEndpoint { get; set; }
        }

        private static string GetExecutablePath()
        {
            DirectoryInfo assemblyDirectory = new(AppContext.BaseDirectory);
            if (!assemblyDirectory.Exists || !File.Exists(Path.Combine(assemblyDirectory.FullName, "Microsoft.Playwright.dll")))
            {
                var assemblyLocation = typeof(Playwright).Assembly.Location;
                assemblyDirectory = new FileInfo(assemblyLocation).Directory;
            }

            string executableFile = GetPath(assemblyDirectory.FullName);
            if (File.Exists(executableFile))
            {
                return executableFile;
            }

            // if the above fails, we can assume we're in the nuget registry
            executableFile = GetPath(assemblyDirectory.Parent.Parent.FullName);
            if (File.Exists(executableFile))
            {
                return executableFile;
            }

            throw new PlaywrightException($"Driver not found: {executableFile}");
        }

        private static string GetPath(string driversPath)
        {
            string platformId;
            string runnerName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platformId = "win32_x64";
                runnerName = "playwright.cmd";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                runnerName = "playwright.sh";
                platformId = "mac";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                runnerName = "playwright.sh";
                platformId = "linux";
            }
            else
            {
                throw new PlaywrightException("Unknown platform");
            }

            return Path.Combine(driversPath, ".playwright", "node", platformId, runnerName);
        }
    }
}
