using System.ComponentModel;
using System.Reflection;
using Moq;
using NewLife;
using NewLife.Data;
using NewLife.Http;
using Xunit;

namespace UnitTest;

/// <summary>内嵌资源 MIME 类型检测单元测试</summary>
/// <remarks>基于 UnitTest 程序集自身的 TestData/sample.xyz 内嵌资源，真实触发资源命中与 MIME 解析路径</remarks>
[Collection("WebPanel")]
public class EmbeddedResourceMimeTests
{
    private static readonly Assembly _testAssembly = typeof(EmbeddedResourceMimeTests).Assembly;

    private static IHttpContext CreateContext(String path)
    {
        var response = new HttpResponse();
        var request = new HttpRequest();
        request.Parse(new ArrayPacket($"GET {path} HTTP/1.1\r\nHost: localhost\r\n\r\n".GetBytes()));

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(response);
        context.Setup(x => x.Path).Returns(path);

        return context.Object;
    }

    private static EmbeddedFileHandler CreateHandler() => new()
    {
        Path = "/",
        ContentPath = "UnitTest.TestData",
        Assembly = _testAssembly
    };

    #region MIME类型
    [Fact]
    [DisplayName("未知扩展名内嵌资源_返回200且MIME回退为八位字节流")]
    public async Task UnknownExtensionEmbeddedResource_Returns200WithOctetStream()
    {
        var handler = CreateHandler();
        var ctx = CreateContext("/sample.xyz");

        await handler.ProcessRequestAsync(ctx);

        Assert.Equal(200, (Int32)ctx.Response.StatusCode);
        Assert.StartsWith("application/octet-stream", ctx.Response.ContentType);

        var stream = ctx.Response.BodyStream;
        Assert.NotNull(stream);
        Assert.True(stream.ReadBytes(-1).Length > 0);
    }
    #endregion

    #region 缺省文档与路径安全
    [Theory]
    [InlineData("/")]
    [InlineData("/:sample.xyz")]
    [DisplayName("缺省文档不存在_返回404且ContentType不为html")]
    public async Task MissingDefaultDocument_Returns404(String path)
    {
        var handler = CreateHandler();
        var ctx = CreateContext(path);

        await handler.ProcessRequestAsync(ctx);

        Assert.Equal(404, (Int32)ctx.Response.StatusCode);
        Assert.NotEqual("text/html", ctx.Response.ContentType);
    }

    [Theory]
    [InlineData("/.hidden")]
    [InlineData("/../../etc/passwd")]
    [DisplayName("隐藏文件或路径穿越_返回404")]
    public async Task HiddenFileOrPathTraversal_Returns404(String path)
    {
        var handler = CreateHandler();
        var ctx = CreateContext(path);

        await handler.ProcessRequestAsync(ctx);

        Assert.Equal(404, (Int32)ctx.Response.StatusCode);
    }
    #endregion
}
