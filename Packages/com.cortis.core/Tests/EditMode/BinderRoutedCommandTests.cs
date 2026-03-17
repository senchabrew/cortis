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
    /// MessageBinding.BindRouted コマンド側の統合テスト。
    /// root 型に Struct、inner 型に StringValue を使い、
    /// unwrap 関数で Struct から StringValue を取り出すパターンを検証する。
    /// </summary>
    public class BinderRoutedCommandTests
    {
        const string FieldKey = "inner";

        TestGateway _gateway;
        RecordingHandler<StringValue> _handler;
        Binder _binder;

        static Struct WrapInStruct(string value)
        {
            var root = new Struct();
            root.Fields[FieldKey] = new Value { StringValue = value };
            return root;
        }

        static StringValue UnwrapFromStruct(Struct root)
        {
            if (root.Fields.TryGetValue(FieldKey, out var v))
                return new StringValue { Value = v.StringValue };
            return null;
        }

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
            _handler = new RecordingHandler<StringValue>();
            _binder = MessageBinding.BindRouted<Struct, StringValue>(
                _handler, _gateway, UnwrapFromStruct);
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            _gateway.Dispose();
        }

        [Test]
        public void root型を受信してunwrapされたinner型がハンドラに届く()
        {
            _gateway.SimulateReceive(Any.Pack(WrapInStruct("hello")));

            Assert.AreEqual(1, _handler.Received.Count);
            Assert.AreEqual("hello", _handler.Received[0].Value);
        }

        [Test]
        public void unwrapがnullを返した場合はハンドラに届かない()
        {
            var empty = new Struct();
            _gateway.SimulateReceive(Any.Pack(empty));

            Assert.AreEqual(0, _handler.Received.Count);
        }

        [Test]
        public void root型以外のメッセージは無視される()
        {
            _gateway.SimulateReceive(Any.Pack(new Int32Value { Value = 42 }));

            Assert.AreEqual(0, _handler.Received.Count);
        }

        [Test]
        public void ハンドラが例外を投げてもストリームが生存する()
        {
            _handler.SetOnHandle(_ => throw new InvalidOperationException("test"));

            LogAssert.Expect(LogType.Error, new Regex(@"\[MessageBinding<StringValue>\] Command error"));
            _gateway.SimulateReceive(Any.Pack(WrapInStruct("boom")));
            Assert.AreEqual(1, _handler.Received.Count);

            _handler.SetOnHandle(null);

            _gateway.SimulateReceive(Any.Pack(WrapInStruct("ok")));
            Assert.AreEqual(2, _handler.Received.Count);
            Assert.AreEqual("ok", _handler.Received[1].Value);
        }

        [Test]
        public void Dispose後はメッセージが届かない()
        {
            _binder.Dispose();

            _gateway.SimulateReceive(Any.Pack(WrapInStruct("after")));

            Assert.AreEqual(0, _handler.Received.Count);
        }

        [Test]
        public void TypeUrlが一致するが破損したペイロードでもストリームが生存する()
        {
            var corrupted = new Any
            {
                TypeUrl = "type.googleapis.com/google.protobuf.Struct",
                Value = ByteString.CopyFrom(0xFF, 0xFE, 0xFF, 0xFE, 0xFF)
            };
            LogAssert.Expect(LogType.Error, new Regex(@"\[MessageBinding<StringValue>\] Failed to unpack root"));
            _gateway.SimulateReceive(corrupted);

            Assert.AreEqual(0, _handler.Received.Count);

            _gateway.SimulateReceive(Any.Pack(WrapInStruct("ok")));
            Assert.AreEqual(1, _handler.Received.Count);
            Assert.AreEqual("ok", _handler.Received[0].Value);
        }

        [Test]
        public void unwrapが例外を投げてもストリームが生存する()
        {
            _binder.Dispose();

            var throwOnce = new[] { true };
            _binder = MessageBinding.BindRouted<Struct, StringValue>(
                _handler, _gateway, root =>
                {
                    if (throwOnce[0])
                    {
                        throwOnce[0] = false;
                        throw new InvalidOperationException("unwrap error");
                    }
                    return UnwrapFromStruct(root);
                });

            LogAssert.Expect(LogType.Error, new Regex(@"\[MessageBinding<StringValue>\] Failed to unwrap command"));
            _gateway.SimulateReceive(Any.Pack(WrapInStruct("boom")));
            Assert.AreEqual(0, _handler.Received.Count);

            _gateway.SimulateReceive(Any.Pack(WrapInStruct("ok")));
            Assert.AreEqual(1, _handler.Received.Count);
            Assert.AreEqual("ok", _handler.Received[0].Value);
        }
    }
}
