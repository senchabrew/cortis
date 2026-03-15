// =============================================================================
// FlutterMessageGateway.cs
// IMessageGateway の具体実装例（FlutterUnityIntegration を使用）
// =============================================================================
//
// 依存: flutter_unity_widget_2 (Flutter 側) のネイティブブリッジ
//
// cortis パッケージ自体はこのクラスを含まない。
// 利用側プロジェクトで FlutterUnityIntegration に依存する形で実装する。
// =============================================================================

using System;
using Google.Protobuf.WellKnownTypes;
using R3;
using UnityEngine;

namespace Example
{
    /// <summary>
    /// Flutter ↔ Unity 間の protobuf メッセージ通信を担う Gateway。
    /// FlutterUnityIntegration の UnityMessageManager を介してバイト列を送受信する。
    /// </summary>
    public sealed class FlutterMessageGateway : Cortis.IMessageGateway, IDisposable
    {
        readonly Subject<Any> _messages = new();

        public Observable<Any> Messages => _messages;

        /// <summary>
        /// Unity → Flutter: Any をシリアライズして送信する。
        /// </summary>
        public void Send(Any packed)
        {
            var bytes = packed.ToByteArray();
            var base64 = Convert.ToBase64String(bytes);

            // FlutterUnityIntegration の API でメッセージ送信
            // UnityMessageManager.Instance.SendMessageToFlutter(base64);
            Debug.Log($"[FlutterMessageGateway] Send: {packed.TypeUrl} ({bytes.Length} bytes)");
        }

        /// <summary>
        /// Flutter → Unity: FlutterUnityIntegration からのコールバックで呼ばれる。
        /// MonoBehaviour 等から OnMessage(string) 経由で接続する。
        /// </summary>
        public void OnMessageReceived(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var any = Any.Parser.ParseFrom(bytes);
                _messages.OnNext(any);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FlutterMessageGateway] Failed to parse message: {e}");
            }
        }

        public void Dispose()
        {
            _messages.OnCompleted();
            _messages.Dispose();
        }
    }
}
