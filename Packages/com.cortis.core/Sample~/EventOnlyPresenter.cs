// =============================================================================
// EventOnlyPresenter.cs
// [ProtoHandler] の Event-only パターン実装例
// =============================================================================
//
// 前提: protobuf で以下のようなメッセージを定義済み
//
//   message Sensor {
//     message Event {
//       message PositionUpdated { float x = 1; float y = 2; float z = 3; }
//       message StatusChanged { int32 status = 1; }
//       oneof event {
//         PositionUpdated position_updated = 1;
//         StatusChanged status_changed = 2;
//       }
//     }
//   }
// =============================================================================

using Cortis;
using UnityEngine;

namespace Example
{
    // Event-only: 第1引数に null を指定すると、コマンド受信なし・イベント発行のみの Presenter になる
    //
    // Source Generator が自動生成するもの:
    //   - IEventSource<Sensor.Types.Event>.Events プロパティ
    //   - DispatchEvent() ヘルパーメソッド（各 event case ごと）
    //   - IInitializable.Initialize() / IDisposable.Dispose()
    //   - Register() 静的メソッド（VContainer 登録用）
    //
    // 生成されないもの:
    //   - ICommandHandler<T> / Handle() — コマンド受信が不要なため
    [ProtoHandler(null, typeof(Sensor.Types.Event))]
    public sealed partial class EventOnlyPresenter
    {
        readonly Transform _sensor;

        public EventOnlyPresenter(Transform sensor)
        {
            _sensor = sensor;
        }

        private partial void OnInitialize()
        {
            Debug.Log("EventOnlyPresenter initialized");
        }

        // Unity 側で検知した変化を外部に通知する例
        public void UpdatePosition()
        {
            var pos = _sensor.position;
            DispatchEvent(new Sensor.Types.Event.Types.PositionUpdated
            {
                X = pos.x,
                Y = pos.y,
                Z = pos.z,
            });
        }

        public void NotifyStatusChange(int status)
        {
            DispatchEvent(new Sensor.Types.Event.Types.StatusChanged
            {
                Status = status,
            });
        }

        private partial void OnDispose() { }
    }
}
