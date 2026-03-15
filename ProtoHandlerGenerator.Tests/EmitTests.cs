using Xunit;

namespace ProtoHandlerGenerator.Tests;

public class HandleMethodTests
{
    [Fact]
    public void 全caseのハンドラがswitch文に生成される()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("switch (command.CommandCase)", generated);
        Assert.Contains("case TestProto.MyCommand.CommandOneofCase.SetScale:", generated);
        Assert.Contains("case TestProto.MyCommand.CommandOneofCase.Reset:", generated);
        Assert.Contains("HandleSetScale(command.SetScale);", generated);
        Assert.Contains("HandleReset(command.Reset);", generated);
    }

    [Fact]
    public void 非同期ハンドラにはForgetが付く()
    {
        var source = @"
using Cortis;
using TestProto;
using Cysharp.Threading.Tasks;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        UniTask HandleSetScale(MyCommand.Types.SetScale cmd) => default;
        void HandleReset(MyCommand.Types.Reset cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs,
            Stubs.UniTaskStubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("HandleSetScale(command.SetScale).Forget();", generated);
        Assert.Contains("HandleReset(command.Reset);", generated);
        Assert.DoesNotContain("HandleReset(command.Reset).Forget();", generated);
    }

    [Fact]
    public void 非同期ハンドラがある場合_UniTaskのusingが追加される()
    {
        var source = @"
using Cortis;
using TestProto;
using Cysharp.Threading.Tasks;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public sealed partial class TestPresenter
    {
        UniTask HandleSetScale(MyCommand.Types.SetScale cmd) => default;
        void HandleReset(MyCommand.Types.Reset cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs,
            Stubs.UniTaskStubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("using Cysharp.Threading.Tasks;", generated);
    }

    [Fact]
    public void 同期のみの場合_UniTaskのusingがない()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.DoesNotContain("using Cysharp.Threading.Tasks;", generated);
    }
}

public class HandlePrefixFilterTests
{
    [Fact]
    public void Handleプレフィックスのないメソッドはハンドラとして認識されない()
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
        // Handle prefix がないのでハンドラとして認識されないべき
        void ProcessSetScale(MyCommand.Types.SetScale cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        // Handle prefix のあるメソッドだけが生成される
        Assert.Contains("HandleSetScale(command.SetScale);", generated);
        Assert.Contains("HandleReset(command.Reset);", generated);
        // Handle prefix のないメソッドは生成されない
        Assert.DoesNotContain("ProcessSetScale", generated);
    }
}

public class DispatchEventTests
{
    [Fact]
    public void EventCaseごとにDispatchEventが生成される()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("DispatchEvent(TestProto.MyEvent.Types.ScaleChanged value)", generated);
        Assert.Contains("_events.OnNext(new TestProto.MyEvent { ScaleChanged = value });", generated);
    }

    [Fact]
    public void sealedクラスではprotectedが付かない()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("void DispatchEvent(", generated);
        Assert.DoesNotContain("protected void DispatchEvent(", generated);
    }

    [Fact]
    public void 非sealedクラスではprotectedが付く()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(MyCommand), typeof(MyEvent))]
    public partial class TestPresenter
    {
        void HandleSetScale(MyCommand.Types.SetScale cmd) { }
        void HandleReset(MyCommand.Types.Reset cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandAndEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("protected void DispatchEvent(", generated);
    }

    [Fact]
    public void CommandOnlyの場合_DispatchEventが生成されない()
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
        void HandleBar(CmdOnly.Types.DoBar cmd) { }
        void HandleBaz(CmdOnly.Types.DoBaz cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.DoesNotContain("DispatchEvent", generated);
        Assert.DoesNotContain("_events", generated);
    }
}

public class RegisterMethodTests
{
    [Fact]
    public void CommandとEvent両方ある場合_自クラスとインタフェースが登録される()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("Register<TestPresenter>(lifetime)", generated);
        Assert.Contains(".As<ICommandHandler<TestProto.MyCommand>>()", generated);
        Assert.Contains(".As<IEventSource<TestProto.MyEvent>>()", generated);
    }

    [Fact]
    public void CommandOnlyの場合_自クラスが登録されIEventSourceがない()
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
        void HandleBar(CmdOnly.Types.DoBar cmd) { }
        void HandleBaz(CmdOnly.Types.DoBaz cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CommandOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("Register<TestPresenter>(lifetime)", generated);
        Assert.DoesNotContain("IEventSource", generated);
    }
}

public class ExternalOneofCaseTests
{
    [Fact]
    public void 外部型のoneofCaseがswitch文に生成される()
    {
        var source = @"
using Cortis;
using ExternalProto;

namespace Test
{
    [ProtoHandler(typeof(AppAction), typeof(AppState))]
    public sealed partial class TestPresenter
    {
        void HandleLoadScene(AppAction.Types.LoadScene cmd) { }
        void HandlePlayerAction(PlayerAction cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.ExternalOneofCaseMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("case ExternalProto.AppAction.ActionOneofCase.LoadScene:", generated);
        Assert.Contains("case ExternalProto.AppAction.ActionOneofCase.PlayerAction:", generated);
        Assert.Contains("HandleLoadScene(command.LoadScene);", generated);
        Assert.Contains("HandlePlayerAction(command.PlayerAction);", generated);
    }

    [Fact]
    public void 外部型のoneofCaseに未処理がある場合_PROTO001が報告される()
    {
        var source = @"
using Cortis;
using ExternalProto;

namespace Test
{
    [ProtoHandler(typeof(AppAction), typeof(AppState))]
    public sealed partial class TestPresenter
    {
        void HandleLoadScene(AppAction.Types.LoadScene cmd) { }
        // PlayerAction is not handled
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.ExternalOneofCaseMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO001");
        Assert.Single(diagnostics);
        Assert.Contains("PlayerAction", diagnostics[0].GetMessage());
    }
}

public class NonOneofEventTests
{
    [Fact]
    public void oneofなしEvent型の場合_単一のDispatchEventが生成される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(SimpleCommand), typeof(SimpleState))]
    public sealed partial class TestPresenter
    {
        void HandleDoAction(SimpleCommand.Types.DoAction cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.NonOneofEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        // 単一のDispatchEvent(TEvent)が生成される
        Assert.Contains("DispatchEvent(TestProto.SimpleState value)", generated);
        // パススルー: _events.OnNext(value) のみ
        Assert.Contains("_events.OnNext(value);", generated);
        // oneof case別のDispatchEventは生成されない
        Assert.DoesNotContain("new TestProto.SimpleState {", generated);
    }

    [Fact]
    public void oneofなしEvent型でもIEventSourceが実装される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(typeof(SimpleCommand), typeof(SimpleState))]
    public sealed partial class TestPresenter
    {
        void HandleDoAction(SimpleCommand.Types.DoAction cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.NonOneofEventMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("IEventSource<TestProto.SimpleState>", generated);
        Assert.Contains("Subject<TestProto.SimpleState> _events", generated);
        Assert.Contains("Register<TestPresenter>(lifetime)", generated);
    }
}

public class EventOnlyTests
{
    [Fact]
    public void EventOnlyの場合_ICommandHandlerが生成されない()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.DoesNotContain("ICommandHandler", generated);
        Assert.DoesNotContain("void Handle(", generated);
        Assert.DoesNotContain("switch", generated);
    }

    [Fact]
    public void EventOnlyの場合_IEventSourceが実装される()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("IEventSource<TestProto.EvtOnly>", generated);
        Assert.Contains("Subject<TestProto.EvtOnly> _events", generated);
    }

    [Fact]
    public void EventOnlyの場合_EventCaseごとにDispatchEventが生成される()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("DispatchEvent(TestProto.EvtOnly.Types.StatusChanged value)", generated);
        Assert.Contains("DispatchEvent(TestProto.EvtOnly.Types.ProgressUpdated value)", generated);
    }

    [Fact]
    public void EventOnlyの場合_RegisterにIEventSourceのみ登録される()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("Register<TestPresenter>(lifetime)", generated);
        Assert.Contains(".As<IEventSource<TestProto.EvtOnly>>()", generated);
        Assert.DoesNotContain("ICommandHandler", generated);
    }

    [Fact]
    public void EventOnlyの場合_BindがEventOnlyオーバーロードを使う()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("MessageBinding.Bind<TestProto.EvtOnly>(this, gateway)", generated);
    }

    [Fact]
    public void EventOnly_oneofなしの場合_単一DispatchEventが生成される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace Test
{
    [ProtoHandler(null, typeof(SimpleEvent))]
    public sealed partial class TestPresenter
    {
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.EventOnlyNonOneofMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("DispatchEvent(TestProto.SimpleEvent value)", generated);
        Assert.Contains("_events.OnNext(value);", generated);
    }
}

public class NamespaceTests
{
    [Fact]
    public void InfrastructureNamespaceがusingに生成される()
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("using Cortis;", generated);
    }

    [Fact]
    public void クラスのnamespaceが生成コードに反映される()
    {
        var source = @"
using Cortis;
using TestProto;

namespace My.Custom.Namespace
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

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("namespace My.Custom.Namespace", generated);
    }
}
