﻿using System.Globalization;
using VerifyTests;
using VerifyXunit;
using Xunit;

// Non-nullable field is uninitialized.
#pragma warning disable CS8618

[UsesVerify]
public class Tests
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.AddExtraDatetimeFormat("F");
        VerifierSettings.AddExtraDatetimeOffsetFormat("F");
    }

    [Theory]
    [InlineData("a")]
    public Task ReplaceInvalidParamChar(string value)
    {
        return Verifier.Verify("foo")
            .UseParameters(Path.GetInvalidPathChars().First());
    }

    [Theory]
    [InlineData(1, 2)]
    public async Task IncorrectParameterCount_TooFew(int one, int two)
    {
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Verifier.Verify("Value").UseParameters(1));
        Assert.Equal("The number of passed in parameters (1) must match the number of parameters for the method (2).", exception.Message);
    }

    [Theory]
    [InlineData(1, 2)]
    public async Task IncorrectParameterCount_TooMany(int one, int two)
    {
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Verifier.Verify("Value").UseParameters(1, 2, 3));
        Assert.Equal("The number of passed in parameters (3) must match the number of parameters for the method (2).", exception.Message);
    }

    [Theory]
    [InlineData(1000.9999d)]
    public async Task LocalizedParam(decimal value)
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            await Verifier.Verify(value)
                .UseParameters(value);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }

    [Fact]
    public Task WithNewline()
    {
        return Verifier.Verify(new { Property = "F\roo" });
    }

    [ModuleInitializer]
    public static void TreatAsStringInit()
    {
        VerifierSettings.TreatAsString<ClassWithToString>(
            (target, _) => target.Property);
    }

    [Fact]
    public Task TreatAsString()
    {
        return Verifier.Verify(new ClassWithToString { Property = "Foo" });
    }

    class ClassWithToString
    {
        public string Property { get; set; } = null!;
    }

    [Fact]
    public async Task OnVerifyMismatch()
    {
        var settings = new VerifySettings();
        settings.DisableDiff();
        var onFirstVerifyCalled = false;
        var onVerifyMismatchCalled = false;
        VerifierSettings.OnFirstVerify(
            filePair =>
            {
                if (filePair.Name.Contains("OnVerifyMismatch"))
                {
                    onFirstVerifyCalled = true;
                }

                return Task.CompletedTask;
            });
        VerifierSettings.OnVerifyMismatch(
            (filePair, _) =>
            {
                if (filePair.Name.Contains("OnVerifyMismatch"))
                {
                    Assert.NotEmpty(filePair.Received);
                    Assert.NotNull(filePair.Received);
                    Assert.NotEmpty(filePair.Verified);
                    Assert.NotNull(filePair.Verified);
                    onVerifyMismatchCalled = true;
                }

                return Task.CompletedTask;
            });
        await Assert.ThrowsAsync<VerifyException>(() => Verifier.Verify("value", settings));
        Assert.False(onFirstVerifyCalled);
        Assert.True(onVerifyMismatchCalled);
    }

    [Fact]
    public async Task OnFirstVerify()
    {
        var settings = new VerifySettings();
        settings.DisableDiff();
        var onFirstVerifyCalled = false;
        var onVerifyMismatchCalled = false;
        VerifierSettings.OnFirstVerify(
            filePair =>
            {
                if (filePair.Name.Contains("OnFirstVerify"))
                {
                    Assert.NotEmpty(filePair.Received);
                    Assert.NotNull(filePair.Received);
                    onFirstVerifyCalled = true;
                }

                return Task.CompletedTask;
            });
        VerifierSettings.OnVerifyMismatch(
            (filePair, _) =>
            {
                if (filePair.Name.Contains("OnFirstVerify"))
                {
                    onVerifyMismatchCalled = true;
                }

                return Task.CompletedTask;
            });
        await Assert.ThrowsAsync<VerifyException>(() => Verifier.Verify("value", settings));
        Assert.True(onFirstVerifyCalled);
        Assert.False(onVerifyMismatchCalled);
    }

    [ModuleInitializer]
    public static void SettingsArePassedInit()
    {
        VerifierSettings.RegisterStreamComparer(
            "SettingsArePassed",
            (_, _, _) => Task.FromResult(new CompareResult(true)));
    }

    [Fact]
    public async Task SettingsArePassed()
    {
        var settings = new VerifySettings();
        settings.UseExtension("SettingsArePassed");
        await Verifier.Verify(new MemoryStream(new byte[] { 1 }), settings)
            .UseExtension("SettingsArePassed");
    }

    [Fact]
    public Task Throws()
    {
        return Verifier.Throws(MethodThatThrows);
    }

    static void MethodThatThrows()
    {
        throw new("The Message");
    }

    [Fact]
    public Task ThrowsNested()
    {
        return Verifier.Throws(Nested.MethodThatThrows);
    }

    static class Nested
    {
        public static void MethodThatThrows()
        {
            throw new("The Message");
        }
    }

    [Fact]
    public Task ThrowsArgumentException()
    {
        return Verifier.Throws(MethodThatThrowsArgumentException);
    }

    static void MethodThatThrowsArgumentException()
    {
        throw new ArgumentException("The Message", "The parameter");
    }

    [Fact]
    public Task ThrowsInheritedArgumentException()
    {
        return Verifier.Throws(MethodThatThrowsArgumentNullException);
    }

    static void MethodThatThrowsArgumentNullException()
    {
        throw new ArgumentNullException("The parameter", "The Message");
    }

    [Fact]
    public Task ThrowsAggregate()
    {
        var settings = new VerifySettings();
        settings.UniqueForRuntime();
        return Verifier.Throws(MethodThatThrowsAggregate, settings);
    }

    static void MethodThatThrowsAggregate()
    {
        throw new AggregateException(new Exception("The Message1"), new Exception("The Message2"));
    }

    [Fact]
    public Task ThrowsTask()
    {
        return Verifier.ThrowsTask(TaskMethodThatThrows)
            .UniqueForRuntime()
            .ScrubLinesContaining("ThrowsAsync");
    }

    static Task TaskMethodThatThrows()
    {
        throw new("The Message");
    }

    [Fact]
    public Task ThrowsTaskGeneric()
    {
        return Verifier.ThrowsTask(TaskMethodThatThrowsGeneric)
            .UniqueForRuntime()
            .ScrubLinesContaining("ThrowsAsync");
    }

    static Task<string> TaskMethodThatThrowsGeneric()
    {
        throw new("The Message");
    }

    [Fact]
    public Task ThrowsValueTask()
    {
        return Verifier.ThrowsValueTask(ValueTaskMethodThatThrows)
            .UniqueForRuntime()
            .ScrubLinesContaining("ThrowsAsync");
    }

    static ValueTask ValueTaskMethodThatThrows()
    {
        throw new("The Message");
    }

    [Fact]
    public Task ThrowsValueTaskGeneric()
    {
        return Verifier.ThrowsValueTask(ValueTaskMethodThatThrowsGeneric)
            .UniqueForRuntime()
            .ScrubLinesContaining("ThrowsAsync");
    }

    static ValueTask<string> ValueTaskMethodThatThrowsGeneric()
    {
        throw new("The Message");
    }

    [Fact]
    public Task StringBuilder()
    {
        return Verifier.Verify(new StringBuilder("value"));
    }

    [Fact]
    public Task NestedStringBuilder()
    {
        return Verifier.Verify(new { StringBuilder = new StringBuilder("value") });
    }

    [Fact]
    public Task TextWriter()
    {
        var target = new StringWriter();
        target.Write("content");
        return Verifier.Verify(target);
    }

    [Fact]
    public Task NestedTextWriter()
    {
        var target = new StringWriter();
        target.Write("content");
        return Verifier.Verify(new { target });
    }

    [Fact]
    public async Task StringWithDifferingNewline()
    {
        var fullPath = Path.GetFullPath("../../../Tests.StringWithDifferingNewline.verified.txt");
        File.Delete(fullPath);
        File.WriteAllText(fullPath, "a\r\nb");
        await Verifier.Verify("a\r\nb");
        PrefixUnique.Clear();
        await Verifier.Verify("a\rb");
        PrefixUnique.Clear();
        await Verifier.Verify("a\nb");
        PrefixUnique.Clear();

        File.Delete(fullPath);
        File.WriteAllText(fullPath, "a\nb");
        await Verifier.Verify("a\r\nb");
        PrefixUnique.Clear();
        await Verifier.Verify("a\rb");
        PrefixUnique.Clear();
        await Verifier.Verify("a\nb");
        PrefixUnique.Clear();

        File.Delete(fullPath);
        File.WriteAllText(fullPath, "a\rb");
        await Verifier.Verify("a\r\nb");
        PrefixUnique.Clear();
        await Verifier.Verify("a\rb");
        PrefixUnique.Clear();
        await Verifier.Verify("a\nb");
    }

    [Fact]
    public Task Stream()
    {
        return Verifier.Verify(new MemoryStream(new byte[] { 1 }));
    }

    [Fact]
    public Task StreamNotAtStart()
    {
        MemoryStream stream = new(new byte[] { 1, 2, 3, 4 });
        stream.Position = 2;
        return Verifier.Verify(stream);
    }

    [Fact]
    public Task StreamNotAtStartAsText()
    {
        MemoryStream stream = new(Encoding.UTF8.GetBytes("foo"));
        stream.Position = 2;
        return Verifier.Verify(stream).UseExtension("txt");
    }

    [Fact]
    public Task Streams()
    {
        return Verifier.Verify(
            new List<Stream>
            {
                new MemoryStream(new byte[] { 1 }),
                new MemoryStream(new byte[] { 2 })
            });
    }

    [Fact]
    public Task StreamsWithNull()
    {
        return Verifier.Verify(
            new List<Stream?>
            {
                new MemoryStream(new byte[] { 1 }),
                null
            });
    }

    [Fact]
    public async Task ShouldNotIgnoreCase()
    {
        await Verifier.Verify("A");
        var settings = new VerifySettings();
        settings.DisableDiff();
        PrefixUnique.Clear();
        await Assert.ThrowsAsync<VerifyException>(() => Verifier.Verify("a", settings));
    }

    [Fact]
    public Task Newlines()
    {
        return Verifier.Verify("a\r\nb\nc\rd\r\n");
    }

    class Element
    {
        public string? Id { get; set; }
    }

    [Fact]
    public Task ShouldThrowForExtensionOnSerialization()
    {
        var settings = new VerifySettings();
        settings.UseExtension("json");
        settings.UseMethodName("Foo");
        settings.IgnoreStackTrack();
        settings.DisableDiff();

        var element = new Element();
        return Verifier.ThrowsTask(() => Verifier.Verify(element, settings))
            .IgnoreStackTrack();
    }

    [Fact]
    public Task StringExtension()
    {
        var settings = new VerifySettings();
        settings.UseExtension("xml");

        return Verifier
            .Verify("<a>b</a>", settings);
    }

    [Fact]
    public Task TaskResult()
    {
        var target = Task.FromResult("value");
        return Verifier.Verify(target);
    }

    static async IAsyncEnumerable<string> AsyncEnumerableMethod()
    {
        await Task.Delay(1);
        yield return "one";
        await Task.Delay(1);
        yield return "two";
    }

    [Fact]
    public Task AsyncEnumerable()
    {
        return Verifier.Verify(AsyncEnumerableMethod());
    }

    static async IAsyncEnumerable<DisposableTarget> AsyncEnumerableDisposableMethod(DisposableTarget target)
    {
        await Task.Delay(1);
        yield return target;
    }

    [Fact]
    public async Task AsyncEnumerableDisposable()
    {
        var target = new DisposableTarget();
        await Verifier.Verify(AsyncEnumerableDisposableMethod(target));
        Assert.True(target.Disposed);
    }

    static async IAsyncEnumerable<AsyncDisposableTarget> AsyncEnumerableAsyncDisposableMethod(AsyncDisposableTarget target)
    {
        await Task.Delay(1);
        yield return target;
    }

    [Fact]
    public async Task AsyncEnumerableAsyncDisposable()
    {
        var target = new AsyncDisposableTarget();
        await Verifier.Verify(AsyncEnumerableAsyncDisposableMethod(target));
        Assert.True(target.AsyncDisposed);
    }

    [Fact]
    public async Task TaskResultAsyncDisposable()
    {
        var disposableTarget = new AsyncDisposableTarget();
        var target = Task.FromResult(disposableTarget);
        await Verifier.Verify(target);
        Assert.True(disposableTarget.AsyncDisposed);
    }

    class AsyncDisposableTarget :
        IAsyncDisposable,
        IDisposable
    {
#pragma warning disable 414
        public string Property = "Value";
#pragma warning restore 414
        public bool AsyncDisposed;

        public ValueTask DisposeAsync()
        {
            AsyncDisposed = true;
            return new();
        }

        public void Dispose()
        {
            throw new();
        }
    }

    [Fact]
    public async Task TaskResultDisposable()
    {
        var disposableTarget = new DisposableTarget();
        var target = Task.FromResult(disposableTarget);
        await Verifier.Verify(target);
        Assert.True(disposableTarget.Disposed);
    }

    class DisposableTarget :
        IDisposable
    {
#pragma warning disable 414
        public string Property = "Value";
#pragma warning restore 414
        public bool Disposed;

        public void Dispose()
        {
            Disposed = true;
        }
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task VerifyBytesAsync()
    {
        var settings = new VerifySettings();
        settings.UseExtension("jpg");
        await Verifier.Verify(File.ReadAllBytesAsync("sample.jpg"), settings);
    }
#endif

    [Fact]
    public async Task VerifyFilePath()
    {
        await Verifier.VerifyFile("sample.txt");
        Assert.False(FileEx.IsFileLocked("sample.txt"));
    }


    #region GetFilePath

    string GetFilePath([CallerFilePath] string sourceFile = "")
    {
        return sourceFile;
    }

    #endregion

    //[Fact(Skip = "explicit")]
    //public async Task ShouldUseExtraSettings()
    //{
    //    ApplyExtraSettings(settings => { settings.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat; });

    //    var person = new Person
    //    {
    //        Dob = new DateTime(1980, 5, 5, 1, 1, 1)
    //    };
    //    DontScrubDateTimes();
    //    await Verify(person);
    //}
}