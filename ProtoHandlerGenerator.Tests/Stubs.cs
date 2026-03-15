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
}
