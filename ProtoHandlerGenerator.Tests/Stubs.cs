namespace ProtoHandlerGenerator.Tests;

public static class Stubs
{
    public const string ProtoHandlerAttribute = @"
using System;

namespace Cortis
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProtoHandlerAttribute : Attribute
    {
        public Type CommandType { get; }
        public Type EventType { get; }

        public ProtoHandlerAttribute(Type commandType, Type eventType)
        {
            CommandType = commandType;
            EventType = eventType;
        }

        public ProtoHandlerAttribute(Type commandType)
        {
            CommandType = commandType;
            EventType = null;
        }
    }

    public interface ICommandHandler<T> { void Handle(T command); }
    public interface IEventSource<T> { R3.Observable<T> Events { get; } }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProtoRouteAttribute : Attribute
    {
        public Type[] Path { get; }
        public ProtoRouteAttribute(params Type[] path) { Path = path; }
    }
}
";

    public const string VContainerStubs = @"
namespace VContainer
{
    public interface IContainerBuilder { }
    public enum Lifetime { Transient, Singleton, Scoped }
}
namespace VContainer.Unity
{
    public interface IInitializable { void Initialize(); }
}
";

    public const string R3Stubs = @"
using System;
namespace R3
{
    public abstract class Observable<T> { }
    public class Subject<T> : Observable<T>, IDisposable
    {
        public void OnNext(T value) { }
        public void Dispose() { }
    }
    public class CompositeDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
";

    public const string UniTaskStubs = @"
namespace Cysharp.Threading.Tasks
{
    public struct UniTask { }
    public struct UniTaskVoid { }
}
public static class UniTaskExtensions
{
    public static void Forget(this Cysharp.Threading.Tasks.UniTask _) { }
    public static void Forget(this Cysharp.Threading.Tasks.UniTaskVoid _) { }
}
";

    /// <summary>
    /// protobuf風メッセージ型: Command(SetScale, Reset) + Event(ScaleChanged)
    /// </summary>
    public const string CommandAndEventMessage = @"
namespace TestProto
{
    public class MyCommand
    {
        public enum CommandOneofCase { None = 0, SetScale = 1, Reset = 2 }
        public CommandOneofCase CommandCase { get; set; }
        public Types.SetScale SetScale { get; set; }
        public Types.Reset Reset { get; set; }
        public static class Types
        {
            public class SetScale { }
            public class Reset { }
        }
    }

    public class MyEvent
    {
        public enum EventOneofCase { None = 0, ScaleChanged = 1 }
        public EventOneofCase EventCase { get; set; }
        public Types.ScaleChanged ScaleChanged { get; set; }
        public static class Types
        {
            public class ScaleChanged { }
        }
    }
}
";

    /// <summary>
    /// protobuf風メッセージ型: Command only (DoFoo, DoBar, DoBaz)
    /// </summary>
    public const string CommandOnlyMessage = @"
namespace TestProto
{
    public class CmdOnly
    {
        public enum CommandOneofCase { None = 0, DoFoo = 1, DoBar = 2, DoBaz = 3 }
        public CommandOneofCase CommandCase { get; set; }
        public Types.DoFoo DoFoo { get; set; }
        public Types.DoBar DoBar { get; set; }
        public Types.DoBaz DoBaz { get; set; }
        public static class Types
        {
            public class DoFoo { }
            public class DoBar { }
            public class DoBaz { }
        }
    }
}
";

    /// <summary>
    /// protobuf風メッセージ型: Commandのoneof caseが外部型を参照するパターン
    /// PAppAction { oneof: PlayerAction (external type) } のケース
    /// </summary>
    public const string ExternalOneofCaseMessage = @"
namespace ExternalProto
{
    // 外部型（Typesクラスの中ではない）
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

    // wrapper型: oneofが外部型を参照
    public class AppAction
    {
        public enum ActionOneofCase { None = 0, LoadScene = 1, PlayerAction = 2 }
        public ActionOneofCase ActionCase { get; set; }
        public Types.LoadScene LoadScene { get; set; }
        // 外部型をプロパティとして持つ（Typesにネストしていない）
        public PlayerAction PlayerAction { get; set; }
        public static class Types
        {
            public class LoadScene { }
            // PlayerActionはここにはない
        }
    }

    // wrapper event型: oneofあり
    public class AppState
    {
        public enum StateOneofCase { None = 0, SceneLoaded = 1 }
        public StateOneofCase StateCase { get; set; }
        public Types.SceneLoaded SceneLoaded { get; set; }
        public static class Types
        {
            public class SceneLoaded { }
        }
    }
}
";

    /// <summary>
    /// protobuf風メッセージ型: Event型にoneofがないパターン
    /// PAurisExperiencePlayerState のように通常フィールドのみ
    /// </summary>
    public const string NonOneofEventMessage = @"
namespace TestProto
{
    public class SimpleCommand
    {
        public enum CommandOneofCase { None = 0, DoAction = 1 }
        public CommandOneofCase CommandCase { get; set; }
        public Types.DoAction DoAction { get; set; }
        public static class Types
        {
            public class DoAction { }
        }
    }

    // oneofを持たないEvent型
    public class SimpleState
    {
        public string Name { get; set; }
        public int Phase { get; set; }
    }
}
";

    /// <summary>
    /// protobuf風メッセージ型: Event only (oneof あり)
    /// </summary>
    public const string EventOnlyMessage = @"
namespace TestProto
{
    public class EvtOnly
    {
        public enum EventOneofCase { None = 0, StatusChanged = 1, ProgressUpdated = 2 }
        public EventOneofCase EventCase { get; set; }
        public Types.StatusChanged StatusChanged { get; set; }
        public Types.ProgressUpdated ProgressUpdated { get; set; }
        public static class Types
        {
            public class StatusChanged { }
            public class ProgressUpdated { }
        }
    }
}
";

    /// <summary>
    /// protobuf風メッセージ型: Event only (oneof なし)
    /// </summary>
    public const string EventOnlyNonOneofMessage = @"
namespace TestProto
{
    public class SimpleEvent
    {
        public string Status { get; set; }
        public float Progress { get; set; }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: inner 型 (PlayerAction/PlayerState) が root 型 (AppAction/AppState) に包まれるパターン。
    /// [ProtoHandler(typeof(PlayerAction), typeof(PlayerState))] で使用すると、
    /// Generator が AppAction/AppState を自動発見してルーティングコードを生成する。
    /// </summary>
    public const string RoutedMessage = @"
namespace RoutedProto
{
    public class PlayerAction
    {
        public enum ActionOneofCase { None = 0, Attack = 1, Defend = 2 }
        public ActionOneofCase ActionCase { get; set; }
        public Types.Attack Attack { get; set; }
        public Types.Defend Defend { get; set; }
        public static class Types
        {
            public class Attack { }
            public class Defend { }
        }
    }

    public class AppAction
    {
        public enum ActionOneofCase { None = 0, LoadScene = 1, PlayerAction = 2 }
        public ActionOneofCase ActionCase { get; set; }
        public Types.LoadScene LoadScene { get; set; }
        public PlayerAction PlayerAction { get; set; }
        public static class Types
        {
            public class LoadScene { }
        }
    }

    public class PlayerState
    {
        public enum StateOneofCase { None = 0, HealthChanged = 1 }
        public StateOneofCase StateCase { get; set; }
        public Types.HealthChanged HealthChanged { get; set; }
        public static class Types
        {
            public class HealthChanged { }
        }
    }

    public class AppState
    {
        public enum StateOneofCase { None = 0, SceneLoaded = 1, PlayerState = 2 }
        public StateOneofCase StateCase { get; set; }
        public Types.SceneLoaded SceneLoaded { get; set; }
        public PlayerState PlayerState { get; set; }
        public static class Types
        {
            public class SceneLoaded { }
        }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: protoc 実出力と同じネスト構造 (App.Types.FCommand が Cube.Types.FCommand を包む)。
    /// CollectOneofMessageTypes が再帰的にネスト型をスキャンする必要がある。
    /// </summary>
    public const string RoutedNestedMessage = @"
namespace NestedProto
{
    public class Cube
    {
        public static class Types
        {
            public class FCommand
            {
                public enum CommandOneofCase { None = 0, SetScale = 1, Reset = 2 }
                public CommandOneofCase CommandCase { get; set; }
                public Types.SetScale SetScale { get; set; }
                public Types.Reset Reset { get; set; }
                public static class Types
                {
                    public class SetScale { }
                    public class Reset { }
                }
            }
            public class UEvent
            {
                public enum EventOneofCase { None = 0, ScaleChanged = 1 }
                public EventOneofCase EventCase { get; set; }
                public Types.ScaleChanged ScaleChanged { get; set; }
                public static class Types
                {
                    public class ScaleChanged { }
                }
            }
        }
    }

    public class App
    {
        public static class Types
        {
            public class FCommand
            {
                public enum CommandOneofCase { None = 0, Cube = 1 }
                public CommandOneofCase CommandCase { get; set; }
                public NestedProto.Cube.Types.FCommand Cube { get; set; }
            }
            public class UEvent
            {
                public enum EventOneofCase { None = 0, Cube = 1 }
                public EventOneofCase EventCase { get; set; }
                public NestedProto.Cube.Types.UEvent Cube { get; set; }
            }
        }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: 3段ネスト (App → Feature → SubFeature)。
    /// multi-hop unwrap/wrap が正しく生成されることを検証する。
    /// </summary>
    public const string RoutedTripleNestedMessage = @"
namespace TripleProto
{
    public class SubFeature
    {
        public static class Types
        {
            public class FCommand
            {
                public enum CommandOneofCase { None = 0, DoAction = 1 }
                public CommandOneofCase CommandCase { get; set; }
                public Types.DoAction DoAction { get; set; }
                public static class Types
                {
                    public class DoAction { }
                }
            }
            public class UEvent
            {
                public enum EventOneofCase { None = 0, ActionDone = 1 }
                public EventOneofCase EventCase { get; set; }
                public Types.ActionDone ActionDone { get; set; }
                public static class Types
                {
                    public class ActionDone { }
                }
            }
        }
    }

    public class Feature
    {
        public static class Types
        {
            public class FCommand
            {
                public enum CommandOneofCase { None = 0, SubFeature = 1 }
                public CommandOneofCase CommandCase { get; set; }
                public TripleProto.SubFeature.Types.FCommand SubFeature { get; set; }
            }
            public class UEvent
            {
                public enum EventOneofCase { None = 0, SubFeature = 1 }
                public EventOneofCase EventCase { get; set; }
                public TripleProto.SubFeature.Types.UEvent SubFeature { get; set; }
            }
        }
    }

    public class App
    {
        public static class Types
        {
            public class FCommand
            {
                public enum CommandOneofCase { None = 0, Feature = 1 }
                public CommandOneofCase CommandCase { get; set; }
                public TripleProto.Feature.Types.FCommand Feature { get; set; }
            }
            public class UEvent
            {
                public enum EventOneofCase { None = 0, Feature = 1 }
                public EventOneofCase EventCase { get; set; }
                public TripleProto.Feature.Types.UEvent Feature { get; set; }
            }
        }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: inner 型が複数の親に含まれるケース (PROTO005: ambiguous route)
    /// </summary>
    public const string AmbiguousRoutedMessage = @"
namespace AmbiguousProto
{
    public class InnerAction
    {
        public enum ActionOneofCase { None = 0, DoStuff = 1 }
        public ActionOneofCase ActionCase { get; set; }
        public Types.DoStuff DoStuff { get; set; }
        public static class Types
        {
            public class DoStuff { }
        }
    }

    public class ParentA
    {
        public enum ActionOneofCase { None = 0, InnerAction = 1 }
        public ActionOneofCase ActionCase { get; set; }
        public InnerAction InnerAction { get; set; }
    }

    public class ParentB
    {
        public enum ActionOneofCase { None = 0, InnerAction = 1 }
        public ActionOneofCase ActionCase { get; set; }
        public InnerAction InnerAction { get; set; }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: Command のみルーティングあり (Event なし)
    /// </summary>
    public const string RoutedCommandOnlyMessage = @"
namespace RoutedCmdOnly
{
    public class InnerCommand
    {
        public enum CommandOneofCase { None = 0, DoFoo = 1 }
        public CommandOneofCase CommandCase { get; set; }
        public Types.DoFoo DoFoo { get; set; }
        public static class Types
        {
            public class DoFoo { }
        }
    }

    public class RootCommand
    {
        public enum CommandOneofCase { None = 0, InnerCommand = 1 }
        public CommandOneofCase CommandCase { get; set; }
        public InnerCommand InnerCommand { get; set; }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: EventOnly + ルーティング (Inner event wrapped by root event)
    /// </summary>
    public const string RoutedEventOnlyMessage = @"
namespace RoutedEvtOnly
{
    public class InnerState
    {
        public enum StateOneofCase { None = 0, StatusChanged = 1 }
        public StateOneofCase StateCase { get; set; }
        public Types.StatusChanged StatusChanged { get; set; }
        public static class Types
        {
            public class StatusChanged { }
        }
    }

    public class RootState
    {
        public enum StateOneofCase { None = 0, InnerState = 1 }
        public StateOneofCase StateCase { get; set; }
        public InnerState InnerState { get; set; }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: Event 側の ambiguous route (PROTO005)
    /// </summary>
    public const string AmbiguousEventRoutedMessage = @"
namespace AmbiguousEvtProto
{
    public class InnerCommand
    {
        public enum CommandOneofCase { None = 0, DoIt = 1 }
        public CommandOneofCase CommandCase { get; set; }
        public Types.DoIt DoIt { get; set; }
        public static class Types
        {
            public class DoIt { }
        }
    }

    public class InnerEvent
    {
        public enum EventOneofCase { None = 0, Done = 1 }
        public EventOneofCase EventCase { get; set; }
        public Types.Done Done { get; set; }
        public static class Types
        {
            public class Done { }
        }
    }

    public class ParentA
    {
        public enum EventOneofCase { None = 0, InnerEvent = 1 }
        public EventOneofCase EventCase { get; set; }
        public InnerEvent InnerEvent { get; set; }
    }

    public class ParentB
    {
        public enum EventOneofCase { None = 0, InnerEvent = 1 }
        public EventOneofCase EventCase { get; set; }
        public InnerEvent InnerEvent { get; set; }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: 多段ネストで中間層が曖昧なケース。
    /// Root → Mid1 → Leaf (command)
    /// Root → Mid2 → Leaf (command)
    /// [ProtoRoute(typeof(Mid1))] で解消する。
    /// </summary>
    public const string AmbiguousMidLayerMessage = @"
namespace AmbiguousMidProto
{
    public class Leaf
    {
        public static class Types
        {
            public class FCommand
            {
                public enum CommandOneofCase { None = 0, DoIt = 1 }
                public CommandOneofCase CommandCase { get; set; }
                public Types.DoIt DoIt { get; set; }
                public static class Types
                {
                    public class DoIt { }
                }
            }
        }
    }

    public class Mid1
    {
        public enum ActionOneofCase { None = 0, Leaf = 1 }
        public ActionOneofCase ActionCase { get; set; }
        public Leaf.Types.FCommand Leaf { get; set; }
    }

    public class Mid2
    {
        public enum ActionOneofCase { None = 0, Leaf = 1 }
        public ActionOneofCase ActionCase { get; set; }
        public Leaf.Types.FCommand Leaf { get; set; }
    }

    public class Root
    {
        public enum ActionOneofCase { None = 0, Mid1 = 1, Mid2 = 2 }
        public ActionOneofCase ActionCase { get; set; }
        public Mid1 Mid1 { get; set; }
        public Mid2 Mid2 { get; set; }
    }
}
";

    /// <summary>
    /// ルーティングテスト用: 循環参照のある oneof 構造。
    /// DiscoverRoute が visited セットで無限ループを防ぐことを検証する。
    /// </summary>
    public const string CircularRoutedMessage = @"
namespace CircularProto
{
    public class TypeA
    {
        public enum AOneofCase { None = 0, TypeB = 1 }
        public AOneofCase ACase { get; set; }
        public TypeB TypeB { get; set; }
    }

    public class TypeB
    {
        public enum BOneofCase { None = 0, TypeA = 1 }
        public BOneofCase BCase { get; set; }
        public TypeA TypeA { get; set; }
    }
}
";
}
