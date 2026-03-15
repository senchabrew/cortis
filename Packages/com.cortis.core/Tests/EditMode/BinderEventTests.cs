using System.Text.RegularExpressions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cortis.Tests.EditMode
{
    /// <summary>
    /// MessageBinding イベント側の統合テスト。
    /// TestGateway を使い、イベント → Pack → Send → Parse → Unpack
    /// という protobuf 往復を検証する。
    /// </summary>
    public class BinderEventTests
    {
        TestGateway _gateway;
        TestEventSource<StringValue> _eventSource;
        RecordingHandler<StringValue> _loopbackHandler;
        Binder _eventBinder;
        Binder _loopbackReceiver;

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
            _eventSource = new TestEventSource<StringValue>();
            _loopbackHandler = new RecordingHandler<StringValue>();

            // ループバック受信側を先にセットアップ
            _loopbackReceiver = MessageBinding.Bind<StringValue>(_loopbackHandler, _gateway);
            _eventBinder = MessageBinding.Bind<StringValue>(_eventSource, _gateway);
        }

        [TearDown]
        public void TearDown()
        {
            _eventBinder.Dispose();
            _loopbackReceiver.Dispose();
            _eventSource.Dispose();
            _gateway.Dispose();
        }

        [Test]
        public void イベントがprotobufシリアライズを経てループバックで受信できる()
        {
            _eventSource.Emit(new StringValue { Value = "hello" });

            Assert.AreEqual(1, _loopbackHandler.Received.Count);
            Assert.AreEqual("hello", _loopbackHandler.Received[0].Value);
        }

        [Test]
        public void 同じイベントが連続した場合_DistinctUntilChangedで1回のみ到達する()
        {
            var same = new StringValue { Value = "same" };

            _eventSource.Emit(same);
            _eventSource.Emit(same);
            _eventSource.Emit(same);

            Assert.AreEqual(1, _loopbackHandler.Received.Count);
        }

        [Test]
        public void 同じ値の別インスタンスが連続した場合_DistinctUntilChangedで1回のみ到達する()
        {
            _eventSource.Emit(new StringValue { Value = "same" });
            _eventSource.Emit(new StringValue { Value = "same" });
            _eventSource.Emit(new StringValue { Value = "same" });

            Assert.AreEqual(1, _loopbackHandler.Received.Count);
        }

        [Test]
        public void 異なるイベントが連続した場合_それぞれ到達する()
        {
            _eventSource.Emit(new StringValue { Value = "a" });
            _eventSource.Emit(new StringValue { Value = "b" });
            _eventSource.Emit(new StringValue { Value = "a" });

            Assert.AreEqual(3, _loopbackHandler.Received.Count);
            Assert.AreEqual("a", _loopbackHandler.Received[0].Value);
            Assert.AreEqual("b", _loopbackHandler.Received[1].Value);
            Assert.AreEqual("a", _loopbackHandler.Received[2].Value);
        }

        [Test]
        public void Sendが例外を投げてもストリームが生存する()
        {
            _gateway.SimulateError = true;

            LogAssert.Expect(LogType.Error, new Regex(@"\[MessageBinding<StringValue>\] Event error"));
            _eventSource.Emit(new StringValue { Value = "boom" });
            Assert.AreEqual(0, _loopbackHandler.Received.Count);

            _gateway.SimulateError = false;

            _eventSource.Emit(new StringValue { Value = "ok" });
            Assert.AreEqual(1, _loopbackHandler.Received.Count);
            Assert.AreEqual("ok", _loopbackHandler.Received[0].Value);
        }

        [Test]
        public void Dispose後_イベントが発行されてもループバックされない()
        {
            _eventBinder.Dispose();

            _eventSource.Emit(new StringValue { Value = "after dispose" });

            Assert.AreEqual(0, _loopbackHandler.Received.Count);
        }
    }
}
