using Microsoft.CodeAnalysis;
using Xunit;

namespace ProtoHandlerGenerator.Tests;

public class IncrementalTests
{
    /// inner 型のみ。この時点では親がいないのでルーティングは発生しない
    const string InnerProto = @"
namespace IncrementalProto
{
    public class PlayerAction
    {
        public enum ActionOneofCase { None = 0, Attack = 1 }
        public ActionOneofCase ActionCase { get; set; }
        public Types.Attack Attack { get; set; }
        public static class Types
        {
            public class Attack { }
        }
    }
}
";

    /// PlayerAction を oneof で包む root 型。後から追加してルート再解決を検証する
    const string RootProto = @"
namespace IncrementalProto
{
    public class AppAction
    {
        public enum ActionOneofCase { None = 0, PlayerAction = 1 }
        public ActionOneofCase ActionCase { get; set; }
        public PlayerAction PlayerAction { get; set; }
    }
}
";

    /// oneof を持たない、ルーティングに無関係な型
    const string UnrelatedSource = @"
namespace Unrelated
{
    public class Helper
    {
        public int Value { get; set; }
    }
}
";

    const string PresenterSource = @"
using Cortis;
using IncrementalProto;

namespace Test
{
    [ProtoHandler(typeof(PlayerAction))]
    public sealed partial class TestPresenter
    {
        void HandleAttack(PlayerAction.Types.Attack cmd) { }
        private partial void OnInitialize() { }
        private partial void OnDispose() { }
    }
}";

    [Fact]
    public void Presenterを触らずroot型を追加してもルートが再解決される()
    {
        var (first, second) = GeneratorTestHelper.RunGeneratorTwice(
            new[] { InnerProto, Stubs.VContainerStubs, Stubs.R3Stubs, PresenterSource },
            RootProto);

        var before = GeneratorTestHelper.GetGeneratedSource(first, "TestPresenter.g.cs");
        Assert.NotNull(before);
        Assert.DoesNotContain("BindRouted", before);

        // Presenter 側のソースは変えていないが、proto 側に親ができたので
        // ルーティング経路が再計算されなければならない
        var after = GeneratorTestHelper.GetGeneratedSource(second, "TestPresenter.g.cs");
        Assert.NotNull(after);
        Assert.Contains("BindRouted", after);
    }

    [Fact]
    public void oneofに無関係な型を追加してもルート解決は再実行されない()
    {
        var (_, second) = GeneratorTestHelper.RunGeneratorTwice(
            new[] { InnerProto, RootProto, Stubs.VContainerStubs, Stubs.R3Stubs, PresenterSource },
            UnrelatedSource);

        var reasons = GeneratorTestHelper.GetStepReasons(
            second, ProtoHandlerGen.TrackingNames.ResolveRoutes);

        Assert.NotEmpty(reasons);
        Assert.All(reasons, reason => Assert.Equal(IncrementalStepRunReason.Cached, reason));
    }
}
