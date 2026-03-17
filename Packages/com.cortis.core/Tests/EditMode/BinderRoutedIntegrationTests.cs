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
    /// BindRouted + IMessageGateway の結合テスト。
    /// protoc 生成型 (Root.Types.Command/Event, Inner.Types.Command/Event) を使い、
    /// Any → unwrap → handle、dispatch → wrap → Any の全経路を検証する。
    /// WellKnownTypes ではなく実際の protoc ネスト構造を使用。
    /// </summary>
    public class BinderRoutedIntegrationTests
    {
        TestGateway _gateway;

        [SetUp]
        public void SetUp()
        {
            _gateway = new TestGateway();
        }

        [TearDown]
        public void TearDown()
        {
            _gateway.Dispose();
        }

        // ---- unwrap/wrap ヘルパー（Source Generator が生成するコードと同等） ----

        static Cortis.Tests.Inner.Types.Command UnwrapCommand(Cortis.Tests.Root.Types.Command root)
        {
            return root.CommandCase == Cortis.Tests.Root.Types.Command.CommandOneofCase.Inner
                ? root.Inner : null;
        }

        static Cortis.Tests.Root.Types.Event WrapEvent(Cortis.Tests.Inner.Types.Event inner)
        {
            return new Cortis.Tests.Root.Types.Event { Inner = inner };
        }

        static Cortis.Tests.Other.Types.Command UnwrapOtherCommand(Cortis.Tests.Root.Types.Command root)
        {
            return root.CommandCase == Cortis.Tests.Root.Types.Command.CommandOneofCase.Other
                ? root.Other : null;
        }

        // ── Command: Any<Root.Command> → unwrap → Inner.Command ──

        [Test]
        public void Root_CommandでラップされたInnerコマンドがハンドラに届く()
        {
            var handler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            var binder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command>(
                handler, _gateway, UnwrapCommand);

            var inner = new Cortis.Tests.Inner.Types.Command
            {
                DoFoo = new Cortis.Tests.Inner.Types.Command.Types.DoFoo { Name = "hello" }
            };
            _gateway.SimulateReceive(Any.Pack(new Cortis.Tests.Root.Types.Command { Inner = inner }));

            Assert.AreEqual(1, handler.Received.Count);
            Assert.AreEqual(
                Cortis.Tests.Inner.Types.Command.CommandOneofCase.DoFoo,
                handler.Received[0].CommandCase);
            Assert.AreEqual("hello", handler.Received[0].DoFoo.Name);

            binder.Dispose();
        }

        [Test]
        public void 別フィーチャーのRoot_CommandはInnerハンドラに届かない()
        {
            var handler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            var binder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command>(
                handler, _gateway, UnwrapCommand);

            var other = new Cortis.Tests.Root.Types.Command
            {
                Other = new Cortis.Tests.Other.Types.Command
                {
                    DoOther = new Cortis.Tests.Other.Types.Command.Types.DoOther { Value = "nope" }
                }
            };
            _gateway.SimulateReceive(Any.Pack(other));

            Assert.AreEqual(0, handler.Received.Count);

            binder.Dispose();
        }

        [Test]
        public void ラップされていない直接のInner_Commandはハンドラに届かない()
        {
            var handler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            var binder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command>(
                handler, _gateway, UnwrapCommand);

            var direct = new Cortis.Tests.Inner.Types.Command
            {
                DoBar = new Cortis.Tests.Inner.Types.Command.Types.DoBar { Count = 42 }
            };
            _gateway.SimulateReceive(Any.Pack(direct));

            Assert.AreEqual(0, handler.Received.Count);

            binder.Dispose();
        }

        // ── Event: Inner.Event → wrap → Any<Root.Event> → ループバック受信 ──

        [Test]
        public void InnerイベントがwrapされRoot_Eventとしてループバック受信できる()
        {
            var source = new TestEventSource<Cortis.Tests.Inner.Types.Event>();
            var loopbackHandler = new RecordingHandler<Cortis.Tests.Root.Types.Event>();

            var loopbackReceiver = MessageBinding.Bind<Cortis.Tests.Root.Types.Event>(
                loopbackHandler, _gateway);
            var eventBinder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Event,
                Cortis.Tests.Inner.Types.Event>(
                source, _gateway, WrapEvent);

            source.Emit(new Cortis.Tests.Inner.Types.Event
            {
                FooDone = new Cortis.Tests.Inner.Types.Event.Types.FooDone { Result = "ok" }
            });

            Assert.AreEqual(1, loopbackHandler.Received.Count);
            Assert.AreEqual(
                Cortis.Tests.Root.Types.Event.EventOneofCase.Inner,
                loopbackHandler.Received[0].EventCase);
            Assert.AreEqual("ok", loopbackHandler.Received[0].Inner.FooDone.Result);

            eventBinder.Dispose();
            loopbackReceiver.Dispose();
            source.Dispose();
        }

        // ── Command + Event 双方向 ──

        [Test]
        public void 双方向ルーティングでコマンド受信とイベント送信が両方動く()
        {
            var handler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            var source = new TestEventSource<Cortis.Tests.Inner.Types.Event>();
            var loopbackHandler = new RecordingHandler<Cortis.Tests.Root.Types.Event>();

            var loopbackReceiver = MessageBinding.Bind<Cortis.Tests.Root.Types.Event>(
                loopbackHandler, _gateway);
            var binder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command,
                Cortis.Tests.Root.Types.Event,
                Cortis.Tests.Inner.Types.Event>(
                handler, source, _gateway, UnwrapCommand, WrapEvent);

            // コマンド受信
            var cmd = new Cortis.Tests.Root.Types.Command
            {
                Inner = new Cortis.Tests.Inner.Types.Command
                {
                    DoBar = new Cortis.Tests.Inner.Types.Command.Types.DoBar { Count = 7 }
                }
            };
            _gateway.SimulateReceive(Any.Pack(cmd));
            Assert.AreEqual(1, handler.Received.Count);
            Assert.AreEqual(7, handler.Received[0].DoBar.Count);

            // イベント送信 → ループバック受信
            source.Emit(new Cortis.Tests.Inner.Types.Event
            {
                FooDone = new Cortis.Tests.Inner.Types.Event.Types.FooDone { Result = "done" }
            });
            Assert.AreEqual(1, loopbackHandler.Received.Count);
            Assert.AreEqual("done", loopbackHandler.Received[0].Inner.FooDone.Result);

            binder.Dispose();
            loopbackReceiver.Dispose();
            source.Dispose();
        }

        [Test]
        public void protobufシリアライズ往復後もコマンドが正しくunwrapされる()
        {
            var handler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            var binder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command>(
                handler, _gateway, UnwrapCommand);

            var root = new Cortis.Tests.Root.Types.Command
            {
                Inner = new Cortis.Tests.Inner.Types.Command
                {
                    DoFoo = new Cortis.Tests.Inner.Types.Command.Types.DoFoo { Name = "serialized" }
                }
            };
            // バイト列経由（実際の通信経路と同じ）
            _gateway.SimulateReceive(Any.Pack(root).ToByteArray());

            Assert.AreEqual(1, handler.Received.Count);
            Assert.AreEqual("serialized", handler.Received[0].DoFoo.Name);

            binder.Dispose();
        }

        // ── 追加テスト: 網羅性向上 ──

        [Test]
        public void 複数BindRoutedが同一Gatewayで互いに干渉しない()
        {
            var innerHandler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            var otherHandler = new RecordingHandler<Cortis.Tests.Other.Types.Command>();

            var innerBinder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command>(
                innerHandler, _gateway, UnwrapCommand);

            var otherBinder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Other.Types.Command>(
                otherHandler, _gateway, UnwrapOtherCommand);

            // Inner コマンド → innerHandler のみ
            var innerCmd = new Cortis.Tests.Root.Types.Command
            {
                Inner = new Cortis.Tests.Inner.Types.Command
                {
                    DoFoo = new Cortis.Tests.Inner.Types.Command.Types.DoFoo { Name = "inner" }
                }
            };
            _gateway.SimulateReceive(Any.Pack(innerCmd));

            Assert.AreEqual(1, innerHandler.Received.Count);
            Assert.AreEqual(0, otherHandler.Received.Count);

            // Other コマンド → otherHandler のみ
            var otherCmd = new Cortis.Tests.Root.Types.Command
            {
                Other = new Cortis.Tests.Other.Types.Command
                {
                    DoOther = new Cortis.Tests.Other.Types.Command.Types.DoOther { Value = "other" }
                }
            };
            _gateway.SimulateReceive(Any.Pack(otherCmd));

            Assert.AreEqual(1, innerHandler.Received.Count);
            Assert.AreEqual(1, otherHandler.Received.Count);

            innerBinder.Dispose();
            otherBinder.Dispose();
        }

        [Test]
        public void oneof未設定のRoot_Commandはハンドラに届かない()
        {
            var handler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            var binder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command>(
                handler, _gateway, UnwrapCommand);

            // デフォルト Root.Command (CommandCase == None)
            var empty = new Cortis.Tests.Root.Types.Command();
            _gateway.SimulateReceive(Any.Pack(empty));

            Assert.AreEqual(0, handler.Received.Count);

            binder.Dispose();
        }

        [Test]
        public void BindRouted経由でもハンドラ例外後にストリームが生存する()
        {
            var handler = new RecordingHandler<Cortis.Tests.Inner.Types.Command>();
            handler.SetOnHandle(_ => throw new InvalidOperationException("test error"));

            var binder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Command,
                Cortis.Tests.Inner.Types.Command>(
                handler, _gateway, UnwrapCommand);

            var cmd1 = new Cortis.Tests.Root.Types.Command
            {
                Inner = new Cortis.Tests.Inner.Types.Command
                {
                    DoFoo = new Cortis.Tests.Inner.Types.Command.Types.DoFoo { Name = "boom" }
                }
            };
            LogAssert.Expect(LogType.Error, new Regex(@"Command error"));
            _gateway.SimulateReceive(Any.Pack(cmd1));
            Assert.AreEqual(1, handler.Received.Count);

            handler.SetOnHandle(null);

            var cmd2 = new Cortis.Tests.Root.Types.Command
            {
                Inner = new Cortis.Tests.Inner.Types.Command
                {
                    DoFoo = new Cortis.Tests.Inner.Types.Command.Types.DoFoo { Name = "ok" }
                }
            };
            _gateway.SimulateReceive(Any.Pack(cmd2));
            Assert.AreEqual(2, handler.Received.Count);
            Assert.AreEqual("ok", handler.Received[1].DoFoo.Name);

            binder.Dispose();
        }

        [Test]
        public void BindRouted経由でもDistinctUntilChangedで重複イベントが排除される()
        {
            var source = new TestEventSource<Cortis.Tests.Inner.Types.Event>();
            var loopbackHandler = new RecordingHandler<Cortis.Tests.Root.Types.Event>();

            var loopbackReceiver = MessageBinding.Bind<Cortis.Tests.Root.Types.Event>(
                loopbackHandler, _gateway);
            var eventBinder = MessageBinding.BindRouted<
                Cortis.Tests.Root.Types.Event,
                Cortis.Tests.Inner.Types.Event>(
                source, _gateway, WrapEvent);

            var sameEvent = new Cortis.Tests.Inner.Types.Event
            {
                FooDone = new Cortis.Tests.Inner.Types.Event.Types.FooDone { Result = "same" }
            };

            source.Emit(sameEvent);
            source.Emit(sameEvent);
            source.Emit(sameEvent);

            Assert.AreEqual(1, loopbackHandler.Received.Count);

            eventBinder.Dispose();
            loopbackReceiver.Dispose();
            source.Dispose();
        }
    }
}
