using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using R3;

namespace Cortis.Tests.EditMode
{
    /// <summary>
    /// VContainer ライフサイクル順序のシミュレーションテスト。
    /// Constructor → [Inject] InjectBindings → Initialize の順序で
    /// Binder の購読が Initialize 前に完了していることを検証する。
    /// </summary>
    public class LifecycleTests
    {
        /// <summary>
        /// 生成コードが作る Handler を模したテスト用クラス。
        /// VContainer のライフサイクルを手動でシミュレートする。
        /// </summary>
        sealed class FakeHandler : ICommandHandler<StringValue>, IEventSource<Int32Value>
        {
            readonly Subject<Int32Value> _events = new();
            Binder _binder;

            public int HandleCount { get; private set; }

            // ICommandHandler
            public void Handle(StringValue command) => HandleCount++;

            // IEventSource
            public Observable<Int32Value> Events => _events;

            // 生成コードの [Inject] void InjectBindings(IMessageGateway) に相当
            public void InjectBindings(IMessageGateway gateway)
            {
                _binder = MessageBinding.Bind<StringValue, Int32Value>(this, this, gateway);
            }

            // 生成コードの Initialize() → OnInitialize() に相当
            public void Initialize()
            {
                // OnInitialize 内でイベントを発行する典型パターン
                _events.OnNext(new Int32Value { Value = 42 });
            }

            public void Dispose()
            {
                _binder?.Dispose();
                _events.Dispose();
            }
        }

        TestGateway _gateway;
        RecordingHandler<Int32Value> _eventCatcher;
        Binder _catcherBinder;

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
            _eventCatcher = new RecordingHandler<Int32Value>();
            _catcherBinder = MessageBinding.Bind<Int32Value>(_eventCatcher, _gateway);
        }

        [TearDown]
        public void TearDown()
        {
            _catcherBinder.Dispose();
            _gateway.Dispose();
        }

        [Test]
        public void InjectBindings後のInitializeでDispatchしたイベントが届く()
        {
            // VContainer ライフサイクルをシミュレート
            var handler = new FakeHandler();          // 1. Constructor
            handler.InjectBindings(_gateway);         // 2. [Inject] method
            handler.Initialize();                     // 3. Initialize

            // Initialize 内で発行された Int32Value(42) がループバックで届く
            Assert.AreEqual(1, _eventCatcher.Received.Count);
            Assert.AreEqual(42, _eventCatcher.Received[0].Value);

            handler.Dispose();
        }

        [Test]
        public void InjectBindings前にDispatchしたイベントは届かない()
        {
            var handler = new FakeHandler();

            // Bind 前に Initialize（旧設計で起きていた問題のシミュレーション）
            handler.Initialize();

            // イベントが届いていないことを確認
            Assert.AreEqual(0, _eventCatcher.Received.Count);

            handler.Dispose();
        }

        [Test]
        public void InjectBindings後にコマンドが即座にハンドラに届く()
        {
            var handler = new FakeHandler();
            handler.InjectBindings(_gateway);

            // Initialize を呼ぶ前でもコマンドは届く
            _gateway.SimulateReceive(
                Any.Pack(new StringValue { Value = "before-init" }));

            Assert.AreEqual(1, handler.HandleCount);

            handler.Dispose();
        }

        [Test]
        public void Dispose後はイベントもコマンドも処理されない()
        {
            var handler = new FakeHandler();
            handler.InjectBindings(_gateway);
            handler.Initialize();

            Assert.AreEqual(1, _eventCatcher.Received.Count);

            // Dispose 前にコマンドが届くことを確認
            _gateway.SimulateReceive(
                Any.Pack(new StringValue { Value = "before-dispose" }));
            Assert.AreEqual(1, handler.HandleCount);

            handler.Dispose();

            // Dispose 後のコマンドは届かない
            _gateway.SimulateReceive(
                Any.Pack(new StringValue { Value = "after-dispose" }));
            Assert.AreEqual(1, handler.HandleCount);
        }
    }
}
