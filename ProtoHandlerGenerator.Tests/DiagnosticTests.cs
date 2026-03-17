using Microsoft.CodeAnalysis;
using Xunit;

namespace ProtoHandlerGenerator.Tests;

public class Proto001UnhandledCaseTests
{
    [Fact]
    public void 全caseにハンドラがある場合_PROTO001が出ない()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO001");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void 未処理caseがある場合_PROTO001が報告される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        // Reset is not handled
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO001");
        Assert.Single(diagnostics);
        Assert.Contains("Reset", diagnostics[0].GetMessage());
        Assert.Contains("void HandleReset(TestProto.MyCommand.Types.Reset command) { }", diagnostics[0].GetMessage());
    }

    [Fact]
    public void 複数の未処理case_その数だけPROTO001が出る()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(CmdOnly))]
    public sealed partial class TestPresenter
    {
        void HandleFoo(CmdOnly.Types.DoFoo cmd) { }
        // DoBar and DoBaz are not handled
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO001");
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void PROTO001のSeverityはError()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        // No handlers
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO001");
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }
}

public class Proto002UnmatchedHandleMethodTests
{
    [Fact]
    public void 全Handleメソッドがマッチする場合_PROTO002が出ない()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO002");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Handleプレフィックスで型が不一致の場合_PROTO002が報告される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
        void HandleUnknown(string arg) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO002");
        Assert.Single(diagnostics);
        Assert.Contains("HandleUnknown", diagnostics[0].GetMessage());
        Assert.Contains("string", diagnostics[0].GetMessage());
    }

    [Fact]
    public void PROTO002のSeverityはError()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
        void HandleTypo(int x) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO002");
        Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
    }

    [Fact]
    public void Handleプレフィックスなしのメソッドは_PROTO002の対象外()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
        void ProcessSomething(string arg) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO002");
        Assert.Empty(diagnostics);
    }
}

public class Proto003HandleMethodInEventOnlyTests
{
    [Fact]
    public void EventOnlyでHandleメソッドがある場合_PROTO003が報告される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(null, typeof(EvtOnly))]
    public sealed partial class TestPresenter
    {
        void HandleSomething(string arg) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.EventOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO003");
        Assert.Single(diagnostics);
        Assert.Contains("HandleSomething", diagnostics[0].GetMessage());
    }

    [Fact]
    public void EventOnlyでHandleメソッドがない場合_PROTO003が出ない()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(null, typeof(EvtOnly))]
    public sealed partial class TestPresenter
    {
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.EventOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO003");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void PROTO003のSeverityはError()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(null, typeof(EvtOnly))]
    public sealed partial class TestPresenter
    {
        void HandleFoo(int x) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.EventOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO003");
        Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
    }
}

public class Proto004MissingPartialTests
{
    [Fact]
    public void 非partialクラスにProtoHandler_PROTO004が報告される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed class TestPresenter  // no partial keyword
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO004");
        Assert.Single(diagnostics);
        Assert.Contains("TestPresenter", diagnostics[0].GetMessage());
    }

    [Fact]
    public void partialクラス_PROTO004が出ない()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO004");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Attribute無しの非partialクラス_何も出ない()
    {
        var source = @"
namespace Test
{
    public sealed class PlainClass { }
}";
        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO004");
        Assert.Empty(diagnostics);
    }
}
