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
        Assert.Contains("void HandlePlayerAction(ExternalProto.PlayerAction command) { }", diagnostics[0].GetMessage());
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

public class RoutedBindingTests
{
    [Fact]
    public void inner型を指定するとroot型が自動発見されBindRoutedが生成される()
    {
        var source = @"
using Cortis;
using RoutedProto;

namespace Test
{
    [ProtoHandler(typeof(PlayerAction), typeof(PlayerState))]
    public sealed partial class TestPresenter
    {
        void HandleAttack(PlayerAction.Types.Attack cmd) { }
        void HandleDefend(PlayerAction.Types.Defend cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("MessageBinding.BindRouted<RoutedProto.AppAction, RoutedProto.PlayerAction, RoutedProto.AppState, RoutedProto.PlayerState>", generated);
    }

    [Fact]
    public void unwrapラムダがoneofCaseチェック付きで生成される()
    {
        var source = @"
using Cortis;
using RoutedProto;

namespace Test
{
    [ProtoHandler(typeof(PlayerAction), typeof(PlayerState))]
    public sealed partial class TestPresenter
    {
        void HandleAttack(PlayerAction.Types.Attack cmd) { }
        void HandleDefend(PlayerAction.Types.Defend cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("root => root.ActionCase == RoutedProto.AppAction.ActionOneofCase.PlayerAction ? root.PlayerAction : null", generated);
    }

    [Fact]
    public void wrapラムダがroot型のオブジェクト初期化子で生成される()
    {
        var source = @"
using Cortis;
using RoutedProto;

namespace Test
{
    [ProtoHandler(typeof(PlayerAction), typeof(PlayerState))]
    public sealed partial class TestPresenter
    {
        void HandleAttack(PlayerAction.Types.Attack cmd) { }
        void HandleDefend(PlayerAction.Types.Defend cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("inner => new RoutedProto.AppState { PlayerState = inner }", generated);
    }

    [Fact]
    public void ルーティングがあってもinterfaceはinner型のまま()
    {
        var source = @"
using Cortis;
using RoutedProto;

namespace Test
{
    [ProtoHandler(typeof(PlayerAction), typeof(PlayerState))]
    public sealed partial class TestPresenter
    {
        void HandleAttack(PlayerAction.Types.Attack cmd) { }
        void HandleDefend(PlayerAction.Types.Defend cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("ICommandHandler<RoutedProto.PlayerAction>", generated);
        Assert.Contains("IEventSource<RoutedProto.PlayerState>", generated);
        Assert.Contains("Handle(RoutedProto.PlayerAction command)", generated);
    }

    [Fact]
    public void root型を直接指定した場合はルーティングなし()
    {
        var source = @"
using Cortis;
using RoutedProto;

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
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("MessageBinding.Bind<RoutedProto.AppAction, RoutedProto.AppState>", generated);
        Assert.DoesNotContain("BindRouted", generated);
    }

    [Fact]
    public void CommandOnlyでルーティングがある場合_2型パラムのBindRoutedが生成される()
    {
        var source = @"
using Cortis;
using RoutedCmdOnly;

namespace Test
{
    [ProtoHandler(typeof(InnerCommand))]
    public sealed partial class TestPresenter
    {
        void HandleDoFoo(InnerCommand.Types.DoFoo cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedCommandOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("MessageBinding.BindRouted<RoutedCmdOnly.RootCommand, RoutedCmdOnly.InnerCommand>", generated);
        Assert.Contains("root => root.CommandCase == RoutedCmdOnly.RootCommand.CommandOneofCase.InnerCommand ? root.InnerCommand : null", generated);
    }

    [Fact]
    public void protoc実出力のネスト構造でもルーティングが検出される()
    {
        var source = @"
using Cortis;
using NestedProto;

namespace Test
{
    [ProtoHandler(typeof(Cube.Types.FCommand), typeof(Cube.Types.UEvent))]
    public sealed partial class TestPresenter
    {
        void HandleSetScale(Cube.Types.FCommand.Types.SetScale cmd) { }
        void HandleReset(Cube.Types.FCommand.Types.Reset cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedNestedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("BindRouted", generated);
        Assert.Contains("NestedProto.App.Types.FCommand", generated);
        Assert.Contains("NestedProto.App.Types.UEvent", generated);
    }

    [Fact]
    public void 三段ネストでもルーティングが検出されBindRoutedが生成される()
    {
        var source = @"
using Cortis;
using TripleProto;

namespace Test
{
    [ProtoHandler(typeof(SubFeature.Types.FCommand), typeof(SubFeature.Types.UEvent))]
    public sealed partial class TestPresenter
    {
        void HandleDoAction(SubFeature.Types.FCommand.Types.DoAction cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedTripleNestedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        // Root型がApp.Types.FCommand/UEventであること
        Assert.Contains("BindRouted", generated);
        Assert.Contains("TripleProto.App.Types.FCommand", generated);
        Assert.Contains("TripleProto.App.Types.UEvent", generated);
    }

    [Fact]
    public void 三段ネストのunwrapが2ホップのチェーンで生成される()
    {
        var source = @"
using Cortis;
using TripleProto;

namespace Test
{
    [ProtoHandler(typeof(SubFeature.Types.FCommand), typeof(SubFeature.Types.UEvent))]
    public sealed partial class TestPresenter
    {
        void HandleDoAction(SubFeature.Types.FCommand.Types.DoAction cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedTripleNestedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        // App → Feature → SubFeature の2ホップ unwrap
        Assert.Contains("TripleProto.App.Types.FCommand.CommandOneofCase.Feature", generated);
        Assert.Contains("TripleProto.Feature.Types.FCommand.CommandOneofCase.SubFeature", generated);
    }

    [Fact]
    public void 三段ネストのwrapが2ホップのチェーンで生成される()
    {
        var source = @"
using Cortis;
using TripleProto;

namespace Test
{
    [ProtoHandler(typeof(SubFeature.Types.FCommand), typeof(SubFeature.Types.UEvent))]
    public sealed partial class TestPresenter
    {
        void HandleDoAction(SubFeature.Types.FCommand.Types.DoAction cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedTripleNestedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        // SubFeature.UEvent → Feature.UEvent → App.UEvent の2ホップ wrap
        Assert.Contains("new TripleProto.Feature.Types.UEvent { SubFeature = inner }", generated);
        Assert.Contains("new TripleProto.App.Types.UEvent { Feature =", generated);
    }

    [Fact]
    public void 複数の親がある場合_PROTO005が報告される()
    {
        var source = @"
using Cortis;
using AmbiguousProto;

namespace Test
{
    [ProtoHandler(typeof(InnerAction))]
    public sealed partial class TestPresenter
    {
        void HandleDoStuff(InnerAction.Types.DoStuff cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.AmbiguousRoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO005");
        Assert.Single(diagnostics);
        var msg = diagnostics[0].GetMessage();
        Assert.Contains("InnerAction", msg);
        Assert.Contains("[ProtoRoute(typeof(ParentA))]", msg);
        Assert.Contains("[ProtoRoute(typeof(ParentB))]", msg);
    }

    [Fact]
    public void ProtoRouteで曖昧性が解消される()
    {
        var source = @"
using Cortis;
using AmbiguousProto;

namespace Test
{
    [ProtoHandler(typeof(InnerAction))]
    [ProtoRoute(typeof(ParentA))]
    public sealed partial class TestPresenter
    {
        void HandleDoStuff(InnerAction.Types.DoStuff cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.AmbiguousRoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO005");
        Assert.Empty(diagnostics);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("BindRouted", generated);
        Assert.Contains("ParentA", generated);
    }

    [Fact]
    public void ProtoRouteで不正な型を指定するとPROTO006が報告される()
    {
        var source = @"
using Cortis;
using AmbiguousProto;

namespace Test
{
    [ProtoHandler(typeof(InnerAction))]
    [ProtoRoute(typeof(InnerAction))]
    public sealed partial class TestPresenter
    {
        void HandleDoStuff(InnerAction.Types.DoStuff cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.AmbiguousRoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO006");
        Assert.Single(diagnostics);
        Assert.Contains("InnerAction", diagnostics[0].GetMessage());
    }

    [Fact]
    public void EventOnlyでルーティングがある場合_BindRoutedとwrapラムダが生成される()
    {
        var source = @"
using Cortis;
using RoutedEvtOnly;

namespace Test
{
    [ProtoHandler(null, typeof(InnerState))]
    public sealed partial class TestPresenter
    {
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedEventOnlyMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("MessageBinding.BindRouted<RoutedEvtOnly.RootState, RoutedEvtOnly.InnerState>(this, gateway,", generated);
        Assert.Contains("inner => new RoutedEvtOnly.RootState { InnerState = inner }", generated);
    }

    [Fact]
    public void Command側のみルーティングありの場合_unwrapが生成されwrapはidentity()
    {
        var source = @"
using Cortis;
using RoutedProto;

namespace Test
{
    [ProtoHandler(typeof(PlayerAction), typeof(AppState))]
    public sealed partial class TestPresenter
    {
        void HandleAttack(PlayerAction.Types.Attack cmd) { }
        void HandleDefend(PlayerAction.Types.Defend cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("BindRouted<RoutedProto.AppAction, RoutedProto.PlayerAction, RoutedProto.AppState, RoutedProto.AppState>", generated);
        Assert.Contains("root => root.ActionCase == RoutedProto.AppAction.ActionOneofCase.PlayerAction ? root.PlayerAction : null", generated);
        Assert.Contains("inner => inner", generated);
    }

    [Fact]
    public void Event側のみルーティングありの場合_wrapが生成されunwrapはidentity()
    {
        var source = @"
using Cortis;
using RoutedProto;

namespace Test
{
    [ProtoHandler(typeof(AppAction), typeof(PlayerState))]
    public sealed partial class TestPresenter
    {
        void HandleLoadScene(AppAction.Types.LoadScene cmd) { }
        void HandlePlayerAction(PlayerAction cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("BindRouted<RoutedProto.AppAction, RoutedProto.AppAction, RoutedProto.AppState, RoutedProto.PlayerState>", generated);
        Assert.Contains("root => root", generated);
        Assert.Contains("inner => new RoutedProto.AppState { PlayerState = inner }", generated);
    }

    [Fact]
    public void Event側で複数の親がある場合_PROTO005が報告される()
    {
        var source = @"
using Cortis;
using AmbiguousEvtProto;

namespace Test
{
    [ProtoHandler(typeof(InnerCommand), typeof(InnerEvent))]
    public sealed partial class TestPresenter
    {
        void HandleDoIt(InnerCommand.Types.DoIt cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.AmbiguousEventRoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO005");
        Assert.Single(diagnostics);
        var msg = diagnostics[0].GetMessage();
        Assert.Contains("InnerEvent", msg);
        Assert.Contains("[ProtoRoute(typeof(ParentA))]", msg);
        Assert.Contains("[ProtoRoute(typeof(ParentB))]", msg);
    }

    [Fact]
    public void Event側でProtoRouteで曖昧性が解消される()
    {
        var source = @"
using Cortis;
using AmbiguousEvtProto;

namespace Test
{
    [ProtoHandler(typeof(InnerCommand), typeof(InnerEvent))]
    [ProtoRoute(typeof(ParentB))]
    public sealed partial class TestPresenter
    {
        void HandleDoIt(InnerCommand.Types.DoIt cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.AmbiguousEventRoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO005");
        Assert.Empty(diagnostics);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("BindRouted", generated);
        Assert.Contains("ParentB", generated);
    }

    [Fact]
    public void 多段ネストで中間層が曖昧な場合_ProtoRouteで解消される()
    {
        var source = @"
using Cortis;
using AmbiguousMidProto;

namespace Test
{
    [ProtoHandler(typeof(Leaf.Types.FCommand))]
    [ProtoRoute(typeof(Mid1))]
    public sealed partial class TestPresenter
    {
        void HandleDoIt(Leaf.Types.FCommand.Types.DoIt cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.AmbiguousMidLayerMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO005");
        Assert.Empty(diagnostics);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("BindRouted", generated);
        Assert.Contains("Root", generated);
        Assert.Contains("Mid1", generated);
    }

    [Fact]
    public void 多段ネストで中間層が曖昧でProtoRouteなし_PROTO005が報告される()
    {
        var source = @"
using Cortis;
using AmbiguousMidProto;

namespace Test
{
    [ProtoHandler(typeof(Leaf.Types.FCommand))]
    public sealed partial class TestPresenter
    {
        void HandleDoIt(Leaf.Types.FCommand.Types.DoIt cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.AmbiguousMidLayerMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics = GeneratorTestHelper.GetDiagnostics(result, "PROTO005");
        Assert.Single(diagnostics);
        var msg = diagnostics[0].GetMessage();
        Assert.Contains("Mid1", msg);
        Assert.Contains("Mid2", msg);
        Assert.Contains("[ProtoRoute(typeof(Mid1))]", msg);
        Assert.Contains("[ProtoRoute(typeof(Mid2))]", msg);
    }

    [Fact]
    public void ProtoRouteなしで一意なルートは正常に動作する()
    {
        var source = @"
using Cortis;
using RoutedProto;

namespace Test
{
    [ProtoHandler(typeof(PlayerAction), typeof(PlayerState))]
    public sealed partial class TestPresenter
    {
        void HandleJump(PlayerAction.Types.Jump cmd) { }
        void HandleMove(PlayerAction.Types.Move cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.RoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var diagnostics005 = GeneratorTestHelper.GetDiagnostics(result, "PROTO005");
        var diagnostics006 = GeneratorTestHelper.GetDiagnostics(result, "PROTO006");
        Assert.Empty(diagnostics005);
        Assert.Empty(diagnostics006);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
        Assert.Contains("BindRouted", generated);
    }

    [Fact]
    public void 循環参照があってもDiscoverRouteが無限ループしない()
    {
        var source = @"
using Cortis;
using CircularProto;

namespace Test
{
    [ProtoHandler(typeof(TypeA))]
    public sealed partial class TestPresenter
    {
        void HandleTypeB(TypeB cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";
        // Should not hang or throw
        var result = GeneratorTestHelper.RunGenerator(
            Stubs.CircularRoutedMessage, Stubs.VContainerStubs, Stubs.R3Stubs, source);

        var generated = GeneratorTestHelper.GetGeneratedSource(result, "TestPresenter.g.cs");
        Assert.NotNull(generated);
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
