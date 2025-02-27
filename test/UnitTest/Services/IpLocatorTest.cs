﻿// Copyright (c) Argo Zhang (argo@163.com). All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Website: https://www.blazor.zone or https://argozhang.github.io/

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace UnitTest.Services;

public class IpLocatorTest : BootstrapBlazorTestBase
{
    [Fact]
    public async Task BaiduIpLocatorProvider_Ok()
    {
        var factory = Context.Services.GetRequiredService<IHttpClientFactory>();
        var option = Context.Services.GetRequiredService<IOptions<BootstrapBlazorOptions>>();
        var logger = Context.Services.GetRequiredService<ILogger<MockProvider>>();
        var provider = new MockProvider(factory, option, logger);

        var result = await provider.Locate("127.0.0.1");
        Assert.Equal("本地连接", result);

        result = await provider.Locate("");
        Assert.Equal("本地连接", result);

        // 河南省漯河市舞阳县 中国移动
        result = await provider.Locate("223.91.188.112");
        Assert.Equal("美国", result);

        // 命中缓存
        await provider.Locate("223.91.188.112");
        Assert.Equal("美国", result);

        // 关闭缓存
        option.Value.IpLocatorOptions.EnableCache = false;
        option.Value.IpLocatorOptions.SlidingExpiration = TimeSpan.FromMinutes(5);
        await provider.Locate("223.91.188.112");
        Assert.Equal("美国", result);
    }

    [Fact]
    public async Task BaiduIpLocatorProviderV2_Ok()
    {
        var factory = Context.Services.GetRequiredService<IHttpClientFactory>();
        var option = Context.Services.GetRequiredService<IOptions<BootstrapBlazorOptions>>();
        var logger = Context.Services.GetRequiredService<ILogger<MockProviderV2>>();
        var provider = new MockProviderV2(factory, option, logger);

        var result = await provider.Locate("127.0.0.1");
        Assert.Equal("本地连接", result);

        result = await provider.Locate("");
        Assert.Equal("本地连接", result);

        // 河南省漯河市 移动
        result = await provider.Locate("223.91.188.112");
        Assert.Equal("省份城市区县 测试", result);
    }

    [Fact]
    public void Factory_Ok()
    {
        var factory = Context.Services.GetRequiredService<IIpLocatorFactory>();
        Assert.NotNull(factory.Create("BaiduIpLocatorProviderV2"));
        Assert.NotNull(factory.Create("BaiduIpLocatorProvider"));
        Assert.NotNull(factory.Create());
    }

    [Fact]
    public void Factory_KeyNotFoundException()
    {
        var factory = Context.Services.GetRequiredService<IIpLocatorFactory>();
        Assert.Throws<KeyNotFoundException>(() => factory.Create("BaiduIpLocatorProviderV0"));
    }

    [Fact]
    public async Task Fetch_Error()
    {
        var factory = Context.Services.GetRequiredService<IHttpClientFactory>();
        var option = Context.Services.GetRequiredService<IOptions<BootstrapBlazorOptions>>();
        var logger = Context.Services.GetRequiredService<ILogger<MockProviderFetchError>>();
        var provider = new MockProviderFetchError(factory, option, logger);
        var result = await provider.Locate("223.91.188.112");
        Assert.Null(result);
    }

    [Fact]
    public async Task Fetch_Result_Fail()
    {
        var factory = Context.Services.GetRequiredService<IHttpClientFactory>();
        var option = Context.Services.GetRequiredService<IOptions<BootstrapBlazorOptions>>();
        option.Value.IpLocatorOptions.EnableCache = false;
        var logger = Context.Services.GetRequiredService<ILogger<MockBaiduProvider>>();
        var provider = new MockBaiduProvider(factory, option, logger);
        var result = await provider.Locate("223.91.188.112");
        Assert.Null(result);

        var loggerV2 = Context.Services.GetRequiredService<ILogger<MockBaiduProviderV2>>();
        var providerV2 = new MockBaiduProviderV2(factory, option, loggerV2);
        result = await providerV2.Locate("223.91.188.112");
        Assert.Null(result);
    }

    class MockProviderFetchError(IHttpClientFactory httpClientFactory, IOptions<BootstrapBlazorOptions> option, ILogger<MockProviderFetchError> logger) : BaiduIpLocatorProvider(httpClientFactory, option, logger)
    {
        protected override Task<string?> Fetch(string url, HttpClient client, CancellationToken token) => throw new InvalidOperationException();
    }

    class MockBaiduProvider(IHttpClientFactory httpClientFactory, IOptions<BootstrapBlazorOptions> option, ILogger<MockBaiduProvider> logger) : BaiduIpLocatorProvider(httpClientFactory, option, logger)
    {
        protected override Task<string?> Fetch(string url, HttpClient client, CancellationToken token)
        {
            client = new HttpClient(new MockHttpNullMessageHandler(), true);
            return base.Fetch(url, client, token);
        }
    }

    class MockBaiduProviderV2(IHttpClientFactory httpClientFactory, IOptions<BootstrapBlazorOptions> option, ILogger<MockBaiduProviderV2> logger) : BaiduIpLocatorProviderV2(httpClientFactory, option, logger)
    {
        protected override Task<string?> Fetch(string url, HttpClient client, CancellationToken token)
        {
            client = new HttpClient(new MockHttpNullMessageHandler(), true);
            return base.Fetch(url, client, token);
        }
    }

    class MockProvider(IHttpClientFactory httpClientFactory, IOptions<BootstrapBlazorOptions> option, ILogger<MockProvider> logger) : BaiduIpLocatorProvider(httpClientFactory, option, logger)
    {
        protected override Task<string?> Fetch(string url, HttpClient client, CancellationToken token)
        {
            client = new HttpClient(new MockHttpSuccessMessageHandler(), true);
            return base.Fetch(url, client, token);
        }
    }

    class MockProviderV2(IHttpClientFactory httpClientFactory, IOptions<BootstrapBlazorOptions> option, ILogger<MockProviderV2> logger) : BaiduIpLocatorProviderV2(httpClientFactory, option, logger)
    {
        protected override Task<string?> Fetch(string url, HttpClient client, CancellationToken token)
        {
            client = new HttpClient(new MockHttpSuccessMessageHandlerV2(), true);
            return base.Fetch(url, client, token);
        }
    }

    class MockHttpNullMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null")
            });
        }
    }

    class MockHttpSuccessMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"0\",\"t\":\"\",\"set_cache_time\":\"\",\"data\":[{\"ExtendedLocation\":\"\",\"OriginQuery\":\"20.205.243.166\",\"appinfo\":\"\",\"disp_type\":0,\"fetchkey\":\"20.205.243.166\",\"location\":\"美国\",\"origip\":\"20.205.243.166\",\"origipquery\":\"20.205.243.166\",\"resourceid\":\"6006\",\"role_id\":0,\"shareImage\":1,\"showLikeShare\":1,\"showlamp\":\"1\",\"titlecont\":\"IP地址查询\",\"tplt\":\"ip\"}]}")
            });
        }
    }

    class MockHttpSuccessMessageHandlerV2 : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":\"Success\",\"data\": {\"country\": \"中国\", \"prov\":\"省份\", \"city\":\"城市\", \"district\":\"区县\", \"isp\": \"测试\"}}")
            });
        }
    }
}
