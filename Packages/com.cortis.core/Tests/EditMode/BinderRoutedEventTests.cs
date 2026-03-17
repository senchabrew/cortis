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
    /// MessageBinding.BindRouted イベント側の統合テスト。
    /// inner 型に StringValue、root 型に Struct を使い、
    /// wrap 関数で StringValue を Struct に包んで送信するパターンを検証する。
    /// </summary>
    public class BinderRoutedEventTests
    {
        const string FieldKey = "inner";

        TestGateway _gateway;
        TestEventSource<StringValue> _eventSource;
        RecordingHandler<Struct> _loopbackHandler;
        Binder _eventBinder;
        Binder _loopbackReceiver;

        static Struct WrapToStruct(StringValue inner)
        {
            var root = new Struct();
            root.Fields[FieldKey] = new Value { StringValue = inner.Value };
            return root;
        }

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
            _eventSource = new TestEventSource<StringValue>();
            _loopbackHandler = new RecordingHandler<Struct>();

            _loopbackReceiver = MessageBinding.Bind<Struct>(_loopbackHandler, _gateway);
            _eventBinder = MessageBinding.BindRouted<Struct, StringValue>(
                _eventSource, _gateway, WrapToStruct);
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
        public void inner型イベントがwrapされてroot型としてループバックで受信できる()
        {
            _eventSource.Emit(new StringValue { Value = "hello" });

            Assert.AreEqual(1, _loopbackHandler.Received.Count);
            Assert.IsTrue(_loopbackHandler.Received[0].Fields.ContainsKey(FieldKey));
            Assert.AreEqual("hello", _loopbackHandler.Received[0].Fields[FieldKey].StringValue);
        }

        [Test]
        public void 同じイベントが連続した場合_DistinctUntilChangedで1回のみ到達する()
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
        }

        [Test]
        public void Dispose後_イベントが発行されてもループバックされない()
        {
            _eventBinder.Dispose();

            _eventSource.Emit(new StringValue { Value = "after dispose" });

            Assert.AreEqual(0, _loopbackHandler.Received.Count);
        }

        [Test]
        public void wrapが例外を投げてもストリームが生存する()
        {
            _eventBinder.Dispose();

            var throwOnce = new[] { true };
            _eventBinder = MessageBinding.BindRouted<Struct, StringValue>(
                _eventSource, _gateway, inner =>
                {
                    if (throwOnce[0])
                    {
                        throwOnce[0] = false;
                        throw new InvalidOperationException("wrap error");
                    }
                    return WrapToStruct(inner);
                });

            LogAssert.Expect(LogType.Error, new Regex(@"\[MessageBinding<StringValue>\] Failed to wrap event"));
            _eventSource.Emit(new StringValue { Value = "boom" });
            Assert.AreEqual(0, _loopbackHandler.Received.Count);

            _eventSource.Emit(new StringValue { Value = "ok" });
            Assert.AreEqual(1, _loopbackHandler.Received.Count);
        }
    }
}
