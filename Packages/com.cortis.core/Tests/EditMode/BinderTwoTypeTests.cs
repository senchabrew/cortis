using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;

namespace Cortis.Tests.EditMode
{
    /// <summary>
    /// MessageBinding.Bind&lt;TCommand, TEvent&gt; の統合テスト。
    /// コマンドに StringValue、イベントに Int32Value を使い分けて検証する。
    /// TestGateway により、イベントの protobuf 往復も確認する。
    /// </summary>
    public class BinderTwoTypeTests
    {
        TestGateway _gateway;
        RecordingHandler<StringValue> _commandHandler;
        TestEventSource<Int32Value> _eventSource;
        RecordingHandler<Int32Value> _eventLoopbackHandler;
        Binder _binder;
        Binder _eventCatcher;

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
            _commandHandler = new RecordingHandler<StringValue>();
            _eventSource = new TestEventSource<Int32Value>();
            _eventLoopbackHandler = new RecordingHandler<Int32Value>();

            // ループバックされたイベントを受信する Binder
            _eventCatcher = MessageBinding.Bind<Int32Value>(_eventLoopbackHandler, _gateway);

            _binder = MessageBinding.Bind<StringValue, Int32Value>(
                _commandHandler, _eventSource, _gateway);
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
        public void コマンドメッセージがハンドラに届く()
        {
            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "cmd" }));

            Assert.AreEqual(1, _commandHandler.Received.Count);
            Assert.AreEqual("cmd", _commandHandler.Received[0].Value);
        }

        [Test]
        public void イベントがループバックで受信できる()
        {
            _eventSource.Emit(new Int32Value { Value = 42 });

            Assert.AreEqual(1, _eventLoopbackHandler.Received.Count);
            Assert.AreEqual(42, _eventLoopbackHandler.Received[0].Value);
        }

        [Test]
        public void Dispose後_コマンドもイベントも処理されない()
        {
            _binder.Dispose();

            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "after" }));
            _eventSource.Emit(new Int32Value { Value = 99 });

            Assert.AreEqual(0, _commandHandler.Received.Count);
            Assert.AreEqual(0, _eventLoopbackHandler.Received.Count);
        }

        [Test]
        public void イベント型のバイト列はコマンドハンドラに届かない()
        {
            _gateway.SimulateReceive(Any.Pack(new Int32Value { Value = 100 }));

            Assert.AreEqual(0, _commandHandler.Received.Count);
        }
    }
}
