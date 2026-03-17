using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;

namespace Cortis.Tests.EditMode
{
    /// <summary>
    /// MessageBinding.BindRouted&lt;TRootCommand, TCommand, TRootEvent, TEvent&gt; の統合テスト。
    /// root コマンドに Struct、inner コマンドに StringValue、
    /// root イベントに Struct、inner イベントに Int32Value を使い、
    /// unwrap/wrap の両方向を同時に検証する。
    /// </summary>
    public class BinderRoutedTwoTypeTests
    {
        const string CmdFieldKey = "cmd";
        const string EvtFieldKey = "evt";

        TestGateway _gateway;
        RecordingHandler<StringValue> _commandHandler;
        TestEventSource<Int32Value> _eventSource;
        RecordingHandler<Struct> _eventLoopbackHandler;
        Binder _binder;
        Binder _eventCatcher;

        static StringValue UnwrapCommand(Struct root)
        {
            if (root.Fields.TryGetValue(CmdFieldKey, out var v))
                return new StringValue { Value = v.StringValue };
            return null;
        }

        static Struct WrapEvent(Int32Value inner)
        {
            var root = new Struct();
            root.Fields[EvtFieldKey] = new Value { NumberValue = inner.Value };
            return root;
        }

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
            _commandHandler = new RecordingHandler<StringValue>();
            _eventSource = new TestEventSource<Int32Value>();
            _eventLoopbackHandler = new RecordingHandler<Struct>();

            _eventCatcher = MessageBinding.Bind<Struct>(_eventLoopbackHandler, _gateway);

            _binder = MessageBinding.BindRouted<Struct, StringValue, Struct, Int32Value>(
                _commandHandler, _eventSource, _gateway,
                UnwrapCommand, WrapEvent);
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            _eventCatcher.Dispose();
            _eventSource.Dispose();
            _gateway.Dispose();
        }

        [Test]
        public void root型コマンドがunwrapされてinner型ハンドラに届く()
        {
            var root = new Struct();
            root.Fields[CmdFieldKey] = new Value { StringValue = "attack" };
            _gateway.SimulateReceive(Any.Pack(root));

            Assert.AreEqual(1, _commandHandler.Received.Count);
            Assert.AreEqual("attack", _commandHandler.Received[0].Value);
        }

        [Test]
        public void inner型イベントがwrapされてroot型としてループバックで受信できる()
        {
            _eventSource.Emit(new Int32Value { Value = 42 });

            Assert.AreEqual(1, _eventLoopbackHandler.Received.Count);
            Assert.IsTrue(_eventLoopbackHandler.Received[0].Fields.ContainsKey(EvtFieldKey));
            Assert.AreEqual(42, _eventLoopbackHandler.Received[0].Fields[EvtFieldKey].NumberValue);
        }

        [Test]
        public void unwrapがnullを返した場合はハンドラに届かない()
        {
            var noCmd = new Struct();
            noCmd.Fields["other"] = new Value { StringValue = "unrelated" };
            _gateway.SimulateReceive(Any.Pack(noCmd));

            Assert.AreEqual(0, _commandHandler.Received.Count);
        }

        [Test]
        public void Dispose後_コマンドもイベントも処理されない()
        {
            _binder.Dispose();

            // コマンド: root 型を流しても unwrap されない
            var root = new Struct();
            root.Fields[CmdFieldKey] = new Value { StringValue = "after" };
            _gateway.SimulateReceive(Any.Pack(root));
            Assert.AreEqual(0, _commandHandler.Received.Count);

            // イベント: inner 型を emit しても wrap されない
            // （_eventCatcher は _binder とは無関係に Struct を受信するので、
            //   _binder 経由のイベント伝搬がないことは loopback 数の増分で検証）
            var countBefore = _eventLoopbackHandler.Received.Count;
            _eventSource.Emit(new Int32Value { Value = 99 });
            Assert.AreEqual(countBefore, _eventLoopbackHandler.Received.Count);
        }
    }
}
