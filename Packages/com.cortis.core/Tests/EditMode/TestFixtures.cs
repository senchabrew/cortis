using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using R3;
using UnityEngine;

namespace Cortis.Tests.EditMode
{
    /// <summary>
    /// コマンド処理結果を記録するハンドラ。
    /// </summary>
    sealed class RecordingHandler<T> : ICommandHandler<T>
        where T : IMessage<T>
    {
        readonly List<T> _received = new();
        Action<T> _onHandle;

        public IReadOnlyList<T> Received => _received;

        /// <summary>Handle 時に追加の動作を差し込む（例外テスト用）</summary>
        public void SetOnHandle(Action<T> action) => _onHandle = action;

        public void Handle(T command)
        {
            _received.Add(command);
            _onHandle?.Invoke(command);
        }
    }

    /// <summary>
    /// テストからイベントを供給するソース。
    /// </summary>
    sealed class TestEventSource<T> : IEventSource<T>
        where T : IMessage<T>
    {
        readonly Subject<T> _subject = new();
        public Observable<T> Events => _subject;
        public void Emit(T value) => _subject.OnNext(value);

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }

    /// <summary>
    /// テスト用の IMessageGateway 実装。
    /// Any メッセージの送受信をインメモリで完結させる。
    /// Send されたメッセージは protobuf シリアライズ→デシリアライズを経て Messages に流れる。
    /// </summary>
    sealed class TestGateway : IMessageGateway
    {
        readonly Subject<Any> _subject = new();

        public bool SimulateError { get; set; }

        public Observable<Any> Messages => _subject;

        public void Send(Any packed)
        {
            if (SimulateError)
                throw new InvalidOperationException("simulated send error");

            // protobuf シリアライズの往復を再現
            var bytes = packed.ToByteArray();
            try
            {
                var parsed = Any.Parser.ParseFrom(bytes);
                _subject.OnNext(parsed);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse Any from bytes: {e}");
            }
        }

        /// <summary>
        /// 外部から Any バイト列を受信したことをシミュレートする。
        /// </summary>
        public void SimulateReceive(byte[] bytes)
        {
            try
            {
                var parsed = Any.Parser.ParseFrom(bytes);
                _subject.OnNext(parsed);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse Any from bytes: {e}");
            }
        }

        /// <summary>
        /// パース済み Any を直接流し込む。
        /// </summary>
        public void SimulateReceive(Any any)
        {
            _subject.OnNext(any);
        }

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }
}
