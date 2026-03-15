using System;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cortis.Tests.EditMode
{
    /// <summary>
    /// MessageBinding コマンド側の統合テスト。
    /// TestGateway → MessageBinding → RecordingHandler で
    /// protobuf メッセージのフィルタからハンドラ呼び出しまでを通しで検証する。
    /// </summary>
    public class BinderCommandTests
    {
        TestGateway _gateway;
        RecordingHandler<StringValue> _handler;
        Binder _binder;

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
            _handler = new RecordingHandler<StringValue>();
            _binder = MessageBinding.Bind<StringValue>(_handler, _gateway);
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            _gateway.Dispose();
        }

        [Test]
        public void protobufメッセージを受信してハンドラに届く()
        {
            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "hello" }));

            Assert.AreEqual(1, _handler.Received.Count);
            Assert.AreEqual("hello", _handler.Received[0].Value);
        }

        [Test]
        public void 型が一致しないメッセージは無視される()
        {
            _gateway.SimulateReceive(Any.Pack(new Int32Value { Value = 42 }));

            Assert.AreEqual(0, _handler.Received.Count);
        }

        [Test]
        public void 複数メッセージが順序通りにハンドラに届く()
        {
            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "a" }));
            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "b" }));
            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "c" }));

            Assert.AreEqual(3, _handler.Received.Count);
            Assert.AreEqual("a", _handler.Received[0].Value);
            Assert.AreEqual("b", _handler.Received[1].Value);
            Assert.AreEqual("c", _handler.Received[2].Value);
        }

        [Test]
        public void ハンドラが例外を投げてもストリームが生存する()
        {
            _handler.SetOnHandle(_ => throw new InvalidOperationException("test"));

            LogAssert.Expect(LogType.Error, new Regex(@"\[MessageBinding<StringValue>\] Command error"));
            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "boom" }));
            Assert.AreEqual(1, _handler.Received.Count);

            _handler.SetOnHandle(null);

            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "ok" }));
            Assert.AreEqual(2, _handler.Received.Count);
            Assert.AreEqual("ok", _handler.Received[1].Value);
        }

        [Test]
        public void 無効なバイト列を受信してもストリームが生存する()
        {
            LogAssert.Expect(LogType.Error, new Regex("Failed to parse Any from bytes"));
            _gateway.SimulateReceive(new byte[] { 0xFF, 0xFF, 0xFF });

            Assert.AreEqual(0, _handler.Received.Count);

            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "ok" }));
            Assert.AreEqual(1, _handler.Received.Count);
            Assert.AreEqual("ok", _handler.Received[0].Value);
        }

        [Test]
        public void TypeUrlが一致するが破損したペイロードでもストリームが生存する()
        {
            var corrupted = new Any
            {
                TypeUrl = "type.googleapis.com/google.protobuf.StringValue",
                Value = ByteString.CopyFrom(0xFF, 0xFE, 0xFF, 0xFE, 0xFF)
            };
            LogAssert.Expect(LogType.Error, new Regex(@"\[MessageBinding<StringValue>\] Failed to unpack"));
            _gateway.SimulateReceive(corrupted);

            var countAfterCorrupted = _handler.Received.Count;

            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "ok" }));
            Assert.AreEqual(countAfterCorrupted + 1, _handler.Received.Count);
            Assert.AreEqual("ok", _handler.Received[countAfterCorrupted].Value);
        }

        [Test]
        public void Dispose後はメッセージが届かない()
        {
            _binder.Dispose();

            _gateway.SimulateReceive(Any.Pack(new StringValue { Value = "after" }));

            Assert.AreEqual(0, _handler.Received.Count);
        }
    }
}
